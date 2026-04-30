"""
Service de Computer Vision pour la detection d'elements UI.

Pipeline standard (screenshot seul):
    Screenshot → YOLOv8 detection → OCR → UIDetectionResponse

Pipeline hybride (avec page Playwright vivante):
    Screenshot → YOLO  ┐
    DOM via F12 selectors ┘ → merge IoU → OCR → UIDetectionResponse

Le mode hybride lit le DOM exactement comme le ferait l'onglet Elements de
DevTools (F12) : querySelectorAll par classe CSS, getBoundingClientRect().
YOLO voit visuellement, le DOM rattrape ce que YOLO rate, les deux se
complètent via déduplication IoU.
"""

import io
import os
import time
import logging
from typing import Optional

import cv2
import numpy as np
from PIL import Image

from app.schemas.ui_schemas import UIDetectionResponse, DetectedElement, BoundingBox
from app.config import settings

logger = logging.getLogger(__name__)

# UI element classes expected from the trained YOLO model
# Order must match data/annotations/classes.txt
YOLO_CLASSES = [
    "button", "input_text", "input_password", "link",
    "checkbox", "dropdown", "label", "modal",
]

# CSS selectors per class — same as dom_annotate.py so DOM extraction is
# consistent with the annotations the model was trained on.
_DOM_SELECTORS: dict[str, str] = {
    "button": (
        "button:not([disabled]):not([hidden]):not([aria-hidden='true']),"
        "input[type='submit']:not([disabled]),"
        "input[type='button']:not([disabled]),"
        "input[type='reset']:not([disabled]),"
        "[role='button']:not([disabled]):not([aria-hidden='true']),"
        "a.btn,a.button,.btn:not(select)"
    ),
    "input_text": (
        "input[type='text']:not([hidden]),"
        "input[type='email']:not([hidden]),"
        "input[type='tel']:not([hidden]),"
        "input[type='number']:not([hidden]),"
        "input[type='search']:not([hidden]),"
        "input:not([type]):not([hidden]),"
        "textarea:not([hidden])"
    ),
    "input_password": "input[type='password']:not([hidden])",
    "link": "a[href]:not(.btn):not(.button):not([role='button'])",
    "checkbox": "input[type='checkbox']:not([hidden]),input[type='radio']:not([hidden])",
    "dropdown": (
        "select:not([hidden]),"
        "[role='combobox']:not([hidden]),"
        "[role='listbox']:not([hidden])"
    ),
    "label": "label,legend,[role='heading'],h1,h2,h3",
    "modal": (
        "[role='dialog'],[role='alertdialog'],"
        ".modal:not(.btn):not(.fade),.popup:not(.btn),.dialog"
    ),
}

# JavaScript that reads bounding boxes from the live DOM (F12 equivalent)
_JS_GET_BOXES = """
(selector) => {
    const elements = document.querySelectorAll(selector);
    const boxes = [];
    for (const el of elements) {
        const rect = el.getBoundingClientRect();
        if (
            rect.width > 0 && rect.height > 0 &&
            rect.left < window.innerWidth &&
            rect.top  < window.innerHeight &&
            rect.right > 0 && rect.bottom > 0
        ) {
            boxes.push({
                x: Math.max(0, rect.left),
                y: Math.max(0, rect.top),
                w: Math.min(rect.width,  window.innerWidth  - Math.max(0, rect.left)),
                h: Math.min(rect.height, window.innerHeight - Math.max(0, rect.top)),
                text: (() => {
                    const parts = [];
                    if (el.innerText) parts.push(el.innerText.trim().slice(0, 80));
                    const id = el.id || "";
                    const name = el.getAttribute("name") || "";
                    const aria = el.getAttribute("aria-label") || "";
                    const ph   = el.getAttribute("placeholder") || "";
                    // Exclude el.value for checkboxes/radios: their default value is
                    // always "on" which is meaningless as a text label and breaks
                    // similarity matching ("on" vs "first" → score 0.0).
                    const isCheckOrRadio = el.tagName === "INPUT" && (el.type === "checkbox" || el.type === "radio");
                    const val  = (!isCheckOrRadio && (el.tagName === "INPUT" || el.tagName === "SELECT")) ? (el.value || "") : "";
                    for (const a of [id, name, aria, ph, val]) { if (a && !parts.includes(a)) parts.push(a); }
                    return parts.join(" ").trim().slice(0, 120);
                })(),
            });
        }
    }
    return boxes;
}
"""


