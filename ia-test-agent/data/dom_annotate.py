#!/usr/bin/env python3
"""
dom_annotate.py — DOM-aware YOLO label generator (v2, 17 classes)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

For each URL in `data/url_manifest.json`:
  1. Open the page in a 1280×900 headless browser (Playwright).
  2. Take a full-viewport screenshot → saved to data/dataset/images/raw/<slug>.png
  3. Query the DOM with fine-grained CSS selectors for each of the 17 classes.
  4. Convert element bounding boxes to YOLO normalised format.
  5. Write labels to data/dataset/labels/raw/<slug>.txt

Usage:
    python data/dom_annotate.py [--limit N] [--category CATEGORY] [--timeout 20]

Requirements:
    pip install playwright && playwright install chromium
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import re
import sys
from pathlib import Path

# ── Project root on sys.path so we can import dataset.yaml class order ───────
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

try:
    from playwright.async_api import async_playwright, Page, TimeoutError as PWTimeout
except ImportError:
    sys.exit("Playwright not installed. Run: pip install playwright && playwright install chromium")

# ─── Paths ───────────────────────────────────────────────────────────────────
DATA_DIR    = ROOT / "data"
MANIFEST    = DATA_DIR / "url_manifest.json"
IMAGES_RAW  = DATA_DIR / "dataset" / "images" / "raw"
LABELS_RAW  = DATA_DIR / "dataset" / "labels" / "raw"

IMAGES_RAW.mkdir(parents=True, exist_ok=True)
LABELS_RAW.mkdir(parents=True, exist_ok=True)

# ─── 17-class selector map (order MUST match dataset.yaml indices) ────────────
# Each value is either a single CSS selector string or a list of selectors
# (ORed together). Sub-selectors that produce false positives are excluded
# with :not() guards.
CLASS_SELECTORS: dict[int, tuple[str, str]] = {
    #  id   (class_name,           CSS selector)
    0: ("button",
        "button:not([hidden]):not([disabled]),"
        "input[type='submit']:not([hidden]),"
        "input[type='button']:not([hidden]),"
        "input[type='reset']:not([hidden]),"
        "[role='button']:not([hidden]):not(select):not(input):not(textarea),"
        "a.btn:not([hidden]),"
        "a.button:not([hidden])"),

    1: ("input_text",
        "input[type='text']:not([hidden]),"
        "input[type='email']:not([hidden]),"
        "input[type='tel']:not([hidden]),"
        "input[type='number']:not([hidden]),"
        "input[type='search']:not([hidden]),"
        "input[type='url']:not([hidden]),"
        "input[type='date']:not([hidden]),"
        "input[type='time']:not([hidden]),"
        "input:not([type]):not([hidden])"),         # implicit text

    2: ("input_password",
        "input[type='password']:not([hidden])"),

    3: ("link",
        "a[href]:not([hidden]):not(.btn):not(.button)"),

    4: ("checkbox",
        "input[type='checkbox']:not([hidden])"),

    5: ("dropdown",
        "select:not([hidden]),"
        "[role='combobox']:not([hidden]):not(input),"
        "[role='listbox']:not([hidden])"),

    6: ("label",
        "label:not([hidden]),"
        "legend:not([hidden]),"
        "h1:not([hidden]),"
        "h2:not([hidden]),"
        "h3:not([hidden]),"
        "[role='heading']:not([hidden])"),

    7: ("modal",
        "[role='dialog']:not([hidden]),"
        ".modal.show:not([hidden]),"
        ".modal-dialog:not([hidden]),"
        "[aria-modal='true']:not([hidden])"),

    8: ("radio",
        "input[type='radio']:not([hidden])"),

    9: ("textarea",
        "textarea:not([hidden])"),

    10: ("image",
        "img:not([hidden]):not([width='0']):not([height='0'])"),

    11: ("table",
        "table:not([hidden]),"
        "[role='grid']:not([hidden]),"
        "[role='treegrid']:not([hidden])"),

    12: ("tab",
        "[role='tab']:not([hidden]),"
        ".nav-link:not([hidden]):not(a[href^='http']):not(a[href^='/'])"),

    13: ("toggle",
        "input[type='checkbox'].toggle:not([hidden]),"
        "input[type='checkbox'].switch:not([hidden]),"
        "[role='switch']:not([hidden])"),

    14: ("slider",
        "input[type='range']:not([hidden]),"
        "[role='slider']:not([hidden])"),

    15: ("alert",
        "[role='alert']:not([hidden]),"
        ".alert:not([hidden]),"
        ".notification:not([hidden]),"
        ".toast:not([hidden]),"
        "[role='status']:not([hidden])"),

    16: ("navbar",
        "nav:not([hidden]),"
        "[role='navigation']:not([hidden]),"
        "header nav:not([hidden]),"
        ".navbar:not([hidden]):not(.btn):not(select)"),
}

# JS snippet injected once per page — returns [{cls_id, x, y, w, h}] in
# viewport-relative pixels for all visible elements matching the selectors.
_JS_COLLECT = r"""
(classSelectors) => {
    const results = [];
    const seen = new Set();
    const viewW = window.innerWidth;
    const viewH = window.innerHeight;

    for (const [clsIdStr, selector] of Object.entries(classSelectors)) {
        const clsId = parseInt(clsIdStr, 10);
        let elements;
        try {
            elements = document.querySelectorAll(selector);
        } catch(e) {
            continue;
        }
        for (const el of elements) {
            // Visibility checks
            const style = window.getComputedStyle(el);
            if (style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity) < 0.05)
                continue;

            const rect = el.getBoundingClientRect();
            if (rect.width < 4 || rect.height < 4)   continue;   // too small
            if (rect.right  < 0 || rect.left > viewW) continue;   // off-screen X
            if (rect.bottom < 0 || rect.top  > viewH) continue;   // off-screen Y

            // Clip to viewport
            const x1 = Math.max(0, rect.left);
            const y1 = Math.max(0, rect.top);
            const x2 = Math.min(viewW, rect.right);
            const y2 = Math.min(viewH, rect.bottom);
            const w  = x2 - x1;
            const h  = y2 - y1;
            if (w < 4 || h < 4) continue;

            // Deduplication key: clsId + rounded bbox to avoid near-duplicates
            const key = `${clsId}_${Math.round(x1)}_${Math.round(y1)}_${Math.round(x2)}_${Math.round(y2)}`;
            if (seen.has(key)) continue;
            seen.add(key);

            results.push({ cls_id: clsId, x1, y1, x2, y2, w: viewW, h: viewH });
        }
    }
    return results;
}
"""

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger("dom_annotate")


def _slugify(url: str) -> str:
    """Convert URL to a safe filename stem."""
    slug = re.sub(r"https?://", "", url)
    slug = re.sub(r"[^a-zA-Z0-9]+", "_", slug).strip("_")
    return slug[:120]   # cap length


def _to_yolo(x1: float, y1: float, x2: float, y2: float,
              vw: float, vh: float) -> tuple[float, float, float, float]:
    """Convert pixel bbox to YOLO normalised (cx, cy, w, h)."""
    cx = ((x1 + x2) / 2) / vw
    cy = ((y1 + y2) / 2) / vh
    w  = (x2 - x1) / vw
    h  = (y2 - y1) / vh
    return (
        max(0.0, min(1.0, cx)),
        max(0.0, min(1.0, cy)),
        max(0.0, min(1.0, w)),
        max(0.0, min(1.0, h)),
    )


async def _annotate_page(
    page: Page,
    url: str,
    slug: str,
    viewport: dict,
    timeout_s: int,
) -> tuple[int, int]:
    """
    Navigate, screenshot and annotate one URL.
    Returns (num_elements_found, error_count).
    """
    img_path   = IMAGES_RAW / f"{slug}.png"
    label_path = LABELS_RAW / f"{slug}.txt"

    # Skip if both files already exist (resume-friendly)
    if img_path.exists() and label_path.exists():
        log.info("SKIP (exists) %s", slug)
        return 0, 0

    try:
        await page.goto(url, wait_until="domcontentloaded", timeout=timeout_s * 1000)
        # Extra settle time for JS-heavy pages
        try:
            await page.wait_for_load_state("networkidle", timeout=5_000)
        except PWTimeout:
            pass  # acceptable — some pages never go fully idle

        # Dismiss cookie banners and overlays that might occlude elements
        for dismiss_sel in [
            "button[id*='accept']",
            "button[id*='cookie']",
            "button[class*='accept']",
            "button[class*='cookie']",
            "[aria-label*='accept' i]",
            "[aria-label*='close' i]",
        ]:
            try:
                btn = page.locator(dismiss_sel).first
                if await btn.is_visible(timeout=500):
                    await btn.click(timeout=500)
            except Exception:
                pass

    except Exception as exc:
        log.warning("LOAD ERROR %s — %s", url, exc)
        return 0, 1

    # Screenshot
    try:
        await page.screenshot(path=str(img_path), full_page=False)
    except Exception as exc:
        log.warning("SCREENSHOT ERROR %s — %s", url, exc)
        return 0, 1

    # Collect DOM bounding boxes via JS
    selector_map = {str(cls_id): sel for cls_id, (_, sel) in CLASS_SELECTORS.items()}
    try:
        raw: list[dict] = await page.evaluate(_JS_COLLECT, selector_map)
    except Exception as exc:
        log.warning("JS ERROR %s — %s", url, exc)
        # Keep the screenshot but write empty label file
        label_path.write_text("")
        return 0, 1

    lines: list[str] = []
    for item in raw:
        try:
            cx, cy, w, h = _to_yolo(
                item["x1"], item["y1"], item["x2"], item["y2"],
                item["w"],  item["h"],
            )
            lines.append(f"{item['cls_id']} {cx:.6f} {cy:.6f} {w:.6f} {h:.6f}")
        except (KeyError, TypeError, ZeroDivisionError):
            continue

    label_path.write_text("\n".join(lines))
    n = len(lines)
    if n:
        log.info("OK  %s → %d annotations", slug, n)
    else:
        log.warning("EMPTY %s (0 annotations)", slug)
    return n, 0


async def _run(
    limit: int | None,
    category_filter: str | None,
    timeout_s: int,
    viewport: dict,
    concurrency: int,
) -> None:
    manifest = json.loads(MANIFEST.read_text())
    categories = manifest["categories"]

    all_urls: list[tuple[str, str]] = []   # [(category, url)]
    for cat_name, cat_data in categories.items():
        if category_filter and cat_name != category_filter:
            continue
        for url in cat_data["urls"]:
            all_urls.append((cat_name, url))

    if limit:
        all_urls = all_urls[:limit]

    log.info("Processing %d URLs (concurrency=%d)", len(all_urls), concurrency)

    sem = asyncio.Semaphore(concurrency)
    total_elems = 0
    total_errors = 0

    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=True)
        context = await browser.new_context(
            viewport=viewport,
            user_agent=(
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
            ),
            locale="en-US",
            ignore_https_errors=True,
        )

        async def _worker(cat: str, url: str) -> None:
            nonlocal total_elems, total_errors
            async with sem:
                page = await context.new_page()
                try:
                    slug = f"{cat}__{_slugify(url)}"
                    n, err = await _annotate_page(page, url, slug, viewport, timeout_s)
                    total_elems  += n
                    total_errors += err
                finally:
                    await page.close()

        tasks = [asyncio.create_task(_worker(cat, url)) for cat, url in all_urls]
        await asyncio.gather(*tasks)

        await context.close()
        await browser.close()

    log.info("Done. Total annotations: %d | Errors: %d", total_elems, total_errors)
    log.info("Images → %s", IMAGES_RAW)
    log.info("Labels → %s", LABELS_RAW)


def main() -> None:
    ap = argparse.ArgumentParser(description="DOM-based YOLO auto-annotator")
    ap.add_argument("--limit",       type=int,   default=None, help="Max URLs to process")
    ap.add_argument("--category",    type=str,   default=None, help="Only process this category key")
    ap.add_argument("--timeout",     type=int,   default=20,   help="Per-page timeout in seconds")
    ap.add_argument("--width",       type=int,   default=1280, help="Viewport width")
    ap.add_argument("--height",      type=int,   default=900,  help="Viewport height")
    ap.add_argument("--concurrency", type=int,   default=4,    help="Parallel browser pages")
    args = ap.parse_args()

    viewport = {"width": args.width, "height": args.height}
    asyncio.run(
        _run(
            limit=args.limit,
            category_filter=args.category,
            timeout_s=args.timeout,
            viewport=viewport,
            concurrency=args.concurrency,
        )
    )


if __name__ == "__main__":
    main()
