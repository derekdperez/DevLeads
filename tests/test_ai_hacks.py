"""Acceptance tests for the generated AI project context.

The tests intentionally exercise the checked-in repository rather than small parser
fixtures.  Keep the expected sets near the top of this file easy to update when the
application surface changes.
"""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any, Iterable


REPO_ROOT = Path(__file__).resolve().parents[1]
GENERATOR = REPO_ROOT / "ai_hacks.py"

EXPECTED_DBSETS = {
    "AiTriageRuns",
    "AuditEvents",
    "Campaigns",
    "ContentDrafts",
    "ContentTopics",
    "Opportunities",
    "OperatorSettings",
    "OutreachAttempts",
    "QueryPacks",
    "Quotes",
    "RawSourceItems",
    "Skills",
    "SourceConfigs",
    "SuppressionEntries",
    "TrendSignals",
    "TrendSources",
    "WorkSessions",
}

EXPECTED_PAGE_ROUTES = {
    "/",
    "/Error",
    "/campaigns",
    "/content",
    "/drafts",
    "/not-found",
    "/opportunities",
    "/opportunities/new",
    "/opportunities/{Id:long}",
    "/quotes",
    "/settings",
    "/skills",
    "/sources",
}

EXPECTED_STATUS_ACTION_ROUTES = {
    f"/api/opportunities/{{id:long}}/{action}"
    for action in (
        "approve",
        "reject",
        "archive",
        "watch",
        "mark-contacted",
        "mark-won",
        "mark-lost",
    )
}

EXPECTED_HTTP_ENDPOINT_COUNT = 36
EXPECTED_HOSTED_WORKERS = {"ContentTrendWorker", "DiscoveryWorker"}
EXPECTED_CONNECTORS = {
    "GitHubSearchConnector",
    "HackerNewsConnector",
    "OpireConnector",
    "RedditConnector",
    "RemotiveConnector",
    "RssConnector",
    "StackExchangeConnector",
}

CALLABLE_KINDS = {
    "constructor",
    "conversion",
    "conversionoperator",
    "destructor",
    "function",
    "method",
    "operator",
}


def _run_generator(out_dir: Path, *, check: bool = False) -> subprocess.CompletedProcess[str]:
    command = [
        sys.executable,
        str(GENERATOR),
        "--root",
        str(REPO_ROOT),
        "--out",
        str(out_dir),
    ]
    if check:
        command.append("--check")

    env = os.environ.copy()
    env["PYTHONDONTWRITEBYTECODE"] = "1"
    return subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=env,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )


def _description(item: dict[str, Any]) -> str:
    """Return the human-facing description, independent of renderer naming."""
    for key in ("description", "summary"):
        value = item.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return ""


def _name(item: Any) -> str:
    if isinstance(item, str):
        return item
    if not isinstance(item, dict):
        return ""
    for key in ("name", "property_name", "propertyName", "implementation", "service"):
        value = item.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return ""


def _route(item: dict[str, Any]) -> str:
    for key in ("route", "path", "template"):
        value = item.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip()
    return ""


