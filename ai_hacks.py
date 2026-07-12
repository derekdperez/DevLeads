#!/usr/bin/env python3
"""Generate a deterministic, semantic project map for AI coding agents.

The generator intentionally records declarations and architecture, never method
bodies, prompt bodies, markup, CSS, seeded values, or configuration values.

Run from the repository root:

    python3 ai_hacks.py
    python3 ai_hacks.py --check

The C# syntax model comes from the SDK-local Roslyn helper in
``tools/AiContextExtractor``.  Razor and application topology are extracted
here because they are not represented by ordinary C# source files.
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


SCHEMA_VERSION = 2
MAX_FILE_BYTES = 1_000_000
APPLICATION_EXTENSIONS = {".cs", ".razor", ".cshtml", ".csproj", ".props", ".targets", ".json"}
SOLUTION_EXTENSIONS = {".sln", ".slnx"}
CONFIG_NAMES = {
    "appsettings.json",
    "appsettings.development.json",
    "appsettings.production.json",
    "appsettings.staging.json",
    "launchsettings.json",
    "web.config",
    "app.config",
}
IGNORE_DIRS = {
    ".git", ".hg", ".svn", ".vs", ".vscode", ".idea", ".ai_hacks",
    "bin", "obj", "node_modules", "dist", "build", "out", "target",
    ".venv", "venv", "__pycache__", "coverage", "TestResults",
}
GENERATOR_INPUTS = (
    "ai_hacks.py",
    "ai_hacks.context.json",
    "tools/AiContextExtractor/AiContextExtractor.csproj",
    "tools/AiContextExtractor/Program.cs",
)
INSTRUCTION_TARGETS = (
    "AGENTS.md",
    "CLAUDE.md",
    "GEMINI.md",
    ".github/copilot-instructions.md",
)
MANAGED_INSTRUCTIONS_BEGIN = "<!-- ai-hacks:instructions:begin -->"
MANAGED_INSTRUCTIONS_END = "<!-- ai-hacks:instructions:end -->"


@dataclass(frozen=True)
class FileInfo:
    path: str
    bytes: int
    lines: int
    sha256: str


@dataclass(frozen=True)
class PageInfo:
    route: str
    component: str
    file: str
    line: int
    description: str
    inherits: str
    implements: tuple[str, ...]
    injects: tuple[str, ...]
    parameters: tuple[str, ...]
    javascript: tuple[str, ...]


@dataclass(frozen=True)
class EndpointInfo:
    verb: str
    route: str
    file: str
    line: int
    owner: str
    description: str


@dataclass(frozen=True)
class DiRegistration:
    lifetime: str
    service: str
    implementation: str
    file: str
    line: int


@dataclass(frozen=True)
class DbSetInfo:
    context: str
    entity: str
    property_name: str
    file: str
    line: int


@dataclass(frozen=True)
class RelationshipInfo:
    source: str
    target: str
    cardinality: str
    property_name: str
    source_kind: str = "inferred from navigation property"


def rel(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def compact(value: str) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def line_no(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def token_estimate(chars: int) -> int:
    return round(chars / 4)


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8", errors="ignore")).hexdigest()


def split_identifier(name: str) -> str:
    name = re.sub(r"Async$", "", name or "")
    name = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", name)
    name = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", name)
    return name.replace("_", " ").strip().lower()


def simple_type_name(value: str) -> str:
    value = compact(value).replace("global::", "")
    value = value.rstrip("?")
    if "." in value and "<" not in value:
        value = value.rsplit(".", 1)[-1]
    return value


def trim_description(value: str, max_chars: int = 220) -> str:
    value = compact(html.unescape(value or ""))
    value = re.sub(r"\s+([.,;:])", r"\1", value)
    if not value:
        return ""
    if len(value) <= max_chars:
        return value
    for end in re.finditer(r"[.!?](?:\s|$)", value[: max_chars + 1]):
        candidate = value[: end.end()].strip()
        if len(candidate) >= max_chars * 0.55:
            return candidate
    shortened = value[: max_chars - 1].rsplit(" ", 1)[0].rstrip(".,;:")
    return shortened + "…"


def md_escape(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def md_table(headers: Sequence[str], rows: Sequence[Sequence[Any]]) -> str:
    if not rows:
        return "_None detected._\n"
    lines = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    lines.extend("| " + " | ".join(md_escape(x) for x in row) + " |" for row in rows)
    return "\n".join(lines) + "\n"


def split_top_level(value: str, delimiter: str = ",") -> list[str]:
    parts: list[str] = []
    start = 0
    depths = {"<": 0, "(": 0, "[": 0, "{": 0}
    pairs = {">": "<", ")": "(", "]": "[", "}": "{"}
    quote = ""
    escaped = False
    for index, char in enumerate(value):
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = ""
            continue
        if char in {'"', "'"}:
            quote = char
        elif char in depths:
            depths[char] += 1
        elif char in pairs:
            opener = pairs[char]
            depths[opener] = max(0, depths[opener] - 1)
        elif char == delimiter and not any(depths.values()):
            parts.append(value[start:index].strip())
            start = index + 1
    parts.append(value[start:].strip())
    return [part for part in parts if part]


def mask_csharp(text: str, *, keep_strings: bool = False) -> str:
    """Mask comments and optionally literals while preserving offsets/newlines."""
    chars = list(text)
    index = 0
    length = len(chars)

    def blank(start: int, end: int) -> None:
        for pos in range(start, end):
            if chars[pos] not in "\r\n":
                chars[pos] = " "

    while index < length:
        if index + 1 < length and text[index:index + 2] == "//":
            end = text.find("\n", index + 2)
            end = length if end < 0 else end
            blank(index, end)
            index = end
            continue
        if index + 1 < length and text[index:index + 2] == "/*":
            end = text.find("*/", index + 2)
            end = length if end < 0 else end + 2
            blank(index, end)
            index = end
            continue
        if text.startswith('"""', index):
            end = text.find('"""', index + 3)
            end = length if end < 0 else end + 3
            if not keep_strings:
                blank(index, end)
            index = end
            continue
        if text[index] == '"' or (text[index] == '@' and index + 1 < length and text[index + 1] == '"'):
            start = index
            verbatim = text[index] == '@'
            index += 2 if verbatim else 1
            while index < length:
                if verbatim and text[index:index + 2] == '""':
                    index += 2
                    continue
                if not verbatim and text[index] == "\\":
                    index += 2
                    continue
                if text[index] == '"':
                    index += 1
                    break
                index += 1
            if not keep_strings:
                blank(start, min(index, length))
            continue
        if text[index] == "'":
            start = index
            index += 1
            while index < length:
                if text[index] == "\\":
                    index += 2
                    continue
                if text[index] == "'":
                    index += 1
                    break
                index += 1
            if not keep_strings:
                blank(start, min(index, length))
            continue
        index += 1
    return "".join(chars)


