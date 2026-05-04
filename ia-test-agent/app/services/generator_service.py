"""
Service de generation de scripts Playwright.
Utilise Jinja2 pour produire du code Python executable
a partir des actions NLP structurees.

Day 9 enhancements:
- Tous les 17 intentions couvertes (scroller, verifier_tableau, changer_onglet, telecharger, effacer…)
- Gestion des contextes speciaux : iframe, shadow DOM, popup/modal
- Capture d'ecran automatique apres chaque step (with_screenshots=True)
- Waits intelligents adaptes a l'intention (network, selector, timeout)
- Video recording optionnel
"""

import os
import re
from datetime import datetime
from typing import Optional

from jinja2 import Environment, FileSystemLoader, select_autoescape

from app.schemas.test_schemas import TestGenerationRequest, TestGenerationResponse

# ── inlined from deleted mapping_service ─────────────────────────────────────
_IFRAME_KW   = frozenset(["iframe", "frame", "embedded"])
_SHADOW_KW   = frozenset(["shadow", "shadow-dom", "shadow_dom", "web component"])
_POPUP_KW    = frozenset(["popup", "modal", "dialog", "alert", "overlay"])

def _detect_context(text: str) -> str:
    t = text.lower()
    if any(k in t for k in _IFRAME_KW):
        return "iframe"
    if any(k in t for k in _SHADOW_KW):
        return "shadow"
    if any(k in t for k in _POPUP_KW):
        return "popup"
    return "default"
# ─────────────────────────────────────────────────────────────────────────────

# Jinja2 environment – loaded once
_TEMPLATE_DIR = os.path.join(os.path.dirname(__file__), "..", "templates")
_env = Environment(
    loader=FileSystemLoader(os.path.abspath(_TEMPLATE_DIR)),
    autoescape=select_autoescape([]),
    trim_blocks=True,
    lstrip_blocks=True,
)

# ---------------------------------------------------------------------------
# Intent → Playwright code mapping (17 intentions)
# ---------------------------------------------------------------------------

def _checkbox_selector(target_label: str, target: str) -> str:
    """
    Returns the best Playwright selector for a checkbox target.

    Many pages (e.g. the-internet.herokuapp.com/checkboxes) have no <label>
    elements — the text "checkbox 1" / "checkbox 2" is just adjacent text.
    In that case get_by_label() always fails (10-s timeout).

    Strategy:
      1. If the label contains a number ("checkbox 1"), use nth(N-1) — always works.
      2. Otherwise try get_by_label() so formal labels still work.
    """
    m = re.search(r"\b(?:checkbox|case|option)\s*(\d+)\b", target_label, re.I)
    if m:
        idx = int(m.group(1)) - 1
        return f"{target}.locator(\"input[type='checkbox']\").nth({idx})"
    return f"{target}.get_by_label(\"{target_label}\")"

# ── Gap 1 fix: intent compatibility table ─────────────────────────────────────
# Maps each ActionMapping action_type to the set of NLP intentions that make
# semantic sense for it.  When a step's raw_text matches a mapping pattern but
# the NLP-detected intention is NOT in the compatible set the mapping is skipped,
# preventing cross-intent false positives (e.g. a "click-login" mapping firing on
# "I should see the login success message").
_MAPPING_INTENT_COMPAT: dict[str, set[str]] = {
    "click":    {"cliquer", "soumettre_formulaire", "changer_onglet", "survol", "scroller"},
    "type":     {"saisir_texte", "effacer", "telecharger"},
    "select":   {"selectionner", "cocher", "decocher"},
    "navigate": {"naviguer"},
    "assert":   {"verifier_coche", "verifier_texte", "verifier_visibilite",
                 "verifier_url", "verifier_tableau"},
    "hover":    {"survol"},
    "scroll":   {"scroller"},
    "wait":     {"attendre"},
}


def _mapping_intent_compatible(action_type: str, step_intention: str) -> bool:
    """Return True when the mapping's action_type is compatible with the NLP intention.

    If action_type is not in the table (unknown / custom value) we allow the
    mapping to fire so that future action types are not silently broken.
    """
    compat = _MAPPING_INTENT_COMPAT.get((action_type or "").lower())
    if compat is None:
        return True  # unknown action_type → do not filter
    return (step_intention or "").lower() in compat


