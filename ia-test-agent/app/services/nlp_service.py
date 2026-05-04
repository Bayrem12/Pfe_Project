"""
Pipeline NLP pour le parsing des scenarios Gherkin.
Pipeline hybride : regex (rapide) → spaCy Matcher (moyen) → fallback heuristique.
Extrait les intentions (NAVIGATE, CLICK, INPUT, …) et les entites
(element_type, identifier, value, url, xpath, css, data-testid) de chaque step.
Supporte aussi la substitution des <placeholders> des Scenario Outline.
"""

import re
import time
from functools import lru_cache
from typing import Optional

import spacy
from spacy.matcher import PhraseMatcher, Matcher

# HuggingFace zero-shot — imported lazily so the service stays importable
# even when transformers/torch are not installed.
def _get_zero_shot_pipeline(model_name: str):
    """Load and cache a zero-shot classification pipeline for the given model (CPU only)."""
    from transformers import pipeline as hf_pipeline  # noqa: PLC0415
    # device=-1 forces CPU regardless of CUDA availability.
    # The GPU on this machine is too small (3.6 GiB) for these 1.6 GB models.
    return hf_pipeline("zero-shot-classification", model=model_name, device=-1)

_ZS_PIPELINE_CACHE: dict[str, object] = {}

def _zero_shot_pipeline(lang: str):
    """Return the appropriate bilingual or English zero-shot pipeline (cached)."""
    from app.config import settings  # noqa: PLC0415 – avoid circular at module level
    key = lang if lang == "fr" else "en"
    if key not in _ZS_PIPELINE_CACHE:
        model = settings.ZEROSHOT_MODEL_FR if lang == "fr" else settings.ZEROSHOT_MODEL_EN
        _ZS_PIPELINE_CACHE[key] = _get_zero_shot_pipeline(model)
    return _ZS_PIPELINE_CACHE[key]


def _embedding_model():
    """Return the multilingual SentenceTransformer (cached as module singleton)."""
    global _EMBEDDING_MODEL_INSTANCE
    if _EMBEDDING_MODEL_INSTANCE is None:
        from sentence_transformers import SentenceTransformer  # noqa: PLC0415
        from app.config import settings  # noqa: PLC0415
        _EMBEDDING_MODEL_INSTANCE = SentenceTransformer(
            settings.EMBEDDING_MODEL, local_files_only=True
        )
    return _EMBEDDING_MODEL_INSTANCE

_EMBEDDING_MODEL_INSTANCE = None

# Sentence-BERT intent embedding cache (populated lazily, keyed by language)
_SBERT_INTENT_CACHE: dict[str, tuple] = {}


def _get_sbert_intent_embeddings(lang: str):
    """Pre-compute and cache SBERT embeddings for all intent label descriptions.

    Returns:
        Tuple of (labels_list, intent_list, embeddings_array) where embeddings_array
        has shape (n_intents, embedding_dim).
    """
    import numpy as np  # noqa: PLC0415

    if lang in _SBERT_INTENT_CACHE:
        return _SBERT_INTENT_CACHE[lang]

    model = _embedding_model()
    labels_map = _INTENT_LABELS_FR if lang == "fr" else _INTENT_LABELS_EN
    intent_list = list(labels_map.keys())
    labels_list = list(labels_map.values())
    embeddings = model.encode(labels_list, convert_to_numpy=True, show_progress_bar=False)
    _SBERT_INTENT_CACHE[lang] = (labels_list, intent_list, embeddings)
    return _SBERT_INTENT_CACHE[lang]


# Human-readable label templates for both languages (used in zero-shot prompts)
_INTENT_LABELS_FR: dict[str, str] = {
    "naviguer":           "naviguer vers une page ou une URL",
    "saisir_texte":       "saisir ou taper du texte dans un champ",
    "cliquer":            "cliquer sur un bouton ou un lien",
    "selectionner":       "sélectionner une option dans une liste",
    "cocher":             "cocher une case ou activer une option",
    "decocher":           "décocher ou désactiver une option",
    "verifier_coche":     "vérifier qu'une case est cochée ou non cochée",
    "verifier_texte":     "vérifier qu'un texte est affiché",
    "verifier_visibilite":"vérifier qu'un élément est visible ou invisible",
    "verifier_url":       "vérifier l'URL ou la redirection de la page",
    "attendre":           "attendre le chargement ou la disparition d'un élément",
    "survol":             "survoler un élément avec la souris",
    "scroller":           "faire défiler la page vers le haut ou le bas",
    "soumettre_formulaire":"soumettre ou envoyer un formulaire",
    "verifier_tableau":   "vérifier le contenu d'un tableau ou d'une grille",
    "changer_onglet":     "cliquer sur ou changer d'onglet",
    "telecharger":        "télécharger ou uploader un fichier",
    "effacer":            "effacer ou vider le contenu d'un champ",
}
_INTENT_LABELS_EN: dict[str, str] = {
    "naviguer":           "navigate to a page or URL",
    "saisir_texte":       "type or enter text into a field",
    "cliquer":            "click a button or link",
    "selectionner":       "select an option from a list",
    "cocher":             "check a checkbox or enable an option",
    "decocher":           "uncheck or disable an option",
    "verifier_coche":     "verify that a checkbox is checked or unchecked",
    "verifier_texte":     "verify that some text is displayed",
    "verifier_visibilite":"verify that an element is visible or hidden",
    "verifier_url":       "verify the page URL or redirect",
    "attendre":           "wait for an element to load or disappear",
    "survol":             "hover over an element with the mouse",
    "scroller":           "scroll the page up or down",
    "soumettre_formulaire":"submit a form",
    "verifier_tableau":   "verify the content of a table or grid",
    "changer_onglet":     "click on or switch to a tab",
    "telecharger":        "upload or download a file",
    "effacer":            "clear or empty the content of a field",
}