def matching_pairs(text: str, opener: str, closer: str) -> dict[int, int]:
    stack: list[int] = []
    pairs: dict[int, int] = {}
    for index, char in enumerate(text):
        if char == opener:
            stack.append(index)
        elif char == closer and stack:
            start = stack.pop()
            pairs[start] = index
    return pairs


def brace_depths(text: str) -> list[int]:
    depths = [0] * (len(text) + 1)
    depth = 0
    for index, char in enumerate(text):
        depths[index] = depth
        if char == "{":
            depth += 1
        elif char == "}":
            depth = max(0, depth - 1)
    depths[len(text)] = depth
    return depths


def application_file(path: Path, root: Path, out_name: str) -> bool:
    try:
        relative = path.relative_to(root)
    except ValueError:
        return False
    if any(part in IGNORE_DIRS or part == out_name for part in relative.parts):
        return False
    if relative.parts[:2] == ("tools", "AiContextExtractor"):
        return False
    lower_name = path.name.lower()
    if lower_name.endswith(".razor.js"):
        return True
    if path.suffix.lower() in SOLUTION_EXTENSIONS:
        return True
    if path.suffix.lower() in APPLICATION_EXTENSIONS:
        return path.suffix.lower() != ".json" or lower_name in CONFIG_NAMES
    return False


def iter_application_files(root: Path, out_name: str) -> list[Path]:
    result: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file() or not application_file(path, root, out_name):
            continue
        try:
            if path.stat().st_size <= MAX_FILE_BYTES:
                result.append(path)
        except OSError:
            continue
    return sorted(result)


def file_info(path: Path, root: Path) -> FileInfo:
    text = read_text(path)
    return FileInfo(
        path=rel(path, root),
        bytes=len(text.encode("utf-8", errors="ignore")),
        lines=text.count("\n") + 1,
        sha256=sha256_text(text),
    )


def source_digest(root: Path, files: Sequence[Path]) -> str:
    inputs = list(files)
    for relative in GENERATOR_INPUTS:
        candidate = root / relative
        if candidate.exists() and candidate not in inputs:
            inputs.append(candidate)
    digest = hashlib.sha256()
    for path in sorted(inputs):
        digest.update(rel(path, root).encode())
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def managed_instruction_block() -> str:
    return f"""{MANAGED_INSTRUCTIONS_BEGIN}
## Mandatory AI project bootstrap

Before analyzing, planning, editing, or running broad searches in this repository:

1. Read `.ai_hacks/AI_CONTEXT_COMPACT.md` completely.
2. Treat its **How to use this map**, **Change routing**, and **Critical invariants** sections as mandatory project instructions, unless they conflict with a higher-priority user or system instruction.
3. Use its type/method index, routes, DI, and persistence maps to narrow the task before opening implementation files.
4. If the generated context is missing or stale, run `python3 ai_hacks.py` from the repository root, then confirm it with `python3 ai_hacks.py --check`.
5. After structural changes, regenerate the context and include the resulting `.ai_hacks/` updates with the code change.

Do not edit this managed block by hand; edit `ai_hacks.py` or `ai_hacks.context.json` and regenerate it.
{MANAGED_INSTRUCTIONS_END}
"""


def merge_managed_instructions(existing: str) -> str:
    """Insert or replace the bootstrap without touching hand-written instructions."""
    block = managed_instruction_block().rstrip()
    pattern = re.compile(
        rf"{re.escape(MANAGED_INSTRUCTIONS_BEGIN)}.*?{re.escape(MANAGED_INSTRUCTIONS_END)}",
        re.S,
    )
    if pattern.search(existing):
        return pattern.sub(block, existing, count=1).rstrip() + "\n"
    existing = existing.rstrip()
    heading = re.match(r"^(# .+?)(?:\r?\n|$)", existing)
    if heading:
        insert_at = heading.end()
        return (existing[:insert_at].rstrip() + "\n\n" + block + "\n\n" + existing[insert_at:].lstrip()).rstrip() + "\n"
    if existing:
        return block + "\n\n" + existing + "\n"
    return block + "\n"


def build_instruction_outputs(root: Path) -> dict[str, str]:
    outputs: dict[str, str] = {}
    for relative in INSTRUCTION_TARGETS:
        path = root / relative
        existing = read_text(path) if path.exists() else ""
        outputs[relative] = merge_managed_instructions(existing)
    return outputs


def load_overlay(root: Path) -> dict[str, Any]:
    path = root / "ai_hacks.context.json"
    if not path.exists():
        return {}
    try:
        value = json.loads(read_text(path))
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Invalid {path.name}: {exc}") from exc
    if value.get("schemaVersion") != 1:
        raise RuntimeError(f"Unsupported {path.name} schemaVersion: {value.get('schemaVersion')!r}")
    return value