def _iter_refs(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key in ("name", "full_name", "fullName", "type", "id"):
            candidate = value.get(key)
            if isinstance(candidate, str) and candidate.strip():
                yield candidate.strip()
    elif isinstance(value, list):
        for item in value:
            yield from _iter_refs(item)


def _type_names(type_info: dict[str, Any]) -> set[str]:
    names: set[str] = set()
    for key in ("id", "name", "full_name", "fullName", "qualified_name", "qualifiedName"):
        value = type_info.get(key)
        if isinstance(value, str) and value.strip():
            names.add(value.strip())
            names.add(value.strip().split(".")[-1])
    return names


def _is_callable(member: dict[str, Any]) -> bool:
    kind = str(member.get("kind", "")).replace("_", "").replace("-", "").lower()
    if kind in CALLABLE_KINDS or "method" in kind or "constructor" in kind:
        return True
    # Final schemas may expose a dedicated callable collection without a kind.
    return not kind and "parameters" in member and bool(member.get("name"))


def _callables(model: dict[str, Any]) -> list[tuple[dict[str, Any], dict[str, Any] | None]]:
    """Return (callable, enclosing type) pairs for flat or nested schemas."""
    flat = model.get("callables")
    if isinstance(flat, list):
        return [(item, None) for item in flat if isinstance(item, dict)]

    found: list[tuple[dict[str, Any], dict[str, Any] | None]] = []
    for type_info in model.get("types", []):
        if not isinstance(type_info, dict):
            continue
        for key in ("callables", "methods", "members"):
            members = type_info.get(key)
            if not isinstance(members, list):
                continue
            for member in members:
                if isinstance(member, dict) and _is_callable(member):
                    found.append((member, type_info))
            break
    return found


def _ref_resolves(reference: str, known_names: set[str]) -> bool:
    reference = reference.strip()
    if reference in known_names:
        return True
    short = re.split(r"[.+]", reference)[-1]
    return short in known_names


def _generated_files(out_dir: Path) -> dict[str, bytes]:
    return {
        path.relative_to(out_dir).as_posix(): path.read_bytes()
        for path in sorted(out_dir.rglob("*"))
        if path.is_file()
    }


class AiContextAcceptanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls._temp_dir = tempfile.TemporaryDirectory(prefix="devleads-ai-context-")
        cls.out_dir = Path(cls._temp_dir.name) / "generated"
        result = _run_generator(cls.out_dir)
        if result.returncode != 0:
            raise AssertionError(f"ai_hacks.py generation failed:\n{result.stdout}")

        model_path = cls.out_dir / "symbols.json"
        if not model_path.is_file():
            raise AssertionError(f"Generator did not create {model_path}")
        cls.model = json.loads(model_path.read_text(encoding="utf-8"))
        cls.compact_path = cls.out_dir / "AI_CONTEXT_COMPACT.md"

    @classmethod
    def tearDownClass(cls) -> None:
        cls._temp_dir.cleanup()

    def test_schema_v2_contract(self) -> None:
        expected_keys = {
            "source_digest",
            "types",
            "pages",
            "endpoints",
            "di",
            "dbsets",
            "coverage",
        }
        self.assertTrue(
            expected_keys.issubset(self.model),
            f"Missing schema-v2 keys: {sorted(expected_keys - self.model.keys())}",
        )

        version = self.model.get("schema_version", self.model.get("schemaVersion"))
        self.assertEqual(2, version)
        self.assertRegex(str(self.model["source_digest"]), r"^[0-9a-f]{64}$")
        self.assertIsInstance(self.model["coverage"], dict)
        self.assertTrue(self.model["coverage"], "coverage must report extraction completeness")

    def test_solution_and_persistence_surface(self) -> None:
        solutions = self.model.get("solutions", self.model.get("slns", []))
        if not solutions:
            solutions = [
                project
                for project in self.model.get("projects", [])
                if ".sln" in json.dumps(project, sort_keys=True).lower()
            ]
        self.assertTrue(
            any("DevLeads.slnx" in json.dumps(item) for item in solutions),
            "DevLeads.slnx must be recognized as the solution",
        )

        dbsets = self.model["dbsets"]
        self.assertEqual(len(EXPECTED_DBSETS), len(dbsets))
        self.assertEqual(EXPECTED_DBSETS, {_name(item) for item in dbsets})

    def test_routing_surface_is_complete(self) -> None:
        page_routes = {_route(page) for page in self.model["pages"]}
        self.assertEqual(EXPECTED_PAGE_ROUTES, page_routes)

        endpoints = self.model["endpoints"]
        endpoint_routes = {_route(endpoint) for endpoint in endpoints}
        self.assertEqual(EXPECTED_HTTP_ENDPOINT_COUNT, len(endpoints))
        self.assertIn("/favicon.ico", endpoint_routes)
        self.assertTrue(
            EXPECTED_STATUS_ACTION_ROUTES.issubset(endpoint_routes),
            "MapStatusAction calls must be expanded to their seven concrete routes",
        )
        self.assertNotIn(
            "/api/opportunities/{id:long}/{action}",
            endpoint_routes,
            "The helper route template is not itself a runtime endpoint",
        )

    def test_di_includes_hosted_workers(self) -> None:
        hosted = []
        for registration in self.model["di"]:
            rendered = json.dumps(registration, sort_keys=True)
            if "hosted" in rendered.lower():
                hosted.append(registration)

        self.assertEqual(2, len(hosted), "Expected exactly two hosted-service registrations")
        hosted_text = "\n".join(json.dumps(item, sort_keys=True) for item in hosted)
        self.assertEqual(
            EXPECTED_HOSTED_WORKERS,
            {worker for worker in EXPECTED_HOSTED_WORKERS if worker in hosted_text},
        )

    def test_type_hierarchy_includes_all_connectors(self) -> None:
        implementations = set()
        for type_info in self.model["types"]:
            interface_refs = set()
            for key in ("interfaces", "implements", "bases"):
                interface_refs.update(_iter_refs(type_info.get(key)))
            if any(ref == "ISourceConnector" or ref.endswith(".ISourceConnector") for ref in interface_refs):
                implementations.add(_name(type_info))

        self.assertEqual(EXPECTED_CONNECTORS, implementations)

    def test_every_type_and_callable_has_a_description_and_parent(self) -> None:
        types = self.model["types"]
        self.assertTrue(types, "types must not be empty")
        for type_info in types:
            with self.subTest(type=_name(type_info)):
                self.assertTrue(_description(type_info), "type description is empty")

        known_type_names: set[str] = set()
        for type_info in types:
            known_type_names.update(_type_names(type_info))

        callables = _callables(self.model)
        self.assertTrue(callables, "no callable members were emitted")
        for callable_info, enclosing_type in callables:
            callable_name = _name(callable_info)
            with self.subTest(callable=callable_name):
                self.assertTrue(_description(callable_info), "callable description is empty")

                if enclosing_type is not None:
                    parent = callable_info.get("parent") or callable_info.get("containing_type")
                    if parent:
                        self.assertTrue(
                            _ref_resolves(str(parent), known_type_names),
                            f"unresolved callable parent: {parent}",
                        )
                    continue

                parent = callable_info.get("parent") or callable_info.get("containing_type")
                self.assertIsInstance(parent, str, "flat callable has no parent")
                self.assertTrue(
                    _ref_resolves(parent, known_type_names),
                    f"unresolved callable parent: {parent}",
                )

    def test_compact_context_is_complete_across_layers(self) -> None:
        self.assertTrue(self.compact_path.is_file())
        compact = self.compact_path.read_text(encoding="utf-8")
        self.assertNotIn("[truncated]", compact.lower())
        self.assertIn("DevLeads.Infrastructure", compact)
        self.assertIn("LeadIngestionService", compact)
        self.assertIn("DevLeads.Web", compact)
        self.assertIn("ApiEndpoints", compact)

    def test_agent_bootstrap_is_installed_once_in_conventional_files(self) -> None:
        targets = (
            REPO_ROOT / "AGENTS.md",
            REPO_ROOT / "CLAUDE.md",
            REPO_ROOT / "GEMINI.md",
            REPO_ROOT / ".github/copilot-instructions.md",
        )
        for path in targets:
            with self.subTest(path=path.relative_to(REPO_ROOT)):
                text = path.read_text(encoding="utf-8")
                self.assertEqual(1, text.count("<!-- ai-hacks:instructions:begin -->"))
                self.assertEqual(1, text.count("<!-- ai-hacks:instructions:end -->"))
                self.assertIn("Read `.ai_hacks/AI_CONTEXT_COMPACT.md` completely", text)
                self.assertIn("Treat its **How to use this map**", text)
                self.assertIn("python3 ai_hacks.py --check", text)

    def test_unchanged_regeneration_is_byte_identical(self) -> None:
        with tempfile.TemporaryDirectory(prefix="devleads-ai-context-repeat-") as temp_dir:
            second_out = Path(temp_dir) / "generated"
            result = _run_generator(second_out)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(_generated_files(self.out_dir), _generated_files(second_out))

    def test_check_mode_detects_stale_output_without_rewriting_it(self) -> None:
        with tempfile.TemporaryDirectory(prefix="devleads-ai-context-check-") as temp_dir:
            out_dir = Path(temp_dir) / "generated"
            generated = _run_generator(out_dir)
            self.assertEqual(0, generated.returncode, generated.stdout)

            current = _run_generator(out_dir, check=True)
            self.assertEqual(0, current.returncode, current.stdout)

            compact_path = out_dir / "AI_CONTEXT_COMPACT.md"
            compact_path.write_text(
                compact_path.read_text(encoding="utf-8") + "\nSTALE TEST SENTINEL\n",
                encoding="utf-8",
            )
            stale_bytes = compact_path.read_bytes()

            stale = _run_generator(out_dir, check=True)
            self.assertNotEqual(0, stale.returncode, "--check accepted stale generated output")
            self.assertEqual(
                stale_bytes,
                compact_path.read_bytes(),
                "--check must not rewrite stale generated files",
            )


if __name__ == "__main__":
    unittest.main()