from app.schemas.gherkin_schemas import (
    GherkinScenarioRequest,
    GherkinParseResponse,
    ParsedStep,
    ExtractedEntity,
)

# ---------------------------------------------------------------------------
# Intention definitions
# ---------------------------------------------------------------------------

INTENTIONS = [
    "naviguer",
    "cliquer",
    "saisir_texte",
    "selectionner",
    "cocher",
    "verifier_texte",
    "verifier_coche",
    "verifier_visibilite",
    "verifier_url",
    "attendre",
    "survol",
    "decocher",
    "scroller",
    "soumettre_formulaire",
    "verifier_tableau",
    "changer_onglet",
    "telecharger",
    "effacer",
]

# Regex patterns per intention — ordered from most specific to least.
# COMPOUND patterns (verb + object context) come first to beat generic verb-only patterns.
_INTENT_PATTERNS: list[tuple[str, re.Pattern]] = [
    # ── 1. COMPOUND: Tab switching (click/select/navigate + tab/onglet) ───────
    ("changer_onglet", re.compile(
        r"(onglet\b"
        r"|switch\s+to\s+(?:the\s+)?(?:tab|onglet)"
        r"|\b(?:cliqu\w*|click|press|tap|s[ée]lectionn\w*|select|navigue\w*|navigate)\b.{0,30}\bonglet\b"
        r"|\b(?:cliqu\w*|click|press|tap|s[ée]lectionn\w*|select|navigue\w*|navigate)\b.{0,30}\btab\b"
        r"|\bonglet\b.{0,30}\b(?:cliqu\w*|click|select)\b"
        r")", re.I
    )),
    # ── 2. COMPOUND: Form submission ─────────────────────────────────────────
    ("soumettre_formulaire", re.compile(
        r"(soumet|soumettre|envoie\b|envoyer"
        r"|submit\s+(?:the\s+)?(?:\w+\s+)?form\b"
        r"|send\s+(?:the\s+)?(?:\w+\s+)?form\b"
        r"|valide\s+le\s+formulaire"
        r")", re.I
    )),
    # ── 3. COMPOUND: File upload/download (before generic selectionner) ───────
    ("telecharger", re.compile(
        r"(t[ée]l[ée]charge|upload|download|attach"
        r"|s[ée]lectionne\w*\s+(?:le\s+)?fichier"
        r"|\bselect\w*\s+(?:the\s+)?file\b"
        r")", re.I
    )),
    # ── 4. Hover / survol (before verifier_texte — "afficher" can confuse) ───
    ("survol", re.compile(r"(survol|hover|mouse\s+over)", re.I)),
    # ── 5. Uncheck (before cocher — "décoche" must not hit "coche" first) ────
    ("decocher", re.compile(r"(d[ée]coche|uncheck|untick)", re.I)),
    # ── 5b. Verify checked state (before cocher — "est coché" must not trigger cocher) ──
    # Matches: est coché/couché, is checked, should be checked, not checked, pas coché
    ("verifier_coche", re.compile(
        r"(est\s+coch[ée é]|[ée]tait\s+coch[éeée]|soit\s+coch[ée é]"
        r"|is\s+checked|should\s+be\s+checked|must\s+be\s+checked"
        r"|n['']est\s+pas\s+coch|not\s+checked|should\s+not\s+be\s+checked"
        r"|est\s+couch[ée é]"
        r")", re.I
    )),
    # ── 6. Verify URL (before naviguer — "redirigé" must beat "page de") ─────
    # NOTE: bare "adresse" removed — it matched French postal address fields (saisir_texte)
    # NOTE: bare https:// removed — navigation steps also contain URLs
    ("verifier_url", re.compile(
        r"(\burl\b|adresse\s+(?:url|web|ip|de\s+la\s+page)|redirect|redirig[ée]"
        r"|l['']adresse\b"
        r")", re.I
    )),
    # ── 7. Navigation ─────────────────────────────────────────────────────────
    ("naviguer", re.compile(
        r"(suis sur|vais sur|navigue|ouvre|accede|page\s+de\b|aller sur|"
        r"go to|navigate|i am on|i open|i visit)", re.I
    )),
    # ── 8. Clear/erase (before saisir_texte — "erase" must not hit "input") ──
    ("effacer", re.compile(
        r"(efface|vide\b|clear\b|supprime\s+(?:le\s+)?contenu|\berase\b)", re.I
    )),
    # ── 8b. COMPOUND: Visibility assertion (before attendre — "should be hidden" must win) ──
    ("verifier_visibilite", re.compile(
        r"(should\s+(?:not\s+)?be\s+(?:visible|displayed|hidden|present)"
        r"|ne\s+devrait\s+pas\s+[êe]tre\s+(?:visible|affiché)"
        r"|devrait\s+[êe]tre\s+(?:visible|affiché|caché|présent)"
        r")", re.I
    )),
    # ── 9. Wait (before cliquer — "submit" in a wait step must not hit cliquer) ─
    ("attendre", re.compile(
        r"(\battend|wait\b|loading|loader|chargement|patienter|dispara)", re.I
    )),
    # ── 10. Input / fill ──────────────────────────────────────────────────────
    ("saisir_texte", re.compile(
        r"(saisis|remplis|[ée]cris|tape|entre\b|renseigne|\bfill\b|type|enter|input|write)", re.I
    )),
    # ── 11. Click (generic — after all compound patterns) ─────────────────────
    ("cliquer", re.compile(
        r"(cliqu|appui|\bpress\b|click|\btap\b|\bsubmit\b|\bvalider?\b)", re.I
    )),
    # ── 12. Select ────────────────────────────────────────────────────────────
    ("selectionner", re.compile(
        r"(s[ée]lectionn|choisis|select|choose|pick)", re.I
    )),
    # ── 13. Check ─────────────────────────────────────────────────────────────
    # Negative lookbehind: exclude "déco" (decocher) and positive lookahead to exclude "est coché" (verifier_coche)
    ("cocher", re.compile(r"((?<!d[eé])coche(?! pas)(?!\s*[ée é])|check(?!box)|tick(?!er))", re.I)),
    # ── 14. Table assertion (before verifier_texte — avoid "should have" clash) ─
    ("verifier_tableau", re.compile(
        r"(\btableau\b(?!\s+de\s+bord)|colonne|column|grille|grid|datagrid"
        r"|\btable\b(?!\s+de)"
        r"|data\s+grid"
        r"|\brow\b.{0,20}\b(?:should|position|at)\b"
        r"|\b(?:nombre|number)\s+(?:de\s+)?(?:lignes?|rows?|colonnes?)\b"
        r")", re.I
    )),
    # ── 15. Verify text ───────────────────────────────────────────────────────
    ("verifier_texte", re.compile(
        r"(vois\b|affiche|contien[t ]|message|texte|"
        r"see\b|\bdisplays?\b|contain|text|doit afficher|should see"
        r"|devrait\s+(?:[êe]tre\b|avoir\b|contenir)\b"
        r"|should\s+(?:be\b|have\b)\s+\"|titre\b"
        r")", re.I
    )),
    # ── 16. Verify visibility ─────────────────────────────────────────────────
    ("verifier_visibilite", re.compile(
        r"(\bvisible\b|apparai|pr[ée]sent|displayed|hidden|cach[ée]"
        r"|\bêtre\s+affiché|\bbe\s+(?:visible|displayed|hidden|present)\b"
        r")", re.I
    )),
    # ── 17. Scroll ────────────────────────────────────────────────────────────
    ("scroller", re.compile(r"(scroll|d[ée]file)", re.I)),
]

