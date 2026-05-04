# IA Test Agent

Agent intelligent d'automatisation des tests fonctionnels a partir de scenarios metier (Gherkin).

## Architecture

- **app/api/** - Endpoints REST (FastAPI)
- **app/services/** - Logique metier (NLP, Vision, Generation, Execution)
- **app/models/** - Chargement des modeles IA (spaCy, YOLO)
- **app/schemas/** - DTOs Pydantic (validation entrees/sorties)
- **app/templates/** - Templates Jinja2 (generation de code Playwright, rapports HTML)
- **app/utils/** - Fonctions utilitaires (parsing Gherkin, traitement images, similarite)
- **notebooks/** - Jupyter Notebooks d'exploration et experimentation
- **trained_models/** - Modeles entraines serialises
- **data/** - Jeux de donnees (scenarios Gherkin, screenshots annotes)
- **tests/** - Tests unitaires et d'integration

## Demarrage rapide

```bash
# Creer et activer l'environnement virtuel
python3.11 -m venv venv
source venv/bin/activate

# Installer les dependances
pip install -r requirements.txt

# Telecharger les modeles spaCy
python -m spacy download fr_core_news_md
python -m spacy download en_core_web_sm

# Installer Playwright
playwright install chromium
playwright install-deps

# Lancer le service
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

## Endpoints API

| Endpoint | Methode | Description |
|---|---|---|
| `/api/ia/health` | GET | Health check |
| `/api/ia/parse-scenario` | POST | Parser un scenario Gherkin (NLP) |
| `/api/ia/map-actions` | POST | Mapper intentions vers actions UI |
| `/api/ia/detect-ui` | POST | Detecter elements UI (YOLO + OCR) |
| `/api/ia/generate-test` | POST | Generer un script Playwright |
| `/api/ia/execute-test` | POST | Executer un test genere |
| `/api/ia/adapt` | POST | Adaptation dynamique |
| `/api/ia/reports/{test_id}` | GET | Recuperer un rapport de test |

## Docker

```bash
docker-compose up --build
```

## Tests

```bash
pytest tests/ -v
```
