"""Turns the echo server's /results JSON into a Markdown report: requests per scenario, with the headers they carried."""
import json
import re
import sys

records = json.load(open(sys.argv[1], encoding="utf-8"))
title = sys.argv[2] if len(sys.argv) > 2 else "Results"

out = [f"## {title}", ""]
env = [r["path"] for r in records if r["kind"] == "log" and r["path"].startswith("env ")]
if env:
    out += [f"`{env[0]}`", ""]
ua = next((r["userAgent"] for r in records if r["kind"] == "request" and r.get("userAgent")), None)
if ua:
    out += [f"User agent: `{ua}`", ""]

current = None
for r in records:
    if r["kind"] == "log":
        line = r["path"]
        m = re.match(r"^(s\d+-[\w-]+) begin$", line)
        if m:
            current = m.group(1)
            out += ["", f"### {current}", "", "| # | Event / request | Origin | Authorization | X-Proto-Header |", "|---|---|---|---|---|"]
        elif current and line.startswith(current + " ") and not line.endswith(" begin") and not line.endswith(" end"):
            out.append(f"| {r['seq']} | _{line[len(current) + 1:]}_ | | | |")
        elif line.startswith("error"):
            out += ["", "```", line, "```"]
    elif current:
        auth = "**yes**" if r.get("authorization") else "no"
        custom = "**yes**" if r.get("protoHeader") else "no"
        out.append(f"| {r['seq']} | `{r['method']} {r['path']}` | {r['origin']} | {auth} | {custom} |")

# Verdict (#60): nothing sent by LoadUrl may reach origin B.
leaks = [r for r in records if r["kind"] == "request" and r["origin"] == "B" and (r.get("authorization") or r.get("protoHeader"))]
out += ["", "### Verdict", ""]
out += [f"- **LEAK**: `{r['method']} {r['path']}` on B (#{r['seq']})" for r in leaks] or ["- No LoadUrl header reached origin B."]

print("\n".join(out))
sys.exit(3 if leaks else 0)