# ---------------------------------------------------------------------------
# Element-type heuristics
# ---------------------------------------------------------------------------

_ELEMENT_TYPE_PATTERNS: list[tuple[str, re.Pattern]] = [
    ("button", re.compile(r"(bouton|button|btn)", re.I)),
    ("input", re.compile(r"(champ|field|input|zone de saisie|text\s*box|search\s*bar|barre de recherche)", re.I)),
    ("link", re.compile(r"(lien|link|href)", re.I)),
    ("dropdown", re.compile(r"(liste|dropdown|select|menu d[ée]roulant)", re.I)),
    ("checkbox", re.compile(r"(case [àa] cocher|checkbox|case)", re.I)),
    ("radio", re.compile(r"(bouton radio|radio)", re.I)),
    ("label", re.compile(r"(label|[ée]tiquette|texte)", re.I)),
    ("image", re.compile(r"(image|icon|logo|img)", re.I)),
    ("tab", re.compile(r"(onglet|tab(?!le))", re.I)),
    ("modal", re.compile(r"(modal|popup|dialogue|dialog)", re.I)),
    ("table", re.compile(r"(tableau|table(?:au)?|grille|grid|datagrid)", re.I)),
    ("row", re.compile(r"(ligne|row|tr\b)", re.I)),
    ("cell", re.compile(r"(cellule|cell|td\b)", re.I)),
    ("form", re.compile(r"(formulaire|form(?:\s+de)?)", re.I)),
    ("file_input", re.compile(r"(fichier|file\s*input|pièce jointe|attachment|upload)", re.I)),
    ("date_picker", re.compile(r"(date\s*pick|calendrier|calendar|datepicker)", re.I)),
    ("slider", re.compile(r"(slider|curseur|range\s*input)", re.I)),
    ("alert", re.compile(r"(alerte|alert|popup\s*confirm)", re.I)),
    ("tooltip", re.compile(r"(tooltip|infobulle|info-bulle)", re.I)),
]

_DOUBLE_QUOTED_PATTERN = re.compile(r'"([^"]*)"')
_SINGLE_QUOTED_PATTERN = re.compile(r"'([^']*)'")
_URL_PATTERN = re.compile(r"https?://[^\s\"']+", re.I)
_EMAIL_PATTERN = re.compile(r"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}")
_NUMBER_PATTERN = re.compile(r"\b(\d+)\b")

# ── Locator patterns (XPath / CSS selector / data-testid) ─────────────────
_XPATH_PATTERN = re.compile(
    r"""(?:
        xpath\s*[=:]\s*['"](.*?)['"]    # xpath='...' or xpath="..."
        |
        by\.xpath\s*,\s*['"](.*?)['"]   # By.XPATH, '...'
        |
        //[a-zA-Z\*][\w\[\]@=\'"./\s]*  # raw XPath like //div[@id='x']
    )""",
    re.IGNORECASE | re.VERBOSE,
)
_CSS_PATTERN = re.compile(
    r"(?:css\s*[=:]\s*['\"]([^'\"]+)['\"]"
    r"|by\.css_selector\s*,\s*['\"]([^'\"]+)['\"]"
    r"|\{\s*['\"]([^'\"]+)['\"]\s*\}"
    r"|(?<!\w)((?:\.[a-z][\w\-]*|#[a-z][\w\-]*)+)(?!\w))",
    re.IGNORECASE,
)
_DATA_TESTID_PATTERN = re.compile(
    r"""(?:
        data-testid\s*[=:]\s*['"]([\w\-]+)['"]   # data-testid="my-btn"
        |
        \[data-testid=['"]([\w\-]+)['"]\]          # [data-testid='my-btn']
        |
        testid\s*[=:]\s*['"]([\w\-]+)['"]          # testid="my-btn"
        |
        data-cy\s*[=:]\s*['"]([\w\-]+)['"]         # data-cy="my-btn" (Cypress)
        |
        data-qa\s*[=:]\s*['"]([\w\-]+)['"]         # data-qa="my-btn"
    )""",
    re.VERBOSE,
)
_PLACEHOLDER_PATTERN = re.compile(r"<(\w[\w\s]*)>")

