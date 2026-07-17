#!/usr/bin/env python3
"""Generate deterministic DataTable prototype CSV fixtures."""

from __future__ import annotations

import argparse
import csv
import random
import shutil
from pathlib import Path


SEED = 20260717
CATEGORIES = (
    ("consumable", "消耗品", 10, "true"),
    ("material", "材料", 20, "true"),
    ("equipment", "装备", 30, "true"),
    ("quest", "任务物品", 40, "false"),
)
ITEM_FIELDS = (
    "id",
    "category_id",
    "display_name",
    "enabled",
    "max_stack",
    "weight",
    "rarity",
    "description",
)


def write_csv(path: Path, fields: tuple[str, ...], rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def category_rows() -> list[dict[str, object]]:
    return [
        {
            "id": category_id,
            "display_name": display_name,
            "sort_order": sort_order,
            "enabled": enabled,
        }
        for category_id, display_name, sort_order, enabled in CATEGORIES
    ]


def item_rows(count: int) -> list[dict[str, object]]:
    randomizer = random.Random(SEED)
    category_ids = tuple(category[0] for category in CATEGORIES)
    rarities = ("Common", "Uncommon", "Rare", "Epic")
    rows: list[dict[str, object]] = []
    for index in range(count):
        item_number = index + 1
        category_id = category_ids[index % len(category_ids)]
        rows.append(
            {
                "id": f"item_{item_number:05d}",
                "category_id": category_id,
                "display_name": f"测试物品 {item_number}",
                "enabled": "" if item_number % 11 == 0 else "true",
                "max_stack": "" if item_number % 13 == 0 else 1 + index % 99,
                "weight": f"{randomizer.uniform(0.05, 250.0):.3f}",
                "rarity": rarities[index % len(rarities)],
                "description": (
                    "<null>"
                    if item_number % 5 == 0
                    else "" if item_number % 3 else f"固定种子说明 {item_number}"
                ),
            }
        )
    return rows


def write_valid_set(directory: Path, count: int) -> None:
    write_csv(
        directory / "ItemCategories.csv",
        ("id", "display_name", "sort_order", "enabled"),
        category_rows(),
    )
    write_csv(directory / "Items.csv", ITEM_FIELDS, item_rows(count))


def write_invalid_sets(root: Path) -> None:
    cases = {
        "missing_column": lambda rows: rows,
        "duplicate_key": lambda rows: rows + [dict(rows[0])],
        "invalid_enum": lambda rows: [dict(rows[0], rarity="Legendary"), *rows[1:]],
        "out_of_range": lambda rows: [dict(rows[0], max_stack=1000), *rows[1:]],
        "invalid_foreign_key": lambda rows: [dict(rows[0], category_id="missing"), *rows[1:]],
    }
    base_rows = item_rows(12)
    for case_name, mutate in cases.items():
        case_dir = root / case_name
        write_csv(
            case_dir / "ItemCategories.csv",
            ("id", "display_name", "sort_order", "enabled"),
            category_rows(),
        )
        rows = mutate([dict(row) for row in base_rows])
        fields = ITEM_FIELDS
        if case_name == "missing_column":
            fields = tuple(field for field in ITEM_FIELDS if field != "weight")
            rows = [{key: value for key, value in row.items() if key != "weight"} for row in rows]
        write_csv(case_dir / "Items.csv", fields, rows)

    short_row_dir = root / "short_row"
    write_csv(
        short_row_dir / "ItemCategories.csv",
        ("id", "display_name", "sort_order", "enabled"),
        category_rows(),
    )
    short_row_dir.mkdir(parents=True, exist_ok=True)
    with (short_row_dir / "Items.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(ITEM_FIELDS)
        writer.writerow([base_rows[0][field] for field in ITEM_FIELDS[:-1]])


def main() -> int:
    parser = argparse.ArgumentParser(description="生成 DataTable 阶段 A 固定种子样例。")
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    output = arguments.output.resolve()
    if output.exists():
        shutil.rmtree(output)
    write_valid_set(output / "small", 12)
    write_valid_set(output / "performance", 10_000)
    write_invalid_sets(output / "invalid")
    print(f"[DataTableFixture] PASS: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
