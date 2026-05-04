#!/usr/bin/env python3
"""
verify_annotations.py — Quality-check the YOLO dataset
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Checks performed per label file:
  • Each line has exactly 5 space-separated tokens.
  • class_id is an integer in [0, num_classes).
  • cx, cy, w, h are in (0.0, 1.0].
  • Bounding box doesn't overflow the image (cx ± w/2 and cy ± h/2 in [0,1]).
  • No duplicate annotations (same class + overlapping bbox > 95% IoU).

Checks performed per split:
  • Every image has a corresponding label file (and vice-versa).
  • Class distribution is printed.

Usage:
    python data/verify_annotations.py [--split {train,val,test,raw}] [--fix]

With --fix:
  • Clamps out-of-bounds values instead of reporting them as errors.
  • Removes duplicate annotations (keeps first).
  • Removes lines with completely invalid format.
"""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from pathlib import Path

import yaml  # PyYAML — already in requirements.txt

ROOT     = Path(__file__).resolve().parent.parent
DATA_DIR = ROOT / "data"
YAML_CFG = DATA_DIR / "dataset.yaml"

# ─── Load class names from dataset.yaml ──────────────────────────────────────
_yaml_data  = yaml.safe_load(YAML_CFG.read_text())
NUM_CLASSES = int(_yaml_data["nc"])
CLASS_NAMES: list[str] = [_yaml_data["names"][i] for i in range(NUM_CLASSES)]


def _iou(a: tuple[float, float, float, float],
         b: tuple[float, float, float, float]) -> float:
    """Compute IoU between two YOLO (cx, cy, w, h) boxes."""
    ax1, ay1 = a[0] - a[2] / 2, a[1] - a[3] / 2
    ax2, ay2 = a[0] + a[2] / 2, a[1] + a[3] / 2
    bx1, by1 = b[0] - b[2] / 2, b[1] - b[3] / 2
    bx2, by2 = b[0] + b[2] / 2, b[1] + b[3] / 2

    ix1 = max(ax1, bx1);  iy1 = max(ay1, by1)
    ix2 = min(ax2, bx2);  iy2 = min(ay2, by2)
    inter = max(0.0, ix2 - ix1) * max(0.0, iy2 - iy1)
    if inter == 0:
        return 0.0
    area_a = a[2] * a[3]
    area_b = b[2] * b[3]
    return inter / (area_a + area_b - inter)


def _clamp(v: float, lo: float = 0.0, hi: float = 1.0) -> float:
    return max(lo, min(hi, v))


def _verify_label_file(
    label_path: Path,
    fix: bool,
) -> tuple[int, int, list[str]]:
    """
    Returns (kept_annotations, issues_found, fixed_lines).
    fixed_lines are only meaningful when fix=True.
    """
    raw_text = label_path.read_text().strip()
    if not raw_text:
        return 0, 0, []

    lines       = raw_text.splitlines()
    good_boxes: list[tuple[int, float, float, float, float]] = []  # (cls_id, cx, cy, w, h)
    fixed_lines: list[str] = []
    issues      = 0

    for line_num, line in enumerate(lines, 1):
        parts = line.strip().split()

        # ── Format check ────────────────────────────────────────────────────
        if len(parts) != 5:
            issues += 1
            if fix:
                continue   # drop malformed line
            else:
                print(f"  [FORMAT]  {label_path.name}:{line_num}  wrong columns: {line!r}")
            continue

        try:
            cls_id = int(parts[0])
            cx, cy, w, h = map(float, parts[1:])
        except ValueError:
            issues += 1
            if not fix:
                print(f"  [PARSE]   {label_path.name}:{line_num}  non-numeric: {line!r}")
            continue

        # ── Class range check ────────────────────────────────────────────────
        if cls_id < 0 or cls_id >= NUM_CLASSES:
            issues += 1
            if not fix:
                print(f"  [CLS_ID]  {label_path.name}:{line_num}  cls_id={cls_id} out of [0,{NUM_CLASSES})")
            continue   # can't fix unknown class — always drop

        # ── Value range check ────────────────────────────────────────────────
        needs_clamp = not (0 < cx <= 1 and 0 < cy <= 1 and 0 < w <= 1 and 0 < h <= 1)
        if needs_clamp:
            issues += 1
            if fix:
                cx = _clamp(cx, 1e-6, 1.0)
                cy = _clamp(cy, 1e-6, 1.0)
                w  = _clamp(w,  1e-6, 1.0)
                h  = _clamp(h,  1e-6, 1.0)
            else:
                print(f"  [RANGE]   {label_path.name}:{line_num}  cx={cx:.4f} cy={cy:.4f} w={w:.4f} h={h:.4f}")

        # ── Overflow check ───────────────────────────────────────────────────
        if cx - w / 2 < -0.01 or cx + w / 2 > 1.01 or \
           cy - h / 2 < -0.01 or cy + h / 2 > 1.01:
            issues += 1
            if fix:
                w = min(w, 2 * min(cx, 1 - cx))
                h = min(h, 2 * min(cy, 1 - cy))
            else:
                print(f"  [OVERFLOW]{label_path.name}:{line_num}  box overflows image boundary")

        # ── Duplicate check (IoU > 95% with same class) ──────────────────────
        box = (cx, cy, w, h)
        is_dup = False
        for prev_cls, *prev_box in good_boxes:
            if prev_cls == cls_id and _iou(box, tuple(prev_box)) > 0.95:  # type: ignore[arg-type]
                is_dup = True
                issues += 1
                if not fix:
                    print(f"  [DUP]     {label_path.name}:{line_num}  duplicate of earlier {CLASS_NAMES[cls_id]}")
                break
        if is_dup and fix:
            continue   # drop duplicate

        good_boxes.append((cls_id, cx, cy, w, h))
        fixed_lines.append(f"{cls_id} {cx:.6f} {cy:.6f} {w:.6f} {h:.6f}")

    return len(good_boxes), issues, fixed_lines