_NER_LABELS = {
    "PER": "personne",
    "PERSON": "personne",
    "ORG": "organisation",
    "LOC": "lieu",
    "GPE": "lieu",
    "FAC": "lieu",
    "MISC": "misc",
}

# ---------------------------------------------------------------------------
# spaCy model cache
# ---------------------------------------------------------------------------

@lru_cache(maxsize=2)
def _load_spacy(lang: str):
    model_name = "fr_core_news_sm" if lang == "fr" else "en_core_web_sm"
    return spacy.load(model_name)


@lru_cache(maxsize=2)
def _build_intent_matcher(lang: str) -> Matcher:
    nlp = _load_spacy(lang)
    matcher = Matcher(nlp.vocab)

    if lang == "fr":
        patterns = {
            "naviguer": [
                [{"LEMMA": {"IN": ["aller", "naviguer", "ouvrir", "accéder", "visiter"]}}],
                [{"LOWER": {"IN": ["je", "j'"]}, "OP": "?"}, {"LEMMA": "être"}, {"LOWER": "sur"}],
            ],
            "saisir_texte": [
                [{"LEMMA": {"IN": ["saisir", "remplir", "écrire", "taper", "entrer", "renseigner"]}}],
            ],
            "cliquer": [
                [{"LEMMA": {"IN": ["cliquer", "appuyer", "presser", "valider"]}}],
            ],
            "selectionner": [
                [{"LEMMA": {"IN": ["sélectionner", "selectionner", "choisir"]}}],
            ],
            "cocher": [[{"LEMMA": {"IN": ["cocher"]}}]],
            "decocher": [[{"LEMMA": {"IN": ["décocher", "decocher"]}}]],
            "verifier_texte": [
                [{"LEMMA": {"IN": ["voir", "afficher", "contenir"]}}],
            ],
            "verifier_visibilite": [
                [{"LOWER": {"IN": ["visible", "affiché", "affiche", "présent", "present", "caché", "cache"]}}],
            ],
            "verifier_url": [[{"LOWER": {"IN": ["url", "adresse", "redirigé", "redirige"]}}]],
            "attendre": [[{"LEMMA": {"IN": ["attendre", "charger", "disparaître", "disparaitre"]}}]],
            "survol": [[{"LEMMA": {"IN": ["survoler"]}}]],
            "scroller": [[{"LEMMA": {"IN": ["scroller", "défiler", "defiler"]}}]],
            # ── NEW ───────────────────────────────────────────────────────────
            "soumettre_formulaire": [
                [{"LEMMA": {"IN": ["soumettre", "envoyer", "valider"]}},
                 {"LOWER": {"IN": ["formulaire", "form"]}, "OP": "?"}],
            ],
            "verifier_tableau": [
                # "tableau" is handled by regex with lookahead (avoids "Tableau de bord")
                # SpaCy catches unambiguous tokens only
                [{"LOWER": {"IN": ["ligne", "colonne", "cellule", "table"]}}],
            ],
            "changer_onglet": [
                [{"LOWER": {"IN": ["onglet", "tab"]}},
                 {"LEMMA": {"IN": ["cliquer", "changer", "ouvrir"]}, "OP": "?"}],
            ],
            "telecharger": [
                [{"LEMMA": {"IN": ["télécharger", "telecharger", "uploader", "joindre"]}}],
            ],
            "effacer": [
                [{"LEMMA": {"IN": ["effacer", "vider", "supprimer", "nettoyer"]}}],
            ],
        }
    else:
        patterns = {
            "naviguer": [
                [{"LEMMA": {"IN": ["access", "go", "navigate", "open", "visit"]}}],
                [{"LOWER": "i", "OP": "?"}, {"LEMMA": "be"}, {"LOWER": "on"}],
            ],
            "saisir_texte": [
                [{"LEMMA": {"IN": ["type", "enter", "fill", "input", "write"]}}],
            ],
            "cliquer": [
                [{"LEMMA": {"IN": ["click", "press", "tap", "submit"]}}],
            ],
            "selectionner": [
                [{"LEMMA": {"IN": ["select", "choose", "pick"]}}],
            ],
            "cocher": [[{"LEMMA": {"IN": ["check", "tick"]}}]],
            "decocher": [[{"LEMMA": {"IN": ["uncheck", "untick"]}}]],
            "verifier_texte": [
                [{"LEMMA": {"IN": ["see", "display", "contain", "show"]}}],
                [{"LOWER": "should"}, {"LEMMA": {"IN": ["see", "contain", "have"]}}],
            ],
            "verifier_visibilite": [
                [{"LOWER": {"IN": ["visible", "displayed", "hidden", "present"]}}],
            ],
            "verifier_url": [[{"LOWER": {"IN": ["url", "address", "redirect", "redirected"]}}]],
            "attendre": [[{"LEMMA": {"IN": ["wait", "load", "disappear"]}}]],
            "survol": [[{"LEMMA": {"IN": ["hover"]}}]],
            "scroller": [[{"LEMMA": {"IN": ["scroll"]}}]],
            # ── NEW ───────────────────────────────────────────────────────────
            "soumettre_formulaire": [
                [{"LEMMA": {"IN": ["submit"]}},
                 {"LOWER": "form", "OP": "?"}],
            ],
            "verifier_tableau": [
                [{"LOWER": {"IN": ["table", "row", "column", "cell", "grid"]}}],
            ],
            "changer_onglet": [
                [{"LOWER": {"IN": ["tab", "panel"]}},
                 {"LEMMA": {"IN": ["click", "switch", "open"]}, "OP": "?"}],
            ],
            "telecharger": [
                [{"LEMMA": {"IN": ["upload", "download", "attach"]}}],
            ],
            "effacer": [
                [{"LEMMA": {"IN": ["clear", "erase", "empty"]}}],
            ],
        }

    for intent, intent_patterns in patterns.items():
        matcher.add(intent, intent_patterns)

    return matcher


