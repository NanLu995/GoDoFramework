#!/usr/bin/env python3
"""Compatibility entry point for the distributable GoDo DataTable compiler."""

from __future__ import annotations

import runpy
from pathlib import Path


TOOL_PATH = (
    Path(__file__).resolve().parents[3]
    / "addons"
    / "godo_framework"
    / "Tools"
    / "DataTable"
    / "godo_datatable.py"
)


if __name__ == "__main__":
    runpy.run_path(str(TOOL_PATH), run_name="__main__")