def _verify_split(
    split: str,
    fix: bool,
) -> tuple[dict[str, int], int, int]:
    """
    Returns (class_counts, total_images, total_issues).
    """
    if split == "raw":
        img_dir = DATA_DIR / "dataset" / "images" / "raw"
        lbl_dir = DATA_DIR / "dataset" / "labels" / "raw"
    else:
        img_dir = DATA_DIR / "dataset" / "images" / split
        lbl_dir = DATA_DIR / "dataset" / "labels" / split

    if not img_dir.exists():
        print(f"  Split directory not found: {img_dir}")
        return {}, 0, 0

    images = set(p.stem for p in img_dir.glob("*.png"))
    labels = set(p.stem for p in lbl_dir.glob("*.txt"))

    orphan_imgs  = images - labels
    orphan_lbls  = labels - images

    if orphan_imgs:
        print(f"  [ORPHAN_IMG] {len(orphan_imgs)} images without labels → e.g. {next(iter(orphan_imgs))}.png")
    if orphan_lbls:
        print(f"  [ORPHAN_LBL] {len(orphan_lbls)} labels without images → e.g. {next(iter(orphan_lbls))}.txt")

    class_counts: dict[str, int] = defaultdict(int)
    total_issues = 0

    paired = images & labels
    for stem in sorted(paired):
        lbl_path = lbl_dir / f"{stem}.txt"
        kept, issues, fixed_lines = _verify_label_file(lbl_path, fix)
        total_issues += issues

        if fix and issues:
            lbl_path.write_text("\n".join(fixed_lines))

        # Count classes
        for line in (fixed_lines if fix else lbl_path.read_text().splitlines()):
            parts = line.strip().split()
            if len(parts) == 5 and parts[0].isdigit():
                cls_id = int(parts[0])
                if 0 <= cls_id < NUM_CLASSES:
                    class_counts[CLASS_NAMES[cls_id]] += 1

    return dict(class_counts), len(paired), total_issues


def main() -> None:
    ap = argparse.ArgumentParser(description="Verify / fix YOLO dataset annotations")
    ap.add_argument("--split", choices=["train", "val", "test", "raw", "all"],
                    default="all", help="Which split to verify (default: all)")
    ap.add_argument("--fix",  action="store_true",
                    help="Auto-fix clamping & duplicate issues in-place")
    args = ap.parse_args()

    splits = ["raw", "train", "val", "test"] if args.split == "all" else [args.split]

    grand_total_images  = 0
    grand_total_issues  = 0
    grand_class_counts: dict[str, int] = defaultdict(int)

    for split in splits:
        print(f"\n{'─'*60}")
        print(f"  Split: {split.upper()}")
        print(f"{'─'*60}")

        class_counts, n_images, n_issues = _verify_split(split, fix=args.fix)
        grand_total_images += n_images
        grand_total_issues += n_issues
        for cls, cnt in class_counts.items():
            grand_class_counts[cls] += cnt

        print(f"  Images checked: {n_images}")
        print(f"  Issues {'fixed' if args.fix else 'found'}: {n_issues}")
        if class_counts:
            print(f"  Class distribution:")
            total_ann = sum(class_counts.values())
            for cls in CLASS_NAMES:
                cnt = class_counts.get(cls, 0)
                bar = "█" * min(40, int(cnt / max(total_ann, 1) * 40))
                print(f"    {cls:<18} {cnt:>6}  {bar}")

    if len(splits) > 1:
        print(f"\n{'═'*60}")
        print(f"  GRAND TOTAL  images={grand_total_images}  "
              f"issues={grand_total_issues}")
        total_ann = sum(grand_class_counts.values())
        print(f"  Total annotations: {total_ann}")
        print(f"{'═'*60}")

    if grand_total_issues and not args.fix:
        print("\nRe-run with --fix to auto-correct fixable issues.")
        sys.exit(1)
    elif grand_total_issues and args.fix:
        print("\nAll fixable issues corrected in-place.")

    if grand_total_images == 0:
        print("\nNo annotated images found. Run dom_annotate.py first.")


if __name__ == "__main__":
    main()