@lru_cache(maxsize=2)
def _build_ui_phrase_matcher(lang: str) -> PhraseMatcher:
    nlp = _load_spacy(lang)
    matcher = PhraseMatcher(nlp.vocab, attr="LOWER")

    if lang == "fr":
        phrase_catalog = {
            "button": ["bouton login", "bouton valider", "bouton rechercher", "bouton soumettre", "bouton envoyer"],
            "input": ["champ email", "champ mot de passe", "champ username", "barre de recherche", "champ prénom", "champ nom"],
            "checkbox": ["case à cocher", "case accepter les cgu", "case se souvenir de moi"],
            "link": ["lien inscription", "lien mot de passe oublié", "lien retour"],
            "label": ["message de confirmation", "message d'erreur", "message de succès"],
            "page": ["page de connexion", "page d'accueil", "page d'inscription", "page de résultats"],
            "table": ["tableau de résultats", "liste des utilisateurs", "grille de données"],
            "tab": ["onglet contact", "onglet paramètres", "onglet profil", "onglet commandes"],
            "form": ["formulaire de contact", "formulaire d'inscription", "formulaire de connexion"],
            "modal": ["fenêtre de confirmation", "boîte de dialogue", "popup d'erreur"],
        }
    else:
        phrase_catalog = {
            "button": ["login button", "search button", "sign up button", "submit button", "save button", "cancel button"],
            "input": ["email field", "password field", "username field", "name field", "search bar", "search field", "first name field", "last name field"],
            "checkbox": ["terms checkbox", "accept terms checkbox", "remember me checkbox", "select all checkbox"],
            "link": ["sign up link", "forgot password link", "reset link", "back link"],
            "label": ["confirmation message", "error message", "success message", "warning message"],
            "page": ["login page", "home page", "registration page", "search results page", "profile page"],
            "table": ["results table", "data table", "user list", "data grid", "records table"],
            "tab": ["contact tab", "settings tab", "profile tab", "orders tab", "overview tab"],
            "form": ["contact form", "registration form", "login form", "search form", "checkout form"],
            "modal": ["confirmation modal", "delete modal", "error dialog", "success dialog"],
        }

    for element_type, phrases in phrase_catalog.items():
        matcher.add(element_type, [nlp.make_doc(phrase) for phrase in phrases])

    return matcher


# ---------------------------------------------------------------------------
# NLP Service
# ---------------------------------------------------------------------------