def run_csharp_extractor(root: Path) -> dict[str, Any]:
    project = root / "tools/AiContextExtractor/AiContextExtractor.csproj"
    if not project.exists():
        raise RuntimeError(f"C# extractor project is missing: {project}")
    build = subprocess.run(
        ["dotnet", "build", str(project), "--configuration", "Release", "--nologo", "--verbosity", "quiet"],
        cwd=root,
        text=True,
        capture_output=True,
    )
    if build.returncode:
        detail = (build.stdout + "\n" + build.stderr).strip()
        raise RuntimeError("C# context extractor failed to build:\n" + detail)
    dll = project.parent / "bin/Release/net10.0/AiContextExtractor.dll"
    run = subprocess.run(["dotnet", str(dll), str(root)], cwd=root, text=True, capture_output=True)
    if run.returncode:
        detail = (run.stdout + "\n" + run.stderr).strip()
        raise RuntimeError("C# context extractor failed:\n" + detail)
    try:
        value = json.loads(run.stdout)
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"C# context extractor emitted invalid JSON: {exc}\n{run.stdout[:1000]}") from exc
    if not isinstance(value, dict) or "types" not in value:
        raise RuntimeError("C# context extractor JSON does not contain a 'types' collection")
    return value


def description_for_type(item: dict[str, Any]) -> tuple[str, str]:
    existing = trim_description(item.get("description") or item.get("summary") or "")
    if existing:
        return existing, item.get("descriptionSource") or "xml"
    name = item.get("name", "type")
    words = split_identifier(name.lstrip("I") if name.startswith("I") and len(name) > 1 and name[1].isupper() else name)
    kind = item.get("kind", "type")
    file = item.get("file", "")
    if kind == "component":
        route = (item.get("routes") or [""])[0]
        return (f"Blazor page for {words}." if route else f"Blazor component for {words}."), "heuristic"
    if "/Entities/" in file:
        return f"Persisted {words} domain data.", "heuristic"
    if kind == "interface":
        return f"Defines the {words} contract.", "heuristic"
    if kind == "enum":
        return f"Defines {words} values.", "heuristic"
    suffixes = {
        "Connector": "Read-only external ingestion adapter for {topic}.",
        "Service": "Coordinates {topic} operations.",
        "Worker": "Runs scheduled {topic} background work.",
        "Provider": "Provides {topic} behavior.",
        "Context": "Persistence boundary for {topic}.",
        "Scorer": "Calculates {topic} scores.",
        "Rules": "Evaluates shared {topic} rules.",
        "Prompts": "Builds {topic} prompts.",
        "Result": "Carries the result of {topic}.",
        "Config": "Configures {topic}.",
        "Dto": "Transfers {topic} data.",
    }
    for suffix, template in suffixes.items():
        if name.endswith(suffix):
            topic = split_identifier(name[:-len(suffix)]) or words
            return template.format(topic=topic), "heuristic"
    return f"Represents {words}.", "heuristic"


def description_for_member(member: dict[str, Any], parent: dict[str, Any]) -> tuple[str, str]:
    existing = trim_description(member.get("description") or member.get("summary") or "", 180)
    if existing:
        return existing, member.get("descriptionSource") or "xml"
    name = member.get("name", "member")
    words = split_identifier(name)
    subject = words
    rules = [
        (r"^(OnInitialized|OnParametersSet|OnAfterRender)", "Runs the component {0} lifecycle step."),
        (r"^(Get|Find|Load|Read|Fetch)", "Loads or resolves {0}."),
        (r"^(Create|Add|Generate|Seed|Build|Make)", "Creates {0}."),
        (r"^(Save|Update|Set|Mark|Apply|Backfill|Migrate)", "Updates {0}."),
        (r"^(Delete|Remove|Purge|Archive|Reject|Cancel|Dismiss|Prune)", "Removes or transitions {0}."),
        (r"^(Check|Is|Has|Can|Should|Contains)", "Checks {0}."),
        (r"^(Run|Process|Ingest|Triage|Scan|Initialize|Execute)", "Coordinates {0}."),
        (r"^(Map|Parse|Resolve|Extract|Format|Normalize|Canonicalize|Compact|Trim|Split)", "Transforms or resolves {0}."),
        (r"^(Send|Approve|Queue|Record|Copy|Toggle|Restart)", "Handles {0}."),
    ]
    for pattern, template in rules:
        match = re.match(pattern, name)
        if match:
            remainder = split_identifier(name[match.end():]) or subject
            return template.format(remainder), "heuristic"
    if member.get("kind") == "constructor":
        return f"Creates {parent.get('name', 'the type')} with its required dependencies.", "heuristic"
    return f"Handles {subject}.", "heuristic"


def normalize_csharp_model(raw: dict[str, Any]) -> list[dict[str, Any]]:
    types = list(raw.get("types") or [])
    for item in types:
        description, source = description_for_type(item)
        item["description"] = description
        item["descriptionSource"] = source
        item.setdefault("members", [])
        item.setdefault("baseTypes", item.get("bases") or [])
        item.setdefault("constructors", [])
        for member in item["members"]:
            description, member_source = description_for_member(member, item)
            member["description"] = description
            member["descriptionSource"] = member_source
    # Reuse interface/base documentation when a concrete member has none of its own.
    by_name = {item.get("name"): item for item in types}
    for item in types:
        inherited = [by_name.get(simple_type_name(base)) for base in item.get("baseTypes", [])]
        inherited = [base for base in inherited if base]
        for member in item.get("members", []):
            if member.get("descriptionSource") != "heuristic":
                continue
            for base in inherited:
                match = next((candidate for candidate in base.get("members", [])
                              if candidate.get("name") == member.get("name")
                              and candidate.get("kind") == member.get("kind")), None)
                if match and match.get("description"):
                    member["description"] = match["description"]
                    member["descriptionSource"] = "inherited"
                    break
    return sorted(types, key=lambda item: (item.get("file", ""), item.get("line", 0), item.get("name", "")))


