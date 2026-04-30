"""
Point d'entree FastAPI pour le service IA.
Agent intelligent d'automatisation des tests fonctionnels.
"""

import os

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from app.api import (
    health,
    nlp_parsing,
    test_generation,
    test_execution,
    adaptation,
    reporting,
    pipeline,
)
from app.config import settings

app = FastAPI(
    title="IA Test Agent",
    description="Agent intelligent d'automatisation des tests fonctionnels "
    "a partir de scenarios metier (Gherkin).",
    version="0.1.0",
    docs_url="/docs",
    redoc_url="/redoc",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Serve generated HTML reports + screenshots so the backend / frontend can
# embed them directly (e.g. http://localhost:8000/reports/html/report_xxx.html).
os.makedirs("reports", exist_ok=True)
app.mount("/reports", StaticFiles(directory="reports"), name="reports")

# Enregistrement des routers
app.include_router(health.router)
app.include_router(nlp_parsing.router)
app.include_router(test_generation.router)
app.include_router(test_execution.router)
app.include_router(adaptation.router)
app.include_router(reporting.router)
app.include_router(pipeline.router)
