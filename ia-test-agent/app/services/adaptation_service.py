"""
Service d'adaptation dynamique.
Diagnostique les erreurs d'execution (popups, elements deplaces,
timeouts) et propose des strategies de recuperation.
"""

import logging
import re

from app.schemas.test_schemas import (
    AdaptationRequest,
    AdaptationResponse,
    CorrectiveAction,
)

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Diagnostic rules
# ---------------------------------------------------------------------------

_POPUP_KEYWORDS = [
    "cookie", "cookies", "consent", "accepter", "accept", "agree",
    "got it", "ok", "dismiss", "fermer", "close", "allow", "autoriser",
    "j'accepte", "i agree", "continuer", "continue",
]

_MODAL_KEYWORDS = [
    "modal", "dialog", "popup", "overlay", "alert",
]


class AdaptationService:
    """Service d'adaptation dynamique pour gerer les imprevus."""

    def adapt(self, request: AdaptationRequest) -> AdaptationResponse:
        """Diagnostique un probleme et propose des actions correctives."""

        error_type = request.type_erreur.lower()
        vision_elements = request.elements_detectes_par_vision or []

        # ---- 1. Popup / cookie banner detection ----
        popup_result = self._check_popup(vision_elements)
        if popup_result:
            return popup_result

        # ---- 2. Element not found → try alternative selectors ----
        if "not_found" in error_type or "timeout" in error_type:
            return self._handle_element_not_found(request, vision_elements)

        # ---- 3. Navigation error ----
        if "navigation" in error_type or "net::" in error_type:
            return AdaptationResponse(
                diagnostic="erreur_navigation",
                actions_correctives=[
                    CorrectiveAction(
                        action="retry_navigation",
                        raison="Retenter la navigation apres un delai",
                    ),
                    CorrectiveAction(
                        action="wait",
                        selector="5000",
                        raison="Attendre 5 secondes avant de retenter",
                    ),
                ],
                strategie="retry_with_delay",
                confiance=0.70,
            )

        # ---- 4. JavaScript error ----
        if "javascript" in error_type or "js" in error_type:
            return AdaptationResponse(
                diagnostic="erreur_javascript",
                actions_correctives=[
                    CorrectiveAction(
                        action="capture_log",
                        raison="Capturer les logs console du navigateur",
                    ),
                    CorrectiveAction(
                        action="continue",
                        raison="Continuer l'execution malgre l'erreur JS",
                    ),
                ],
                strategie="log_and_continue",
                confiance=0.60,
            )

        # ---- 5. Fallback ----
        return AdaptationResponse(
            diagnostic="erreur_inconnue",
            actions_correctives=[
                CorrectiveAction(
                    action="screenshot",
                    raison="Prendre un screenshot pour analyse manuelle",
                ),
                CorrectiveAction(
                    action="retry_step",
                    raison="Retenter le step en cours",
                ),
            ],
            strategie="screenshot_and_retry",
            confiance=0.40,
        )

    # ------------------------------------------------------------------
    # Popup detection
    # ------------------------------------------------------------------

    def _check_popup(self, elements: list[dict]) -> AdaptationResponse | None:
        """Detecte un popup/cookie barrel dans les elements de vision."""
        for elem in elements:
            label = (elem.get("label") or elem.get("texte_ocr") or "").lower()
            elem_type = (elem.get("type") or "").lower()

            # Check for cookie/consent popup
            if any(kw in label for kw in _POPUP_KEYWORDS):
                return AdaptationResponse(
                    diagnostic="popup_cookies_bloquant",
                    actions_correctives=[
                        CorrectiveAction(
                            action="click",
                            selector=self._selector_from_element(elem),
                            raison=f"Fermer le popup en cliquant sur '{label}'",
                        ),
                        CorrectiveAction(
                            action="retry_step",
                            raison="Retenter le step apres fermeture du popup",
                        ),
                    ],
                    strategie="handle_popup_then_retry",
                    confiance=0.90,
                )

            # Check for modal overlay
            if elem_type in ("modal", "dialog") or any(kw in label for kw in _MODAL_KEYWORDS):
                return AdaptationResponse(
                    diagnostic="modale_inattendue",
                    actions_correctives=[
                        CorrectiveAction(
                            action="press_escape",
                            raison="Tenter de fermer la modale avec Escape",
                        ),
                        CorrectiveAction(
                            action="click",
                            selector='button:has-text("close"), button:has-text("fermer"), [aria-label="close"]',
                            raison="Chercher un bouton de fermeture",
                        ),
                        CorrectiveAction(
                            action="retry_step",
                            raison="Retenter le step apres fermeture",
                        ),
                    ],
                    strategie="close_modal_then_retry",
                    confiance=0.80,
                )

        return None

    # ------------------------------------------------------------------
    # Element not found
    # ------------------------------------------------------------------

    def _handle_element_not_found(
        self, request: AdaptationRequest, vision_elements: list[dict]
    ) -> AdaptationResponse:
        """Gere le cas ou un element n'est pas trouve."""
        step = request.step_en_cours
        original_selector = step.get("selector_original", "")

        actions = [
            CorrectiveAction(
                action="screenshot_and_redetect",
                raison="Re-capturer l'ecran et re-detecter les elements par vision",
            ),
        ]

        # If we have vision elements, suggest clicking the closest match
        if vision_elements:
            best_match = vision_elements[0]  # Vision elements presumably sorted by relevance
            actions.append(CorrectiveAction(
                action="click_by_coordinates",
                selector=f"{best_match.get('position', [0, 0])}",
                raison=f"Cliquer par coordonnees sur l'element le plus probable: {best_match.get('label', '?')}",
            ))

        actions.append(CorrectiveAction(
            action="increase_timeout",
            selector="15000",
            raison="Augmenter le timeout et retenter",
        ))

        return AdaptationResponse(
            diagnostic="element_non_trouve",
            actions_correctives=actions,
            strategie="redetect_then_retry",
            confiance=0.75,
        )

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _selector_from_element(elem: dict) -> str:
        """Construit un selecteur Playwright a partir d'un element detecte."""
        label = elem.get("label") or elem.get("texte_ocr") or ""
        elem_type = (elem.get("type") or "").lower()

        if elem_type == "button":
            return f'button:has-text("{label}")'
        elif elem_type == "link":
            return f'a:has-text("{label}")'
        elif label:
            return f'text="{label}"'
        return "body"