RAZOR_METHOD_RE = re.compile(
    r"(?m)^\s*(?P<mods>(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|new)\s+)*)"
    r"(?P<ret>(?:\([^\n]+?\)|[A-Za-z_][A-Za-z0-9_<>,.?\[\] ]*))\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*\((?P<params>[^)]*)\)\s*(?:=>|\{|where\b)",
)


def page_description(name: str) -> str:
    descriptions = {
        "Home": "Campaign-scoped dashboard with lead KPIs, activity, and top opportunities.",
        "Opportunities": "Searchable and filterable lead-review queue.",
        "OpportunityDetail": "Lead detail, triage, scoring, outreach, quotes, work tracking, and audit history.",
        "NewOpportunity": "Manual lead entry through the normal triage pipeline.",
        "Drafts": "Outreach generation and human approval queues.",
        "Quotes": "Quote and payment-state management.",
        "Campaigns": "Campaign objectives and source/lead ownership management.",
        "Content": "Trend signals, suggested topics, and publishable draft management.",
        "Sources": "Source configuration, health checks, and manual discovery runs.",
        "SkillProfile": "Operator skill-profile management.",
        "Settings": "Operator, AI, safety, discovery, and restart settings.",
        "Error": "Unhandled-error page.",
        "NotFound": "Missing-route page.",
    }
    return descriptions.get(name, f"Blazor component for {split_identifier(name)}.")


def extract_razor(root: Path, files: Sequence[Path]) -> tuple[list[dict[str, Any]], list[PageInfo]]:
    types: list[dict[str, Any]] = []
    pages: list[PageInfo] = []
    for path in files:
        if path.suffix.lower() != ".razor":
            continue
        text = read_text(path)
        file = rel(path, root)
        name = path.stem
        namespace = "DevLeads.Web.Components"
        if "/Components/" in file:
            tail = file.split("/Components/", 1)[1].rsplit("/", 1)[0] if "/" in file.split("/Components/", 1)[1] else ""
            if tail:
                namespace += "." + tail.replace("/", ".")
        routes = re.findall(r'^(?:\ufeff)?\s*@page\s+"([^"]+)"', text, re.M)
        inherits_match = re.search(r"^\s*@inherits\s+([^\s]+)", text, re.M)
        inherits = inherits_match.group(1) if inherits_match else "ComponentBase"
        implements = tuple(re.findall(r"^\s*@implements\s+([^\s]+)", text, re.M))
        injects = tuple(compact(match.group(1)) for match in re.finditer(r"^\s*@inject\s+(.+?)\s+[A-Za-z_]\w*\s*$", text, re.M))
        parameters = tuple(
            compact(f"{match.group(1)} {match.group(2)}")
            for match in re.finditer(r"\[Parameter[^\]]*\]\s*(?:public|private|protected|internal)\s+([^\n{=]+?)\s+([A-Za-z_]\w*)\s*\{", text)
        )
        members: list[dict[str, Any]] = []
        for match in RAZOR_METHOD_RE.finditer(mask_csharp(text)):
            mods = compact(match.group("mods")).split()
            access = next((value for value in mods if value in {"public", "private", "protected", "internal"}), "private")
            member = {
                "kind": "method", "name": match.group("name"), "access": access,
                "modifiers": mods, "returnType": compact(match.group("ret")),
                "parameters": [compact(value) for value in split_top_level(text[match.start("params"):match.end("params")])],
                "file": file, "line": line_no(text, match.start()),
            }
            member["description"], member["descriptionSource"] = description_for_member(member, {"name": name})
            members.append(member)
        js_functions: tuple[str, ...] = ()
        js_path = path.with_suffix(path.suffix + ".js")
        if js_path.exists():
            js_text = read_text(js_path)
            js_functions = tuple(re.findall(r"^(?:export\s+)?(?:async\s+)?function\s+([A-Za-z_]\w*)\s*\(", js_text, re.M))
            for function in js_functions:
                member = {
                    "kind": "function", "name": function, "access": "module", "modifiers": [],
                    "returnType": "JavaScript", "parameters": [], "file": rel(js_path, root),
                    "line": line_no(js_text, js_text.find("function " + function)),
                }
                member["description"], member["descriptionSource"] = description_for_member(member, {"name": name})
                members.append(member)
        item = {
            "namespace": namespace, "name": name, "fullName": f"{namespace}.{name}",
            "kind": "component", "access": "public", "modifiers": ["partial"],
            "bases": [inherits, *implements], "baseTypes": [inherits, *implements],
            "containingType": None, "file": file, "line": 1,
            "summary": page_description(name), "description": page_description(name),
            "descriptionSource": "curated", "members": members, "routes": routes,
            "injects": list(injects), "parameters": list(parameters), "javascript": list(js_functions),
        }
        types.append(item)
        for route in routes:
            pages.append(PageInfo(route, name, file, 1, page_description(name), inherits, implements, injects, parameters, js_functions))
    return types, sorted(pages, key=lambda page: (page.route.lower(), page.file))


def owner_for_line(types: Sequence[dict[str, Any]], file: str, line: int) -> str:
    candidates: list[tuple[int, str]] = []
    for item in types:
        if item.get("file") != file:
            continue
        for member in item.get("members", []):
            if member.get("kind") in {"method", "constructor"} and int(member.get("line", 0)) <= line:
                candidates.append((int(member.get("line", 0)), str(member.get("name", ""))))
    return max(candidates, default=(0, "startup"))[1]


def endpoint_description(verb: str, route: str) -> str:
    last = route.rstrip("/").split("/")[-1] or "home"
    last = re.sub(r"\{([^}:]+)(?::[^}]+)?\}", r"\1", last)
    action = split_identifier(last.replace("-", "_"))
    if verb == "GET":
        return f"Reads {action}."
    if verb == "DELETE":
        return f"Deletes {action}."
    return f"Runs the {action} action."


