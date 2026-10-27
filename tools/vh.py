#!/usr/bin/env python3
"""vh -- drive the modded Valheim dev copy through the MistWorms DevBridge (spec section 15.4).

Every command prints one JSON document on stdout. Exit code 0 = success, 1 = the command ran but
reported a failure (HTTP error, failed scenario, timeout), 2 = usage or environment error.

    py tools/vh.py launch                 py tools/vh.py scenario S00
    py tools/vh.py wait-ready             py tools/vh.py scenario-all
    py tools/vh.py cmd "devcommands"      py tools/vh.py log-tail -n 50 --mw
    py tools/vh.py s17                    server + client + S17 + refusal of a client without the mod (phase 8-2)
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
import tempfile
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
DEV_SERVER_DIR = Path(os.environ.get("MW_DEV_SERVER_DIR", r"C:\ValheimDevServer"))
SERVER_LOG = DEV_SERVER_DIR / "BepInEx" / "LogOutput.log"
S17_SUMMARY = REPO_ROOT / "docs" / "s17-summary.json"
CHECKLIST_MARKER = "<!-- manual-checklist -->"   # write_report keeps everything from this line of docs/test-report.md
REPORT_PATH = REPO_ROOT / "docs" / "test-report.md"
SHOTS_DIR = REPO_ROOT / "docs" / "shots"
DEFAULT_PORT = 27781
SCENARIO_POLL_DEFAULT = 600.0  # per-scenario polling limit; scenarios set their own timeout inside the game (up to 600 s) and the bridge does not expose it
IDLE_WAIT = 300.0               # after giving up on a run, how long to wait for the bridge to be idle again


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
    # No pipes: a game/server child started by the script inherits stdout/stderr and would keep a pipe
    # open forever (no EOF, communicate() hangs even after a kill). Temp files have no such problem.
    with tempfile.TemporaryFile() as fout, tempfile.TemporaryFile() as ferr:
        p = subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=fout, stderr=ferr, env=env, cwd=str(REPO_ROOT))
        try:
            p.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            p.kill()
            p.wait()
            raise VhError(f"{script.name} did not finish within {timeout:.0f} s", 1) from None
        fout.seek(0)
        ferr.seek(0)
        stdout_text = fout.read().decode("utf-8", "replace")
        stderr_text = ferr.read().decode("utf-8", "replace")
    proc = subprocess.CompletedProcess(cmd, p.returncode, stdout_text, stderr_text)
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
    if a.no_mod:
        if not a.join:
            raise VhError("--no-mod needs --join host:port (a vanilla client cannot autoload a world)", 2)
        ps_args += ["-NoMod"]
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


def cmd_mass_visual(a: argparse.Namespace) -> int:
    # What the swarm renderer did for a mass: roles visible and drawn, LOD, cap, engulf, cost. No id = the mass closest to the player.
    query = {"id": a.id} if a.id else None
    emit(Bridge(a.port).get("/mass-visual", query=query))
    return 0


def cmd_mass_engulf(a: argparse.Namespace) -> int:
    # The mw_mass_engulf console command owns the switch (dev only until phase 5); the bridge only relays it.
    emit(Bridge(a.port).post("/console", {"cmd": f"mw_mass_engulf {a.value}"}))
    return 0


def cmd_perf(a: argparse.Namespace) -> int:
    emit(Bridge(a.port).get("/perf"))
    return 0


def cmd_shot(a: argparse.Namespace) -> int:
    query = {"name": a.name} if a.name else None
    emit(Bridge(a.port).get("/screenshot", query=query, timeout=60))
    return 0


def assemble_clip(name: str, fps: float, max_width: int) -> None:
    """Builds NAME.gif and NAME.webp from docs/shots/clips/NAME/frame_*.jpg."""
    from PIL import Image
    folder = REPO_ROOT / "docs" / "shots" / "clips" / name
    files = sorted(folder.glob("frame_*.jpg"))
    if not files:
        raise VhError(f"no frame_*.jpg in {folder}")
    frames = []
    for f in files:
        with Image.open(f) as im:
            im = im.convert("RGB")
            if im.width > max_width:
                im = im.resize((max_width, round(im.height * max_width / im.width)), Image.LANCZOS)
            frames.append(im)
    duration = max(1, round(1000.0 / fps))
    out = folder.parent
    gif, webp = out / f"{name}.gif", out / f"{name}.webp"
    pal = [f.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
    pal[0].save(gif, save_all=True, append_images=pal[1:], duration=duration, loop=0, optimize=False, disposal=1)
    frames[0].save(webp, save_all=True, append_images=frames[1:], duration=duration, loop=0, quality=80, method=4)
    print(f"{len(frames)} frames, {fps:.1f} fps, {frames[0].width}x{frames[0].height}")
    for path in (gif, webp):
        print(f"{path}  {path.stat().st_size / 1024:.0f} KiB")


def cmd_clip(a: argparse.Namespace) -> int:
    body = {"name": a.name, "seconds": a.seconds, "fps": a.fps, "scale": a.scale}
    info = Bridge(a.port).post("/clip", body, timeout=a.seconds * 3 + 60)
    emit(info)
    assemble_clip(info.get("name", a.name), float(info["fps"]), a.max_width)
    return 0


def cmd_assemble(a: argparse.Namespace) -> int:
    assemble_clip(a.name, a.fps, a.max_width)
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


def wait_bridge_idle(bridge: Bridge, max_wait: float = IDLE_WAIT) -> bool:
    """Waits until no scenario runs (GET /scenarios -> running is null). The bridge has no cancel route, but its watchdog ends any run at timeout + 20 s."""
    end = time.monotonic() + max_wait
    while time.monotonic() < end:
        if not bridge.get("/scenarios").get("running"):
            return True
        time.sleep(1.0)
    return False


def scenario_poll_limit(params: dict[str, str], cap: float | None) -> float:
    """Polling limit for one scenario: its own `timeout` param when given (plus headroom for the watchdog), else a wide default; bounded by cap (explicit or remaining global budget)."""
    try:
        limit = float(params["timeout"]) + 30.0 if "timeout" in params else SCENARIO_POLL_DEFAULT
    except ValueError:
        limit = SCENARIO_POLL_DEFAULT
    return limit if cap is None else min(limit, cap)


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
            idle = wait_bridge_idle(bridge)   # never start the next run while this one is alive (HTTP 409)
            result["error"] = f"vh gave up polling after {poll_timeout:.0f} s" + ("" if idle else "; the bridge is still busy")
            return result
        time.sleep(0.5)


def cmd_scenario(a: argparse.Namespace) -> int:
    params = parse_params(a.param)
    result = run_scenario(Bridge(a.port), a.name, params, scenario_poll_limit(params, a.timeout))
    emit(result)
    return 0 if result.get("status") in ("passed", "skipped") else 1   # skipped: a precondition the session cannot meet (see skipReason), not a failure


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
    kept = ""
    if REPORT_PATH.exists():
        old = REPORT_PATH.read_text(encoding="utf-8")
        at = old.find(CHECKLIST_MARKER)
        if at >= 0:
            kept = old[at:]   # the hand-written check-list for two real players survives the regeneration
    now = _dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    passed = sum(1 for r in results if r.get("status") == "passed")
    skipped = [r for r in results if r.get("status") == "skipped"]
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
        f"**{passed}/{len(results)} scenarios passed**" + (f" ({len(skipped)} skipped: {', '.join(_md(r.get('name')) for r in skipped)})." if skipped else "."),
        "",
        "| Scenario | Result | Duration (s) | Assertions (ok/total) | Shots |",
        "| --- | --- | --- | --- | --- |",
    ]
    for r in results:
        ok = r.get("status") == "passed"
        n_ok = r.get("assertionsPassed", 0)
        n_all = n_ok + r.get("assertionsFailed", 0)
        shots = " ".join(f"[{Path(s).stem}]({_link_shot(s)})" for s in r.get("shots") or []) or "-"
        result = "OK" if ok else ("skipped: " + _md(r.get("skipReason")) if r.get("status") == "skipped" else f"KO ({r.get('status')})")
        lines.append(f"| {_md(r.get('name'))} | {result} | {float(r.get('durationSeconds') or 0):.1f} | {n_ok}/{n_all} | {shots} |")

    failures = [r for r in results if r.get("status") not in ("passed", "skipped")]
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
    text = "\n".join(lines) + "\n"
    if kept:
        text += "\n" + kept
    REPORT_PATH.write_text(text, encoding="utf-8")


def cmd_scenario_all(a: argparse.Namespace) -> int:
    bridge = Bridge(a.port)
    names: list[str] = bridge.get("/scenarios").get("names", [])
    if a.only:
        wanted = {n.lower() for n in a.only}
        names = [n for n in names if n.lower() in wanted or n.split("_", 1)[0].lower() in wanted]
    results = []
    params = parse_params(a.param)
    global_end = time.monotonic() + a.timeout if a.timeout else None   # --timeout is the budget of the whole run
    for name in names:
        remaining = None if global_end is None else global_end - time.monotonic()
        if remaining is not None and remaining <= 0:
            results.append({"name": name, "status": "error", "error": "global --timeout exhausted before this scenario", "assertions": [], "shots": [], "log": []})
            continue
        try:
            results.append(run_scenario(bridge, name, params, scenario_poll_limit(params, remaining)))
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

    failed = [r.get("name") for r in results if r.get("status") not in ("passed", "skipped")]
    skipped_names = [r.get("name") for r in results if r.get("status") == "skipped"]
    emit({
        "report": str(REPORT_PATH),
        "total": len(results),
        "passed": len(results) - len(failed) - len(skipped_names),
        "skipped": skipped_names,
        "failed": failed,
        "results": [{k: r.get(k) for k in ("id", "name", "status", "skipReason", "durationSeconds", "assertionsPassed", "assertionsFailed", "shots", "error")}
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


# ---------------------------------------------------------------------------- s17 (phase 8-2)

# What the dedicated server logs when it refuses a client that has no mod: Jotunn.Utils.ModCompatibility.ZNet_RPC_PeerInfo (a prefix of ZNet.RPC_PeerInfo, so it runs BEFORE the password check and
# before the vanilla "Network version check" line) writes, as a warning, "<Jotunn> is not installed on the client. Server has mandatory mods, cancelling connection. Mods that need to be installed
# on the client:" and the names of the mods, then sends Error 3 (incompatible version). The "o with a diaeresis" of Jotunn comes out as a broken character in the log: the pattern starts after it.
# That prefix only refuses a peer whose address is not in Jotunn's ClientVersions, which is keyed by the Steam id and never emptied: after a modded client of the same Steam id has connected,
# the server takes the vanilla client for it (the first real run of this pass was accepted for that reason). So the client without the mod runs FIRST, before the modded client.
REFUSAL_RX = re.compile(r"is not installed on the client\. Server has mandatory mods, cancelling connection")
PEER_ACCEPTED_RX = re.compile(r"New peer connected", re.IGNORECASE)   # vanilla ZNet.RPC_PeerInfo, after the checks passed
PEER_TRACE_RX = re.compile(r"connection|peer|Jotunn|Disconnect|socket|mod", re.IGNORECASE)


def _read_lines(path: Path) -> list[str]:
    try:
        return path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return []


def _server_ready(since: float) -> bool:
    try:
        if not SERVER_LOG.exists() or SERVER_LOG.stat().st_mtime < since - 2:
            return False
    except OSError:
        return False
    return any("Game server connected" in l for l in _read_lines(SERVER_LOG))


def _wait_game_ready(port: int, timeout: float) -> dict[str, Any]:
    bridge = Bridge(port)
    deadline = time.monotonic() + timeout
    last: Any = None
    while time.monotonic() < deadline:
        try:
            last = bridge.get("/status", timeout=10)
            if isinstance(last, dict) and last.get("ready"):
                return {"ready": True, "status": last}
        except VhError as e:
            last = {"error": str(e)}
        time.sleep(2)
    return {"ready": False, "status": last}


def _quit_game(port: int, timeout: float) -> dict[str, Any]:
    before = process_running("valheim.exe")
    try:
        Bridge(port).post("/quit", {})
    except VhError as e:
        return {"exited": False, "error": str(e), "remainingPids": before}
    deadline = time.monotonic() + timeout
    remaining = before
    while remaining and time.monotonic() < deadline:
        time.sleep(1)
        remaining = [p for p in process_running("valheim.exe") if p in before]
    return {"exited": not remaining, "remainingPids": remaining}


def _kill(pid: int | None) -> None:
    if pid:
        subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True, timeout=30)


def cmd_s17(a: argparse.Namespace) -> int:
    """Server with a test .cfg, client without the mod (must be refused), client joined, S17, quit, stop, JSON summary."""
    import tempfile

    t0 = time.time()
    summary: dict[str, Any] = {"startedAt": _dt.datetime.now().isoformat(timespec="seconds"), "steps": [], "serverLog": str(SERVER_LOG)}
    pids: list[int] = []
    ok_all = True

    def step(name: str, ok: bool, detail: Any = None) -> bool:
        summary["steps"].append({"step": name, "ok": ok, "detail": detail, "t": round(time.time() - t0, 1)})
        return ok

    for image in ("valheim_server.exe", "valheim.exe"):
        running = process_running(image)
        if running:
            raise VhError(f"{image} is already running (PID {running}): the dev copies are shared with another session; nothing was launched", 2)

    tmp = Path(tempfile.mkdtemp(prefix="mw-s17-"))
    cfg = tmp / "dev.pointvirgule.mistworms.cfg"
    cfg.write_text("## Written by tools/vh.py s17: a synced admin-only key that the client must see\n[Colony]\n\nAlertRadius = %g\n" % a.alert, encoding="utf-8")
    summary["expectAlert"] = a.alert
    join = f"127.0.0.1:{a.server_port}"
    scenario: dict[str, Any] = {}
    server_started = False
    try:
        # 1. the server, with the test .cfg (saved aside and restored by -Stop)
        res = run_powershell(TOOLS_DIR / "run-server.ps1", ["-ConfigFile", str(cfg), "-Port", str(a.server_port), "-Password", a.password], timeout=120)
        server_started = res["exitCode"] == 0
        if not step("server_start", res["exitCode"] == 0 and bool(res["pid"]), res):
            raise VhError("the server did not start", 1, summary)
        started_at = time.time()
        deadline = time.monotonic() + a.server_timeout
        while time.monotonic() < deadline and not _server_ready(started_at):
            time.sleep(2)
        if not step("server_ready", _server_ready(started_at), "'Game server connected' in the server log"):
            raise VhError("the server did not report 'Game server connected' in time", 1, summary)

        # 1b. the client without the mod FIRST (see REFUSAL_RX: Jotunn remembers a modded client by its Steam id for the life of the server process)
        if a.skip_refusal:
            summary["refusal"] = {"skipped": True}
        else:
            offset = len(_read_lines(SERVER_LOG))
            bepinex_log = GAME_LOG
            before_mtime = bepinex_log.stat().st_mtime if bepinex_log is not None and bepinex_log.exists() else 0.0
            res = run_powershell(TOOLS_DIR / "run-game.ps1", ["-Join", join, "-NoMod", "-Character", a.character, "-JoinPassword", a.password], timeout=60)
            vanilla = res["pid"]
            if vanilla:
                pids.append(vanilla)
            step("vanilla_client_launch", res["exitCode"] == 0 and bool(vanilla), res)
            trace: list[str] = []
            accepted: list[str] = []
            new_lines: list[str] = []
            deadline = time.monotonic() + a.refusal_wait
            while time.monotonic() < deadline:
                time.sleep(2)
                new_lines = _read_lines(SERVER_LOG)[offset:]
                trace = [l for l in new_lines if REFUSAL_RX.search(l)]
                accepted = [l for l in new_lines if PEER_ACCEPTED_RX.search(l)]
                if trace or accepted:
                    break
            context = [l for l in new_lines if PEER_TRACE_RX.search(l)][:40]
            if trace and not accepted:
                verdict = "refused"
            elif accepted:
                verdict = "accepted (the server did NOT refuse the client without the mod)"
            else:
                verdict = "inconclusive (no refusal line and no acceptance line: the client may not have connected; see context)"
            mod_loaded = bool(bepinex_log is not None and bepinex_log.exists() and bepinex_log.stat().st_mtime > before_mtime)
            summary["refusal"] = {"verdict": verdict, "traceLines": trace, "acceptedLines": accepted, "context": context, "waitedSeconds": a.refusal_wait,
                                  "vanillaClientAlive": bool(vanilla and vanilla in process_running("valheim.exe")),
                                  "clientBepInExLogTouched": mod_loaded}   # True = BepInEx may have loaded in the "vanilla" client (DOORSTOP_DISABLE not honoured?)
            step("refusal_of_client_without_mod", verdict == "refused", verdict)
            _kill(vanilla)
            time.sleep(3)   # the process is gone before the modded client starts (shared window, one valheim.exe at a time)

        # 2. the client, joined to it
        res = run_powershell(TOOLS_DIR / "run-game.ps1", ["-Join", join, "-Character", a.character, "-BridgePort", str(a.port), "-JoinPassword", a.password], timeout=60)
        if res["pid"]:
            pids.append(res["pid"])
        if not step("client_launch", res["exitCode"] == 0 and bool(res["pid"]), res):
            raise VhError("the client did not start", 1, summary)
        ready = _wait_game_ready(a.port, a.client_timeout)
        if not step("client_ready", ready["ready"], {"status": ready["status"]}):
            raise VhError("the client was not ready in time (joined to the server)", 1, summary)

        # 3. S17
        scenario = run_scenario(Bridge(a.port), "S17_Multiplayer", {"expect_alert": "%g" % a.alert}, a.timeout)
        summary["scenario"] = {k: scenario.get(k) for k in ("id", "name", "status", "skipReason", "durationSeconds", "assertionsPassed", "assertionsFailed", "shots", "error")}
        summary["assertions"] = scenario.get("assertions")
        summary["scenarioLog"] = (scenario.get("log") or [])[-60:]
        step("scenario_S17", scenario.get("status") == "passed", summary["scenario"])

        # 4. out
        q = _quit_game(a.port, 40)
        step("client_quit", q.get("exited", False), q)

    except VhError as e:
        step("error", False, str(e))
        ok_all = False
    finally:
        for pid in pids:
            if pid in process_running("valheim.exe"):
                _kill(pid)
        if server_started:
            res = run_powershell(TOOLS_DIR / "run-server.ps1", ["-Stop"], timeout=180)
            res["stillRunning"] = process_running("valheim_server.exe")
            step("server_stop_and_config_restore", res["exitCode"] == 0 and not res["stillRunning"], res)
        shutil.rmtree(tmp, ignore_errors=True)
        summary["serverLogErrors"] = [l for l in _read_lines(SERVER_LOG) if re.search(r"\[(Error|Fatal)\s*:", l) or "Exception" in l][:40]
        summary["finishedAt"] = _dt.datetime.now().isoformat(timespec="seconds")
        summary["durationSeconds"] = round(time.time() - t0, 1)
        passed = scenario.get("status") == "passed" and (a.skip_refusal or summary.get("refusal", {}).get("verdict") == "refused")
        summary["ok"] = bool(passed) and ok_all and all(s["ok"] for s in summary["steps"])
        out = Path(a.out) if a.out else S17_SUMMARY
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(summary, indent=2, ensure_ascii=False, default=str), encoding="utf-8")
        summary["summaryFile"] = str(out)
    emit(summary)
    return 0 if summary["ok"] else 1


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
    sp.add_argument("--no-mod", action="store_true",
                    help="start the client WITHOUT BepInEx (DOORSTOP_DISABLE=1 for that process only); needs --join; no bridge")

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

    sp = add("mass-visual", cmd_mass_visual, "GET /mass-visual: roles visible/drawn, LOD, cap, engulf and cost of the swarm of a mass")
    sp.add_argument("id", nargs="?", help="ZDOID user:id of the mass (default: the closest to the player)")

    sp = add("mass-engulf", cmd_mass_engulf, "force the render-only engulf parameter (0..1, or off) of the closest mass (mw_mass_engulf)")
    sp.add_argument("value", help="0..1 or off")

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

    sp = add("clip", cmd_clip, "record a short clip (bridge /clip) and assemble docs/shots/clips/NAME.gif and .webp")
    sp.add_argument("name")
    sp.add_argument("--seconds", type=float, default=4.0, help="max 12")
    sp.add_argument("--fps", type=float, default=15.0, help="max 30")
    sp.add_argument("--scale", type=float, default=0.5, help="fraction of the game resolution")
    sp.add_argument("--max-width", type=int, default=720)

    sp = add("assemble", cmd_assemble, "assemble NAME.gif / .webp from an existing docs/shots/clips/NAME folder")
    sp.add_argument("name")
    sp.add_argument("--fps", type=float, default=15.0)
    sp.add_argument("--max-width", type=int, default=720)

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
    sp.add_argument("--timeout", type=float, default=None, help="polling limit in seconds (default: the scenario's timeout param + 30, else 600)")

    sp = add("scenario-all", cmd_scenario_all, "run every scenario and write docs/test-report.md")
    sp.add_argument("--only", nargs="*", help="restrict to these names or Sxx ids")
    sp.add_argument("--param", "-p", action="append", metavar="KEY=VALUE")
    sp.add_argument("--timeout", type=float, default=None, help="global budget in seconds for the whole run (default: none); each scenario is polled up to 600 s, bounded by what remains")

    sp = add("s17", cmd_s17, "phase 8-2 orchestration: dedicated server with a test .cfg, client joined, client without the mod refused first, client joined, S17, JSON summary")
    sp.add_argument("--alert", type=float, default=37.5, help="Colony.AlertRadius written in the .cfg of the server and expected by the client (default 37.5)")
    sp.add_argument("--server-port", type=int, default=2456)
    sp.add_argument("--password", default="mwdev123")
    sp.add_argument("--character", default="MWTester")
    sp.add_argument("--server-timeout", type=float, default=240.0, help="seconds to wait for 'Game server connected'")
    sp.add_argument("--client-timeout", type=float, default=300.0, help="seconds to wait for the joined client to be ready")
    sp.add_argument("--timeout", type=float, default=600.0, help="seconds to poll S17")
    sp.add_argument("--refusal-wait", type=float, default=60.0, help="at most this many seconds for the client without the mod to be refused (it stops waiting at the first refusal or acceptance line)")
    sp.add_argument("--skip-refusal", action="store_true", help="do not run the client without the mod")
    sp.add_argument("--out", help=f"summary file (default {S17_SUMMARY})")

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
