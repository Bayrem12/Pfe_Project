"""Tests unitaires pour le service de generation de scripts."""

from app.services.generator_service import GeneratorService
from app.schemas.test_schemas import TestGenerationRequest
from app.schemas.gherkin_schemas import ParsedStep, ExtractedEntity


service = GeneratorService()


def test_generate_script_basic():
    request = TestGenerationRequest(
        scenario_name="Connexion reussie",
        url_cible="https://example.com/login",
        steps=[
            ParsedStep(
                step_type="Given",
                raw_text="je suis sur la page de connexion",
                intention="naviguer",
                entites=[],
                confiance=0.9,
            ),
        ],
    )
    response = service.generate(request)
    assert "Connexion reussie" in response.script_code
    assert response.script_filename.endswith(".py")
    assert "playwright" in response.script_code.lower()


def test_generate_click_action():
    request = TestGenerationRequest(
        scenario_name="Test click",
        url_cible="",
        steps=[
            ParsedStep(
                step_type="When",
                raw_text='je clique sur le bouton "Login"',
                intention="cliquer",
                entites=[
                    ExtractedEntity(nom="cible", valeur="Login", type_entite="cible"),
                    ExtractedEntity(nom="type_element", valeur="button", type_entite="element_type"),
                ],
                confiance=0.95,
            ),
        ],
    )
    response = service.generate(request)
    assert "click" in response.script_code.lower()
    assert "Login" in response.script_code


def test_generate_fill_action():
    request = TestGenerationRequest(
        scenario_name="Test fill",
        url_cible="",
        steps=[
            ParsedStep(
                step_type="When",
                raw_text='je saisis "admin" dans le champ "Username"',
                intention="saisir_texte",
                entites=[
                    ExtractedEntity(nom="valeur", valeur="admin", type_entite="valeur"),
                    ExtractedEntity(nom="cible", valeur="Username", type_entite="cible"),
                    ExtractedEntity(nom="type_element", valeur="input", type_entite="element_type"),
                ],
                confiance=0.92,
            ),
        ],
    )
    response = service.generate(request)
    assert "fill" in response.script_code.lower()
    assert "admin" in response.script_code


def test_generate_verify_text():
    request = TestGenerationRequest(
        scenario_name="Test verify",
        url_cible="",
        steps=[
            ParsedStep(
                step_type="Then",
                raw_text='je vois le texte "Dashboard"',
                intention="verifier_texte",
                entites=[
                    ExtractedEntity(nom="valeur", valeur="Dashboard", type_entite="valeur"),
                ],
                confiance=0.90,
            ),
        ],
    )
    response = service.generate(request)
    assert "Dashboard" in response.script_code
    assert "expect" in response.script_code.lower()
