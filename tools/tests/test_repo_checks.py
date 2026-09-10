"""Tests for tools/repo-checks.py.

A checker that has never caught anything is indistinguishable from a checker that
cannot catch anything. Every test here builds a fixture repository that violates one
invariant and asserts the check fails on it, then asserts the clean variant passes.
"""
from __future__ import annotations

import importlib.util
import subprocess
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

_spec = importlib.util.spec_from_file_location("repo_checks", ROOT / "tools" / "repo-checks.py")
assert _spec and _spec.loader
repo_checks = importlib.util.module_from_spec(_spec)
sys.modules["repo_checks"] = repo_checks
_spec.loader.exec_module(repo_checks)


class FixtureRepo:
    """A throwaway git repository, because several checks read `git ls-files`."""

    def __init__(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="deckard-checks-"))
        subprocess.run(["git", "init", "-q", "-b", "main"], cwd=self.root, check=True)

    def write(self, rel: str, content: bytes | str, *, track: bool = True) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content.encode("utf-8") if isinstance(content, str) else content)
        if track:
            subprocess.run(["git", "add", "-f", rel], cwd=self.root, check=True)
        return path

    def cleanup(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)


class CheckTestCase(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = FixtureRepo()
        self.addCleanup(self.repo.cleanup)

    def assertCaught(self, failures, needle: str) -> None:
        self.assertTrue(failures, f"expected a failure mentioning {needle!r}, got none")
        self.assertTrue(
            any(needle in f for f in failures),
            f"expected {needle!r} in {failures}",
        )


class TextHygieneTests(CheckTestCase):
    def test_clean_file_passes(self):
        self.repo.write("a.md", "hello\n")
        self.assertEqual(repo_checks.check_text_hygiene(self.repo.root), [])

    def test_bom_is_caught(self):
        self.repo.write("a.md", b"\xef\xbb\xbfhello\n")
        self.assertCaught(repo_checks.check_text_hygiene(self.repo.root), "BOM")

    def test_crlf_is_caught(self):
        self.repo.write("a.md", b"hello\r\nworld\n")
        self.assertCaught(repo_checks.check_text_hygiene(self.repo.root), "CRLF")

    def test_missing_trailing_newline_is_caught(self):
        self.repo.write("a.md", b"hello")
        self.assertCaught(repo_checks.check_text_hygiene(self.repo.root), "missing trailing newline")

    def test_double_trailing_newline_is_caught(self):
        self.repo.write("a.md", b"hello\n\n")
        self.assertCaught(repo_checks.check_text_hygiene(self.repo.root), "more than one trailing")

    def test_binary_suffixes_are_ignored(self):
        self.repo.write("logo.png", b"\x89PNG\r\n\x1a\n\xff")
        self.assertEqual(repo_checks.check_text_hygiene(self.repo.root), [])


class DeterminismTests(CheckTestCase):
    CLEAN = "namespace Deckard.Core;\npublic sealed class Roller { }\n"

    def test_clean_engine_source_passes(self):
        self.repo.write("src/Deckard.Core/Roller.cs", self.CLEAN)
        self.assertEqual(repo_checks.check_determinism(self.repo.root), [])

    def test_random_shared_is_caught(self):
        self.repo.write("src/Deckard.Core/R.cs", "var x = Random.Shared.Next();\n")
        self.assertCaught(repo_checks.check_determinism(self.repo.root), "Random.Shared")

    def test_new_random_is_caught(self):
        self.repo.write("src/Deckard.Core/R.cs", "var r = new Random(42);\n")
        self.assertCaught(repo_checks.check_determinism(self.repo.root), "IRandomSource")

    def test_datetime_now_is_caught(self):
        self.repo.write("src/Deckard.Rules/R.cs", "var t = DateTime.UtcNow;\n")
        self.assertCaught(repo_checks.check_determinism(self.repo.root), "ambient clock")

    def test_guid_newguid_is_caught(self):
        self.repo.write("src/Deckard.Core/R.cs", "var id = Guid.NewGuid();\n")
        self.assertCaught(repo_checks.check_determinism(self.repo.root), "non-reproducible")

    def test_parallelism_is_caught(self):
        self.repo.write("src/Deckard.Rules/R.cs", "items.AsParallel().Select(x => x);\n")
        self.assertCaught(repo_checks.check_determinism(self.repo.root), "non-deterministic")

    def test_tests_are_not_scanned(self):
        """Test code may legitimately construct a seeded Random to build fixtures."""
        self.repo.write("tests/Deckard.Core.Tests/T.cs", "var r = new Random(1);\n")
        self.assertEqual(repo_checks.check_determinism(self.repo.root), [])

    def test_explicit_marker_allows_an_exception(self):
        self.repo.write(
            "src/Deckard.Core/R.cs",
            "var t = DateTime.UtcNow; // deckard:allow-nondeterminism diagnostics only\n",
        )
        self.assertEqual(repo_checks.check_determinism(self.repo.root), [])

    def test_generated_obj_output_is_skipped(self):
        self.repo.write("src/Deckard.Core/obj/Debug/G.cs", "var x = Random.Shared.Next();\n")
        self.assertEqual(repo_checks.check_determinism(self.repo.root), [])


class LayeringTests(CheckTestCase):
    def _project(self, name: str, refs: list[str]) -> None:
        body = "\n".join(
            f'    <ProjectReference Include="../{r}/{r}.csproj" />' for r in refs
        )
        self.repo.write(
            f"src/{name}/{name}.csproj",
            f"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n{body}\n  </ItemGroup>\n</Project>\n",
        )

    def test_declared_graph_matching_the_spec_passes(self):
        self._project("Deckard.Core", [])
        self._project("Deckard.Data", ["Deckard.Core"])
        self._project("Deckard.Rules", ["Deckard.Core", "Deckard.Data"])
        self.assertEqual(repo_checks.check_layering(self.repo.root), [])

    def test_core_depending_upward_is_caught(self):
        self._project("Deckard.Core", ["Deckard.Rules"])
        self._project("Deckard.Data", ["Deckard.Core"])
        self._project("Deckard.Rules", ["Deckard.Core", "Deckard.Data"])
        self.assertCaught(repo_checks.check_layering(self.repo.root), "Deckard.Core declares forbidden")

    def test_data_depending_on_rules_is_caught(self):
        self._project("Deckard.Core", [])
        self._project("Deckard.Data", ["Deckard.Core", "Deckard.Rules"])
        self._project("Deckard.Rules", [])
        self.assertCaught(repo_checks.check_layering(self.repo.root), "Deckard.Data declares forbidden")

    def test_missing_project_is_caught(self):
        self._project("Deckard.Core", [])
        self.assertCaught(repo_checks.check_layering(self.repo.root), "missing expected project")


class SourceBoundaryTests(CheckTestCase):
    def test_clean_repo_passes(self):
        self.repo.write("docs/architecture.md", "Deckard layering.\n")
        self.assertEqual(repo_checks.check_source_boundary(self.repo.root), [])

    def test_tracked_pdf_is_caught(self):
        self.repo.write("reference/core.pdf", b"%PDF-1.4\n")
        self.assertCaught(repo_checks.check_source_boundary(self.repo.root), "tracked in git")

    def test_committed_source_packet_is_caught(self):
        marker = "DECKARD SOURCE" + " PACKET"
        self.repo.write("docs/notes.md", f"{marker} -- pasted rulebook text follows\n")
        self.assertCaught(repo_checks.check_source_boundary(self.repo.root), "extracted source packet")

    def test_leaked_local_source_path_is_caught(self):
        self.repo.write("scripts/run.sh", "export SR6_CORE_PDF=" + "/home/" + "someone/book.pdf\n")
        self.assertCaught(repo_checks.check_source_boundary(self.repo.root), "leaks a local")

    def test_macos_home_path_is_also_caught(self):
        self.repo.write("scripts/run.sh", "export SR6_CORE_PDF=" + "/Users/" + "someone/book.pdf\n")
        self.assertCaught(repo_checks.check_source_boundary(self.repo.root), "leaks a local")

    def test_a_generic_absolute_path_is_fine(self):
        self.repo.write("scripts/run.sh", "cd /usr/share/doc\n")
        self.assertEqual(repo_checks.check_source_boundary(self.repo.root), [])

    def test_manifest_without_valid_hash_is_caught(self):
        self.repo.write(
            ".github/source-manifest.json",
            '{"sources": [{"sourceId": "sr6-core", "sha256": "TODO"}]}\n',
        )
        self.assertCaught(repo_checks.check_source_boundary(self.repo.root), "no valid sha256")

    def test_manifest_carrying_a_local_path_is_caught(self):
        self.repo.write(
            ".github/source-manifest.json",
            '{"sources": [{"sourceId": "sr6-core", "sha256": "' + "a" * 64
            + '", "path": "/home/x/b.pdf", "envVar": "SR6_CORE_PDF"}]}\n',
        )
        failures = repo_checks.check_source_boundary(self.repo.root)
        self.assertCaught(failures, "never in git")


class SingleQueueTests(CheckTestCase):
    def test_prose_roadmap_passes(self):
        self.repo.write("docs/roadmap.md", "Phase 1 depends on Phase 0.\n")
        self.assertEqual(repo_checks.check_single_queue(self.repo.root), [])

    def test_checklist_in_a_governing_doc_is_caught(self):
        self.repo.write("CLAUDE.md", "# Deckard\n\n- [ ] implement dice pools\n")
        self.assertCaught(repo_checks.check_single_queue(self.repo.root), "outside GitHub Issues")

    def test_completed_checklist_is_also_caught(self):
        self.repo.write("docs/roadmap.md", "- [x] done thing\n")
        self.assertCaught(repo_checks.check_single_queue(self.repo.root), "outside GitHub Issues")

    def test_bullet_lists_are_fine(self):
        self.repo.write("README.md", "- Deckard is a rules engine\n- It is deterministic\n")
        self.assertEqual(repo_checks.check_single_queue(self.repo.root), [])


class ActionPinTests(CheckTestCase):
    """A mutable action tag is remote code execution with this repo's workflow token."""

    def _workflow(self, uses: str) -> None:
        self.repo.write(
            ".github/workflows/ci.yml",
            f"jobs:\n  build:\n    steps:\n      - uses: {uses}\n",
        )

    def test_sha_pinned_action_passes(self):
        self._workflow("actions/checkout@" + "a" * 40 + " # v4.2.2")
        self.assertEqual(repo_checks.check_action_pins(self.repo.root), [])

    def test_tag_pinned_action_is_caught(self):
        self._workflow("actions/checkout@v4")
        self.assertCaught(repo_checks.check_action_pins(self.repo.root), "mutable ref")

    def test_semver_tag_is_caught(self):
        self._workflow("actions/setup-dotnet@v4.3.1")
        self.assertCaught(repo_checks.check_action_pins(self.repo.root), "mutable ref")

    def test_branch_ref_is_caught(self):
        self._workflow("some/action@main")
        self.assertCaught(repo_checks.check_action_pins(self.repo.root), "mutable ref")

    def test_short_sha_is_caught(self):
        """An abbreviated SHA is ambiguous and not what the pin contract means."""
        self._workflow("actions/checkout@a1b2c3d")
        self.assertCaught(repo_checks.check_action_pins(self.repo.root), "mutable ref")

    def test_unversioned_action_is_caught(self):
        self._workflow("actions/checkout")
        self.assertCaught(repo_checks.check_action_pins(self.repo.root), "no version")

    def test_local_composite_action_is_allowed(self):
        self._workflow("./.github/actions/local-thing")
        self.assertEqual(repo_checks.check_action_pins(self.repo.root), [])


class RealRepositoryTests(unittest.TestCase):
    """The repository itself must satisfy every check it ships."""

    def test_repo_passes_all_of_its_own_checks(self):
        results = repo_checks.run(ROOT, sorted(repo_checks.CHECKS))
        problems = {k: v for k, v in results.items() if v}
        self.assertEqual(problems, {})


if __name__ == "__main__":
    unittest.main()
