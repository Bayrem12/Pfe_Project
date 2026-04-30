"""
Schemas Pydantic pour la detection d'elements UI.
"""

from pydantic import BaseModel, Field


class BoundingBox(BaseModel):
    """Rectangle delimitant un element detecte."""

    x: int
    y: int
    width: int
    height: int


class DetectedElement(BaseModel):
    """Un element UI detecte sur un screenshot."""

    id: str
    type: str = Field(..., description="button, input_text, input_password, link, checkbox, radio, dropdown, etc.")
    label: str = ""
    bounding_box: BoundingBox
    confiance_detection: float
    texte_ocr: str = ""
    placeholder: str = ""
    source: str = Field(default="yolo", description="yolo | dom | contour")


class UIDetectionResponse(BaseModel):
    """Reponse de la detection d'elements UI."""

    elements: list[DetectedElement]
    nombre_elements: int
    temps_traitement_ms: float