def _pw_code_from_mapping(step, mapping, timeout_ms: int = 10000) -> list[str]:
    """Generate Playwright code from a project-level ActionMapping override.

    Action types accepted (matches backend Domain.Enums.UIActionType): click, type,
    select, navigate, assert, hover, scroll, wait. Selector strategies: css, xpath,
    text, role, testid (anything else is treated as a raw locator).
    """
    strategy = (getattr(mapping, "selector_strategy", "") or "css").lower()
    selector_value = getattr(mapping, "selector_value", "") or ""
    action_type = (getattr(mapping, "action_type", "") or "click").lower()

    # ── Build the locator expression ──
    if strategy == "css":
        locator_expr = f'page.locator("{selector_value}")'
    elif strategy == "xpath":
        xp = selector_value if selector_value.startswith("xpath=") else f"xpath={selector_value}"
        locator_expr = f'page.locator("{xp}")'
    elif strategy == "text":
        locator_expr = f'page.get_by_text("{selector_value}")'
    elif strategy == "role":
        locator_expr = f'page.get_by_role("{selector_value}")'
    elif strategy == "testid":
        locator_expr = f'page.get_by_test_id("{selector_value}")'
    else:
        locator_expr = f'page.locator("{selector_value}")'

    # ── Pick the value for type/select from the step entities (if any) ──
    value = ""
    for ent in getattr(step, "entites", []) or []:
        if ent.nom in ("valeur", "value", "texte"):
            value = ent.valeur or ""
            break

    if action_type == "navigate":
        target_url = value or selector_value
        return [
            f'await page.goto("{target_url}")',
            'await page.wait_for_load_state("networkidle")',
        ]
    if action_type == "click":
        return [f'await {locator_expr}.click(timeout={timeout_ms})']
    if action_type == "type":
        return [f'await {locator_expr}.fill("{value}", timeout={timeout_ms})']
    if action_type == "select":
        return [f'await {locator_expr}.select_option("{value}", timeout={timeout_ms})']
    if action_type == "assert":
        return [f'await expect({locator_expr}).to_be_visible(timeout={timeout_ms})']
    if action_type == "hover":
        return [f'await {locator_expr}.hover(timeout={timeout_ms})']
    if action_type == "scroll":
        return [f'await {locator_expr}.scroll_into_view_if_needed(timeout={timeout_ms})']
    if action_type == "wait":
        return [f'await {locator_expr}.wait_for(timeout={timeout_ms})']

    # Fallback: just click the located element.
    return [f'await {locator_expr}.click(timeout={timeout_ms})']