class VisionService:
    """Service de detection d'elements UI par Computer Vision."""

    def __init__(self):
        self._yolo_model = None
        self._yolo_loaded = False

    # ------------------------------------------------------------------
    # Model loading
    # ------------------------------------------------------------------

    def _ensure_yolo(self):
        """Charge le modele YOLO si disponible."""
        if self._yolo_loaded:
            return
        self._yolo_loaded = True

        model_path = settings.YOLO_MODEL_PATH
        if os.path.isfile(model_path):
            try:
                from ultralytics import YOLO
                self._yolo_model = YOLO(model_path)
                logger.info("YOLOv8 model loaded from %s", model_path)
            except Exception as e:
                logger.warning("Failed to load YOLO model: %s", e)
        else:
            logger.info(
                "No YOLO model at %s — running in OCR-only mode", model_path
            )

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def detect_elements(self, image_bytes: bytes) -> UIDetectionResponse:
        """Detecte les elements UI sur un screenshot (YOLO + OCR)."""
        start = time.time()
        self._ensure_yolo()

        # Decode image
        nparr = np.frombuffer(image_bytes, np.uint8)
        img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img_bgr is None:
            return UIDetectionResponse(elements=[], nombre_elements=0, temps_traitement_ms=0)

        elements: list[DetectedElement] = []

        if self._yolo_model is not None:
            elements = self._detect_with_yolo(img_bgr)
        else:
            # Fallback: heuristic contour-based detection + OCR
            elements = self._detect_with_contours(img_bgr)

        # Run OCR on each detected element to read its text
        for elem in elements:
            if not elem.texte_ocr:
                elem.texte_ocr = self._ocr_region(img_bgr, elem.bounding_box)

        elapsed_ms = (time.time() - start) * 1000
        return UIDetectionResponse(
            elements=elements,
            nombre_elements=len(elements),
            temps_traitement_ms=round(elapsed_ms, 2),
        )

    def detect_from_file(self, image_path: str) -> UIDetectionResponse:
        """Detecte les elements UI a partir d'un chemin de fichier."""
        with open(image_path, "rb") as f:
            return self.detect_elements(f.read())

    async def detect_from_page(self, page, image_bytes: bytes) -> UIDetectionResponse:
        """
        Pipeline hybride : YOLO (visuel) + DOM F12 (structurel).

        Parameters
        ----------
        page        : playwright Page object (navigateur ouvert)
        image_bytes : screenshot de la page courante (PNG bytes)

        Fonctionnement
        --------------
        1. YOLO analyse le screenshot → detections visuelles
        2. JavaScript querySelectorAll (= onglet Elements F12) → bboxes DOM reelles
        3. Fusion : les elements DOM qui ne chevauchent pas un resultat YOLO
           (IoU < 0.4) sont ajoutes comme detections complementaires
        4. OCR sur chaque element pour lire son texte
        """
        start = time.time()
        self._ensure_yolo()

        nparr = np.frombuffer(image_bytes, np.uint8)
        img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img_bgr is None:
            return UIDetectionResponse(elements=[], nombre_elements=0, temps_traitement_ms=0)

        # Step 1 — YOLO visual detection
        if self._yolo_model is not None:
            yolo_elements = self._detect_with_yolo(img_bgr)
        else:
            yolo_elements = self._detect_with_contours(img_bgr)

        # Step 2 — DOM extraction via live browser (F12 equivalent)
        dom_elements = await self._extract_dom_elements(page)

        # Step 3 — Merge: keep all YOLO results + DOM results not already covered
        merged = self._merge_yolo_dom(yolo_elements, dom_elements)

        # Step 4 — OCR text on every element
        for elem in merged:
            if not elem.texte_ocr:
                elem.texte_ocr = self._ocr_region(img_bgr, elem.bounding_box)

        elapsed_ms = (time.time() - start) * 1000
        logger.info(
            "Hybrid detection: %d YOLO + %d DOM → %d merged (%.0f ms)",
            len(yolo_elements), len(dom_elements), len(merged), elapsed_ms,
        )
        return UIDetectionResponse(
            elements=merged,
            nombre_elements=len(merged),
            temps_traitement_ms=round(elapsed_ms, 2),
        )

    async def detect_from_page_with_breakdown(
        self, page, image_bytes: bytes,
    ) -> tuple[UIDetectionResponse, list[DetectedElement], list[DetectedElement]]:
        """
        Same as detect_from_page() but also returns the raw YOLO and DOM
        element lists separately, for diagnostic / annotation use cases.

        Returns: (merged_response, yolo_only, dom_only)
        """
        start = time.time()
        self._ensure_yolo()

        nparr = np.frombuffer(image_bytes, np.uint8)
        img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img_bgr is None:
            empty = UIDetectionResponse(elements=[], nombre_elements=0, temps_traitement_ms=0)
            return empty, [], []

        if self._yolo_model is not None:
            yolo_elements = self._detect_with_yolo(img_bgr)
        else:
            yolo_elements = self._detect_with_contours(img_bgr)

        dom_elements = await self._extract_dom_elements(page)
        merged = self._merge_yolo_dom(yolo_elements, dom_elements)

        for elem in merged:
            if not elem.texte_ocr:
                elem.texte_ocr = self._ocr_region(img_bgr, elem.bounding_box)
        for elem in yolo_elements:
            if not elem.texte_ocr:
                elem.texte_ocr = self._ocr_region(img_bgr, elem.bounding_box)

        elapsed_ms = (time.time() - start) * 1000
        resp = UIDetectionResponse(
            elements=merged,
            nombre_elements=len(merged),
            temps_traitement_ms=round(elapsed_ms, 2),
        )
        return resp, yolo_elements, dom_elements

    # ------------------------------------------------------------------
    # DOM extraction (F12 equivalent)
    # ------------------------------------------------------------------

    async def _extract_dom_elements(self, page) -> list[DetectedElement]:
        """
        Lit le DOM de la page via JavaScript getBoundingClientRect()
        exactement comme le fait l'onglet Elements de DevTools (F12).
        """
        elements: list[DetectedElement] = []
        idx = 0

        for cls_name, selector in _DOM_SELECTORS.items():
            try:
                boxes = await page.evaluate(_JS_GET_BOXES, selector)
            except Exception as e:
                logger.debug("DOM selector failed for %s: %s", cls_name, e)
                continue

            for box in boxes:
                x, y, w, h = int(box["x"]), int(box["y"]), int(box["w"]), int(box["h"])
                if w < 8 or h < 6:
                    continue
                elements.append(
                    DetectedElement(
                        id=f"dom_{idx:04d}",
                        type=cls_name,
                        label=box.get("text", ""),
                        bounding_box=BoundingBox(x=x, y=y, width=w, height=h),
                        confiance_detection=0.85,   # DOM is ground-truth accurate
                        texte_ocr=box.get("text", ""),
                        source="dom",
                    )
                )
                idx += 1

        return elements

    # ------------------------------------------------------------------
    # Merge YOLO + DOM results
    # ------------------------------------------------------------------

    def _merge_yolo_dom(
        self,
        yolo: list[DetectedElement],
        dom: list[DetectedElement],
        iou_threshold: float = 0.40,
    ) -> list[DetectedElement]:
        """
        Fusionne les detections YOLO et DOM.

        Strategie :
        - Garde TOUS les resultats YOLO (confiance visuelle)
        - Ajoute les elements DOM dont l'IoU avec tout element YOLO < seuil
          (= elements que YOLO a rates, rattrapes par le DOM)
        - Les elements DOM qui chevauchent un resultat YOLO sont ignores
          (YOLO est suffisant et plus precis visuellement)
        """
        merged = list(yolo)

        for dom_elem in dom:
            db = dom_elem.bounding_box
            covered = any(
                self._iou(db, ye.bounding_box) >= iou_threshold
                for ye in yolo
            )
            if not covered:
                merged.append(dom_elem)

        # Re-index IDs
        for i, elem in enumerate(merged):
            elem.id = f"elem_{i:03d}"

        return merged

    @staticmethod
    def _iou(a: BoundingBox, b: BoundingBox) -> float:
        """Intersection over Union entre deux bounding boxes."""
        ax1, ay1 = a.x, a.y
        ax2, ay2 = a.x + a.width, a.y + a.height
        bx1, by1 = b.x, b.y
        bx2, by2 = b.x + b.width, b.y + b.height

        inter_w = max(0, min(ax2, bx2) - max(ax1, bx1))
        inter_h = max(0, min(ay2, by2) - max(ay1, by1))
        inter   = inter_w * inter_h

        area_a = (ax2 - ax1) * (ay2 - ay1)
        area_b = (bx2 - bx1) * (by2 - by1)
        union  = area_a + area_b - inter

        return inter / union if union > 0 else 0.0

    # ------------------------------------------------------------------
    # Screenshot annotation (debug / report)
    # ------------------------------------------------------------------

    @staticmethod
    def annotate_screenshot(
        image_bytes: bytes,
        yolo_elements: list[DetectedElement],
        dom_elements: list[DetectedElement],
        winner: Optional[DetectedElement] = None,
    ) -> bytes:
        """
        Draw colored rectangles + labels on a screenshot for diagnostic reports.

        Color scheme:
            - YOLO detections   → red    (BGR 0,0,220)
            - DOM detections    → blue   (BGR 220,0,0)
            - Chosen winner     → green  (BGR 0,200,0), thicker outline + glow

        Returns PNG bytes.
        """
        nparr = np.frombuffer(image_bytes, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img is None:
            return image_bytes

        overlay = img.copy()

        def _draw(elem: DetectedElement, color, thickness, label_prefix=""):
            bb = elem.bounding_box
            x1, y1 = bb.x, bb.y
            x2, y2 = bb.x + bb.width, bb.y + bb.height
            cv2.rectangle(overlay, (x1, y1), (x2, y2), color, thickness)
            text = label_prefix + elem.type
            txt = (elem.texte_ocr or elem.label or "").strip()
            if txt:
                text += f" · {txt[:24]}"
            text += f" ({elem.confiance_detection:.2f})"
            (tw, th), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 0.42, 1)
            ty = max(y1 - 6, th + 6)
            cv2.rectangle(overlay, (x1, ty - th - 4), (x1 + tw + 6, ty + 2), color, -1)
            cv2.putText(overlay, text, (x1 + 3, ty - 2), cv2.FONT_HERSHEY_SIMPLEX,
                        0.42, (255, 255, 255), 1, cv2.LINE_AA)

        # YOLO — red, semi-transparent
        for e in yolo_elements:
            _draw(e, (0, 0, 220), 1, "YOLO ")
        # DOM — blue, semi-transparent
        for e in dom_elements:
            _draw(e, (220, 80, 0), 1, "DOM ")

        # Blend overlay with original to soften the noise
        img = cv2.addWeighted(overlay, 0.65, img, 0.35, 0)

        # Winner — solid green on top, no transparency
        if winner is not None:
            bb = winner.bounding_box
            x1, y1 = bb.x, bb.y
            x2, y2 = bb.x + bb.width, bb.y + bb.height
            # outer glow
            cv2.rectangle(img, (x1 - 4, y1 - 4), (x2 + 4, y2 + 4), (0, 255, 0), 2)
            cv2.rectangle(img, (x1, y1), (x2, y2), (0, 200, 0), 3)
            wt = f"WINNER · {winner.type} · {winner.source}"
            (tw, th), _ = cv2.getTextSize(wt, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 2)
            cv2.rectangle(img, (x1, y2 + 4), (x1 + tw + 8, y2 + th + 12), (0, 200, 0), -1)
            cv2.putText(img, wt, (x1 + 4, y2 + th + 8), cv2.FONT_HERSHEY_SIMPLEX,
                        0.5, (255, 255, 255), 2, cv2.LINE_AA)

        # Top-left legend
        legend_x, legend_y = 12, 12
        items = [("YOLO", (0, 0, 220)), ("DOM", (220, 80, 0)), ("WINNER", (0, 200, 0))]
        cv2.rectangle(img, (legend_x - 4, legend_y - 4), (legend_x + 240, legend_y + 26), (40, 40, 40), -1)
        cx = legend_x + 4
        for label, color in items:
            cv2.rectangle(img, (cx, legend_y + 4), (cx + 14, legend_y + 18), color, -1)
            cv2.putText(img, label, (cx + 18, legend_y + 16), cv2.FONT_HERSHEY_SIMPLEX,
                        0.42, (255, 255, 255), 1, cv2.LINE_AA)
            cx += 70

        ok, buf = cv2.imencode(".png", img)
        return buf.tobytes() if ok else image_bytes

    # ------------------------------------------------------------------
    # YOLO detection
    # ------------------------------------------------------------------

    def _detect_with_yolo(
        self,
        img_bgr: np.ndarray,
        conf: Optional[float] = None,
    ) -> list[DetectedElement]:
        """Detection via YOLOv8."""
        threshold = conf if conf is not None else settings.YOLO_CONF_THRESHOLD
        results = self._yolo_model.predict(img_bgr, conf=threshold, verbose=False)
        elements: list[DetectedElement] = []

        for result in results:
            for i, box in enumerate(result.boxes):
                x1, y1, x2, y2 = map(int, box.xyxy[0].tolist())
                cls_id = int(box.cls[0])
                conf = float(box.conf[0])
                cls_name = (
                    YOLO_CLASSES[cls_id]
                    if cls_id < len(YOLO_CLASSES)
                    else f"class_{cls_id}"
                )

                elements.append(
                    DetectedElement(
                        id=f"elem_{i:03d}",
                        type=cls_name,
                        label="",
                        bounding_box=BoundingBox(
                            x=x1, y=y1, width=x2 - x1, height=y2 - y1
                        ),
                        confiance_detection=round(conf, 3),
                        texte_ocr="",
                        source="yolo",
                    )
                )
        return elements

    # ------------------------------------------------------------------
    # Contour-based fallback detection
    # ------------------------------------------------------------------

    def _detect_with_contours(self, img_bgr: np.ndarray) -> list[DetectedElement]:
        """Fallback: detect rectangular UI elements via contours + morphology."""
        gray = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2GRAY)
        h, w = gray.shape

        # Adaptive threshold
        thresh = cv2.adaptiveThreshold(
            gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
            cv2.THRESH_BINARY_INV, 15, 4,
        )

        # Morphological close to merge nearby components
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (15, 5))
        closed = cv2.morphologyEx(thresh, cv2.MORPH_CLOSE, kernel)

        contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        elements: list[DetectedElement] = []
        min_area = (w * h) * 0.0005  # skip tiny noise
        max_area = (w * h) * 0.5     # skip full-page contours

        for i, cnt in enumerate(contours):
            x, y, bw, bh = cv2.boundingRect(cnt)
            area = bw * bh
            if area < min_area or area > max_area:
                continue
            if bw < 20 or bh < 10:
                continue

            aspect = bw / max(bh, 1)
            # Classify by shape heuristics
            if aspect > 3 and bh < 60:
                elem_type = "input_text"
            elif 0.7 < aspect < 4 and bh < 80:
                elem_type = "button"
            else:
                elem_type = "label"

            elements.append(
                DetectedElement(
                    id=f"elem_{i:03d}",
                    type=elem_type,
                    label="",
                    bounding_box=BoundingBox(x=x, y=y, width=bw, height=bh),
                    confiance_detection=0.50,
                    texte_ocr="",
                    source="contour",
                )
            )

        return elements[:50]  # cap to avoid noise

    # ------------------------------------------------------------------
    # OCR
    # ------------------------------------------------------------------

    def _ocr_region(self, img_bgr: np.ndarray, bbox: BoundingBox) -> str:
        """Extrait le texte d'une region via Tesseract OCR."""
        try:
            import pytesseract
        except ImportError:
            return ""

        x, y, w, h = bbox.x, bbox.y, bbox.width, bbox.height
        # Add small padding
        pad = 4
        ih, iw = img_bgr.shape[:2]
        x1 = max(0, x - pad)
        y1 = max(0, y - pad)
        x2 = min(iw, x + w + pad)
        y2 = min(ih, y + h + pad)

        crop = img_bgr[y1:y2, x1:x2]
        if crop.size == 0:
            return ""

        # Pre-process for OCR
        gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
        gray = cv2.resize(gray, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        pil_img = Image.fromarray(binary)
        try:
            text = pytesseract.image_to_string(
                pil_img, lang="fra+eng", config="--psm 7"
            ).strip()
        except Exception:
            text = pytesseract.image_to_string(pil_img, config="--psm 7").strip()

        # Clean up common OCR noise
        text = text.replace("\n", " ").strip()
        return text

    def ocr_full_image(self, image_bytes: bytes) -> str:
        """Extrait tout le texte d'une image via OCR."""
        try:
            import pytesseract
        except ImportError:
            return ""

        nparr = np.frombuffer(image_bytes, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img is None:
            return ""
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        pil_img = Image.fromarray(gray)
        try:
            return pytesseract.image_to_string(pil_img, lang="fra+eng").strip()
        except Exception:
            return pytesseract.image_to_string(pil_img).strip()

    # ------------------------------------------------------------------
    # Gherkin step  ↔  detected element  matching
    # ------------------------------------------------------------------

    # Keywords that hint at an element type in a Gherkin step
    _TYPE_HINTS: dict[str, list[str]] = {
        "button":         ["bouton", "button", "btn", "submit", "valider", "confirmer",
                           "envoyer", "send", "save", "sauvegarder", "ok"],
        "input_text":     ["champ", "field", "input", "zone", "saisir", "entrer",
                           "remplir", "fill", "type", "saisis", "tape"],
        "input_password": ["mot de passe", "password", "passwd", "mdp"],
        "link":           ["lien", "link", "href", "cliquer sur le lien"],
        "checkbox":       ["case", "checkbox", "coche", "check", "cocher", "decocher"],
        "dropdown":       ["liste", "dropdown", "select", "selectionner", "menu",
                           "option", "choisir", "choose"],
        "label":          ["texte", "text", "message", "titre", "title", "label"],
    }

    @staticmethod
    def _normalize_text(text: str) -> str:
        """Lowercase, strip accents and punctuation for robust comparison."""
        import unicodedata
        text = unicodedata.normalize("NFD", text.lower())
        text = "".join(c for c in text if unicodedata.category(c) != "Mn")
        # Keep only alphanumeric + spaces
        text = "".join(c if c.isalnum() or c == " " else " " for c in text)
        return " ".join(text.split())  # collapse whitespace

    @staticmethod
    def _text_similarity(a: str, b: str) -> float:
        """
        Simple overlap-based similarity between two normalised strings.
        Returns 0.0 – 1.0.
        """
        if not a or not b:
            return 0.0
        # Token overlap (Jaccard)
        tokens_a = set(a.split())
        tokens_b = set(b.split())
        if not tokens_a or not tokens_b:
            return 0.0
        intersection = tokens_a & tokens_b
        union = tokens_a | tokens_b
        jaccard = len(intersection) / len(union)
        # Substring bonus: if one fully contains the other
        bonus = 0.25 if (a in b or b in a) else 0.0
        return min(1.0, jaccard + bonus)

    def _detect_type_hints(self, step_text: str) -> list[str]:
        """Return YOLO class names hinted by the step text."""
        norm = self._normalize_text(step_text)
        hints = []
        for cls, keywords in self._TYPE_HINTS.items():
            if any(kw in norm for kw in keywords):
                hints.append(cls)
        return hints

    def match_elements(
        self,
        step_text: str,
        elements: list[DetectedElement],
        top_k: int = 3,
    ) -> list[tuple[DetectedElement, float]]:
        """
        Match a Gherkin step text to detected elements.

        Matching score (0–1) :
          - 0.40  type match  (element type matches intent keyword in step)
          - 0.60  text similarity  (OCR text vs quoted target in step)

        Returns a list of (element, score) sorted by score desc, limited to top_k.

        Example
        -------
        step  : 'je clique sur le bouton "Login"'
        match : button element whose OCR text contains "Login"  → score ~0.85
        """
        import re

        # Extract quoted target from step (highest signal)
        quoted = re.findall(r'["\u00ab\u00bb](.*?)["\u00ab\u00bb]', step_text)
        target = quoted[0] if quoted else step_text
        target_norm = self._normalize_text(target)

        type_hints = self._detect_type_hints(step_text)

        scored: list[tuple[DetectedElement, float]] = []
        for elem in elements:
            # Text similarity component (60%)
            ocr_norm = self._normalize_text(elem.texte_ocr or elem.label or "")
            text_score = self._text_similarity(target_norm, ocr_norm) * 0.60

            # Type match component (40%)
            type_score = 0.40 if (elem.type in type_hints) else 0.0

            total = text_score + type_score
            if total > 0.0:
                scored.append((elem, round(total, 3)))

        scored.sort(key=lambda x: x[1], reverse=True)
        return scored[:top_k]

    # ------------------------------------------------------------------
    # YOLO vs Contours benchmark
    # ------------------------------------------------------------------

    def benchmark_vs_contours(
        self, image_bytes: bytes
    ) -> dict:
        """
        Run both YOLO and contour detection on the same image.
        Returns a comparison dict useful for analysis and tests.
        """
        start = time.time()
        self._ensure_yolo()

        nparr = np.frombuffer(image_bytes, np.uint8)
        img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img_bgr is None:
            return {"error": "Cannot decode image"}

        # YOLO
        yolo_elems: list[DetectedElement] = []
        if self._yolo_model is not None:
            yolo_elems = self._detect_with_yolo(img_bgr)
            for e in yolo_elems:
                e.texte_ocr = self._ocr_region(img_bgr, e.bounding_box)

        # Contours
        cont_elems = self._detect_with_contours(img_bgr)
        for e in cont_elems:
            e.texte_ocr = self._ocr_region(img_bgr, e.bounding_box)

        elapsed_ms = (time.time() - start) * 1000

        def summarise(elems: list[DetectedElement]) -> dict:
            by_type: dict[str, int] = {}
            for e in elems:
                by_type[e.type] = by_type.get(e.type, 0) + 1
            avg_conf = (
                round(sum(e.confiance_detection for e in elems) / len(elems), 3)
                if elems else 0.0
            )
            with_text = sum(1 for e in elems if e.texte_ocr)
            return {
                "total": len(elems),
                "avg_confidence": avg_conf,
                "with_ocr_text": with_text,
                "by_type": by_type,
            }

        return {
            "yolo":     summarise(yolo_elems),
            "contours": summarise(cont_elems),
            "temps_ms": round(elapsed_ms, 1),
            "yolo_available": self._yolo_model is not None,
        }