def extract_endpoints(root: Path, files: Sequence[Path], types: Sequence[dict[str, Any]]) -> list[EndpointInfo]:
    endpoints: list[EndpointInfo] = []
    group_prefixes: dict[str, str] = {}
    invocation_re = re.compile(r'\b(?P<receiver>[A-Za-z_]\w*)\.Map(?P<verb>Get|Post|Put|Delete|Patch|Methods)\s*\(\s*(?P<dollar>\$?)"(?P<route>[^"]+)"', re.I)
    for path in files:
        if path.suffix.lower() != ".cs":
            continue
        text = read_text(path)
        visible = mask_csharp(text, keep_strings=True)
        file = rel(path, root)
        for match in re.finditer(r'\bvar\s+([A-Za-z_]\w*)\s*=\s*([A-Za-z_]\w*)\.MapGroup\s*\(\s*"([^"]+)"', visible):
            group_prefixes[match.group(1)] = group_prefixes.get(match.group(2), "") + match.group(3)
        dynamic: list[tuple[re.Match[str], str, str, str]] = []
        for match in invocation_re.finditer(visible):
            verb = match.group("verb").upper()
            route = group_prefixes.get(match.group("receiver"), "") + match.group("route")
            line = line_no(text, match.start())
            owner = owner_for_line(types, file, line)
            placeholders = re.findall(r"(?<!\{)\{([A-Za-z_]\w*)\}(?!\})", route) if match.group("dollar") else []
            if placeholders:
                dynamic.append((match, route, owner, placeholders[0]))
                continue
            route = route.replace("{{", "{").replace("}}", "}")
            endpoints.append(EndpointInfo(verb, route, file, line, owner, endpoint_description(verb, route)))
        for match, template, owner, placeholder in dynamic:
            call_re = re.compile(rf"\b{re.escape(owner)}\s*\(\s*[A-Za-z_]\w*\s*,\s*\"([^\"]+)\"")
            for call in call_re.finditer(visible):
                route = template.replace("{" + placeholder + "}", call.group(1)).replace("{{", "{").replace("}}", "}")
                endpoints.append(EndpointInfo(match.group("verb").upper(), route, file, line_no(text, call.start()), owner, endpoint_description(match.group("verb").upper(), route)))
    unique = {(item.verb, item.route, item.file, item.line): item for item in endpoints}
    return sorted(unique.values(), key=lambda item: (item.route, item.verb, item.file, item.line))


def extract_di(root: Path, files: Sequence[Path]) -> list[DiRegistration]:
    result: list[DiRegistration] = []
    pattern = re.compile(r"\.Add(?P<kind>Scoped|Transient|Singleton|HostedService|DbContextFactory|HttpClient)\s*(?:<(?P<generic>[^;()]+?)>)?\s*\(", re.M)
    for path in files:
        if path.suffix.lower() != ".cs": continue
        text = read_text(path); visible = mask_csharp(text)
        file = rel(path, root)
        for match in pattern.finditer(visible):
            kind = match.group("kind"); generic = compact(match.group("generic") or "")
            parts = split_top_level(generic)
            service = parts[0] if parts else ""
            impl = parts[1] if len(parts) > 1 else service
            tail = visible[match.end(): visible.find(";", match.end()) + 1]
            factory = re.search(r"GetRequiredService\s*<\s*([^>]+)\s*>", tail)
            if factory: impl = compact(factory.group(1))
            if kind == "HostedService": service, impl = "IHostedService", (parts[0] if parts else "")
            elif kind == "DbContextFactory": service, impl = f"IDbContextFactory<{service}>", service
            elif kind == "HttpClient": service, impl = "named HttpClient", "HttpClient"
            result.append(DiRegistration(kind, service, impl, file, line_no(text, match.start())))
    return sorted(result, key=lambda item: (item.file, item.line, item.lifetime, item.service))


def extract_dbsets(types: Sequence[dict[str, Any]]) -> list[DbSetInfo]:
    result: list[DbSetInfo] = []
    for item in types:
        for member in item.get("members", []):
            match = re.fullmatch(r"DbSet\s*<\s*([^>]+)\s*>", compact(member.get("type", "")))
            if match:
                result.append(DbSetInfo(item["name"], match.group(1), member["name"], member["file"], member["line"]))
    return sorted(result, key=lambda item: item.property_name)


def extract_relationships(types: Sequence[dict[str, Any]], dbsets: Sequence[DbSetInfo]) -> list[RelationshipInfo]:
    entities = {item.entity for item in dbsets}; result: list[RelationshipInfo] = []
    by_name = {item.get("name"): item for item in types}
    for source in sorted(entities):
        for member in by_name.get(source, {}).get("members", []):
            if member.get("kind") not in {"property", "field"}: continue
            typ = compact(member.get("type", "")).rstrip("?")
            many = re.fullmatch(r"(?:List|ICollection|IReadOnlyList|IEnumerable)<([^>]+)>", typ)
            target = many.group(1) if many else typ
            if target in entities:
                result.append(RelationshipInfo(source, target, "one-to-many" if many else "many-to-one", member.get("name", "")))
    return result


def parse_projects(root: Path, files: Sequence[Path]) -> tuple[list[dict[str, Any]], list[str]]:
    projects: list[dict[str, Any]] = []; solutions: list[str] = []
    for path in files:
        if path.suffix.lower() in SOLUTION_EXTENSIONS:
            solutions.append(rel(path, root)); continue
        if path.suffix.lower() != ".csproj": continue
        data: dict[str, Any] = {"file": rel(path, root), "frameworks": [], "packages": [], "references": []}
        try: xml = ET.fromstring(read_text(path))
        except ET.ParseError: projects.append(data); continue
        for node in xml.iter():
            tag = node.tag.split("}")[-1]
            if tag in {"TargetFramework", "TargetFrameworks"} and node.text: data["frameworks"].extend(re.split(r"[;,]", node.text.strip()))
            elif tag == "PackageReference": data["packages"].append({"name": node.attrib.get("Include"), "version": node.attrib.get("Version")})
            elif tag == "ProjectReference" and node.attrib.get("Include"): data["references"].append(node.attrib["Include"].replace("\\", "/"))
        projects.append(data)
    return sorted(projects, key=lambda item: item["file"]), sorted(solutions)


