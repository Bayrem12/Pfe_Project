"""Tests unitaires pour le service NLP."""

from app.services.nlp_service import NLPService
from app.schemas.gherkin_schemas import GherkinScenarioRequest, GherkinStep


service = NLPService()


# -- Intention classification --

def test_parse_step_click():
    result = service.parse_step("When", 'je clique sur le bouton "Valider"', "fr")
    assert result.intention == "cliquer"
    assert result.step_type == "When"
    assert result.confiance > 0.7


def test_parse_step_fill():
    result = service.parse_step("When", 'je saisis "admin" dans le champ "Username"', "fr")
    assert result.intention == "saisir_texte"
    assert len(result.entites) >= 2


def test_parse_step_navigate():
    result = service.parse_step("Given", "je suis sur la page de connexion", "fr")
    assert result.intention == "naviguer"


def test_parse_step_verify_text():
    result = service.parse_step("Then", 'je vois le message "Bienvenue"', "fr")
    assert result.intention == "verifier_texte"


def test_parse_step_select():
    result = service.parse_step("When", 'je selectionne "France" dans la liste "Pays"', "fr")
    assert result.intention == "selectionner"


def test_parse_step_check():
    result = service.parse_step("When", 'je coche la case "Accepter les CGU"', "fr")
    assert result.intention == "cocher"


def test_parse_step_wait():
    result = service.parse_step("When", "j'attends que le loader disparaisse", "fr")
    assert result.intention == "attendre"


def test_parse_step_verify_visible():
    result = service.parse_step("Then", 'le bouton "Supprimer" est visible', "fr")
    assert result.intention == "verifier_visibilite"


def test_parse_step_unknown():
    result = service.parse_step("Given", "quelque chose de totalement ambigu", "fr")
    assert result.confiance < 0.5


# -- Entity extraction --

def test_entity_extraction_two_quotes():
    result = service.parse_step("When", 'je saisis "admin@test.com" dans le champ "Email"', "fr")
    noms = {e.nom for e in result.entites}
    assert "valeur" in noms or "email" in noms
    assert "cible" in noms


def test_entity_extraction_email():
    result = service.parse_step("When", 'je saisis "nour@example.com" dans le champ "Email"', "fr")
    emails = [e for e in result.entites if e.nom == "email" or "email" in e.valeur]
    assert len(emails) >= 1


def test_entity_extraction_email_keeps_value_role_for_generator():
    result = service.parse_step("When", 'I type "nour@example.com" in the "Email" field', "en")
    entities = {e.nom: e.valeur for e in result.entites}
    assert entities["valeur"] == "nour@example.com"
    assert entities["cible"] == "Email"
    assert entities["email"] == "nour@example.com"


def test_entity_extraction_password_keeps_value_role_for_generator():
    result = service.parse_step("When", 'I type "SuperSecretPassword!" in the "Password" field', "en")
    entities = {e.nom: e.valeur for e in result.entites}
    assert entities["valeur"] == "SuperSecretPassword!"
    assert entities["cible"] == "Password"
    assert entities["mot_de_passe"] == "SuperSecretPassword!"


def test_entity_extraction_unquoted_email_english():
    result = service.parse_step("When", "I type nour@email.com in the email field", "en")
    entities = {e.nom: e.valeur for e in result.entites}
    assert entities["email"] == "nour@email.com"
    assert entities["valeur"] == "nour@email.com"
    assert entities["cible"] == "email field"


def test_entity_extraction_url():
    result = service.parse_step("Given", 'je navigue vers https://example.com/login', "fr")
    urls = [e for e in result.entites if e.type_entite == "url"]
    assert len(urls) == 1
    assert "example.com" in urls[0].valeur


def test_element_type_detection_button():
    result = service.parse_step("When", 'je clique sur le bouton "Login"', "fr")
    types = [e for e in result.entites if e.type_entite == "element_type"]
    assert any(e.valeur == "button" for e in types)


def test_element_type_detection_input():
    result = service.parse_step("When", 'je remplis le champ "Nom"', "fr")
    types = [e for e in result.entites if e.type_entite == "element_type"]
    assert any(e.valeur == "input" for e in types)


def test_entity_extraction_person_english():
    result = service.parse_step("Then", "John Doe is visible", "en")
    names = [e for e in result.entites if e.type_entite == "ner_personne"]
    assert any(e.valeur == "John Doe" for e in names)


def test_parse_step_imperative_access_english():
    result = service.parse_step("Given", "Access the dashboard", "en")
    assert result.intention == "naviguer"
    assert result.confiance >= 0.7


# -- Full scenario --

def test_parse_scenario():
    request = GherkinScenarioRequest(
        scenario_name="Connexion reussie",
        steps=[
            GherkinStep(keyword="Given", text="je suis sur la page de connexion"),
            GherkinStep(keyword="When", text='je saisis "admin" dans le champ "Username"'),
            GherkinStep(keyword="And", text='je saisis "password123" dans le champ "Password"'),
            GherkinStep(keyword="And", text='je clique sur le bouton "Login"'),
            GherkinStep(keyword="Then", text='je vois le texte "Dashboard"'),
        ],
        language="fr",
    )
    response = service.parse_scenario(request)
    assert response.nombre_steps == 5
    assert response.scenario_name == "Connexion reussie"
    assert response.temps_traitement_ms > 0

    # And steps should be resolved
    intentions = [s.intention for s in response.steps_analyses]
    assert intentions[0] == "naviguer"
    assert intentions[1] == "saisir_texte"
    assert intentions[2] == "saisir_texte"  # And → When
    assert intentions[3] == "cliquer"       # And → When
    assert intentions[4] == "verifier_texte"


def test_parse_scenario_english():
    request = GherkinScenarioRequest(
        scenario_name="Login success",
        steps=[
            GherkinStep(keyword="Given", text="I am on the login page"),
            GherkinStep(keyword="When", text='I type "admin" in the "Username" field'),
            GherkinStep(keyword="Then", text='I should see "Welcome"'),
        ],
        language="en",
    )
    response = service.parse_scenario(request)
    assert response.nombre_steps == 3
