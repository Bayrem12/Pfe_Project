"""
Point d'entree FastAPI pour le service IA.
Agent intelligent d'automatisation des tests fonctionnels.
"""

import os
import sys
import asyncio

# Windows + Python 3.8+ : Playwright spawns a Node.js subprocess via
# asyncio.create_subprocess_exec, which is NOT supported on the
# WindowsSelectorEventLoopPolicy (it raises NotImplementedError).
# Force the Proactor policy BEFORE uvicorn / FastAPI create the loop so
# the executor service can take screenshots without crashing.
if sys.platform == "win32":
    asyncio.set_event_loop_policy(asyncio.WindowsProactorEventLoopPolicy())

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
    failure_analysis,
    scenario_quality,
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
app.include_router(failure_analysis.router)
app.include_router(scenario_quality.router)
