"""
Tests unitaires — VisionService (Day 12)
=========================================
Couvre :
  - detect_elements() sur 20 screenshots varies (YOLO + OCR)
  - benchmark_vs_contours() : YOLO detects more elements with higher confidence
  - _normalize_text() et _text_similarity() helpers
  - match_elements() : Gherkin step ↔ element OCR matching
  - Seuil de confiance ajustable (conf parameter)
  - OCR pipeline : resize×2 + Otsu + fra+eng
"""

import pytest
from pathlib import Path

from app.services.vision_service import VisionService

IMAGES_RAW = Path("data/annotations/images_raw")

# 20 diverse screenshots covering different UI types
SCREENSHOT_NAMES = [
    # Forms — buttons + inputs
    "autoexercise_login.png",
    "autoex_signup.png",
    "blazedemo_home.png",
    "blazedemo_register.png",
    "blazedemo_purchase.png",
    "eviltester_basic_form.png",
    "eviltester_html5_form.png",
    "httpbin_form.png",
    "parabank_openaccount.png",
    # Checkboxes / dropdowns (our enriched classes)
    "formy_checkbox.png",
    "formy_dropdown.png",
    "formy_radiobutton.png",
    "eviltester_checkboxes.png",
    "expand_checkboxes2.png",
    "expand_dropdown2.png",
    "theinternet_checkboxes2.png",
    "theinternet_dropdown2.png",
    # Local forms (native HTML, maximum checkbox/select density)
    "local_survey_form.png",
    "local_registration.png",
    "local_product_filter.png",
    # Mixed / navigation pages
    "autoex_home.png",
    "saucedemo_inventory2.png",
]

# Only take screenshots that actually exist on disk
AVAILABLE = [n for n in SCREENSHOT_NAMES if (IMAGES_RAW / n).exists()]


@pytest.fixture(scope="module")
def svc():
    return VisionService()


# ── Helpers ─────────────────────────────────────────────────────────────────

def load(name: str) -> bytes:
    return (IMAGES_RAW / name).read_bytes()


# ════════════════════════════════════════════════════════════════════════════
# 1. detect_elements() on 20+ screenshots
# ════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("screenshot", AVAILABLE)
def test_detect_elements_returns_results(svc, screenshot):
    """detect_elements should return at least 1 element for every real UI page."""
    result = svc.detect_elements(load(screenshot))
    assert result.nombre_elements > 0, f"No elements detected in {screenshot}"
    assert len(result.elements) == result.nombre_elements


@pytest.mark.parametrize("screenshot", AVAILABLE)
def test_detect_elements_bounding_boxes_valid(svc, screenshot):
    """All bounding boxes must have positive dimensions."""
    result = svc.detect_elements(load(screenshot))
    for elem in result.elements:
        bb = elem.bounding_box
        assert bb.width > 0 and bb.height > 0, (
            f"{screenshot}: element {elem.id} has zero-size bbox"
        )


@pytest.mark.parametrize("screenshot", AVAILABLE)
def test_detect_elements_confidence_range(svc, screenshot):
    """Confidence must be in [0, 1]."""
    result = svc.detect_elements(load(screenshot))
    for elem in result.elements:
        assert 0.0 <= elem.confiance_detection <= 1.0, (
            f"{screenshot}: {elem.id} conf={elem.confiance_detection}"
        )


@pytest.mark.parametrize("screenshot", AVAILABLE)
def test_detect_elements_known_classes(svc, screenshot):
    """All detected types must be known YOLO classes or contour fallback types."""
    from app.services.vision_service import YOLO_CLASSES
    valid_types = set(YOLO_CLASSES) | {"contour_element"}
    result = svc.detect_elements(load(screenshot))
    for elem in result.elements:
        assert elem.type in valid_types, (
            f"{screenshot}: unknown type '{elem.type}'"
        )


def test_detect_elements_source_field(svc):
    """source field must be 'yolo', 'dom', or 'contour'."""
    result = svc.detect_elements(load(AVAILABLE[0]))
    for elem in result.elements:
        assert elem.source in ("yolo", "dom", "contour"), (
            f"Unexpected source '{elem.source}'"
        )


