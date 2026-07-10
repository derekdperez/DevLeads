#!/usr/bin/env python3
"""
ai_hacks.py

Simple .NET project context reducer for AI coding agents.

Run from your project root:

    python ai_hacks.py

Optional:

    python ai_hacks.py --root /path/to/project
    python ai_hacks.py --out .ai_hacks

Generated:

    .ai_hacks/
      AI_CONTEXT_COMPACT.md   # Give this to the AI first
      PUBLIC_API.md           # Public classes/methods/properties only
      DEPENDENCIES.md         # .csproj/.sln/package summary
      ENTRYPOINTS.md          # Program.cs, Startup.cs, appsettings, controllers, etc.
      ROUTES_AND_DI.md        # ASP.NET routes, DI registrations, EF DbSets
      TESTS.md                # Test classes and test method names
      TOKEN_ESTIMATE.md       # Rough context savings estimate
      .aiignore               # Folders/files the AI should avoid
      symbols.json            # Machine-readable version

Goal:
    Reduce token/context usage by making the AI read a compact map first,
    instead of opening full implementation files.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any


# ----------------------------
# Configuration
# ----------------------------

IGNORE_DIRS = {
    ".git", ".svn", ".hg",
    ".vs", ".vscode", ".idea",
    "bin", "obj", "node_modules",
    "dist", "build", "out", "target",
    ".venv", "venv", "__pycache__",
    "coverage", "TestResults", ".ai_hacks",
}

SOURCE_EXTS = {".cs", ".razor", ".cshtml"}
PROJECT_EXTS = {".sln", ".csproj", ".props", ".targets"}
CONFIG_NAMES = {
    "appsettings.json",
    "appsettings.development.json",
    "appsettings.production.json",
    "appsettings.staging.json",
    "launchsettings.json",
    "web.config",
    "app.config",
    "dockerfile",
    "docker-compose.yml",
    "docker-compose.yaml",
    "package.json",
}

MAX_FILE_BYTES = 1_000_000


# ----------------------------
# Data
# ----------------------------

@dataclass
class FileInfo:
    path: str
    bytes: int
    lines: int
    sha256: str


@dataclass
class Symbol:
    kind: str
    name: str
    file: str
    line: int
    signature: str
    access: str = ""
    parent: str = ""


@dataclass
class Route:
    verb: str
    route: str
    file: str
    line: int
    method: str = ""


@dataclass
class DiRegistration:
    lifetime: str
    service: str
    implementation: str
    file: str
    line: int


@dataclass
class DbSetInfo:
    dbcontext: str
    entity: str
    property_name: str
    file: str
    line: int


# ----------------------------
# Helpers
# ----------------------------

def rel(path: Path, root: Path) -> str:
    return str(path.relative_to(root)).replace("\\", "/")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8", errors="ignore")).hexdigest()


def line_no(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def token_estimate(chars: int) -> int:
    # Crude but useful: 1 token ~= 4 chars.
    return round(chars / 4)


def compact(value: str) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def strip_comments(text: str) -> str:
    def keep_newlines(match: re.Match[str]) -> str:
        s = match.group(0)
        return "".join("\n" if ch == "\n" else " " for ch in s)

    text = re.sub(r"/\*.*?\*/", keep_newlines, text, flags=re.S)
    text = re.sub(r"//.*", "", text)
    return text


def should_skip(path: Path, root: Path, out_dir_name: str) -> bool:
    try:
        relative = path.relative_to(root)
    except ValueError:
        return True

    if any(part in IGNORE_DIRS or part == out_dir_name for part in relative.parts):
        return True

    name = path.name.lower()

    if name.endswith(".min.js") or name.endswith(".min.css"):
        return True

    if name in {
        "package-lock.json",
        "yarn.lock",
        "pnpm-lock.yaml",
        "packages.lock.json",
        "project.assets.json",
    }:
        return True

    return False


def interesting_file(path: Path) -> bool:
    name = path.name.lower()
    return (
        path.suffix.lower() in SOURCE_EXTS
        or path.suffix.lower() in PROJECT_EXTS
        or name in CONFIG_NAMES
    )


def iter_files(root: Path, out_dir_name: str) -> list[Path]:
    files: list[Path] = []
    for p in root.rglob("*"):
        if p.is_file() and not should_skip(p, root, out_dir_name) and interesting_file(p):
            try:
                if p.stat().st_size <= MAX_FILE_BYTES:
                    files.append(p)
            except OSError:
                pass
    return sorted(files)


def write(path: Path, text: str) -> None:
    path.write_text(text.rstrip() + "\n", encoding="utf-8")


def md_table(headers: list[str], rows: list[list[Any]]) -> str:
    if not rows:
        return "_None found._\n"

    result = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]

    for row in rows:
        result.append("| " + " | ".join(str(x).replace("|", "\\|") for x in row) + " |")

    return "\n".join(result) + "\n"


# ----------------------------
# C# extraction
# ----------------------------

CS_TYPE_RE = re.compile(
    r"""
    ^\s*
    (?P<attrs>(?:\[[^\]]+\]\s*)*)
    (?P<mods>(?:(?:public|private|protected|internal|sealed|abstract|static|partial|readonly|file)\s+)*)
    (?P<kind>class|interface|struct|record|enum)
    \s+
    (?P<name>[A-Za-z_][A-Za-z0-9_]*)
    (?P<tail>[^\n{;]*)?
    """,
    re.M | re.X,
)

CS_METHOD_RE = re.compile(
    r"""
    ^\s*
    (?P<attrs>(?:\[[^\]]+\]\s*)*)
    (?P<mods>(?:(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|partial|extern|new)\s+)+)
    (?P<return>[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*)
    \s+
    (?P<name>[A-Za-z_][A-Za-z0-9_]*)
    \s*
    \((?P<args>[^)]*)\)
    """,
    re.M | re.X,
)

CS_PROPERTY_RE = re.compile(
    r"""
    ^\s*
    (?P<attrs>(?:\[[^\]]+\]\s*)*)
    (?P<mods>(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|new|required)\s+)+)
    (?P<type>[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*)
    \s+
    (?P<name>[A-Za-z_][A-Za-z0-9_]*)
    \s*
    \{\s*(?:get|init|set)
    """,
    re.M | re.X,
)

NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", re.M)

USING_RE = re.compile(r"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*);", re.M)

HTTP_ATTR_RE = re.compile(
    r"\[(HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch|HttpHead|HttpOptions)(?:\(\s*\"([^\"]*)\"\s*\))?\]"
)

ROUTE_ATTR_RE = re.compile(r"\[Route\(\s*\"([^\"]+)\"\s*\)\]")

MINIMAL_API_RE = re.compile(
    r"\bapp\.Map(Get|Post|Put|Delete|Patch|Methods)\s*\(\s*\"([^\"]+)\"",
    re.I,
)

DI_RE = re.compile(
    r"""
    \.
    (?P<method>AddScoped|AddTransient|AddSingleton)
    \s*
    (?:<(?P<generic>[^>]+)>)?
    \s*
    \(
        (?P<args>[^;]*?)
    \)
    """,
    re.X | re.S,
)

DBCONTEXT_RE = re.compile(
    r"\bclass\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?:[A-Za-z0-9_<>.,\s]*\b)?DbContext\b"
)

DBSET_RE = re.compile(
    r"public\s+DbSet\s*<\s*(?P<entity>[A-Za-z_][A-Za-z0-9_]*)\s*>\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{"
)

TEST_METHOD_RE = re.compile(
    r"""
    ^\s*
    (?:\[(?:Fact|Theory|Test|TestMethod|DataTestMethod)(?:\([^\]]*\))?\]\s*)+
    (?:public\s+|private\s+|protected\s+|internal\s+)?
    (?:async\s+)?
    (?P<return>[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*)
    \s+
    (?P<name>[A-Za-z_][A-Za-z0-9_]*)
    \s*\(
    """,
    re.M | re.X,
)


def access_from_mods(mods: str) -> str:
    for access in ["public", "private", "protected", "internal"]:
        if re.search(rf"\b{access}\b", mods or ""):
            return access
    return ""


def extract_csharp_symbols(root: Path, path: Path, text: str) -> list[Symbol]:
    file = rel(path, root)
    clean = strip_comments(text)
    symbols: list[Symbol] = []

    # Keep this deliberately simple: public-ish API surface, not exact Roslyn output.
    for m in CS_TYPE_RE.finditer(clean):
        mods = compact(m.group("mods"))
        access = access_from_mods(mods)
        kind = m.group("kind")
        name = m.group("name")
        tail = compact(m.group("tail") or "")
        sig = compact(f"{mods} {kind} {name} {tail}")
        symbols.append(Symbol(kind=kind, name=name, file=file, line=line_no(clean, m.start()), signature=sig, access=access))

    for m in CS_METHOD_RE.finditer(clean):
        mods = compact(m.group("mods"))
        access = access_from_mods(mods)
        if access != "public":
            continue

        name = m.group("name")
        if name in {"if", "for", "foreach", "while", "switch", "catch", "using", "lock"}:
            continue

        ret = compact(m.group("return"))
        args = compact(m.group("args"))
        sig = compact(f"{mods} {ret} {name}({args})")
        symbols.append(Symbol(kind="method", name=name, file=file, line=line_no(clean, m.start()), signature=sig, access=access))

    for m in CS_PROPERTY_RE.finditer(clean):
        mods = compact(m.group("mods"))
        access = access_from_mods(mods)
        if access != "public":
            continue

        typ = compact(m.group("type"))
        name = m.group("name")
        sig = compact(f"{mods} {typ} {name} {{ get; }}")
        symbols.append(Symbol(kind="property", name=name, file=file, line=line_no(clean, m.start()), signature=sig, access=access))

    return sorted(symbols, key=lambda s: s.line)


def extract_usings(text: str) -> list[str]:
    return sorted(set(USING_RE.findall(text)))


def extract_routes(root: Path, path: Path, text: str) -> list[Route]:
    file = rel(path, root)
    clean = strip_comments(text)
    routes: list[Route] = []

    class_routes = ROUTE_ATTR_RE.findall(clean[:2000])
    base_route = class_routes[0] if class_routes else ""

    for m in HTTP_ATTR_RE.finditer(clean):
        verb_attr = m.group(1)
        verb = verb_attr.replace("Http", "").upper()
        route_suffix = m.group(2) or ""

        after = clean[m.end(): m.end() + 600]
        method_match = re.search(
            r"(?:public|private|protected|internal)?\s*(?:async\s+)?[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(",
            after,
        )
        method = method_match.group(1) if method_match else ""

        full_route = "/".join(x.strip("/") for x in [base_route, route_suffix] if x.strip("/"))
        full_route = "/" + full_route if full_route else "/"

        routes.append(Route(verb=verb, route=full_route, file=file, line=line_no(clean, m.start()), method=method))

    for m in MINIMAL_API_RE.finditer(clean):
        verb = m.group(1).upper()
        if verb == "METHODS":
            verb = "METHODS"
        routes.append(Route(verb=verb, route=m.group(2), file=file, line=line_no(clean, m.start()), method="minimal-api"))

    return routes


def extract_di(root: Path, path: Path, text: str) -> list[DiRegistration]:
    file = rel(path, root)
    clean = strip_comments(text)
    registrations: list[DiRegistration] = []

    for m in DI_RE.finditer(clean):
        lifetime = m.group("method").replace("Add", "")
        generic = compact(m.group("generic") or "")
        args = compact(m.group("args") or "")

        service = ""
        implementation = ""

        if generic:
            parts = [p.strip() for p in generic.split(",")]
            service = parts[0] if parts else ""
            implementation = parts[1] if len(parts) > 1 else ""
        elif args:
            # Common form: AddScoped(typeof(IFoo), typeof(Foo))
            type_names = re.findall(r"typeof\(([^)]+)\)", args)
            if type_names:
                service = type_names[0]
                implementation = type_names[1] if len(type_names) > 1 else ""

        if service or implementation:
            registrations.append(
                DiRegistration(
                    lifetime=lifetime,
                    service=service,
                    implementation=implementation,
                    file=file,
                    line=line_no(clean, m.start()),
                )
            )

    return registrations


def extract_ef(root: Path, path: Path, text: str) -> tuple[list[str], list[DbSetInfo]]:
    file = rel(path, root)
    clean = strip_comments(text)

    contexts = [m.group("name") for m in DBCONTEXT_RE.finditer(clean)]
    dbcontext = contexts[0] if contexts else ""

    dbsets: list[DbSetInfo] = []
    if dbcontext:
        for m in DBSET_RE.finditer(clean):
            dbsets.append(
                DbSetInfo(
                    dbcontext=dbcontext,
                    entity=m.group("entity"),
                    property_name=m.group("name"),
                    file=file,
                    line=line_no(clean, m.start()),
                )
            )

    return contexts, dbsets


def extract_tests(root: Path, path: Path, text: str) -> list[dict[str, Any]]:
    file = rel(path, root)
    clean = strip_comments(text)

    found = []
    for m in TEST_METHOD_RE.finditer(clean):
        found.append(
            {
                "file": file,
                "line": line_no(clean, m.start()),
                "test": m.group("name"),
            }
        )
    return found


# ----------------------------
# Project/dependency extraction
# ----------------------------

def xml_local(tag: str) -> str:
    return tag.split("}", 1)[-1] if "}" in tag else tag


def extract_csproj(path: Path, root: Path) -> dict[str, Any]:
    data: dict[str, Any] = {
        "file": rel(path, root),
        "sdk": None,
        "target_frameworks": [],
        "package_references": [],
        "project_references": [],
    }

    text = read_text(path)

    try:
        root_xml = ET.fromstring(text)
    except ET.ParseError:
        return data

    data["sdk"] = root_xml.attrib.get("Sdk")

    for elem in root_xml.iter():
        name = xml_local(elem.tag)

        if name in {"TargetFramework", "TargetFrameworks"} and elem.text:
            frameworks = re.split(r"[;,]", elem.text.strip())
            data["target_frameworks"].extend([x for x in frameworks if x])

        if name == "PackageReference":
            data["package_references"].append(
                {
                    "include": elem.attrib.get("Include") or elem.attrib.get("Update"),
                    "version": elem.attrib.get("Version"),
                }
            )

        if name == "ProjectReference":
            include = elem.attrib.get("Include")
            if include:
                data["project_references"].append(include.replace("\\", "/"))

    data["target_frameworks"] = sorted(set(data["target_frameworks"]))
    return data


def extract_package_json(path: Path, root: Path) -> dict[str, Any]:
    try:
        data = json.loads(read_text(path))
    except Exception:
        return {"file": rel(path, root), "dependencies": [], "devDependencies": []}

    return {
        "file": rel(path, root),
        "name": data.get("name"),
        "scripts": sorted((data.get("scripts") or {}).keys()),
        "dependencies": sorted((data.get("dependencies") or {}).keys()),
        "devDependencies": sorted((data.get("devDependencies") or {}).keys()),
    }


def detect_entrypoints(root: Path, files: list[Path]) -> list[dict[str, str]]:
    entries: list[dict[str, str]] = []

    for p in files:
        r = rel(p, root)
        name = p.name.lower()
        parts = set(p.parts)

        reason = ""
        if name == "program.cs":
            reason = "Main ASP.NET/Core startup or console entry point."
        elif name == "startup.cs":
            reason = "Legacy ASP.NET Core startup class."
        elif name.startswith("appsettings") and name.endswith(".json"):
            reason = "Application configuration; values should be redacted before sharing."
        elif p.suffix.lower() == ".sln":
            reason = "Solution file; shows project composition."
        elif p.suffix.lower() == ".csproj":
            reason = "Project file; shows target framework and dependencies."
        elif name in {"dockerfile", "docker-compose.yml", "docker-compose.yaml"}:
            reason = "Container/runtime entry point."
        elif "Controllers" in parts:
            reason = "ASP.NET controller folder."
        elif "Components" in parts or "Pages" in parts:
            reason = "UI/page/component folder."

        if reason:
            entries.append({"file": r, "reason": reason})

    return entries


# ----------------------------
# Config redaction summary
# ----------------------------

SENSITIVE_KEY_RE = re.compile(
    r"(password|passwd|pwd|secret|token|apikey|api_key|connectionstring|connectionstrings|clientsecret|privatekey|key)",
    re.I,
)


def summarize_json_config(path: Path, root: Path) -> dict[str, Any]:
    file = rel(path, root)
    try:
        data = json.loads(read_text(path))
    except Exception:
        return {"file": file, "type": "json", "summary": "Could not parse JSON."}

    def walk(obj: Any, prefix: str = "") -> list[str]:
        keys: list[str] = []

        if isinstance(obj, dict):
            for k, v in obj.items():
                full = f"{prefix}:{k}" if prefix else str(k)
                if SENSITIVE_KEY_RE.search(str(k)):
                    keys.append(f"{full} = [REDACTED]")
                elif isinstance(v, (dict, list)):
                    keys.extend(walk(v, full))
                else:
                    keys.append(f"{full} = {type(v).__name__}")

        elif isinstance(obj, list):
            keys.append(f"{prefix} = list[{len(obj)}]")

        return keys

    return {"file": file, "type": "json", "summary": walk(data)[:100]}


def summarize_config(root: Path, files: list[Path]) -> list[dict[str, Any]]:
    summaries = []
    for p in files:
        name = p.name.lower()
        if name.startswith("appsettings") and name.endswith(".json"):
            summaries.append(summarize_json_config(p, root))
        elif name in {"launchsettings.json"}:
            summaries.append(summarize_json_config(p, root))
        elif name in {"web.config", "app.config", ".env", ".env.example"}:
            summaries.append({"file": rel(p, root), "type": name, "summary": "Config file detected. Values not copied."})
    return summaries


# ----------------------------
# Rendering
# ----------------------------

def render_public_api(symbols: list[Symbol]) -> str:
    rows = []
    for s in symbols:
        if s.access == "public" or s.kind in {"class", "interface", "record", "struct", "enum"}:
            rows.append([s.file, s.line, s.kind, s.signature])

    return "# Public API Surface\n\nOnly public/high-level symbols are listed. Implementation bodies are intentionally omitted.\n\n" + md_table(
        ["File", "Line", "Kind", "Signature"],
        rows,
    )


def render_dependencies(csprojs: list[dict[str, Any]], package_jsons: list[dict[str, Any]], slns: list[str]) -> str:
    out = ["# Dependencies\n"]

    out.append("## Solutions\n")
    if slns:
        out.extend(f"- `{x}`" for x in slns)
    else:
        out.append("_None found._")

    out.append("\n## .NET projects\n")
    if not csprojs:
        out.append("_None found._")
    else:
        for p in csprojs:
            out.append(f"### `{p['file']}`")
            out.append(f"- SDK: `{p.get('sdk') or ''}`")
            out.append(f"- Target frameworks: `{', '.join(p.get('target_frameworks') or [])}`")
            packages = p.get("package_references") or []
            if packages:
                out.append("- Packages:")
                for pkg in packages:
                    version = f" {pkg.get('version')}" if pkg.get("version") else ""
                    out.append(f"  - `{pkg.get('include')}{version}`")
            refs = p.get("project_references") or []
            if refs:
                out.append("- Project references:")
                for ref in refs:
                    out.append(f"  - `{ref}`")
            out.append("")

    out.append("\n## package.json\n")
    if not package_jsons:
        out.append("_None found._")
    else:
        for p in package_jsons:
            out.append(f"### `{p['file']}`")
            if p.get("name"):
                out.append(f"- Name: `{p['name']}`")
            if p.get("scripts"):
                out.append(f"- Scripts: `{', '.join(p['scripts'])}`")
            if p.get("dependencies"):
                out.append(f"- Dependencies: `{', '.join(p['dependencies'])}`")
            if p.get("devDependencies"):
                out.append(f"- Dev dependencies: `{', '.join(p['devDependencies'])}`")
            out.append("")

    return "\n".join(out)


def render_entrypoints(entries: list[dict[str, str]]) -> str:
    rows = [[e["file"], e["reason"]] for e in entries]
    return "# Entry Points\n\nRead these before opening random implementation files.\n\n" + md_table(["File/Folder", "Why it matters"], rows)


def render_routes_di_ef(routes: list[Route], di: list[DiRegistration], dbsets: list[DbSetInfo]) -> str:
    out = ["# Routes, Dependency Injection, and EF Core\n"]

    out.append("## API Routes\n")
    out.append(md_table(
        ["Verb", "Route", "File", "Line", "Method"],
        [[r.verb, r.route, r.file, r.line, r.method] for r in routes],
    ))

    out.append("\n## Dependency Injection\n")
    out.append(md_table(
        ["Lifetime", "Service", "Implementation", "File", "Line"],
        [[d.lifetime, d.service, d.implementation, d.file, d.line] for d in di],
    ))

    out.append("\n## EF Core DbSets\n")
    out.append(md_table(
        ["DbContext", "Entity", "Property", "File", "Line"],
        [[d.dbcontext, d.entity, d.property_name, d.file, d.line] for d in dbsets],
    ))

    return "\n".join(out)


def render_tests(tests: list[dict[str, Any]]) -> str:
    return "# Test Map\n\nTest names are often the fastest behavior summary.\n\n" + md_table(
        ["File", "Line", "Test"],
        [[t["file"], t["line"], t["test"]] for t in tests],
    )


def render_token_estimate(file_infos: list[FileInfo], compact_text: str) -> str:
    source_chars = sum(f.bytes for f in file_infos)
    compact_chars = len(compact_text)

    source_tokens = token_estimate(source_chars)
    compact_tokens = token_estimate(compact_chars)

    if source_tokens:
        reduction = round((1 - (compact_tokens / source_tokens)) * 100, 1)
    else:
        reduction = 0

    return f"""# Token Estimate

Approximation: 1 token ≈ 4 characters.

| Context | Characters | Estimated tokens |
| --- | ---: | ---: |
| Full indexed files | {source_chars:,} | {source_tokens:,} |
| AI_CONTEXT_COMPACT.md | {compact_chars:,} | {compact_tokens:,} |

Estimated context reduction: **{reduction}%**

This is approximate, but good enough to show whether the script is doing useful work.
"""


def render_ignore_guide() -> str:
    items = sorted(IGNORE_DIRS | {
        "*.min.js",
        "*.min.css",
        "package-lock.json",
        "yarn.lock",
        "pnpm-lock.yaml",
        "packages.lock.json",
        "project.assets.json",
    })

    return "# AI Ignore Guide\n\nAgents should avoid these unless explicitly needed:\n\n" + "\n".join(f"- `{x}`" for x in items) + "\n"


def render_aiignore() -> str:
    items = sorted(IGNORE_DIRS | {
        "*.min.js",
        "*.min.css",
        "package-lock.json",
        "yarn.lock",
        "pnpm-lock.yaml",
        "packages.lock.json",
        "project.assets.json",
    })
    return "\n".join(items) + "\n"


def render_project_map(root: Path, file_infos: list[FileInfo], symbols: list[Symbol]) -> str:
    by_ext: dict[str, int] = {}
    for f in file_infos:
        ext = Path(f.path).suffix.lower() or "[none]"
        by_ext[ext] = by_ext.get(ext, 0) + 1

    out = [
        "# Project Map",
        "",
        f"Root: `{root}`",
        "",
        "## Summary",
        "",
        f"- Indexed files: {len(file_infos)}",
        f"- Indexed lines: {sum(f.lines for f in file_infos):,}",
        f"- Public/high-level symbols: {len(symbols):,}",
        "",
        "## File types",
        "",
    ]

    for ext, count in sorted(by_ext.items()):
        out.append(f"- `{ext}`: {count}")

    out.append("\n## Files\n")
    for f in file_infos:
        out.append(f"- `{f.path}` — {f.lines:,} lines")

    return "\n".join(out)


def render_compact_context(
    project_map: str,
    entrypoints: str,
    dependencies: str,
    routes_di_ef: str,
    public_api: str,
    tests: str,
    token_text: str,
) -> str:
    def section_only(text: str, max_chars: int) -> str:
        return text[:max_chars].rstrip() + ("\n...[truncated]\n" if len(text) > max_chars else "")

    return f"""# AI Context Compact

Read this file first. Do not open implementation files until you have narrowed the task to a specific file, symbol, route, or registration.

## Agent rule

Use this generated context as the project map. Implementation bodies are intentionally omitted to reduce token usage.

---

{section_only(project_map, 8_000)}

---

{section_only(entrypoints, 8_000)}

---

{section_only(dependencies, 12_000)}

---

{section_only(routes_di_ef, 14_000)}

---

{section_only(public_api, 18_000)}

---

{section_only(tests, 8_000)}

---

{token_text}
"""


# ----------------------------
# Main analysis
# ----------------------------

def analyze(root: Path, out_dir_name: str) -> dict[str, Any]:
    files = iter_files(root, out_dir_name)

    file_infos: list[FileInfo] = []
    symbols: list[Symbol] = []
    routes: list[Route] = []
    di: list[DiRegistration] = []
    dbcontexts: list[str] = []
    dbsets: list[DbSetInfo] = []
    tests: list[dict[str, Any]] = []
    imports_by_file: dict[str, list[str]] = {}
    csprojs: list[dict[str, Any]] = []
    package_jsons: list[dict[str, Any]] = []
    slns: list[str] = []

    for p in files:
        try:
            text = read_text(p)
        except Exception:
            continue

        file_infos.append(
            FileInfo(
                path=rel(p, root),
                bytes=len(text.encode("utf-8", errors="ignore")),
                lines=text.count("\n") + 1,
                sha256=sha256(text),
            )
        )

        ext = p.suffix.lower()
        name = p.name.lower()

        if ext == ".cs":
            file_symbols = extract_csharp_symbols(root, p, text)
            symbols.extend(file_symbols)
            routes.extend(extract_routes(root, p, text))
            di.extend(extract_di(root, p, text))
            ctxs, sets = extract_ef(root, p, text)
            dbcontexts.extend(ctxs)
            dbsets.extend(sets)
            tests.extend(extract_tests(root, p, text))

            usings = extract_usings(text)
            if usings:
                imports_by_file[rel(p, root)] = usings

        elif ext == ".csproj":
            csprojs.append(extract_csproj(p, root))

        elif ext == ".sln":
            slns.append(rel(p, root))

        elif name == "package.json":
            package_jsons.append(extract_package_json(p, root))

    entries = detect_entrypoints(root, files)
    config_summaries = summarize_config(root, files)

    return {
        "root": str(root),
        "files": file_infos,
        "symbols": symbols,
        "routes": routes,
        "di": di,
        "dbcontexts": sorted(set(dbcontexts)),
        "dbsets": dbsets,
        "tests": tests,
        "imports_by_file": imports_by_file,
        "csprojs": csprojs,
        "package_jsons": package_jsons,
        "slns": slns,
        "entrypoints": entries,
        "config_summaries": config_summaries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate compact AI context for a .NET project.")
    parser.add_argument("--root", default=".", help="Project root. Default: current directory.")
    parser.add_argument("--out", default=".ai_hacks", help="Output directory. Default: .ai_hacks")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    out = (root / args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)

    result = analyze(root, out.name)

    project_map = render_project_map(root, result["files"], result["symbols"])
    public_api = render_public_api(result["symbols"])
    dependencies = render_dependencies(result["csprojs"], result["package_jsons"], result["slns"])
    entrypoints = render_entrypoints(result["entrypoints"])
    routes_di_ef = render_routes_di_ef(result["routes"], result["di"], result["dbsets"])
    tests = render_tests(result["tests"])

    # Compact context gets rendered before token estimate, then token estimate is based on compact text.
    compact_without_token = render_compact_context(
        project_map,
        entrypoints,
        dependencies,
        routes_di_ef,
        public_api,
        tests,
        "# Token Estimate\n\nPending.\n",
    )
    token_text = render_token_estimate(result["files"], compact_without_token)

    compact_context = render_compact_context(
        project_map,
        entrypoints,
        dependencies,
        routes_di_ef,
        public_api,
        tests,
        token_text,
    )

    write(out / "AI_CONTEXT_COMPACT.md", compact_context)
    write(out / "PROJECT_MAP.md", project_map)
    write(out / "PUBLIC_API.md", public_api)
    write(out / "DEPENDENCIES.md", dependencies)
    write(out / "ENTRYPOINTS.md", entrypoints)
    write(out / "ROUTES_AND_DI.md", routes_di_ef)
    write(out / "TESTS.md", tests)
    write(out / "TOKEN_ESTIMATE.md", token_text)
    write(out / "IGNORE_GUIDE.md", render_ignore_guide())
    write(out / ".aiignore", render_aiignore())

    symbols_json = {
        "root": result["root"],
        "files": [asdict(f) for f in result["files"]],
        "symbols": [asdict(s) for s in result["symbols"]],
        "routes": [asdict(r) for r in result["routes"]],
        "di": [asdict(d) for d in result["di"]],
        "dbcontexts": result["dbcontexts"],
        "dbsets": [asdict(d) for d in result["dbsets"]],
        "tests": result["tests"],
        "csprojs": result["csprojs"],
        "package_jsons": result["package_jsons"],
        "entrypoints": result["entrypoints"],
        "config_summaries": result["config_summaries"],
    }

    write(out / "symbols.json", json.dumps(symbols_json, indent=2))

    full_chars = sum(f.bytes for f in result["files"])
    compact_chars = len(compact_context)
    full_tokens = token_estimate(full_chars)
    compact_tokens = token_estimate(compact_chars)
    reduction = round((1 - compact_tokens / full_tokens) * 100, 1) if full_tokens else 0

    print(f"Generated AI context in: {out}")
    print(f"Indexed files: {len(result['files'])}")
    print(f"Public/high-level symbols: {len(result['symbols'])}")
    print(f"Routes: {len(result['routes'])}")
    print(f"DI registrations: {len(result['di'])}")
    print(f"EF DbSets: {len(result['dbsets'])}")
    print(f"Tests: {len(result['tests'])}")
    print(f"Estimated context reduction: {reduction}%")
    print()
    print("Give this file to the AI first:")
    print(out / "AI_CONTEXT_COMPACT.md")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