class NLPService:
    """Service NLP pour l'analyse des scenarios Gherkin."""

    def parse_scenario(self, request: GherkinScenarioRequest) -> GherkinParseResponse:
        """Parse un scenario Gherkin complet."""
        start = time.time()

        # Resolve And/But → real keyword based on previous step
        resolved_keywords = self._resolve_and_but(
            [(s.keyword, s.text) for s in request.steps]
        )

        parsed_steps: list[ParsedStep] = []
        for (real_kw, _), step in zip(resolved_keywords, request.steps):
            parsed = self.parse_step(
                keyword=real_kw,
                text=step.text,
                language=request.language,
            )
            parsed_steps.append(parsed)

        elapsed_ms = (time.time() - start) * 1000
        return GherkinParseResponse(
            scenario_name=request.scenario_name,
            nombre_steps=len(parsed_steps),
            steps_analyses=parsed_steps,
            temps_traitement_ms=round(elapsed_ms, 2),
        )

    # ------------------------------------------------------------------
    # Step parsing
    # ------------------------------------------------------------------

    def parse_step(self, keyword: str, text: str, language: str = "fr") -> ParsedStep:
        """Analyse un step Gherkin individuel (regex + spaCy)."""
        doc = self._safe_parse_doc(text, language)
        intention, confidence = self._classify_intention(text, language, doc)
        entites = self._extract_entities(text, language, doc)
        element_type = self._detect_element_type(text, language, doc)

        if element_type:
            entites.append(
                ExtractedEntity(
                    nom="type_element", valeur=element_type, type_entite="element_type"
                )
            )

        entites = self._dedupe_entities(entites)

        return ParsedStep(
            step_type=keyword,
            raw_text=text,
            intention=intention,
            entites=entites,
            confiance=round(confidence, 2),
        )

    # ------------------------------------------------------------------
    # Intention classification (hybrid)
    # ------------------------------------------------------------------

    def _classify_intention(
        self,
        text: str,
        language: str = "fr",
        doc=None,
    ) -> tuple[str, float]:
        """Classifie l'intention via regex patterns, spaCy Matcher puis heuristiques."""
        text_lower = text.lower()

        # 1) Regex patterns (high confidence if explicit verb match)
        for intent, pattern in _INTENT_PATTERNS:
            m = pattern.search(text_lower)
            if m:
                # Confidence depends on how specific the match is
                span_ratio = len(m.group(0)) / max(len(text_lower), 1)
                conf = min(0.80 + span_ratio * 0.5, 0.98)
                return intent, conf

        # 2) spaCy Matcher fallback (better than plain keywords on imperative steps)
        if doc is not None:
            intent, confidence = self._classify_intention_with_matcher(doc, language)
            if intent:
                return intent, confidence

        # 3) Sentence-BERT semantic similarity (faster than zero-shot, multilingual)
        intent, confidence = self._classify_intention_with_sbert(text, language)
        if intent and confidence >= 0.40:
            # Scale SBERT cosine similarity [0.40, 1.0] to confidence [0.55, 0.85]
            scaled = 0.55 + (confidence - 0.40) * (0.85 - 0.55) / (1.0 - 0.40)
            return intent, min(scaled, 0.85)

        # 4) Transformer zero-shot fallback (bilingual: FR uses mDeBERTa, EN uses BART)
        intent, confidence = self._classify_intention_with_transformer(text, language)
        if intent and confidence >= 0.45:
            return intent, confidence

        # 5) Keyword heuristic based on step type context
        if any(w in text_lower for w in ("page", "url", "site", "accueil", "home")):
            return "naviguer", 0.65

        return "inconnu", 0.30

    def _classify_intention_with_sbert(
        self,
        text: str,
        language: str = "fr",
    ) -> tuple[Optional[str], float]:
        """Classifie l'intention par similarite cosinus avec Sentence-BERT.

        Plus rapide que le zero-shot, multilingue, et plus robuste sur les
        formulations synonymes non couvertes par les regex/spaCy.
        """
        try:
            from sklearn.metrics.pairwise import cosine_similarity  # noqa: PLC0415
            import numpy as np  # noqa: PLC0415

            model = _embedding_model()
            _, intent_list, label_embeddings = _get_sbert_intent_embeddings(language)
            text_emb = model.encode([text], convert_to_numpy=True, show_progress_bar=False)
            sims = cosine_similarity(text_emb, label_embeddings)[0]
            best_idx = int(np.argmax(sims))
            return intent_list[best_idx], float(sims[best_idx])
        except Exception:
            return None, 0.0

    def _classify_intention_with_transformer(
        self,
        text: str,
        language: str = "fr",
    ) -> tuple[Optional[str], float]:
        """Classifie l'intention via zero-shot (HuggingFace) — FR: mDeBERTa, EN: BART."""
        try:
            pipe = _zero_shot_pipeline(language)
            labels_map = _INTENT_LABELS_FR if language == "fr" else _INTENT_LABELS_EN
            candidate_labels = list(labels_map.values())
            label_to_intent = {v: k for k, v in labels_map.items()}

            result = pipe(text, candidate_labels=candidate_labels, multi_label=False)
            best_label = result["labels"][0]
            best_score = result["scores"][0]
            intent = label_to_intent.get(best_label, "inconnu")
            return intent, float(best_score)
        except Exception:
            return None, 0.0

    def _classify_intention_with_matcher(self, doc, language: str) -> tuple[Optional[str], float]:
        """Utilise spaCy Matcher pour reconnaitre l'intention par lemmes et structure."""
        try:
            matches = _build_intent_matcher(language)(doc)
        except Exception:
            return None, 0.0

        if not matches:
            return None, 0.0

        match_id, start, end = max(matches, key=lambda item: (item[2] - item[1], -item[1]))
        intent = doc.vocab.strings[match_id]
        span_ratio = (end - start) / max(len(doc), 1)
        confidence = min(0.68 + span_ratio * 0.20, 0.88)
        return intent, confidence

    # ------------------------------------------------------------------
    # Entity extraction
    # ------------------------------------------------------------------

    def _extract_entities(self, text: str, language: str, doc=None) -> list[ExtractedEntity]:
        """Extrait les entites d'un step Gherkin (quoted values + regex + spaCy)."""
        entites: list[ExtractedEntity] = []
        text_lower = text.lower()

        # 1) Values between double-quotes
        quoted = _DOUBLE_QUOTED_PATTERN.findall(text)
        # 2) Fallback: single-quotes
        if not quoted:
            quoted = _SINGLE_QUOTED_PATTERN.findall(text)

        # Heuristic assignment based on position & content
        for i, val in enumerate(quoted):
            role = self._guess_entity_role(val, i, text)
            entites.append(ExtractedEntity(nom=role, valeur=val, type_entite=role))
            specific_type = self._guess_specific_entity_type(val, text, role)
            if specific_type and specific_type != role:
                entites.append(
                    ExtractedEntity(
                        nom=specific_type,
                        valeur=val,
                        type_entite=specific_type,
                    )
                )

        # 3) URL detection
        urls = _URL_PATTERN.findall(text)
        for url in urls:
            entites.append(ExtractedEntity(nom="url", valeur=url, type_entite="url"))

        # 4) Email detection (quoted or not)
        for email in _EMAIL_PATTERN.findall(text):
            entites.append(ExtractedEntity(nom="email", valeur=email, type_entite="email"))
            if email not in set(quoted) and self._looks_like_input_step(text_lower):
                entites.append(ExtractedEntity(nom="valeur", valeur=email, type_entite="valeur"))

        # 5) Number detection
        numbers = _NUMBER_PATTERN.findall(text)
        # Only add if not already captured as a quoted value AND not a substring
        # of any quoted value (e.g. "1" inside "Option 1" must not become a
        # separate valeur entity that overwrites the correct one).
        quoted_set = set(quoted)
        for num in numbers:
            if num not in quoted_set and not any(num in q for q in quoted):
                entites.append(
                    ExtractedEntity(nom="nombre", valeur=num, type_entite="nombre")
                )
                if self._looks_like_input_step(text_lower):
                    entites.append(ExtractedEntity(nom="valeur", valeur=num, type_entite="valeur"))

        # 6) PhraseMatcher for fixed UI targets and spaCy NER for richer extraction
        if doc is not None:
            phrase_target = self._extract_ui_phrase_target(doc, language)
            if phrase_target and not any(entity.nom == "cible" for entity in entites):
                entites.append(
                    ExtractedEntity(nom="cible", valeur=phrase_target, type_entite="cible")
                )

            for ent in doc.ents:
                label = _NER_LABELS.get(ent.label_)
                if label:
                    entites.append(
                        ExtractedEntity(
                            nom=label,
                            valeur=ent.text,
                            type_entite="ner_" + label,
                        )
                    )

        # 7) XPath / CSS selector / data-testid detection
        for m in _XPATH_PATTERN.finditer(text):
            xpath_val = next(filter(None, m.groups()), None)
            if xpath_val:
                entites.append(ExtractedEntity(nom="xpath", valeur=xpath_val, type_entite="xpath"))

        # 6b) Regex fallback for click targets when no cible was found above.
        # Handles unquoted steps like "I click on the Login button" or
        # "Je clique sur le bouton Connexion" where the phrase may not be in
        # the PhraseMatcher catalog.
        if not quoted and not any(e.nom == "cible" for e in entites):
            _click_re = re.search(
                r'\b(?:click|press|tap|cliqu\w+|appui\w+)\s+'
                r'(?:on\s+)?'
                r'(?:the\s+|le\s+|la\s+|les\s+|sur\s+le\s+|sur\s+la\s+|sur\s+)?'
                r'((?:[\w\u00C0-\u024F][\w\u00C0-\u024F\s]*?))'
                r'(?:\s+(?:button|btn|bouton|link|lien|icon|tab|onglet|menu|field|champ))?\s*$',
                text, re.I
            )
            if _click_re:
                _ct = _click_re.group(1).strip()
                if _ct and len(_ct) < 60:  # sanity-check: avoid capturing the whole sentence
                    entites.append(
                        ExtractedEntity(nom="cible", valeur=_ct, type_entite="cible")
                    )

        # 6c) Regex fallback for fill / saisir targets — extract the FIELD name.
        # Without this, both "enter X in the username field" and "enter Y in the
        # password field" produce the same generic "input.first" selector and the
        # second fill overwrites the first. Handles patterns like:
        #   - "enter <val> in/into the <NAME> field"
        #   - "type <val> in <NAME>"
        #   - "saisir/entrer <val> dans le champ <NAME>"
        if self._looks_like_input_step(text_lower) and not any(e.nom == "cible" for e in entites):
            _fill_re = re.search(
                r'(?:in|into|inside|on|dans|sur)\s+'
                r'(?:the\s+|le\s+|la\s+|les\s+|l\'\s*)?'
                r'(?:champ\s+|zone\s+|field\s+|input\s+)?'
                r'([\w\u00C0-\u024F][\w\u00C0-\u024F\s\-]*?)'
                r'(?:\s+(?:field|input|champ|zone|textbox|box|area))?'
                r'\s*$',
                text, re.I
            )
            if _fill_re:
                _ft = _fill_re.group(1).strip().rstrip('.')
                if _ft and len(_ft) < 60 and _ft.lower() not in {"the", "a", "an", "le", "la", "les"}:
                    entites.append(
                        ExtractedEntity(nom="cible", valeur=_ft, type_entite="cible")
                    )

        for m in _DATA_TESTID_PATTERN.finditer(text):
            testid_val = next(filter(None, m.groups()), None)
            if testid_val:
                entites.append(ExtractedEntity(nom="data_testid", valeur=testid_val, type_entite="data_testid"))

        for m in _CSS_PATTERN.finditer(text):
            css_val = next(filter(None, m.groups()), None)
            if css_val and not any(e.valeur == css_val for e in entites):
                entites.append(ExtractedEntity(nom="css_selector", valeur=css_val, type_entite="css_selector"))

        # 8) Detect un-substituted <placeholders> (Scenario Outline sans substitution)
        for ph in _PLACEHOLDER_PATTERN.findall(text):
            entites.append(ExtractedEntity(nom="placeholder", valeur=f"<{ph}>", type_entite="outline_placeholder"))

        return self._dedupe_entities(entites)

    def _guess_entity_role(self, value: str, index: int, full_text: str) -> str:
        """Devine le role semantique d'une valeur extraite entre guillemets."""
        text_lower = full_text.lower()
        quoted_count = len(_DOUBLE_QUOTED_PATTERN.findall(full_text))
        if quoted_count == 0:
            quoted_count = len(_SINGLE_QUOTED_PATTERN.findall(full_text))

        if self._looks_like_input_step(text_lower) or self._text_matches_intent(
            text_lower, "selectionner"
        ):
            return "valeur" if index == 0 else "cible"

        if self._text_matches_intent(text_lower, "verifier_texte") and index == 0:
            return "valeur"

        # Position-based heuristic for the common patterns:
        #   'je saisis "VALUE" dans le champ "FIELD"'
        #   'je clique sur le bouton "LABEL"'
        if index == 0:
            # If only one quoted value → it's the target/label
            if quoted_count == 1:
                return "cible"
            return "valeur"
        elif index == 1:
            return "cible"
        else:
            return f"param_{index}"

    def _guess_specific_entity_type(
        self,
        value: str,
        full_text: str,
        generic_role: str,
    ) -> Optional[str]:
        """Ajoute un type semantique plus precis sans perdre le role generique."""
        text_lower = full_text.lower()

        if _EMAIL_PATTERN.fullmatch(value):
            return "email"
        if value.startswith(("http://", "https://", "www.")):
            return "url"
        if generic_role == "valeur" and any(
            w in text_lower for w in ("mot de passe", "password", "mdp")
        ):
            return "mot_de_passe"
        return None

    # ------------------------------------------------------------------
    # Element type detection
    # ------------------------------------------------------------------

    def _detect_element_type(self, text: str, language: str, doc=None) -> Optional[str]:
        """Detecte le type d'element UI mentionne dans le step."""
        for etype, pattern in _ELEMENT_TYPE_PATTERNS:
            if pattern.search(text):
                return etype

        if doc is not None:
            element_type = self._match_ui_phrase_type(doc, language)
            if element_type:
                return element_type

        return None

    def _extract_ui_phrase_target(self, doc, language: str) -> Optional[str]:
        """Detecte une cible UI fixe via PhraseMatcher."""
        try:
            matches = _build_ui_phrase_matcher(language)(doc)
        except Exception:
            return None

        if not matches:
            return None

        _, start, end = max(matches, key=lambda item: (item[2] - item[1], -item[1]))
        return doc[start:end].text

    def _match_ui_phrase_type(self, doc, language: str) -> Optional[str]:
        """Retourne le type d'element le plus probable depuis PhraseMatcher."""
        try:
            matches = _build_ui_phrase_matcher(language)(doc)
        except Exception:
            return None

        if not matches:
            return None

        match_id, _, _ = max(matches, key=lambda item: (item[2] - item[1], -item[1]))
        return doc.vocab.strings[match_id]

    @staticmethod
    def _dedupe_entities(entites: list[ExtractedEntity]) -> list[ExtractedEntity]:
        """Supprime les doublons exacts tout en preservant l'ordre."""
        unique: list[ExtractedEntity] = []
        seen: set[tuple[str, str, str]] = set()

        for entity in entites:
            key = (entity.nom, entity.valeur, entity.type_entite)
            if key in seen:
                continue
            seen.add(key)
            unique.append(entity)

        return unique

    @staticmethod
    def _text_matches_intent(text_lower: str, intent: str) -> bool:
        """Teste si le texte correspond a un pattern regex d'intention donne."""
        for candidate_intent, pattern in _INTENT_PATTERNS:
            if candidate_intent == intent and pattern.search(text_lower):
                return True
        return False

    def _looks_like_input_step(self, text_lower: str) -> bool:
        """Detecte rapidement les steps qui portent une valeur a saisir/selectionner."""
        return self._text_matches_intent(text_lower, "saisir_texte") or self._text_matches_intent(
            text_lower, "selectionner"
        )

    @staticmethod
    def _safe_parse_doc(text: str, language: str):
        """Parse le texte avec spaCy si le modele est disponible."""
        try:
            return _load_spacy(language)(text)
        except Exception:
            return None

    # ------------------------------------------------------------------
    # And/But resolution
    # ------------------------------------------------------------------

    @staticmethod
    def _resolve_and_but(
        steps: list[tuple[str, str]],
    ) -> list[tuple[str, str]]:
        """Resout les And/But vers le vrai mot-cle precedent."""
        resolved: list[tuple[str, str]] = []
        last_real = "Given"
        for kw, text in steps:
            if kw in ("And", "But", "Et", "Mais"):
                resolved.append((last_real, text))
            else:
                last_real = kw
                resolved.append((kw, text))
        return resolved

    # ------------------------------------------------------------------
    # Scenario Outline placeholder substitution
    # ------------------------------------------------------------------

    @staticmethod
    def substitute_outline_placeholders(
        step_text: str,
        example_row: dict[str, str],
    ) -> str:
        """Remplace les <placeholders> d'un Scenario Outline par les valeurs Examples.

        Args:
            step_text: texte du step avec des <placeholder>.
            example_row: dict {nom_colonne: valeur} depuis la table Examples.

        Returns:
            Le texte du step avec les placeholders remplacés.
        """
        result = step_text
        for key, value in example_row.items():
            result = re.sub(rf"<{re.escape(key)}>", value, result)
        return result

    def expand_scenario_outline(
        self,
        outline_steps: list[tuple[str, str]],
        examples: list[dict[str, str]],
        language: str = "fr",
    ) -> list[list[tuple[str, str]]]:
        """Genere une liste de scenarios concrets a partir d'un Scenario Outline.

        Args:
            outline_steps: liste de (keyword, text) avec <placeholders>.
            examples: liste de dicts (chaque dict = une ligne Examples).
            language: langue du scenario.

        Returns:
            Liste de scenarios (chaque scenario est une liste de (keyword, text)).
        """
        expanded: list[list[tuple[str, str]]] = []
        for row in examples:
            concrete_steps = [
                (kw, self.substitute_outline_placeholders(text, row))
                for kw, text in outline_steps
            ]
            expanded.append(concrete_steps)
        return expanded