def render_architecture(model: dict[str, Any], overlay: dict[str, Any]) -> str:
    product = overlay.get("product", {})
    out = ["# Architecture and Navigation Guide", "", product.get("purpose", "Semantic application map."), ""]
    out += ["## Project dependency direction", "", "```text", "DevLeads.Web  ->  DevLeads.Infrastructure  ->  DevLeads.Core", "```", ""]
    for project in overlay.get("projects", []):
        out.append(f"- **{project['name']}** (`{project['path']}`) — {project['responsibility']}")
    out += ["", "## Change routing", ""]
    rows = []
    for route in overlay.get("changeRouting", []):
        start = ", ".join(f"`{value}`" for value in route.get("startAt", []))
        related = ", ".join(f"`{value}`" for value in route.get("alsoCheck", []))
        if route.get("warning"): related += f" Warning: {route['warning']}"
        rows.append([route.get("concern", ""), start, related])
    out.append(md_table(["Concern", "Start here", "Also check / warning"], rows))
    out += ["## Runtime workflows", ""]
    for workflow in overlay.get("workflows", []):
        out += [f"### {workflow['name']}", "", workflow.get("summary", ""), "", " → ".join(f"`{step}`" for step in workflow.get("steps", [])), ""]
        out.extend(f"- {note}" for note in workflow.get("notes", []))
        out.append("")
    out += ["## Critical invariants", ""]
    out.extend(f"- {item}" for item in overlay.get("invariants", []))
    out += ["", "## Type hierarchy", ""]
    reverse: dict[str, list[str]] = defaultdict(list)
    for item in model["types"]:
        for base in item.get("bases", item.get("baseTypes", [])):
            reverse[simple_type_name(base)].append(item["name"])
    for base, children in sorted(reverse.items()):
        if len(children) >= 2 or base in {"DbContext", "BackgroundService", "ISourceConnector", "IAiTriageProvider", "IQueryPackProvider"}:
            out.append(f"- `{base}` → " + ", ".join(f"`{child}`" for child in sorted(set(children))))
    return "\n".join(out).rstrip() + "\n"


def member_signature(member: dict[str, Any]) -> str:
    name = member.get("name", "")
    params = ", ".join(member.get("parameters") or [])
    if member.get("kind") == "constructor": return f"{name}({params})"
    return_type = member.get("returnType") or member.get("type") or ""
    return f"{name}({params})" + (f" → {return_type}" if return_type else "")


def render_type_catalog(types: Sequence[dict[str, Any]], *, detailed: bool) -> str:
    out = ["# Type and Method Catalog", "", "All source-authored types and callable members are listed; implementation bodies and literal data are omitted.", ""]
    current_project = current_namespace = ""
    for item in types:
        file = item.get("file", "")
        project = next((part for part in ("DevLeads.Core", "DevLeads.Infrastructure", "DevLeads.Web") if part in file), "Other")
        namespace = item.get("namespace") or "global"
        if project != current_project:
            out += [f"## {project}", ""]; current_project = project; current_namespace = ""
        if namespace != current_namespace:
            out += [f"### `{namespace}`", ""]; current_namespace = namespace
        bases = item.get("bases", item.get("baseTypes", []))
        declaration = f"{item.get('access', '')} {item.get('kind', 'type')} `{item['name']}`"
        if bases: declaration += " : " + ", ".join(f"`{base}`" for base in bases)
        out += [f"#### {item['name']}", "", f"{declaration} · `{file}:{item.get('line', 1)}`", "", trim_description(item.get("description", ""), 260 if detailed else 180)]
        if item.get("descriptionSource") == "heuristic": out[-1] += " _(inferred)_"
        constructors = [m for m in item.get("members", []) if m.get("kind") == "constructor"]
        primary = item.get("primaryConstructorParameters") or []
        dependencies = primary or (max((m.get("parameters", []) for m in constructors), key=len, default=[]))
        if dependencies: out += ["", "Depends on: " + ", ".join(f"`{value}`" for value in dependencies) + "."]
        data_members = [m for m in item.get("members", []) if m.get("kind") in {"property", "field", "constant", "event", "enum-value", "enumMember"}]
        if data_members and ("/Entities/" in file or item.get("kind") in {"record", "enum"} or detailed):
            rendered = []
            for member in data_members:
                typ = member.get("type", "")
                rendered.append(f"`{member.get('name')}`" + (f": {typ}" if typ else ""))
            out += ["", "Data: " + ", ".join(rendered) + "."]
        callables = [m for m in item.get("members", []) if m.get("kind") in {"method", "constructor", "operator", "conversion", "destructor", "function"} and m.get("kind") != "constructor"]
        if callables:
            out += [""]
            for member in callables:
                description = trim_description(member.get("description", ""), 180 if detailed else 120)
                inferred = " _(inferred)_" if member.get("descriptionSource") == "heuristic" else ""
                out.append(f"- {member.get('access', '')} `{member_signature(member)}` — {description}{inferred}")
        out.append("")
    return "\n".join(out).rstrip() + "\n"