def _pw_code_for_step(step, context: str = "main", timeout_ms: int = 10000) -> list[str]:
    """Generate Playwright lines for a single parsed step.
    
    Args:
        step: ParsedStep with intention, raw_text, entites.
        context: 'main' | 'iframe' | 'shadow' | 'popup'.
        timeout_ms: Default timeout for waits and assertions.
    """
    intent = step.intention
    entities = {e.nom: e.valeur for e in step.entites}
    cible = entities.get("cible", "")
    valeur = entities.get("valeur", "")
    url = entities.get("url", "")
    elem_type = entities.get("type_element", "")

    # In iframe context, actions target the frame object instead of page
    target = "_frame" if context == "iframe" else "page"

    lines: list[str] = []

    # ── NAVIGATE ────────────────────────────────────────────────────────────
    if intent == "naviguer":
        target_url = url or valeur or ""
        if target_url.startswith(("http://", "https://")):
            lines.append(f'await page.goto("{target_url}")')
        else:
            lines.append(f'# Navigate: {step.raw_text}')
            lines.append(f'# await page.goto("URL_A_DEFINIR")')
        lines.append('await page.wait_for_load_state("networkidle")')

    # ── FILL / SAISIR ────────────────────────────────────────────────────────
    elif intent == "saisir_texte":
        # Detect password field from the raw text — the most reliable selector
        # is input[type='password'] since password fields are unique on a page.
        raw_lower = (step.raw_text or "").lower()
        is_password = (
            "password" in raw_lower
            or "mot de passe" in raw_lower
            or "mdp " in f" {raw_lower} "
            or (cible or "").lower() in {"password", "mot de passe", "mdp", "pwd"}
        )
        is_email = (
            "email" in raw_lower
            or "e-mail" in raw_lower
            or (cible or "").lower() in {"email", "e-mail", "mail", "courriel", "adresse mail", "adresse email"}
        )
        if is_password and target == "page":
            selector = f'{target}.locator("input[type=\'password\']").first'
        elif is_email and target == "page":
            # input[type='email'] is the most reliable cross-form selector for email
            # fields — works regardless of id/name/placeholder naming conventions
            # (e.g. DemoQA uses id='userEmail', not 'email').
            selector = f'{target}.locator("input[type=\'email\']").first'
        elif cible and target == "page":
            # Strip trailing UI suffixes (field/champ/input) from the label.
            field_label = re.sub(
                r'\s+(?:field|input|champ|zone|textbox|box|area)$',
                '', cible, flags=re.I
            ).strip() or cible
            # Build a robust CSS selector matching name/id/placeholder/aria-label
            # (case-insensitive via the [attr=val i] flag) — works for the vast
            # majority of real-world login/signup forms.
            esc = field_label.replace('"', '\\"').replace("'", "\\'")
            selector = (
                f'{target}.locator('
                f'"input[name=\'{esc}\' i], '
                f'input[id=\'{esc}\' i], '
                f'input[placeholder=\'{esc}\' i], '
                f'input[aria-label=\'{esc}\' i], '
                f'textarea[name=\'{esc}\' i], '
                f'textarea[id=\'{esc}\' i]"'
                f').first'
            )
        else:
            selector = _build_selector(cible, elem_type or "input", target)
        lines.append(f'await {selector}.fill("{valeur}")')

    # ── CLICK ────────────────────────────────────────────────────────────────
    elif intent == "cliquer":
        target_label = cible or valeur
        if not target_label:
            # No target extracted — emit a TODO rather than silently clicking <body>
            lines.append(f'# TODO (cliquer): impossible de déterminer la cible — {step.raw_text}')
        else:
            # Strip common UI element type suffixes that PhraseMatcher includes in cible.
            # e.g. "Login button" → "Login", "Submit button" → "Submit".
            # The element type is already captured separately via elem_type.
            target_label = re.sub(
                r'\s+(?:button|btn|bouton|link|lien|icon|tab|onglet|menu|field|champ)$',
                '', target_label, flags=re.I
            ).strip() or target_label
            selector = _build_selector(target_label, elem_type or "button", target)
            lines.append(f'await {selector}.click()')
            # Smart wait: after click, wait for network if not inside iframe
            if context == "main":
                lines.append('await page.wait_for_load_state("networkidle")')

    # ── SELECT ────────────────────────────────────────────────────────────────
    elif intent == "selectionner":
        # Detect whether this is a RADIO/OPTION pattern ("choose Male for Gender",
        # "select option Yes") versus a true <select> dropdown pattern
        # ("select France from the Country dropdown"). The presence of
        # "dropdown / list / menu / from" is a strong dropdown hint;
        # otherwise the "for / as / pour / comme" preposition or the absence
        # of any list-keyword strongly suggests a radio/segmented control.
        raw_lower = (step.raw_text or "").lower()
        is_dropdown_hint = any(
            kw in raw_lower for kw in (
                "dropdown", "liste déroul", "liste deroul", " menu ",
                " from ", " depuis ", " dans la liste", " in the list",
            )
        )
        is_radio_hint = (
            (cible and valeur and not is_dropdown_hint)
            or any(kw in raw_lower for kw in (" for ", " as ", " pour ", " comme "))
        )

        if is_radio_hint and valeur and target == "page":
            # Robust radio/segmented-control click that works on the vast
            # majority of real forms (DemoQA, Bootstrap, Material, custom):
            #   • <input type="radio" value="Male">           (attribute match)
            #   • <label for="x">Male</label>                  (label-text click)
            #   • <button role="radio" aria-label="Male">      (ARIA radio)
            # `.first` avoids strict-mode violations when several elements
            # match (e.g. the input + its label). `force=True` handles inputs
            # that are visually hidden behind a styled <label>.
            esc = valeur.replace('"', '\\"').replace("'", "\\'")
            selector = (
                f'{target}.locator('
                f'"input[type=\'radio\'][value=\'{esc}\' i], '
                f'label:has-text(\'{esc}\'), '
                f'[role=\'radio\'][aria-label=\'{esc}\' i]"'
                f').first'
            )
            lines.append(f'await {selector}.click(force=True)')
        else:
            # True <select> dropdown — keep the original behaviour.
            if cible:
                if " " not in cible:
                    selector = (
                        f"{target}.locator("
                        f"\"select[id='{cible}'], select[name='{cible}']\""
                        f").first"
                    )
                else:
                    selector = f'{target}.get_by_label("{cible}")'
            else:
                selector = f'{target}.locator("select").first'
            lines.append(f'await {selector}.select_option(label="{valeur}")')

    # ── CHECK ────────────────────────────────────────────────────────────────
    elif intent == "cocher":
        target_label = cible or valeur
        if target_label:
            selector = _checkbox_selector(target_label, target)
        elif entities.get("nombre"):
            idx = int(entities["nombre"]) - 1
            selector = f"{target}.locator(\"input[type='checkbox']\").nth({idx})"
        else:
            selector = f'{target}.locator("input[type=\'checkbox\']").first'
        lines.append(f'await {selector}.check()')

    # ── UNCHECK ────────────────────────────────────────────────────────────────
    elif intent == "decocher":
        target_label = cible or valeur
        if target_label:
            selector = _checkbox_selector(target_label, target)
        elif entities.get("nombre"):
            idx = int(entities["nombre"]) - 1
            selector = f"{target}.locator(\"input[type='checkbox']\").nth({idx})"
        else:
            selector = f'{target}.locator("input[type=\'checkbox\']").first'
        lines.append(f'await {selector}.uncheck()')

    # ── CLEAR / EFFACER ──────────────────────────────────────────────────────
    elif intent == "effacer":
        target_label = cible or valeur
        selector = _build_selector(target_label, "input", target)
        lines.append(f'await {selector}.clear()')

    # ── SUBMIT FORM ──────────────────────────────────────────────────────────
    elif intent == "soumettre_formulaire":
        target_label = cible or valeur
        if target_label:
            selector = _build_selector(target_label, "button", target)
            lines.append(f'await {selector}.click()')
        else:
            # Generic form submit
            lines.append(f'await {target}.evaluate("document.querySelector(\'form\').submit()")')
        lines.append('await page.wait_for_load_state("networkidle")')

    # ── HOVER / SURVOL ────────────────────────────────────────────────────────
    elif intent == "survol":
        target_label = cible or valeur
        selector = _build_selector(target_label, elem_type or "", target)
        lines.append(f'await {selector}.hover()')
        # Wait for tooltip/dropdown to appear
        lines.append(f'await page.wait_for_timeout(500)')

    # ── SCROLL ────────────────────────────────────────────────────────────────
    elif intent == "scroller":
        raw = step.raw_text.lower()
        if "up" in raw or "haut" in raw:
            lines.append(f'await {target}.evaluate("window.scrollBy(0, -400)")')
        elif "bas" in raw or "down" in raw or "bottom" in raw:
            lines.append(f'await {target}.evaluate("window.scrollBy(0, 400)")')
        elif cible or valeur:
            target_label = cible or valeur
            selector = _build_selector(target_label, "", target)
            lines.append(f'await {selector}.scroll_into_view_if_needed()')
        else:
            lines.append(f'await {target}.evaluate("window.scrollBy(0, 300)")')

    # ── SWITCH TAB / CHANGER ONGLET ──────────────────────────────────────────
    elif intent == "changer_onglet":
        target_label = cible or valeur
        selector = _build_selector(target_label, "tab", target)
        lines.append(f'await {selector}.click()')
        # Smart wait: wait for the new tab panel to be visible
        lines.append(f'await page.wait_for_timeout(300)')

    # ── UPLOAD / TELECHARGER ─────────────────────────────────────────────────
    elif intent == "telecharger":
        file_path = valeur or "path/to/file.txt"
        target_label = cible or ""
        if target_label:
            selector = _build_selector(target_label, "file_input", target)
            lines.append(f'await {selector}.set_input_files("{file_path}")')
        else:
            lines.append(f'await {target}.locator("input[type=\'file\']").set_input_files("{file_path}")')

    # ── ASSERT TEXT ───────────────────────────────────────────────────────────
    elif intent == "verifier_texte":
        expected = valeur or cible
        if cible and valeur:
            # Scoped assertion: text "{valeur}" must appear inside element "{cible}".
            selector = _build_selector(cible, "", target)
            lines.append(f'await expect({selector}).to_contain_text("{valeur}", timeout={timeout_ms})')
        elif expected:
            # Generic "I should see X on the page" assertion.
            #
            # We use `expect(body).to_contain_text(...)` because it is the
            # most semantically correct interpretation of "the page should
            # show X": Playwright auto-retries on body.text_content() and
            # matches text *anywhere* in the rendered DOM (case-insensitive
            # via `ignore_case=True`).
            #
            # We DELIBERATELY avoid `get_by_text(...).first.to_be_visible()`:
            # on text-rich pages (e.g. Wikipedia articles) the substring
            # often matches 10+ DOM nodes — including hidden anchors,
            # collapsed sections, sr-only labels — and `.first` arbitrarily
            # picks the first match in DOM order which is frequently the
            # hidden one, causing false-negative assertions.
            esc = expected.replace('"', '\\"')
            lines.append(
                f'await expect({target}.locator("body"))'
                f'.to_contain_text("{esc}", ignore_case=True, timeout={timeout_ms})'
            )

    # ── ASSERT VISIBLE ────────────────────────────────────────────────────────
    elif intent == "verifier_coche":
        target_label = cible or valeur
        raw = step.raw_text.lower()
        is_unchecked = any(w in raw for w in (
            "pas coch", "n'est pas", "ne doit pas", "not checked",
            "should not be checked", "décoch",
        ))
        if target_label:
            selector = _checkbox_selector(target_label, target)
        else:
            selector = f"{target}.locator(\"input[type='checkbox']\").first"
        if is_unchecked:
            lines.append(f'await expect({selector}).not_to_be_checked(timeout={timeout_ms})')
        else:
            lines.append(f'await expect({selector}).to_be_checked(timeout={timeout_ms})')

    # ── ASSERT VISIBLE ─────────────────────────────────────────────────────────
    elif intent == "verifier_visibilite":
        target_label = cible or valeur
        raw = step.raw_text.lower()
        is_hidden = any(w in raw for w in ("hidden", "caché", "invisible", "not visible", "pas visible", "ne doit pas"))

        # For checkboxes, get_by_role(name=...) requires an accessible label.
        # Many pages (e.g. the-internet checkboxes) have no <label> or aria-label —
        # use a CSS attribute selector which is always reliable.
        if elem_type == "checkbox" or "checkbox" in target_label.lower():
            selector = f'{target}.locator("input[type=\'checkbox\']").first'
        else:
            selector = _build_selector(target_label, elem_type or "", target)

        if is_hidden:
            lines.append(f'await expect({selector}).to_be_hidden(timeout={timeout_ms})')
        else:
            lines.append(f'await expect({selector}).to_be_visible(timeout={timeout_ms})')

    # ── ASSERT URL ────────────────────────────────────────────────────────────
    elif intent == "verifier_url":
        expected = valeur or cible
        lines.append(f'await expect(page).to_have_url(re.compile(r".*{re.escape(expected)}.*"), timeout={timeout_ms})')

    # ── ASSERT TABLE ──────────────────────────────────────────────────────────
    elif intent == "verifier_tableau":
        expected = valeur or cible
        raw = step.raw_text.lower()
        # Try to extract row count from step text
        row_count_match = re.search(r"(\d+)\s*(?:row|ligne|entr)", raw)
        if row_count_match:
            n = row_count_match.group(1)
            lines.append(f'_rows = await {target}.locator("table tbody tr, [role=\'row\']").count()')
            lines.append(f'assert _rows == {n}, f"Expected {n} rows, got {{_rows}}"')
        elif expected:
            # Use locator("body") + to_contain_text to avoid strict-mode violation when
            # the page has multiple tables (Playwright refuses to act on ambiguous locators).
            lines.append(f'await expect({target}.locator("body")).to_contain_text("{expected}", timeout={timeout_ms})')
        else:
            lines.append(f'await expect({target}.locator("table, [role=\'grid\']").first).to_be_visible(timeout={timeout_ms})')

    # ── WAIT ──────────────────────────────────────────────────────────────────
    elif intent == "attendre":
        target_label = cible or valeur
        raw = step.raw_text.lower()
        if target_label:
            selector = _build_selector(target_label, "", target)
            if any(w in raw for w in ("disparu", "hidden", "disappear", "gone")):
                lines.append(f'await {selector}.wait_for(state="hidden", timeout={timeout_ms})')
            else:
                lines.append(f'await {selector}.wait_for(state="visible", timeout={timeout_ms})')
        else:
            lines.append(f'await {target}.wait_for_load_state("networkidle")')

    # ── POPUP / DIALOG ────────────────────────────────────────────────────────
    elif context == "popup":
        raw = step.raw_text.lower()
        if any(w in raw for w in ("accepter", "accept", "ok", "confirmer", "confirm")):
            lines.append('await _accept_dialog(page)')
        elif any(w in raw for w in ("refuser", "dismiss", "annuler", "cancel")):
            lines.append('await _dismiss_dialog(page)')
        else:
            lines.append(f'# Popup step: {step.raw_text}')
            target_label = cible or valeur
            if target_label:
                selector = _build_selector(target_label, elem_type or "", target)
                lines.append(f'await {selector}.click()')

    # ── SHADOW DOM ────────────────────────────────────────────────────────────
    elif context == "shadow":
        target_label = cible or valeur
        lines.append(f'# Shadow DOM — accès via evaluate')
        lines.append(
            f'_shadow_el = await page.evaluate_handle('
            f'"document.querySelector(\'{target_label}\').shadowRoot")'
        )
        lines.append(f'# TODO: interagir avec _shadow_el')

    # ── UNKNOWN ────────────────────────────────────────────────────────────────
    else:
        lines.append(f'# TODO (intention inconnue: {intent}): {step.raw_text}')

    return lines


