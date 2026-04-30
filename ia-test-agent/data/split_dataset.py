#!/usr/bin/env python3
"""
split_dataset.py — Stratified train/val/test split for the YOLO UI dataset
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Reads images + labels from  data/dataset/images/raw/  and
                             data/dataset/labels/raw/

Copies (not moves) them into:
    data/dataset/images/{train,val,test}/
    data/dataset/labels/{train,val,test}/

Split ratios default: 70 / 20 / 10  (configurable via CLI flags).

The split is stratified by URL category (prefix of the slug before "__"),
so every category is proportionally represented in each split.

Usage:
    python data/split_dataset.py [--train 0.70] [--val 0.20] [--test 0.10] [--seed 42]
"""

from __future__ import annotations

import argparse
import random
import shutil
from collections import defaultdict
from pathlib import Path

ROOT       = Path(__file__).resolve().parent.parent
DATA_DIR   = ROOT / "data" / "dataset"
IMAGES_RAW = DATA_DIR / "images" / "raw"
LABELS_RAW = DATA_DIR / "labels" / "raw"

SPLITS = ("train", "val", "test")


def _make_split_dirs() -> None:
    for split in SPLITS:
        (DATA_DIR / "images" / split).mkdir(parents=True, exist_ok=True)
        (DATA_DIR / "labels" / split).mkdir(parents=True, exist_ok=True)


def _copy_pair(stem: str, split: str) -> None:
    """Copy image + label pair to the correct split directory."""
    src_img = IMAGES_RAW / f"{stem}.png"
    src_lbl = LABELS_RAW / f"{stem}.txt"

    dst_img = DATA_DIR / "images" / split / f"{stem}.png"
    dst_lbl = DATA_DIR / "labels" / split / f"{stem}.txt"

    if src_img.exists():
        shutil.copy2(src_img, dst_img)
    if src_lbl.exists():
        shutil.copy2(src_lbl, dst_lbl)
    else:
        # Write empty label file to keep image/label 1:1 correspondence
        dst_lbl.write_text("")


def _category_of(stem: str) -> str:
    """Extract category prefix from slug (e.g., 'login_forms__...' → 'login_forms')."""
    return stem.split("__")[0] if "__" in stem else "misc"


def split_dataset(
    train_ratio: float,
    val_ratio:   float,
    test_ratio:  float,
    seed:        int,
) -> None:
    assert abs(train_ratio + val_ratio + test_ratio - 1.0) < 1e-6, \
        "Ratios must sum to 1.0"

    # Gather all image stems that have a corresponding label file
    stems = sorted([
        p.stem
        for p in IMAGES_RAW.glob("*.png")
        if (LABELS_RAW / f"{p.stem}.txt").exists()
    ])

    if not stems:
        print(f"No annotated images found in {IMAGES_RAW}")
        print("Run dom_annotate.py first.")
        return

    print(f"Found {len(stems)} annotated images.")

    # Group by category for stratified split
    by_cat: dict[str, list[str]] = defaultdict(list)
    for stem in stems:
        by_cat[_category_of(stem)].append(stem)

    rng = random.Random(seed)
    split_counts: dict[str, int] = {s: 0 for s in SPLITS}

    _make_split_dirs()

    for cat, cat_stems in sorted(by_cat.items()):
        rng.shuffle(cat_stems)
        n = len(cat_stems)
        n_train = max(1, round(n * train_ratio))
        n_val   = max(1, round(n * val_ratio))
        n_test  = n - n_train - n_val

        if n_test < 0:
            # Adjust if rounding left us short
            n_val  += n_test
            n_test  = 0

        partitions = {
            "train": cat_stems[:n_train],
            "val":   cat_stems[n_train : n_train + n_val],
            "test":  cat_stems[n_train + n_val :],
        }

        for split, part_stems in partitions.items():
            for stem in part_stems:
                _copy_pair(stem, split)
                split_counts[split] += 1

        print(f"  {cat:<30} total={n:>3}  train={len(partitions['train']):>3}  "
              f"val={len(partitions['val']):>3}  test={len(partitions['test']):>3}")

    print("\nFinal split:")
    total = sum(split_counts.values())
    for split in SPLITS:
        pct = split_counts[split] / total * 100 if total else 0
        print(f"  {split:<6}: {split_counts[split]:>4} images  ({pct:.1f}%)")

    print(f"\nImages  → {DATA_DIR / 'images'}")
    print(f"Labels  → {DATA_DIR / 'labels'}")
    print("\nNext step: python data/verify_annotations.py")


def main() -> None:
    ap = argparse.ArgumentParser(description="Stratified train/val/test split")
    ap.add_argument("--train", type=float, default=0.70, help="Train ratio (default 0.70)")
    ap.add_argument("--val",   type=float, default=0.20, help="Val ratio   (default 0.20)")
    ap.add_argument("--test",  type=float, default=0.10, help="Test ratio  (default 0.10)")
    ap.add_argument("--seed",  type=int,   default=42,   help="Random seed (default 42)")
    args = ap.parse_args()

    total = args.train + args.val + args.test
    if abs(total - 1.0) > 1e-6:
        # Auto-normalise
        args.train /= total
        args.val   /= total
        args.test  /= total
        print(f"Ratios normalised: train={args.train:.2f} val={args.val:.2f} test={args.test:.2f}")

    split_dataset(
        train_ratio=args.train,
        val_ratio=args.val,
        test_ratio=args.test,
        seed=args.seed,
    )


if __name__ == "__main__":
    main()
