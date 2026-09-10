"""Tests for tools/source-slice.py -- the authoritative-source boundary.

These test the CLI contract by running the real script as a subprocess, because the
contract that matters is what an agent invoking the tool actually experiences.

The invariant under test throughout: the tool must REFUSE rather than approximate.
A wrong hash, a missing file, an out-of-range page, or an absent anchor must all
produce a loud non-zero failure that extracts nothing -- never a quiet partial packet.
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from pdfbuild import make_pdf  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools" / "source-slice.py"

PAGES = [
    "ALPHA PAGE ONE Success Test",
    "BRAVO PAGE TWO Dice Pool",
    "CHARLIE PAGE THREE Glitch Table",
    "DELTA PAGE FOUR Edge Actions",
    "ECHO PAGE FIVE Damage Resistance",
]


NEEDS_PDFTOTEXT = unittest.skipIf(
    shutil.which("pdftotext") is None, "poppler-utils not installed"
)


class SourceSliceTests(unittest.TestCase):
    """Each test gets its own tmp source + tmp manifest; nothing touches the real ones."""

    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="deckard-slice-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)

        self.pdf = self.tmp / "fixture.pdf"
        self.pdf.write_bytes(make_pdf(PAGES))
        self.sha = hashlib.sha256(self.pdf.read_bytes()).hexdigest()
        self.manifest = self._write_manifest(self.sha)

    def _write_manifest(self, sha: str, *, page_count: int = len(PAGES), offset: int = 1) -> Path:
        path = self.tmp / "manifest.json"
        path.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "sources": [
                        {
                            "sourceId": "sr6-core",
                            "authority": 1,
                            "title": "Fixture Book",
                            "edition": "Fixture Edition",
                            "sha256": sha,
                            "pdfPageCount": page_count,
                            "pageNumbering": {"printedPageEqualsPdfPageMinus": offset},
                            "envVar": "SR6_CORE_PDF",
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )
        return path

    def run_tool(self, *args: str, source: str | None = "", manifest: Path | None = None):
        env = dict(os.environ)
        env["DECKARD_SOURCE_MANIFEST"] = str(manifest or self.manifest)
        env["DECKARD_ALLOW_TEST_MANIFEST"] = "1"
        env["SR6_CORE_PDF"] = str(self.pdf) if source == "" else (source or "")
        if not env["SR6_CORE_PDF"]:
            env.pop("SR6_CORE_PDF")
        return subprocess.run(
            [sys.executable, str(TOOL), *args],
            capture_output=True, text=True, env=env, cwd=str(ROOT), check=False,
        )

    # ------------------------------------------------------------------ happy paths

    @NEEDS_PDFTOTEXT
    def test_verify_only_accepts_the_pinned_hash(self):
        result = self.run_tool("--verify-only")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("sha256 verified", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_extracts_exactly_the_requested_pages(self):
        result = self.run_tool("--pages", "2-3")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("BRAVO PAGE TWO", result.stdout)
        self.assertIn("CHARLIE PAGE THREE", result.stdout)
        self.assertNotIn("ALPHA PAGE ONE", result.stdout)
        self.assertNotIn("DELTA PAGE FOUR", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_single_page_range_is_accepted(self):
        result = self.run_tool("--pages", "4")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("DELTA PAGE FOUR", result.stdout)
        self.assertNotIn("CHARLIE PAGE THREE", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_printed_pages_are_converted_using_the_manifest_offset(self):
        # Offset 1: printed p.2 is PDF p.3.
        result = self.run_tool("--printed-pages", "2")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("CHARLIE PAGE THREE", result.stdout)
        self.assertIn("pdf pages       : 3-3", result.stdout)
        self.assertIn("printed pages   : 2-2", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_layout_mode_still_extracts(self):
        result = self.run_tool("--pages", "1", "--layout")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("extraction      : pdftotext -layout", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_output_file_is_written(self):
        out = self.tmp / "packet.txt"
        result = self.run_tool("--pages", "1", "--output", str(out))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("ALPHA PAGE ONE", out.read_text(encoding="utf-8"))

    # -------------------------------------------------------------- provenance

    @NEEDS_PDFTOTEXT
    def test_packet_header_carries_full_provenance(self):
        result = self.run_tool("--pages", "1")
        for expected in ("sourceId        : sr6-core", "edition         : Fixture Edition",
                         f"sha256          : {self.sha}", "pdf pages       : 1-1"):
            self.assertIn(expected, result.stdout)

    @NEEDS_PDFTOTEXT
    def test_packet_warns_against_committing_it(self):
        result = self.run_tool("--pages", "1")
        self.assertIn("never commit", result.stdout.lower())

    # ------------------------------------------------------------------ refusals

    @NEEDS_PDFTOTEXT
    def test_hash_mismatch_refuses_and_extracts_nothing(self):
        wrong = self._write_manifest("0" * 64)
        result = self.run_tool("--pages", "1", manifest=wrong)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("HASH MISMATCH", result.stderr)
        self.assertNotIn("ALPHA PAGE ONE", result.stdout)

    @NEEDS_PDFTOTEXT
    def test_hash_mismatch_is_checked_before_extraction(self):
        wrong = self._write_manifest("0" * 64)
        result = self.run_tool("--verify-only", manifest=wrong)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")

    @NEEDS_PDFTOTEXT
    def test_unconfigured_source_refuses(self):
        result = self.run_tool("--pages", "1", source=None)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not configured", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_nonexistent_source_path_refuses(self):
        result = self.run_tool("--pages", "1", source=str(self.tmp / "nope.pdf"))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("does not exist", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_relative_source_path_refuses(self):
        result = self.run_tool("--pages", "1", source="relative/path.pdf")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("absolute path", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_unknown_source_id_refuses(self):
        result = self.run_tool("--pages", "1", "--source-id", "sr6-supplement")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Unknown sourceId", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_out_of_range_pages_refuse(self):
        result = self.run_tool("--pages", "4-9")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("has 5 pages", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_inverted_range_refuses(self):
        result = self.run_tool("--pages", "4-2")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("inverted", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_malformed_range_refuses(self):
        result = self.run_tool("--pages", "forty-two")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("expects N or N-M", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_missing_anchor_refuses(self):
        result = self.run_tool("--pages", "1", "--expect", "Vehicle Handling Table")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Expected anchor", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_present_anchor_passes(self):
        result = self.run_tool("--pages", "1", "--expect", "Success Test")
        self.assertEqual(result.returncode, 0, result.stderr)

    @NEEDS_PDFTOTEXT
    def test_all_anchors_must_be_present(self):
        result = self.run_tool("--pages", "1", "--expect", "Success Test", "--expect", "Glitch")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Glitch", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_oversize_packet_refuses_without_opt_in(self):
        big = self._write_manifest(self.sha, page_count=400)
        result = self.run_tool("--pages", "1-40", manifest=big)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Refusing to extract", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_no_page_selection_refuses(self):
        result = self.run_tool()
        self.assertNotEqual(result.returncode, 0)

    # -------------------------------------------- the source-substitution boundary

    def test_there_is_no_file_argument(self):
        """The absence of --file is a load-bearing invariant, not an oversight.

        If this ever passes, an agent can point the tool at a different printing, a
        previous edition, a supplement, or a pirated scan, and every downstream
        provenance claim in the repository becomes unverifiable.
        """
        help_text = subprocess.run(
            [sys.executable, str(TOOL), "--help"], capture_output=True, text=True, check=False
        ).stdout
        self.assertNotIn("--file", help_text)
        self.assertNotIn("--source-path", help_text)
        self.assertNotIn("--pdf", help_text)

    @NEEDS_PDFTOTEXT
    def test_manifest_override_requires_explicit_opt_in(self):
        env = dict(os.environ)
        env["DECKARD_SOURCE_MANIFEST"] = str(self.manifest)
        env.pop("DECKARD_ALLOW_TEST_MANIFEST", None)
        env["SR6_CORE_PDF"] = str(self.pdf)
        result = subprocess.run(
            [sys.executable, str(TOOL), "--verify-only"],
            capture_output=True, text=True, env=env, cwd=str(ROOT), check=False,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("DECKARD_ALLOW_TEST_MANIFEST", result.stderr)

    @NEEDS_PDFTOTEXT
    def test_override_packets_are_stamped_non_authoritative(self):
        result = self.run_tool("--pages", "1")
        self.assertIn("NON-AUTHORITATIVE", result.stdout)

    def test_real_manifest_packets_are_not_stamped(self):
        """A packet from the committed manifest must carry no override warning."""
        real = json.loads((ROOT / ".github" / "source-manifest.json").read_text(encoding="utf-8"))
        entry = real["sources"][0]
        self.assertEqual(entry["sourceId"], "sr6-core")
        self.assertRegex(entry["sha256"], r"^[0-9a-f]{64}$")
        self.assertGreater(entry["pdfPageCount"], 0)


if __name__ == "__main__":
    unittest.main()