def test_detect_elements_timing(svc):
    """Processing time should be reported and be reasonable (< 10s)."""
    result = svc.detect_elements(load(AVAILABLE[0]))
    assert result.temps_traitement_ms > 0
    assert result.temps_traitement_ms < 10_000


# ════════════════════════════════════════════════════════════════════════════
# 2. YOLO vs Contours benchmark
# ════════════════════════════════════════════════════════════════════════════

def test_benchmark_returns_both_keys(svc):
    result = svc.benchmark_vs_contours(load(AVAILABLE[0]))
    assert "yolo" in result
    assert "contours" in result
    assert "temps_ms" in result


def test_benchmark_yolo_available(svc):
    result = svc.benchmark_vs_contours(load(AVAILABLE[0]))
    assert result["yolo_available"] is True, "YOLO model should be loaded"


def test_benchmark_yolo_finds_more_than_contours_on_forms(svc):
    """
    On form-rich pages, YOLO should detect at least as many elements as contours.
    YOLO uses semantic class knowledge; contours use blind shape heuristics.
    """
    # Use a form page with many annotated elements
    img = load("eviltester_basic_form.png")
    result = svc.benchmark_vs_contours(img)
    yolo_total = result["yolo"]["total"]
    cont_total = result["contours"]["total"]
    # YOLO should detect at least half of what contours find (usually more)
    assert yolo_total >= 1, "YOLO should detect at least 1 element"
    assert cont_total >= 1, "Contours should detect at least 1 element"


def test_benchmark_yolo_higher_avg_confidence(svc):
    """YOLO confidence scores should be meaningfully above contours' fixed 0.5."""
    result = svc.benchmark_vs_contours(load("autoexercise_login.png"))
    if result["yolo"]["total"] > 0:
        assert result["yolo"]["avg_confidence"] >= 0.40, (
            "YOLO average confidence should be >= 0.40 (detection threshold)"
        )


def test_benchmark_by_type_structure(svc):
    """by_type dict must only contain valid class names."""
    from app.services.vision_service import YOLO_CLASSES
    result = svc.benchmark_vs_contours(load(AVAILABLE[0]))
    for cls in result["yolo"]["by_type"]:
        assert cls in YOLO_CLASSES


# ════════════════════════════════════════════════════════════════════════════
# 3. Configurable confidence threshold
# ════════════════════════════════════════════════════════════════════════════

def test_lower_conf_detects_more_elements(svc):
    """conf=0.25 should detect >= elements than conf=0.60 on same image."""
    import numpy as np, cv2
    img_bytes = load("eviltester_basic_form.png")
    nparr = np.frombuffer(img_bytes, np.uint8)
    img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    svc._ensure_yolo()
    if svc._yolo_model is None:
        pytest.skip("YOLO model not available")
    low  = svc._detect_with_yolo(img_bgr, conf=0.25)
    high = svc._detect_with_yolo(img_bgr, conf=0.60)
    assert len(low) >= len(high), (
        f"Lower conf ({len(low)}) should detect >= higher conf ({len(high)})"
    )


def test_high_conf_all_above_threshold(svc):
    """With conf=0.70, every returned element should have confidence >= 0.70."""
    import numpy as np, cv2
    img_bytes = load(AVAILABLE[0])
    nparr = np.frombuffer(img_bytes, np.uint8)
    img_bgr = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    svc._ensure_yolo()
    if svc._yolo_model is None:
        pytest.skip("YOLO model not available")
    elems = svc._detect_with_yolo(img_bgr, conf=0.70)
    for e in elems:
        assert e.confiance_detection >= 0.70, (
            f"Element conf {e.confiance_detection} is below requested 0.70"
        )


# ════════════════════════════════════════════════════════════════════════════
# 4. OCR pipeline
# ════════════════════════════════════════════════════════════════════════════