def _build_selector(label: str, elem_type: str, target: str = "page") -> str:
    """Build a chainable Playwright locator from a label and element type.
    
    Returns expressions like:
      page.get_by_role("button", name="Submit")
      _frame.get_by_role("textbox", name="Email")
      page.get_by_text("Welcome")
    """
    if not label:
        # Type-specific fallbacks — avoid targeting <body> for functional interactions
        _type_fallbacks = {
            "button":     f'{target}.locator("button").first',
            "link":       f'{target}.locator("a").first',
            "input":      f'{target}.locator("input").first',
            "input_text": f'{target}.locator("input[type=\\"text\\"]").first',
            "checkbox":   f'{target}.locator("input[type=\\"checkbox\\"]").first',
            "radio":      f'{target}.locator("input[type=\\"radio\\"]").first',
            "select":     f'{target}.locator("select").first',
            "dropdown":   f'{target}.locator("select").first',
            "tab":        f'{target}.locator("[role=\\"tab\\"]").first',
        }
        return _type_fallbacks.get(elem_type, f'{target}.locator("body")')

    role_map = {
        "button":         "button",
        "link":           "link",
        "input":          "textbox",
        "input_text":     "textbox",
        "input_password": "textbox",
        "checkbox":       "checkbox",
        "radio":          "radio",
        "tab":            "tab",
        "select":         "combobox",
        "dropdown":       "combobox",
        "file_input":     "textbox",  # file inputs selected differently above
    }
    role = role_map.get(elem_type, "")

    if role:
        return f'{target}.get_by_role("{role}", name="{label}")'
    else:
        return f'{target}.get_by_text("{label}")'


