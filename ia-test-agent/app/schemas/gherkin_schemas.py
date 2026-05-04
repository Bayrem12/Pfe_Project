"""
Schemas Pydantic pour les scenarios Gherkin et le pipeline NLP.
"""

from pydantic import BaseModel, Field


# --- Entrees ---


class GherkinStep(BaseModel):
    """Un step individuel d'un scenario Gherkin."""

    keyword: str = Field(..., description="Given, When, Then, And, But")
    text: str = Field(..., description="Le texte du step")


class GherkinScenarioRequest(BaseModel):
    """Requete de parsing d'un scenario Gherkin."""

    scenario_name: str = Field(..., description="Nom du scenario")
    steps: list[GherkinStep] = Field(..., description="Liste des steps")
    language: str = Field(default="fr", description="Langue du scenario (fr/en)")


# --- Sorties NLP ---


class ExtractedEntity(BaseModel):
    """Une entite extraite par le NLP."""

    nom: str
    valeur: str
    type_entite: str


class ParsedStep(BaseModel):
    """Resultat du parsing NLP d'un step."""

    step_type: str
    raw_text: str
    intention: str
    entites: list[ExtractedEntity]
    confiance: float


class GherkinParseResponse(BaseModel):
    """Reponse complete du parsing d'un scenario."""

    scenario_name: str
    nombre_steps: int
    steps_analyses: list[ParsedStep]
    temps_traitement_ms: float


# --- Mapping semantique ---


class MappingRequest(BaseModel):
    """Requete de mapping semantique."""

    parsed_steps: list[ParsedStep] = Field(..., description="Steps analyses par le NLP")
    ui_elements: list[dict] = Field(default=[], description="Elements UI detectes (optionnel)")


class MappedAction(BaseModel):
    """Une action UI mappee a partir d'une intention NLP."""

    step_type: str
    intention: str
    action_type: str
    selector: str = ""
    params: dict = {}
    score_correspondance: float = 0.0


class MappingResponse(BaseModel):
    """Reponse du mapping semantique."""

    actions: list[MappedAction]


# --- Parse-feature ---

class RawStepSchema(BaseModel):
    keyword: str
    text: str
    line_number: int = 0


class ScenarioSchema(BaseModel):
    name: str
    tags: list[str] = []
    is_outline: bool = False
    steps: list[RawStepSchema]
    examples: list[dict] = []


class FeatureParseResponse(BaseModel):
    """Reponse de l'endpoint /parse-feature."""

    feature: str
    description: str = ""
    tags: list[str] = []
    background: list[RawStepSchema] = []
    scenarios: list[ScenarioSchema]
    stats: dict
    parsing_errors: list[str] = []
    source_filename: str = ""
