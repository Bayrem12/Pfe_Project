# ── Stage 1: Python dependencies ──────────────────────────────────────────
# Use a slim Python 3.11 image as the base.
FROM python:3.11-slim AS base

WORKDIR /app

# System dependencies needed by:
#   - Playwright / Chromium (fonts, X11 libs, nss)
#   - OpenCV (libgl1)
#   - Tesseract OCR
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-fra \
    libgl1 \
    libglib2.0-0 \
    # Playwright Chromium dependencies
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpango-1.0-0 \
    libcairo2 \
    # Virtual display for headed (Watch Run) mode
    xvfb \
    && rm -rf /var/lib/apt/lists/*

# Install Python packages.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Install Playwright's Chromium browser binary inside the image.
RUN playwright install chromium

# Download spaCy language models.
RUN python -m spacy download fr_core_news_md && \
    python -m spacy download en_core_web_sm

# Copy application source.
COPY . .

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
