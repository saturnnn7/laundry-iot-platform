from __future__ import annotations

from pathlib import Path

import pytest

from laundry_sim.certs import cert_paths


def test_layout(tmp_path: Path) -> None:
    paths = cert_paths("sim-0001", tmp_path)
    assert paths.ca == tmp_path / "ca.crt"
    assert paths.cert == tmp_path / "sim-0001.crt"
    assert paths.key == tmp_path / "sim-0001.key"


def test_all_missing(tmp_path: Path) -> None:
    paths = cert_paths("sim-0001", tmp_path)
    assert paths.missing == [paths.ca, paths.cert, paths.key]


def test_some_missing(tmp_path: Path) -> None:
    (tmp_path / "ca.crt").write_text("fake")
    (tmp_path / "sim-0001.key").write_text("fake")
    paths = cert_paths("sim-0001", tmp_path)
    assert paths.missing == [tmp_path / "sim-0001.crt"]


def test_none_missing(tmp_path: Path) -> None:
    for name in ("ca.crt", "sim-0002.crt", "sim-0002.key"):
        (tmp_path / name).write_text("fake")
    assert cert_paths("sim-0002", tmp_path).missing == []


def test_rejects_path_like_device_id(tmp_path: Path) -> None:
    with pytest.raises(ValueError):
        cert_paths("../evil", tmp_path)