def render_compact_catalog(types: Sequence[dict[str, Any]]) -> str:
    """Dense complete catalog for the mandatory first-read document."""
    out = ["# Complete Type and Method Index", "", "Every source-authored type and callable name is present. Full signatures and data members are in `PUBLIC_API.md`.", ""]
    current_project = ""
    for item in types:
        file = item.get("file", "")
        project = next((part for part in ("DevLeads.Core", "DevLeads.Infrastructure", "DevLeads.Web") if part in file), "Other")
        if project != current_project:
            out += [f"## {project}", ""]; current_project = project
        bases = item.get("bases", item.get("baseTypes", []))
        base_text = " : " + ", ".join(bases) if bases else ""
        description = trim_description(item.get("description", ""), 150)
        inferred = " _(inferred)_" if item.get("descriptionSource") == "heuristic" else ""
        out.append(f"- **`{item['name']}{base_text}`** — {description}{inferred} (`{file}:{item.get('line', 1)}`)")
        for member in item.get("members", []):
            if member.get("kind") not in {"method", "operator", "conversion", "destructor", "function"}: continue
            description = trim_description(member.get("description", ""), 92)
            inferred = " _(inferred)_" if member.get("descriptionSource") == "heuristic" else ""
            out.append(f"  - {member.get('access', '')} `{member.get('name')}` — {description}{inferred}")
    return "\n".join(out).rstrip() + "\n"


def render_compact_surface(model: dict[str, Any]) -> str:
    out = ["# Runtime Surface Summary", "", "## Blazor pages", ""]
    out.extend(f"- `{page['route']}` → `{page['component']}` — {page['description']}" for page in model["pages"])
    groups: dict[str, int] = defaultdict(int)
    for endpoint in model["endpoints"]:
        parts = endpoint["route"].strip("/").split("/")
        group = "/" + "/".join(parts[:2] if parts and parts[0] == "api" else parts[:1])
        groups[group] += 1
    out += ["", "## HTTP, DI, and data", "", "- HTTP endpoint groups: " + ", ".join(f"`{name}` ({count})" for name, count in sorted(groups.items())) + "."]
    hosted = [item["implementation"] for item in model["di"] if item["lifetime"] == "HostedService"]
    out.append("- Hosted workers: " + ", ".join(f"`{name}`" for name in hosted) + ".")
    out.append("- EF DbSets: " + ", ".join(f"`{item['property_name']}`" for item in model["dbsets"]) + ".")
    out.append("- Complete endpoint, registration, and relationship tables: `ROUTES_AND_DI.md`.")
    return "\n".join(out) + "\n"


def render_routes_di(model: dict[str, Any]) -> str:
    out = ["# Routes, Dependency Injection, and Persistence", "", "## Blazor page routes", ""]
    out.append(md_table(["Route", "Component", "Purpose", "Injects"], [[p["route"], p["component"], p["description"], ", ".join(p["injects"])] for p in model["pages"]]))
    out += ["## HTTP endpoints", ""]
    out.append(md_table(["Verb", "Route", "Owner", "Purpose", "Source"], [[e["verb"], e["route"], e["owner"], e["description"], f"{e['file']}:{e['line']}"] for e in model["endpoints"]]))
    out += ["## Dependency injection", ""]
    out.append(md_table(["Registration", "Service", "Implementation", "Source"], [[d["lifetime"], d["service"], d["implementation"], f"{d['file']}:{d['line']}"] for d in model["di"]]))
    out += ["## EF Core DbSets", ""]
    out.append(md_table(["Context", "Entity", "Property", "Source"], [[d["context"], d["entity"], d["property_name"], f"{d['file']}:{d['line']}"] for d in model["dbsets"]]))
    out += ["## Inferred entity relationships", ""]
    out.append(md_table(["Source", "Cardinality", "Target", "Navigation"], [[r["source"], r["cardinality"], r["target"], r["property_name"]] for r in model["relationships"]]))
    return "\n".join(out).rstrip() + "\n"


def render_dependencies(model: dict[str, Any]) -> str:
    out = ["# Dependencies", "", "## Solutions", ""] + [f"- `{value}`" for value in model["solutions"]] + ["", "## Projects", ""]
    for project in model["projects"]:
        out += [f"### `{project['file']}`", "", f"- Frameworks: {', '.join(project['frameworks']) or 'not declared'}"]
        if project["references"]: out.append("- Project references: " + ", ".join(f"`{value}`" for value in project["references"]))
        if project["packages"]: out.append("- Packages: " + ", ".join(f"`{p['name']} {p.get('version') or ''}`" for p in project["packages"]))
        out.append("")
    return "\n".join(out).rstrip() + "\n"


def render_project_map(model: dict[str, Any]) -> str:
    out = ["# Project Map", "", f"Source digest: `{model['source_digest']}`", "", f"- Files: {len(model['files'])}", f"- Types/components: {len(model['types'])}", f"- Callable members: {model['coverage']['callables']}", f"- XML/manual/inherited descriptions: {model['coverage']['authored_descriptions']}", "", "## Files", ""]
    out.extend(f"- `{item['path']}` — {item['lines']} lines" for item in model["files"])
    return "\n".join(out) + "\n"


def render_compact(model: dict[str, Any], overlay: dict[str, Any], architecture: str, routes: str, catalog: str) -> str:
    ops = overlay.get("operations", {})
    return f"""# DevLeads AI Project Context

> Generated; do not hand-edit. Schema {SCHEMA_VERSION}, source digest `{model['source_digest']}`.
> Regenerate with `{ops.get('regenerate', 'python3 ai_hacks.py')}`; verify freshness with `{ops.get('check', 'python3 ai_hacks.py --check')}`.

## How to use this map

1. Use **Change routing** to choose the owning symbol and likely downstream files.
2. Use the hierarchy, routes, DI, and catalog to narrow the task before opening implementation bodies.
3. Read only the selected implementation and direct collaborators. Deeper generated references live beside this file.

The navigation rules, change-routing warnings, and critical invariants in this document are project instructions. Follow them unless a higher-priority user or system instruction explicitly overrides them. Repository bootstrap files for Codex, Claude, Gemini, and GitHub Copilot point agents here automatically.

Descriptions marked _(inferred)_ come from deterministic name/signature rules; all others come from source XML docs or the checked-in architecture overlay. No implementation code is copied here.

---

{architecture}

---

{routes}

---

{catalog}

---

## Completeness

- {len(model['types'])} source-authored C# types and Razor components.
- {model['coverage']['callables']} source-authored callable members.
- {len(model['pages'])} Blazor page routes; {len(model['endpoints'])} HTTP endpoints; {len(model['dbsets'])} EF DbSets.
- Full indexed source: {model['coverage']['source_characters']:,} characters (~{token_estimate(model['coverage']['source_characters']):,} tokens).
"""