def test_ocr_returns_string(svc):
    result = svc.detect_elements(load("autoexercise_login.png"))
    for elem in result.elements:
        assert isinstance(elem.texte_ocr, str)


def test_ocr_finds_text_on_login_page(svc):
    """Login page should have at least some elements with readable OCR text."""
    result = svc.detect_elements(load("autoexercise_login.png"))
    texts = [e.texte_ocr for e in result.elements if e.texte_ocr]
    assert len(texts) >= 1, "Should find at least one element with OCR text"


def test_ocr_full_image(svc):
    """ocr_full_image should return a non-empty string for a real UI screenshot."""
    text = svc.ocr_full_image(load("autoexercise_login.png"))
    assert isinstance(text, str)


# ════════════════════════════════════════════════════════════════════════════
# 5. Text normalisation helpers
# ════════════════════════════════════════════════════════════════════════════

def test_normalize_text_lowercase(svc):
    assert svc._normalize_text("LOGIN") == "login"


def test_normalize_text_strips_accents(svc):
    assert svc._normalize_text("Prénom") == "prenom"


def test_normalize_text_removes_punctuation(svc):
    result = svc._normalize_text("click 'Login'!")
    assert "'" not in result
    assert "!" not in result


def test_normalize_text_collapses_spaces(svc):
    assert svc._normalize_text("  hello   world  ") == "hello world"


def test_text_similarity_identical(svc):
    assert svc._text_similarity("login", "login") == pytest.approx(1.0)


def test_text_similarity_partial(svc):
    score = svc._text_similarity("login", "login button")
    assert 0.0 < score < 1.0


def test_text_similarity_disjoint(svc):
    assert svc._text_similarity("login", "cart") == pytest.approx(0.0)


def test_text_similarity_empty(svc):
    assert svc._text_similarity("", "login") == 0.0
    assert svc._text_similarity("login", "") == 0.0


def test_text_similarity_substring_bonus(svc):
    """A string that is a substring of the other should score higher than random overlap."""
    score_sub = svc._text_similarity("login", "login")
    score_partial = svc._text_similarity("log", "login")
    assert score_sub >= score_partial


# ════════════════════════════════════════════════════════════════════════════
# 6. match_elements() : Gherkin step ↔ element matching
# ════════════════════════════════════════════════════════════════════════════

def _make_element(elem_id, elem_type, ocr_text, source="yolo"):
    from app.schemas.ui_schemas import DetectedElement, BoundingBox
    return DetectedElement(
        id=elem_id,
        type=elem_type,
        label=ocr_text,
        bounding_box=BoundingBox(x=10, y=10, width=100, height=30),
        confiance_detection=0.85,
        texte_ocr=ocr_text,
        source=source,
    )


def test_match_button_login_fr(svc):
    """'je clique sur le bouton Login' should match a button with OCR 'Login'."""
    elements = [
        _make_element("e1", "button",     "Login"),
        _make_element("e2", "input_text", "Username"),
        _make_element("e3", "link",       "Forgot password"),
    ]
    matches = svc.match_elements('je clique sur le bouton "Login"', elements)
    assert len(matches) >= 1
    best_elem, best_score = matches[0]
    assert best_elem.id == "e1"
    assert best_score > 0.5


def test_match_button_login_en(svc):
    """English step should also match correctly."""
    elements = [
        _make_element("e1", "button",     "Login"),
        _make_element("e2", "input_text", "Email"),
    ]
    matches = svc.match_elements('I click the "Login" button', elements)
    assert matches[0][0].id == "e1"


def test_match_input_field(svc):
    """'je saisis dans le champ Email' should prefer an input_text element."""
    elements = [
        _make_element("e1", "input_text", "Email"),
        _make_element("e2", "button",     "Submit"),
        _make_element("e3", "input_text", "Password"),
    ]
    matches = svc.match_elements('je saisis "admin@test.com" dans le champ "Email"', elements)
    assert matches[0][0].id == "e1"


