#!/usr/bin/env python3
"""vh -- drive the modded Valheim dev copy through the MistWorms DevBridge (spec section 15.4).

Every command prints one JSON document on stdout. Exit code 0 = success, 1 = the command ran but
reported a failure (HTTP error, failed scenario, timeout), 2 = usage or environment error.

    py tools/vh.py launch                 py tools/vh.py scenario S00
    py tools/vh.py wait-ready             py tools/vh.py scenario-all
    py tools/vh.py cmd "devcommands"      py tools/vh.py log-tail -n 50 --mw
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parent.parent
TOOLS_DIR = REPO_ROOT / "tools"
DEV_DIR = Path(os.environ.get("MW_DEV_DIR", r"C:\ValheimDev"))
GAME_LOG = DEV_DIR / "BepInEx" / "LogOutput.log"
REPORT_PATH = REPO_ROOT / "docs" / "test-report.md"
SHOTS_DIR = REPO_ROOT / "docs" / "shots"
DEFAULT_PORT = 27781
SCENARIO_POLL_TIMEOUT = 180.0  # the bridge fails a scenario after 120 s; leave headroom


class VhError(Exception):
    """A failure that should be reported as JSON with a non-zero exit code."""

    def __init__(self, message: str, code: int = 1, payload: Any = None):
        super().__init__(message)
        self.code = code
        self.payload = payload


# ---------------------------------------------------------------------------- output


def emit(obj: Any) -> None:
    print(json.dumps(obj, indent=2, ensure_ascii=False))


# ---------------------------------------------------------------------------- HTTP


class Bridge:
    def __init__(self, port: int):
        self.base = f"http://127.0.0.1:{port}"

    def request(self, method: str, path: str, body: Any = None, timeout: float = 30.0,
                query: dict[str, str] | None = None) -> Any:
        url = self.base + path
        if query:
            url += "?" + urllib.parse.urlencode(query)
        data = None
        headers = {"Accept": "application/json"}
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json; charset=utf-8"
        req = urllib.request.Request(url, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as res:
                return _decode(res.read())
        except urllib.error.HTTPError as e:
            payload = _decode(e.read())
            msg = payload.get("error") if isinstance(payload, dict) else None
            raise VhError(f"HTTP {e.code} on {method} {path}: {msg or e.reason}", 1,
                          {"status": e.code, "response": payload}) from None
        except (urllib.error.URLError, ConnectionError, TimeoutError, OSError) as e:
            reason = getattr(e, "reason", e)
            raise VhError(f"bridge unreachable at {self.base} ({reason}); is the game running with -mwdev?", 1) from None

    def get(self, path: str, **kw: Any) -> Any:
        return self.request("GET", path, **kw)

    def post(self, path: str, body: Any = None, **kw: Any) -> Any:
        return self.request("POST", path, body if body is not None else {}, **kw)


def _decode(raw: bytes) -> Any:
    if not raw:
        return {}
    try:
        return json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return {"raw": raw.decode("utf-8", "replace")}


# ---------------------------------------------------------------------------- processes


def run_powershell(script: Path, args: list[str], timeout: float = 120.0) -> dict[str, Any]:
    if not script.exists():
        raise VhError(f"{script} not found", 2)
    cmd = ["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(script), *args]
    env = dict(os.environ, MW_REPO_ROOT=str(REPO_ROOT))
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, env=env, cwd=str(REPO_ROOT))
    except subprocess.TimeoutExpired:
        raise VhError(f"{script.name} did not finish within {timeout:.0f} s", 1) from None
    out_lines = [l for l in proc.stdout.splitlines() if l.strip()]
    pid = None
    if out_lines and re.fullmatch(r"\s*\d+\s*", out_lines[-1]):
        pid = int(out_lines[-1])
    return {
        "exitCode": proc.returncode,
        "pid": pid,
        "stdout": out_lines,
        "stderr": [l for l in proc.stderr.splitlines() if l.strip()],
    }


def process_running(image: str) -> list[int]:
    try:
        out = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {image}", "/FO", "CSV", "/NH"],
                             capture_output=True, text=True, timeout=15).stdout
    except (OSError, subprocess.TimeoutExpired):
        return []
    pids = []
    for line in out.splitlines():
        parts = [p.strip('"') for p in line.split('","')]
        if len(parts) > 1 and parts[0].lower() == image.lower() and parts[1].strip('"').isdigit():
            pids.append(int(parts[1].strip('"')))
    return pids


# ---------------------------------------------------------------------------- commands


def cmd_launch(a: argparse.Namespace) -> int:
    ps_args = ["-World", a.world, "-Character", a.character, "-BridgePort", str(a.port)]
    if a.join:
        ps_args += ["-Join", a.join]
    res = run_powershell(TOOLS_DIR / "run-game.ps1", ps_args)
    emit(res)
    return 0 if res["exitCode"] == 0 and res["pid"] else 1


def cmd_wait_ready(a: argparse.Namespace) -> int:
    bridge = Bridge(a.port)
    deadline = time.monotonic() + a.timeout
    last: Any = None
    last_error = None
    while time.monotonic() < deadline:
        try:
            last = bridge.get("/status", timeout=10)
            last_error = None
            if isinstance(last, dict) and last.get("ready"):
                emit({"ready": True, "waitedSeconds": round(a.timeout - (deadline - time.monotonic()), 1), "status": last})
                return 0
        except VhError as e:
            last_error = str(e)
        time.sleep(2)
    emit({"ready": False, "error": f"not ready after {a.timeout:.0f} s", "lastError": last_error, "status": last})
    return 1


def cmd_status(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).get("/status"))
    return 0


def cmd_cmd(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"cmd": a.command}
    if a.wait:
        body["waitSeconds"] = a.wait
    emit(Bridge(a.port).post("/console", body, timeout=60 + a.wait))
    return 0


def cmd_tp(a: argparse.Namespace) -> int:
    # "tp X Y Z", or "tp X Z" to land on the terrain surface.
    if len(a.coords) == 3:
        body: dict[str, Any] = {"x": a.coords[0], "y": a.coords[1], "z": a.coords[2]}
    elif len(a.coords) == 2:
        body = {"x": a.coords[0], "z": a.coords[1]}
    else:
        raise VhError("usage: vh tp X Y Z  (or X Z for ground level)", 2)
    emit(Bridge(a.port).post("/tp", body, timeout=90))
    return 0


def cmd_tp_mine(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).post("/tp-mine", {}, timeout=120))
    return 0


def cmd_spawn(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"n": a.n}
    if a.radius is not None:
        body["radius"] = a.radius
    if a.state:
        body["state"] = a.state
    if a.nest_index is not None:
        body["nestIndex"] = a.nest_index
    if a.force:
        body["force"] = True
    emit(Bridge(a.port).post("/spawn", body))
    return 0


def cmd_mass(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"n": a.n}
    if a.force:
        body["force"] = True
    if a.no_colony:
        body["colony"] = False
    if a.freeze:
        body["freeze"] = True
    emit(Bridge(a.port).post("/mass", body))
    return 0


def cmd_preview(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"kind": a.kind}
    if a.n is not None:
        body["n"] = a.n
    if a.anim:
        body["anim"] = a.anim
    if a.count is not None:
        body["count"] = a.count
    if a.compare:
        body["compare"] = a.compare
    if a.distance is not None:
        body["distance"] = a.distance
    emit(Bridge(a.port).post("/preview", body))
    return 0


def cmd_camera(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"mode": a.mode}
    if a.pos:
        body["pos"] = a.pos
    if a.look_at:
        body["lookAt"] = a.look_at
    emit(Bridge(a.port).post("/camera", body))
    return 0


def cmd_nest(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).post("/nest", {"force": True} if a.force else {}))
    return 0


def cmd_state(a: argparse.Namespace) -> int:
    query = {"worms": "1"} if a.worms else None
    emit(Bridge(a.port).get("/state", query=query))
    return 0


def cmd_freeze(a: argparse.Namespace) -> int:
    # The mw_freeze console command owns the switch; the bridge only relays it.
    emit(Bridge(a.port).post("/console", {"cmd": f"mw_freeze {a.mode}"}))
    return 0


def cmd_hit(a: argparse.Namespace) -> int:
    damage: dict[str, float] = {}
    for item in a.damage:
        key, sep, value = item.partition("=")
        if not sep:
            raise VhError(f"damage '{item}' must look like slash=35", 2)
        damage[key] = float(value)
    body: dict[str, Any] = {"damage": damage}
    if ":" in a.target:
        body["targetZdoid"] = a.target  # a vanilla object by ZDOID (user:id)
    else:
        try:
            body["worm"] = int(a.target)
        except ValueError:
            raise VhError(f"target '{a.target}' is neither a worm index nor a ZDOID (user:id)", 2) from None
    if a.colony:
        body["colony"] = a.colony
    if a.point:
        body["point"] = a.point
    emit(Bridge(a.port).post("/hit", body))
    return 0


def cmd_attack(a: argparse.Namespace) -> int:
    body: dict[str, Any] = {"weapon": a.weapon}
    if a.look_at:
        body["lookAt"] = a.look_at
    emit(Bridge(a.port).post("/attack", body))
    return 0


def cmd_perf(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).get("/perf"))
    return 0


def cmd_shot(a: argparse.Namespace) -> int:
    query = {"name": a.name} if a.name else None
    emit(Bridge(a.port).get("/screenshot", query=query, timeout=60))
    return 0


def cmd_player(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).post("/player", json.loads(a.json)))
    return 0


def cmd_time(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).post("/time", {"scale": a.scale}))
    return 0


def cmd_wait(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).post("/wait", {"seconds": a.seconds, "game": a.game}, timeout=a.seconds * (8 if a.game else 1) + 30))
    return 0


def parse_params(pairs: list[str] | None) -> dict[str, str]:
    params: dict[str, str] = {}
    for pair in pairs or []:
        if "=" not in pair:
            raise VhError(f"bad --param '{pair}' (expected key=value)", 2)
        k, v = pair.split("=", 1)
        params[k.strip()] = v
    return params


def run_scenario(bridge: Bridge, name: str, params: dict[str, str], poll_timeout: float) -> dict[str, Any]:
    started = bridge.post("/scenario", {"name": name, "params": params})
    run_id = started["id"]
    deadline = time.monotonic() + poll_timeout
    while True:
        result = bridge.get(f"/scenario/{urllib.parse.quote(run_id)}")
        if result.get("status") != "running":
            return result
        if time.monotonic() > deadline:
            result["status"] = "error"
            result["error"] = f"vh gave up polling after {poll_timeout:.0f} s"
            return result
        time.sleep(0.5)


def cmd_scenario(a: argparse.Namespace) -> int:
    result = run_scenario(Bridge(a.port), a.name, parse_params(a.param), a.timeout)
    emit(result)
    return 0 if result.get("status") == "passed" else 1


def _link_shot(path_str: str) -> str:
    """Returns a docs/-relative link, copying the shot into docs/shots when the game wrote it elsewhere."""
    path = Path(path_str)
    docs = REPORT_PATH.parent
    try:
        return path.resolve().relative_to(docs.resolve()).as_posix()
    except (ValueError, OSError):
        pass
    if path.exists():
        SHOTS_DIR.mkdir(parents=True, exist_ok=True)
        dest = SHOTS_DIR / path.name
        shutil.copy2(path, dest)
        return dest.relative_to(docs).as_posix()
    return path.as_posix()


def _md(text: Any) -> str:
    return str(text).replace("|", "\\|").replace("\n", " ")


def write_report(results: list[dict[str, Any]], status: Any, perf: Any) -> None:
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    now = _dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    passed = sum(1 for r in results if r.get("status") == "passed")
    lines = [
        "# MistWorms - in-game test report",
        "",
        f"Generated by `tools/vh.py scenario-all` on {now}.",
    ]
    if isinstance(status, dict):
        lines.append(f"Plugin {status.get('pluginVersion')} - scene `{status.get('scene')}` - world `{status.get('world')}` "
                     f"- server: {status.get('isServer')} - fps: {round(status.get('fps') or 0, 1)}")
    lines += [
        "",
        f"**{passed}/{len(results)} scenarios passed.**",
        "",
        "| Scenario | Result | Duration (s) | Assertions (ok/total) | Shots |",
        "| --- | --- | --- | --- | --- |",
    ]
    for r in results:
        ok = r.get("status") == "passed"
        n_ok = r.get("assertionsPassed", 0)
        n_all = n_ok + r.get("assertionsFailed", 0)
        shots = " ".join(f"[{Path(s).stem}]({_link_shot(s)})" for s in r.get("shots") or []) or "-"
        result = "OK" if ok else f"KO ({r.get('status')})"
        lines.append(f"| {_md(r.get('name'))} | {result} | {float(r.get('durationSeconds') or 0):.1f} | {n_ok}/{n_all} | {shots} |")

    failures = [r for r in results if r.get("status") != "passed"]
    if failures:
        lines += ["", "## Failures", ""]
        for r in failures:
            lines.append(f"### {_md(r.get('name'))} ({r.get('id', '-')})")
            lines.append("")
            for asr in r.get("assertions") or []:
                if not asr.get("ok"):
                    lines.append(f"- `{_md(asr.get('name'))}`: expected `{_md(asr.get('expected'))}`, actual `{_md(asr.get('actual'))}`")
            if r.get("error"):
                first = str(r["error"]).strip().splitlines()[0]
                lines.append(f"- error: `{_md(first)}`")
            tail = (r.get("log") or [])[-10:]
            if tail:
                lines += ["", "```text", *tail, "```"]
            lines.append("")

    lines += ["", "## Performance", ""]
    if isinstance(perf, dict) and "error" not in perf:
        lines += ["```json", json.dumps(perf, indent=2), "```"]
    else:
        err = perf.get("error") if isinstance(perf, dict) else perf
        lines.append(f"Not available ({_md(err)}).")
    REPORT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")


def cmd_scenario_all(a: argparse.Namespace) -> int:
    bridge = Bridge(a.port)
    names: list[str] = bridge.get("/scenarios").get("names", [])
    if a.only:
        wanted = {n.lower() for n in a.only}
        names = [n for n in names if n.lower() in wanted or n.split("_", 1)[0].lower() in wanted]
    results = []
    for name in names:
        try:
            results.append(run_scenario(bridge, name, parse_params(a.param), a.timeout))
        except VhError as e:
            results.append({"name": name, "status": "error", "error": str(e), "assertions": [], "shots": [], "log": []})
            if "unreachable" in str(e):
                break  # the game is gone; the remaining runs would fail the same way
    try:
        status = bridge.get("/status")
    except VhError as e:
        status = {"error": str(e)}
    try:
        perf = bridge.get("/perf")
    except VhError as e:
        perf = {"error": str(e)}
    write_report(results, status, perf)

    failed = [r.get("name") for r in results if r.get("status") != "passed"]
    emit({
        "report": str(REPORT_PATH),
        "total": len(results),
        "passed": len(results) - len(failed),
        "failed": failed,
        "results": [{k: r.get(k) for k in ("id", "name", "status", "durationSeconds", "assertionsPassed", "assertionsFailed", "shots", "error")}
                    for r in results],
    })
    return 1 if failed or not results else 0


def cmd_log_tail(a: argparse.Namespace) -> int:
    path = Path(a.file) if a.file else GAME_LOG
    if not path.exists():
        raise VhError(f"{path} not found", 1)
    with path.open("r", encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    if a.mw:
        lines = [l for l in lines if "MistWorms" in l]
    if a.errors:
        lines = [l for l in lines if re.search(r"\[(Error|Fatal)\s*:", l) or "Exception" in l]
    if a.filter:
        rx = re.compile(a.filter, re.IGNORECASE)
        lines = [l for l in lines if rx.search(l)]
    emit({"file": str(path), "count": min(a.n, len(lines)), "lines": lines[-a.n:] if a.n > 0 else lines})
    return 0


def cmd_dump(a: argparse.Namespace) -> int:
    body = {"path": str(Path(a.path).resolve())} if a.path else {}
    emit(Bridge(a.port).post("/dump", body, timeout=300))
    return 0


def cmd_server(a: argparse.Namespace) -> int:
    script = TOOLS_DIR / "run-server.ps1"
    if a.action == "start":
        res = run_powershell(script, [], timeout=90)
    else:
        res = run_powershell(script, ["-Stop"], timeout=120)
        res["stillRunning"] = process_running("valheim_server.exe")
    emit(res)
    return 0 if res["exitCode"] == 0 else 1


def cmd_quit(a: argparse.Namespace) -> int:
    before = process_running("valheim.exe")
    res = Bridge(a.port).post("/quit", {})
    deadline = time.monotonic() + a.timeout
    remaining = before
    while remaining and time.monotonic() < deadline:
        time.sleep(1)
        remaining = [p for p in process_running("valheim.exe") if p in before]
    res["exited"] = not remaining
    res["remainingPids"] = remaining
    emit(res)
    return 0 if not remaining else 1


def cmd_raw(a: argparse.Namespace) -> int:
    body = json.loads(a.body) if a.body else None
    emit(Bridge(a.port).request(a.method.upper(), a.path if a.path.startswith("/") else "/" + a.path, body, timeout=a.timeout))
    return 0


# ---------------------------------------------------------------------------- CLI


def build_parser() -> argparse.ArgumentParser:
    # --port is accepted before or after the sub-command.
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--port", type=int, default=argparse.SUPPRESS, help=f"DevBridge port (default {DEFAULT_PORT})")

    p = argparse.ArgumentParser(prog="vh", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter, parents=[common])
    p.set_defaults(port=DEFAULT_PORT)
    sub = p.add_subparsers(dest="command", required=True, metavar="COMMAND")

    def add(name: str, fn, help_text: str) -> argparse.ArgumentParser:
        sp = sub.add_parser(name, help=help_text, parents=[common])
        sp.set_defaults(fn=fn)
        return sp

    sp = add("preview", cmd_preview, "show a static worm / mass / nest preview next to the player")
    sp.add_argument("kind", choices=["worm", "mass", "nest", "clear"])
    sp.add_argument("--n", type=int, help="mass size (kind=mass)")
    sp.add_argument("--anim", help="worm animation: crawl, rear, strike, sleep")
    sp.add_argument("--count", type=int)
    sp.add_argument("--compare", help="vanilla prefab shown beside the subject, e.g. Tick")
    sp.add_argument("--distance", type=float)

    sp = add("camera", cmd_camera, "free camera (bridge /camera)")
    sp.add_argument("mode", nargs="?", choices=["free", "follow"], default="free")
    sp.add_argument("--pos", type=float, nargs=3, metavar=("X", "Y", "Z"))
    sp.add_argument("--look-at", type=float, nargs=3, metavar=("X", "Y", "Z"))

    sp = add("launch", cmd_launch, "start the dev game copy (tools/run-game.ps1)")
    sp.add_argument("--world", default="MWTest")
    sp.add_argument("--character", default="MWTester")
    sp.add_argument("--join", default="", help="host:port to join instead of autoloading the local world")

    sp = add("wait-ready", cmd_wait_ready, "poll /status until the player is in a loaded zone")
    sp.add_argument("--timeout", type=float, default=180.0)

    add("status", cmd_status, "GET /status")

    sp = add("cmd", cmd_cmd, "run a console command and print its output")
    sp.add_argument("command")
    sp.add_argument("--wait", type=float, default=0.0, help="keep capturing output for N more seconds")

    sp = add("tp", cmd_tp, "teleport the local player")
    sp.add_argument("coords", type=float, nargs="+", metavar="COORD", help="X Y Z, or X Z for ground level")

    add("tp-mine", cmd_tp_mine, "teleport into the closest infested mine")

    sp = add("spawn", cmd_spawn, "spawn N virtual worms on the floor around the player")
    sp.add_argument("n", type=int)
    sp.add_argument("--radius", type=float, help="disc radius in metres (default 4)")
    sp.add_argument("--state", help="Idle (default), Sleeping, Hunting, Fleeing...")
    sp.add_argument("--nest-index", type=int)
    sp.add_argument("--force", action="store_true", help="ignore population caps")

    sp = add("mass", cmd_mass, "create a real writhing mass of N worms in front of the player, attached to the local colony")
    sp.add_argument("n", type=int)
    sp.add_argument("--force", action="store_true")
    sp.add_argument("--no-colony", action="store_true", help="no colony: the mass cannot dissolve into worms")
    sp.add_argument("--freeze", action="store_true", help="freeze the AI of this mass")

    sp = add("nest", cmd_nest, "create a nest in front of the player")
    sp.add_argument("--force", action="store_true")

    sp = add("state", cmd_state, "GET /state: colonies, worms per state, player health and gear")
    sp.add_argument("--worms", action="store_true", help="also list every worm (index, position, state, hp)")

    sp = add("freeze", cmd_freeze, "stop or resume the simulation of every colony (mw_freeze)")
    sp.add_argument("mode", choices=["on", "off"])
    add("perf", cmd_perf, "GET /perf")

    sp = add("hit", cmd_hit, "inject a vanilla HitData on a worm (bridge /hit)")
    sp.add_argument("target", help="worm slot index (see state --worms), or a ZDOID user:id for any vanilla object")
    sp.add_argument("damage", nargs="+", metavar="TYPE=AMOUNT", help="e.g. slash=35 fire=10")
    sp.add_argument("--colony", help="colony ZDOID when several are loaded")
    sp.add_argument("--point", type=float, nargs=3, metavar=("X", "Y", "Z"))

    sp = add("attack", cmd_attack, "equip a weapon and start a real melee attack (bridge /attack)")
    sp.add_argument("weapon", help="item prefab, e.g. SwordBronze")
    sp.add_argument("--look-at", type=float, nargs=3, metavar=("X", "Y", "Z"))

    sp = add("shot", cmd_shot, "take a screenshot into docs/shots")
    sp.add_argument("name", nargs="?")

    sp = add("player", cmd_player, "POST /player with a JSON body")
    sp.add_argument("json", help='e.g. \'{"god":true,"clear":true,"equip":["SwordBronze"]}\'')

    sp = add("time", cmd_time, "set the time scale (0..4)")
    sp.add_argument("scale", type=float)

    sp = add("wait", cmd_wait, "wait N seconds inside the game")
    sp.add_argument("seconds", type=float)
    sp.add_argument("--game", action="store_true", help="scaled game seconds instead of real seconds")

    sp = add("scenario", cmd_scenario, "run one scenario and print its result")
    sp.add_argument("name")
    sp.add_argument("--param", "-p", action="append", metavar="KEY=VALUE")
    sp.add_argument("--timeout", type=float, default=SCENARIO_POLL_TIMEOUT)

    sp = add("scenario-all", cmd_scenario_all, "run every scenario and write docs/test-report.md")
    sp.add_argument("--only", nargs="*", help="restrict to these names or Sxx ids")
    sp.add_argument("--param", "-p", action="append", metavar="KEY=VALUE")
    sp.add_argument("--timeout", type=float, default=SCENARIO_POLL_TIMEOUT)

    sp = add("log-tail", cmd_log_tail, "print the end of BepInEx/LogOutput.log")
    sp.add_argument("-n", type=int, default=50, help="number of lines (0 = all)")
    sp.add_argument("--mw", action="store_true", help="only lines mentioning MistWorms")
    sp.add_argument("--errors", action="store_true", help="only errors and exceptions")
    sp.add_argument("--filter", help="regular expression to match")
    sp.add_argument("--file", help="another log file (e.g. the server's)")

    sp = add("dump", cmd_dump, "write the vanilla reference dump")
    sp.add_argument("path", nargs="?")

    sp = add("server", cmd_server, "start or stop the local dedicated server")
    sp.add_argument("action", choices=["start", "stop"])

    sp = add("quit", cmd_quit, "quit the game cleanly")
    sp.add_argument("--timeout", type=float, default=30.0, help="seconds to wait for the process to exit")

    sp = add("raw", cmd_raw, "send an arbitrary request to the bridge")
    sp.add_argument("method")
    sp.add_argument("path")
    sp.add_argument("body", nargs="?")
    sp.add_argument("--timeout", type=float, default=60.0)
    return p


def main(argv: list[str] | None = None) -> int:
    # JSON consumers read UTF-8 regardless of the console code page.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = build_parser().parse_args(argv)
    try:
        return args.fn(args)
    except VhError as e:
        out: dict[str, Any] = {"error": str(e)}
        if e.payload is not None:
            out["details"] = e.payload
        emit(out)
        return e.code
    except json.JSONDecodeError as e:
        emit({"error": f"invalid JSON argument: {e}"})
        return 2
    except KeyboardInterrupt:
        emit({"error": "interrupted"})
        return 130


if __name__ == "__main__":
    sys.exit(main())