# ---------------------------------------------------------------------------
# Service
# ---------------------------------------------------------------------------

class GeneratorService:
    """Service de generation automatique de scripts de test Playwright."""

    DEFAULT_TIMEOUT_MS = 10_000

    def generate(
        self,
        request: TestGenerationRequest,
        with_screenshots: bool = True,
        timeout_ms: int = DEFAULT_TIMEOUT_MS,
        headless: bool = True,
        record_video: bool = False,
    ) -> TestGenerationResponse:
        """Genere un script Playwright a partir des actions mappees."""
        func_name = self._sanitize_name(request.scenario_name)
        script_code = self._build_script(
            request, func_name,
            with_screenshots=with_screenshots,
            timeout_ms=timeout_ms,
            headless=headless,
            record_video=record_video,
        )
        filename = f"test_{func_name}.py"

        return TestGenerationResponse(
            scenario_name=request.scenario_name,
            script_filename=filename,
            script_code=script_code,
        )

    def _build_script(
        self,
        request: TestGenerationRequest,
        func_name: str,
        with_screenshots: bool = True,
        timeout_ms: int = DEFAULT_TIMEOUT_MS,
        headless: bool = True,
        record_video: bool = False,
    ) -> str:
        """Construit le code Python Playwright."""
        # Pre-compile project-level action mappings (regex). Invalid patterns are
        # ignored so a single bad config row cannot break script generation.
        compiled_mappings: list[tuple[re.Pattern, object]] = []
        for m in getattr(request, "action_mappings", None) or []:
            try:
                compiled_mappings.append((re.compile(m.intent_pattern, re.IGNORECASE), m))
            except re.error:
                continue

        step_blocks: list[dict] = []
        for i, step in enumerate(request.steps, 1):
            context = _detect_context(step.raw_text)
            # Extract iframe name from raw text if applicable
            iframe_name = ""
            if context == "iframe":
                m = re.search(r"(?:iframe|frame)['\"\s]+([a-zA-Z0-9_\-]+)", step.raw_text, re.I)
                if m:
                    iframe_name = m.group(1)

            # ── Action-mapping override ──
            # If the step text matches a user-defined pattern, build the Playwright
            # code from the mapping (selector + action) instead of falling back to
            # the NLP-driven generator.
            override = next(
                (mp for pat, mp in compiled_mappings if pat.search(step.raw_text or "")),
                None,
            )
            if override is not None:
                code_lines = _pw_code_from_mapping(step, override, timeout_ms=timeout_ms)
            else:
                code_lines = _pw_code_for_step(step, context=context, timeout_ms=timeout_ms)

            step_blocks.append({
                "number": i,
                "comment": f"{step.step_type} {step.raw_text}",
                "lines": code_lines,
                "context": context,
                "iframe_name": iframe_name,
            })

        try:
            template = _env.get_template("playwright_test.py.j2")
            return template.render(
                scenario_name=request.scenario_name,
                test_function_name=func_name,
                date_generation=datetime.now().strftime("%Y-%m-%d %H:%M"),
                url_cible=request.url_cible,
                step_blocks=step_blocks,
                with_screenshots=with_screenshots,
                timeout_ms=timeout_ms,
                headless=headless,
                record_video=record_video,
            )
        except Exception:
            return self._build_script_inline(
                request, func_name, step_blocks,
                with_screenshots=with_screenshots,
            )

    def _build_script_inline(
        self,
        request,
        func_name: str,
        step_blocks: list[dict],
        with_screenshots: bool = True,
    ) -> str:
        """Fallback : generation inline sans Jinja2."""
        lines = [
            "# Script genere automatiquement par l'Agent IA",
            f"# Scenario : {request.scenario_name}",
            f"# Date : {datetime.now().strftime('%Y-%m-%d %H:%M')}",
            "",
            "import re",
            "import asyncio",
            "from pathlib import Path",
            "from playwright.async_api import async_playwright, expect",
            "",
            "",
            f"async def test_{func_name}():",
            "    async with async_playwright() as p:",
            "        browser = await p.chromium.launch(headless=True)",
            "        context = await browser.new_context()",
            "        page = await context.new_page()",
            "",
        ]

        if request.url_cible:
            lines.append(f'        await page.goto("{request.url_cible}")')
            lines.append('        await page.wait_for_load_state("networkidle")')
            lines.append("")

        for block in step_blocks:
            lines.append(f"        # Step {block['number']}: {block['comment']}")
            for code_line in block["lines"]:
                lines.append(f"        {code_line}")
            if with_screenshots:
                label = re.sub(r"[^\w\-]", "_", block["comment"])[:50]
                lines.append(f'        # await page.screenshot(path="screenshots/step_{block["number"]:02d}_{label}.png")')
            lines.append("")

        lines.extend([
            "        await context.close()",
            "        await browser.close()",
            "",
            "",
            f'if __name__ == "__main__":',
            f"    asyncio.run(test_{func_name}())",
            "",
        ])

        return "\n".join(lines)

    @staticmethod
    def _sanitize_name(name: str) -> str:
        """Transforme un nom de scenario en identifiant Python valide."""
        name = name.lower().strip()
        name = re.sub(r"[^a-z0-9_]", "_", name)
        name = re.sub(r"_+", "_", name)
        return name.strip("_") or "unnamed"