def analyze(root: Path, out_name: str) -> tuple[dict[str, Any], dict[str, Any]]:
    paths = iter_application_files(root, out_name); infos = [file_info(path, root) for path in paths]
    overlay = load_overlay(root); csharp_raw = run_csharp_extractor(root); types = normalize_csharp_model(csharp_raw)
    razor_types, pages = extract_razor(root, paths); types.extend(razor_types); types.sort(key=lambda item: (item.get("file", ""), item.get("line", 0)))
    endpoints = extract_endpoints(root, paths, types); di = extract_di(root, paths); dbsets = extract_dbsets(types)
    relationships = extract_relationships(types, dbsets); projects, solutions = parse_projects(root, paths)
    callables = [m for t in types for m in t.get("members", []) if m.get("kind") in {"method", "constructor", "operator", "conversion", "destructor", "function"}]
    authored = sum(1 for t in types if t.get("descriptionSource") != "heuristic") + sum(1 for m in callables if m.get("descriptionSource") != "heuristic")
    model = {
        "schema_version": SCHEMA_VERSION, "source_digest": source_digest(root, paths), "root": ".",
        "files": [asdict(info) for info in infos], "solutions": solutions, "projects": projects,
        "types": types, "pages": [asdict(page) for page in pages], "endpoints": [asdict(endpoint) for endpoint in endpoints],
        "di": [asdict(item) for item in di], "dbsets": [asdict(item) for item in dbsets],
        "relationships": [asdict(item) for item in relationships], "overlay": overlay,
        "coverage": {"types": len(types), "callables": len(callables), "authored_descriptions": authored, "source_characters": sum(info.bytes for info in infos)},
    }
    return model, overlay


def build_outputs(model: dict[str, Any], overlay: dict[str, Any]) -> dict[str, str]:
    architecture = render_architecture(model, overlay); routes = render_routes_di(model)
    compact_catalog = render_compact_catalog(model["types"]); full_catalog = render_type_catalog(model["types"], detailed=True)
    compact_context = render_compact(model, overlay, architecture, render_compact_surface(model), compact_catalog)
    token_text = f"# Token Estimate\n\n- Indexed source: {model['coverage']['source_characters']:,} characters (~{token_estimate(model['coverage']['source_characters']):,} tokens).\n- Compact context: {len(compact_context):,} characters (~{token_estimate(len(compact_context)):,} tokens).\n- Reduction: {(1-len(compact_context)/model['coverage']['source_characters'])*100:.1f}%.\n"
    return {
        "AI_CONTEXT_COMPACT.md": compact_context, "ARCHITECTURE.md": architecture,
        "PUBLIC_API.md": full_catalog, "ROUTES_AND_DI.md": routes,
        "DEPENDENCIES.md": render_dependencies(model), "ENTRYPOINTS.md": "# Entry Points\n\nStart with `src/DevLeads.Web/Program.cs`, then use `ARCHITECTURE.md` and `ROUTES_AND_DI.md`.\n",
        "PROJECT_MAP.md": render_project_map(model), "TESTS.md": "# Test Map\n\nNo .NET test project is currently present. Generator validation lives in `tests/test_ai_hacks.py`.\n",
        "TOKEN_ESTIMATE.md": token_text,
        "IGNORE_GUIDE.md": "# AI Ignore Guide\n\nAvoid generated/build artifacts (`bin`, `obj`, `.git`, `.ai_hacks`) unless the task specifically targets them.\n",
        ".aiignore": "\n".join(sorted(IGNORE_DIRS)) + "\n",
        "symbols.json": json.dumps(model, indent=2, sort_keys=True) + "\n",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate semantic AI context for DevLeads.")
    parser.add_argument("--root", default="."); parser.add_argument("--out", default=".ai_hacks"); parser.add_argument("--check", action="store_true")
    args = parser.parse_args(); root = Path(args.root).resolve(); out = (root / args.out).resolve()
    try:
        model, overlay = analyze(root, out.name)
        outputs = build_outputs(model, overlay)
        instruction_outputs = build_instruction_outputs(root)
    except Exception as exc:
        print(f"ai_hacks: {exc}", file=sys.stderr); return 2
    if args.check:
        stale = [name for name, content in outputs.items() if not (out / name).exists() or read_text(out / name) != content]
        stale.extend(relative for relative, content in instruction_outputs.items()
                     if not (root / relative).exists() or read_text(root / relative) != content)
        if stale:
            print("Generated context is stale: " + ", ".join(stale)); return 1
        print(f"Generated context is current ({model['source_digest'][:12]})."); return 0
    out.mkdir(parents=True, exist_ok=True)
    for name, content in outputs.items():
        (out / name).write_text(content.rstrip() + "\n", encoding="utf-8")
    for relative, content in instruction_outputs.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
    print(f"Generated {len(outputs)} context files in {out}")
    print("Installed mandatory AI bootstrap instructions in: " + ", ".join(INSTRUCTION_TARGETS))
    print(f"Types {len(model['types'])}; callables {model['coverage']['callables']}; pages {len(model['pages'])}; endpoints {len(model['endpoints'])}; DbSets {len(model['dbsets'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
