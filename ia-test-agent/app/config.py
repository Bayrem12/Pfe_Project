"""
Configuration du service IA via variables d'environnement.
"""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    APP_NAME: str = "IA Test Agent"
    DEBUG: bool = True
    HOST: str = "0.0.0.0"
    PORT: int = 8000

    # CORS
    CORS_ORIGINS: list[str] = ["*"]

    # Modeles IA
    NLP_MODEL_PATH: str = "trained_models/nlp"
    YOLO_MODEL_PATH: str = "trained_models/vision/yolov8_ui_elements.pt"
    # Seuil de confiance YOLO : detections < conf ignorees (0.0-1.0)
    # 0.25 = permissif (plus de detections, plus de faux positifs)
    # 0.40 = equilibre (defaut)
    # 0.60 = strict (moins de detections, haute precision)
    YOLO_CONF_THRESHOLD: float = 0.40
    # Zero-shot classification models (bilingual)
    # English: facebook/bart-large-mnli
    # Multilingual (FR+EN): MoritzLaurer/mDeBERTa-v3-base-mnli-xnli
    ZEROSHOT_MODEL_EN: str = "facebook/bart-large-mnli"
    ZEROSHOT_MODEL_FR: str = "MoritzLaurer/mDeBERTa-v3-base-mnli-xnli"
    # Sentence embedding model (multilingual)
    EMBEDDING_MODEL: str = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"

    # Backend .NET API (for integration callbacks)
    BACKEND_URL: str = "http://localhost:5288"

    # Playwright
    HEADLESS: bool = True
    BROWSER_TYPE: str = "chromium"
    # Slow motion : delai (ms) entre chaque action Playwright.
    # 0 = vitesse maximale (defaut headless)
    # 800 = confortable pour observation (recommande avec HEADLESS=False)
    SLOW_MO_MS: int = 0
    # Timeout par etape (ms) — attend que le selecteur soit visible
    STEP_TIMEOUT_MS: int = 10_000
    # Nombre maxi de tentatives par etape avant FAILED
    MAX_RETRIES: int = 2
    # Capture d'ecran apres chaque etape
    STEP_SCREENSHOT: bool = True
    # Timeout pour wait_for_load_state("networkidle") apres navigation
    NETWORKIDLE_TIMEOUT_MS: int = 8_000

    # Chemins
    SCREENSHOTS_DIR: str = "reports/screenshots"
    REPORTS_DIR: str = "reports/html"
    GENERATED_TESTS_DIR: str = "reports/generated_tests"

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