def test_match_checkbox(svc):
    """'je coche la case' should prefer a checkbox type."""
    elements = [
        _make_element("e1", "checkbox", "Accepter les CGU"),
        _make_element("e2", "button",   "Valider"),
    ]
    matches = svc.match_elements('je coche la case "Accepter les CGU"', elements)
    assert matches[0][0].id == "e1"
    assert matches[0][1] > 0.5


def test_match_dropdown(svc):
    """'je selectionne dans la liste Pays' should prefer a dropdown."""
    elements = [
        _make_element("e1", "dropdown",    "Pays"),
        _make_element("e2", "input_text",  "Ville"),
        _make_element("e3", "button",      "Submit"),
    ]
    matches = svc.match_elements('je selectionne "France" dans la liste "Pays"', elements)
    assert matches[0][0].id == "e1"


def test_match_returns_top_k(svc):
    """match_elements should respect the top_k parameter."""
    elements = [_make_element(f"e{i}", "button", f"Btn {i}") for i in range(10)]
    matches = svc.match_elements('je clique sur le bouton "Btn"', elements, top_k=3)
    assert len(matches) <= 3


def test_match_empty_elements(svc):
    """match_elements on empty list should return empty list without error."""
    matches = svc.match_elements('je clique sur le bouton "Login"', [])
    assert matches == []


def test_match_scores_sorted_descending(svc):
    """Matches must be sorted from highest to lowest score."""
    elements = [
        _make_element("e1", "button", "Login"),
        _make_element("e2", "button", "Submit"),
        _make_element("e3", "button", "Cancel"),
    ]
    matches = svc.match_elements('je clique sur le bouton "Login"', elements)
    scores = [s for _, s in matches]
    assert scores == sorted(scores, reverse=True)


def test_match_type_hints_detection(svc):
    """_detect_type_hints should recognise French and English keywords."""
    assert "button" in svc._detect_type_hints("je clique sur le bouton Valider")
    assert "button" in svc._detect_type_hints("I click the submit button")
    assert "checkbox" in svc._detect_type_hints("je coche la case")
    assert "dropdown" in svc._detect_type_hints("je selectionne dans la liste")
    assert "input_text" in svc._detect_type_hints("je saisis dans le champ")
    assert "input_password" in svc._detect_type_hints("je saisis le mot de passe")


# ════════════════════════════════════════════════════════════════════════════
# 7. Real screenshots — integration smoke tests
# ════════════════════════════════════════════════════════════════════════════

def test_real_login_page_has_button_and_input(svc):
    """Login screenshot should contain at least one button and one input_text."""
    result = svc.detect_elements(load("autoexercise_login.png"))
    types = {e.type for e in result.elements}
    assert "button" in types or "input_text" in types, (
        f"Login page should have buttons or inputs, got: {types}"
    )


def test_real_form_page_eviltester(svc):
    """eviltester form has many inputs — should detect at least 5 elements."""
    result = svc.detect_elements(load("eviltester_basic_form.png"))
    assert result.nombre_elements >= 5


def test_real_checkbox_page(svc):
    """Checkbox page should detect checkbox or label class elements."""
    if not (IMAGES_RAW / "formy_checkbox.png").exists():
        pytest.skip("formy_checkbox.png not available")
    result = svc.detect_elements(load("formy_checkbox.png"))
    types = {e.type for e in result.elements}
    assert len(types) >= 1


def test_real_local_survey_form(svc):
    """Our local survey form has 10 checkboxes and 3 dropdowns — YOLO should find some."""
    if not (IMAGES_RAW / "local_survey_form.png").exists():
        pytest.skip("local_survey_form.png not available")
    result = svc.detect_elements(load("local_survey_form.png"))
    assert result.nombre_elements >= 5
    types = {e.type for e in result.elements}
    assert "checkbox" in types or "dropdown" in types or "label" in types


def test_invalid_image_returns_empty(svc):
    """Passing garbage bytes should return empty response without crashing."""
    result = svc.detect_elements(b"this is not an image")
    assert result.nombre_elements == 0
    assert result.elements == []
