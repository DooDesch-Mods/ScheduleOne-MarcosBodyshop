"""Co-op check: a client tunes at Marco's like the host, and the roof load switch holds for every player.

Needs a pair from `pwsh -File Workspace/tools/session/duo.ps1 -Build _wt-MarcosBodyshop-trunkload`: the host in a
loaded save, the client in the menu, both with Debug builds of Marcos Bodyshop and Chassis. The client joins through
LocalLobby's --join. Then:

  client-car     the host spawns car A in Marco's hall and gives it to the client, who gets in and out
  client-buys    the client buys Spare Tyres on car A from its own bank balance; host and client see tier 1
  load-entry     the menu lists "Roof Load" as a free switch without text
  client-hides   the client hides the load on car A; host and client see it hidden, the slots stay
  host-car       the host spawns car B; the client buys Box Stack on it, hides the load and shows it again
  save-load      the host saves and loads; car A keeps tier 1 with the load hidden, car B tier 2 with the load shown
  late-join      the client joins again and sees the same
  errors         no exception from Marcos Bodyshop or Chassis on either peer

Each join stops the client and gives it the host's NPC packs first (`coop-packs.py --apply`); put the client's own
packs back afterwards with `coop-packs.py --restore` while the client is stopped. Prints PASS or FAIL per check,
exit 1 on any FAIL.

    python -I tests/coop-tuning.py --host mptest5 --client mptest6
"""
import argparse
import os
import re
import subprocess
import sys
import time


def _bridge():
    folder = os.path.dirname(os.path.abspath(__file__))
    for _ in range(6):
        candidate = os.path.join(folder, "Workspace", "tools", "bridge")
        if os.path.isfile(os.path.join(candidate, "s1.py")):
            return candidate
        folder = os.path.dirname(folder)
    raise SystemExit("[coop-tuning] Workspace/tools/bridge/s1.py not found above " + os.path.dirname(os.path.abspath(__file__)))


sys.path.insert(0, _bridge())
from s1 import Game  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
HALL = (5.0, 1.2, -12.5)
OUT_OF_HALL = (5.0, 1.2, -30.0)
STAND = (3.0, 1.2, -18.0)
CAR_A, CAR_B = "shitbox", "bruiser"
ERROR = re.compile(r"Exception|\[ERROR\]", re.I)
OURS = re.compile(r"MarcosBodyshop|Marco's Bodyshop|\[Trunk\]|\[Menu\]|\[Pay\]|\[Shop\]|\[Net\]|Chassis", re.I)


class Log:
    """The slot's newest MelonLoader log from a mark on; a restarted game starts a new file, read from its start."""

    def __init__(self, game):
        self.g = game
        self.path = game.log_path()
        self.offset = os.path.getsize(self.path) if self.path else 0

    def lines(self):
        now = self.g.log_path()
        if not now:
            return []
        with open(now, "rb") as handle:
            handle.seek(self.offset if now == self.path else 0)
            return handle.read().decode("utf-8", errors="replace").splitlines()

    def wait(self, pattern, timeout):
        rx, end = re.compile(pattern), time.time() + timeout
        while time.time() < end:
            for line in self.lines():
                if rx.search(line):
                    return line
            time.sleep(0.5)
        return None


class Run:
    def __init__(self, host, client):
        for stream in (sys.stdout, sys.stderr):
            stream.reconfigure(encoding="utf-8", errors="replace")
        self.host, self.client = Game(host), Game(client)
        self.results = []
        self.shots = []

    def check(self, name, ok, detail=""):
        verdict = "PASS" if ok else "FAIL"
        self.results.append((name, verdict, detail))
        print("%s  %s%s" % (verdict, name, ("  - " + str(detail)) if detail else ""), flush=True)
        return ok

    def summary(self):
        failed = [r for r in self.results if r[1] != "PASS"]
        print("\n===== %d PASS, %d FAIL" % (len(self.results) - len(failed), len(failed)))
        for name, verdict, detail in failed:
            print("%s  %s  - %s" % (verdict, name, detail))
        for who, path in self.shots:
            print("shot %s: %s" % (who, path))
        return 1 if failed else 0

    @staticmethod
    def who(g):
        return g.slot

    def cmd(self, g, command, until=None, wait=8000):
        """Runs a console command and returns the log lines it wrote, one string."""
        try:
            answer = g.run(command, until=until, max_wait_ms=wait)
        except TimeoutError as e:
            print("  [%s] > %s  (main thread busy: %s)" % (g.slot, command, e), flush=True)
            return ""
        if not answer.get("ok"):
            print("  [%s] > %s  (bridge error %s)" % (g.slot, command, answer.get("error")), flush=True)
            return ""
        lines = (answer.get("result") or {}).get("lines") or []
        text = "\n".join(str(l.get("text") or l.get("message") or l) if isinstance(l, dict) else str(l) for l in lines)
        print("  [%s] > %s" % (g.slot, command), flush=True)
        for line in [l for l in text.splitlines() if "[Console]" in l or "car " in l][-4:]:
            print("      " + line[-260:], flush=True)
        return text

    # ---- reports ------------------------------------------------------------------------------------------------

    def trunk(self, g, code):
        """The `bodyshop trunk` entry of the car with that Chassis code that has a Bodyshop tier, else the first one."""
        text = self.cmd(g, "bodyshop trunk", until=r"trunk props:", wait=6000)
        m = re.search(r"\[Console\] trunk: (.*)", text)
        if not m:
            return ""
        found = [p for p in m.group(1).split(" | ") if "(" + code + ")" in p]
        tiered = [p for p in found if not re.search(r": tier 0,", p)]
        return (tiered or found or [""])[0]

    def wait_trunk(self, g, code, pattern, timeout=20):
        end, last = time.time() + timeout, ""
        while time.time() < end:
            last = self.trunk(g, code)
            if re.search(pattern, last):
                return True, last
            time.sleep(2)
        return False, last

    # ---- joining ------------------------------------------------------------------------------------------------

    def join(self, timeout=300):
        host_file = os.path.join(self.host.root, "UserData", "LocalLobby", "lobby.txt")
        if not os.path.isfile(host_file):
            return self.check("host has a lobby id", False, "no " + host_file + " (start the pair with duo.ps1)")
        lobby = open(host_file, encoding="utf-8").read().strip()
        client_dir = os.path.join(self.client.root, "UserData", "LocalLobby")
        os.makedirs(client_dir, exist_ok=True)
        with open(os.path.join(client_dir, "lobby.txt"), "w", encoding="utf-8") as handle:
            handle.write(lobby)
        exe = os.path.join(self.client.root, "Schedule I.exe")
        stop = ("Get-CimInstance Win32_Process -Filter \"Name='Schedule I.exe'\" | Where-Object { $_.ExecutablePath -eq '%s' } "
                "| ForEach-Object { Stop-Process -Id $_.ProcessId -Force; 'stopped ' + $_.ProcessId }") % exe.replace("'", "''")
        stopped = subprocess.run(["pwsh", "-NoProfile", "-Command", stop], capture_output=True, text=True)
        print("      client restart: %s" % (stopped.stdout.strip() or "was not running"), flush=True)
        time.sleep(4)
        # Different Personnel packs hang the join on "Spawning player..."; the stopped client gets the host's packs.
        packs = subprocess.run([sys.executable, "-I", os.path.join(HERE, "coop-packs.py"), "--host", self.host.slot,
                                "--client", self.client.slot, "--apply"], capture_output=True, text=True, encoding="utf-8", errors="replace")
        print("      " + packs.stdout.strip().replace("\n", "\n      "), flush=True)
        if not self.check("host and client load the same NPC packs", packs.returncode == 0, packs.stderr.strip()[-200:]):
            return False
        subprocess.Popen([exe, "--join"], cwd=self.client.root)
        print("      client launched with --join for lobby %s" % lobby, flush=True)
        ready, last = self.client.wait_ready(timeout=timeout, say=lambda s: None)
        return self.check("client finishes the join", ready, "stuck after %d s; last log line: %s" % (timeout, last) if not ready else "")

    def creator_open(self):
        try:
            return self.client.call("ui_find", {"query": "Character Creator Interface"}, timeout_ms=8000).get("result", {}).get("count", 0) > 0
        except TimeoutError:
            return False

    def finish_creator(self, appears_within=360):
        """A player new to the host's save plays the intro, then gets the character creator; pressed through like a player."""
        start = time.time()
        while time.time() - start < appears_within and not self.creator_open():
            time.sleep(3)
        if not self.creator_open():
            print("      no character creator within %d s (the client is known to the save)" % appears_within, flush=True)
            return True
        for _ in range(12):
            done = self.client.call("ui_find", {"query": "Done"}, timeout_ms=8000).get("result", {}).get("count", 0)
            self.client.call("ui_click", {"query": "Done" if done else "Next"}, timeout_ms=8000)
            time.sleep(1.5)
            if not self.creator_open():
                print("      intro and creator took %d s" % (time.time() - start), flush=True)
                time.sleep(3)
                return True
        return self.check("client finishes the character creator", False, "creator still open after 12 clicks")

    # ---- steps --------------------------------------------------------------------------------------------------

    def tp_client(self, at):
        answer = self.client.world("teleport", {"x": at[0], "y": at[1], "z": at[2], "yaw": 0})
        print("      client teleport: %s" % (answer.get("result") or answer.get("error")), flush=True)
        time.sleep(2)

    def put(self, code):
        text = self.cmd(self.host, "chassis car put %s %.1f %.1f %.1f 0" % ((code,) + HALL), until=r"car put:", wait=8000)
        time.sleep(4)
        return "car put: " in text and "not spawned" not in text, text

    def open_menu(self):
        log = Log(self.client)
        self.cmd(self.client, "bodyshop open", until=r"\[Console\]", wait=5000)
        line = log.wait(r"\[Menu\] (open: garage|garage '.*' not opened|the host refused)", 20)
        return bool(line and "open: garage" in line), line or "no menu line in 20 s"

    def buy(self, entry, what):
        """Buys on the client; returns the host's [Shop] line for this purchase."""
        hlog = Log(self.host)
        text = self.cmd(self.client, "bodyshop buy trunk/" + entry, until=r"buy:", wait=5000)
        m = re.search(r"\[Console\] buy: (.*)", text)
        sent = bool(m and "sent to the host" in m.group(1))
        line = hlog.wait(r"\[Shop\] .*(bought|refused).*trunk/" + re.escape(entry), 20) if sent else None
        ok = sent and bool(line and " bought " in line)
        self.check(what, ok, line or (m.group(1) if m else "no buy answer"))
        time.sleep(2)
        return ok

    def car_a(self):
        ok, text = self.put(CAR_A)
        if not self.check("host spawns car A in the hall", ok, text.strip().splitlines()[-1][-200:] if text.strip() else "no answer"):
            return False
        sid = self.client_code()
        text = self.cmd(self.host, "chassis car owner " + sid, until=r"car owner", wait=6000)
        self.check("car A belongs to the client", "now belongs to" in text, re.findall(r"car owner.*", text)[-1:] or "no answer")
        self.tp_client(STAND)
        text = self.cmd(self.client, "chassis car enter " + CAR_A, until=r"car enter|car: no vehicle", wait=6000)
        self.check("client gets into its car", "(driver)" in text, re.findall(r"car enter.*|car: no vehicle.*", text)[-1:] or "no answer")
        time.sleep(2)
        self.cmd(self.client, "chassis exit", until=r"exit", wait=6000)
        time.sleep(3)
        return True

    def client_code(self):
        """The client's Steam id from the slot table; Chassis keys owners by it."""
        slots = os.path.join(_bridge(), "..", "slots", "slots.py")
        out = subprocess.run([sys.executable, "-I", slots, "table"], capture_output=True, text=True, encoding="utf-8", errors="replace")
        try:
            import json
            return str(json.loads(out.stdout)[self.client.slot]["steamid"])
        except (ValueError, KeyError) as e:
            print("      [coop-tuning] warn: no steamid for %s in the slot table (%s)" % (self.client.slot, e), flush=True)
            return ""

    def client_buys_a(self):
        ok, line = self.open_menu()
        if not self.check("client opens Marco's menu on car A", ok, line):
            return False
        self.cmd(self.client, "bodyshop cash 60000", until=r"bank balance", wait=4000)
        time.sleep(2)
        self.buy("bodyshop_trunk_1", "client buys Spare Tyres on its own car")
        ok, rep = self.wait_trunk(self.host, CAR_A, r"tier 1, 10 slot")
        self.check("host sees tier 1 on car A", ok, rep)
        ok, rep = self.wait_trunk(self.client, CAR_A, r"tier 1, 10 slot\(s\).*load shown, \d+/\d+ piece")
        self.check("client sees tier 1 with the load on car A", ok, rep)

        listing = self.cmd(self.client, "bodyshop list trunk", until=r"list:", wait=5000)
        m = re.search(r'\{[^{}]*"id"\s*:\s*"load"[^{}]*\}', listing)
        entry = m.group(0) if m else ""
        free = bool(re.search(r'"price"\s*:\s*0\b', entry)) and bool(re.search(r'"toggle"\s*:\s*true', entry))
        no_text = not re.search(r'"(text|note|description)"\s*:\s*"[^"]+"', entry)
        self.check("menu lists Roof Load as a free switch without text", bool(entry) and free and no_text, entry or "no 'load' entry in the trunk list")
        self.cmd(self.client, "bodyshop pick trunk/load", until=r"pick:", wait=4000)
        time.sleep(2)
        self.shots.append(("client menu, car A load shown", self.client.shot()))

        self.buy("load", "client flips the roof load switch on car A")
        ok, rep = self.wait_trunk(self.host, CAR_A, r"tier 1, 10 slot\(s\).*load hidden, 0/\d+ piece")
        self.check("host sees the load hidden on car A, 10 slots stay", ok, rep)
        ok, rep = self.wait_trunk(self.client, CAR_A, r"tier 1, 10 slot\(s\).*load hidden, 0/\d+ piece")
        self.check("client sees the load hidden on car A, 10 slots stay", ok, rep)
        time.sleep(2)
        self.shots.append(("client menu, car A load hidden", self.client.shot()))
        self.cmd(self.client, "bodyshop close", wait=3000)
        time.sleep(2)
        return True

    def car_b(self):
        text = self.cmd(self.host, "bodyshop trunk move %.1f %.1f %.1f" % OUT_OF_HALL, until=r"trunk move", wait=6000)
        print("      " + (re.findall(r"trunk move.*", text)[-1:] or ["no answer"])[0], flush=True)
        time.sleep(3)
        ok, text = self.put(CAR_B)
        if not self.check("host spawns car B (the host's) in the hall", ok, text.strip().splitlines()[-1][-200:] if text.strip() else "no answer"):
            return False
        self.tp_client(STAND)
        ok, line = self.open_menu()
        if not self.check("client opens Marco's menu on the host's car B", ok, line):
            return False
        self.buy("bodyshop_trunk_2", "client buys Box Stack on the host's car")
        ok, rep = self.wait_trunk(self.host, CAR_B, r"tier 2, 15 slot\(s\).*load shown")
        self.check("host sees tier 2 with the load on car B", ok, rep)
        self.buy("load", "client hides the load on the host's car")
        ok, rep = self.wait_trunk(self.host, CAR_B, r"tier 2, 15 slot\(s\).*load hidden, 0/")
        self.check("host sees the load hidden on car B", ok, rep)
        self.buy("load", "client shows the load again on the host's car")
        ok, rep = self.wait_trunk(self.host, CAR_B, r"tier 2, 15 slot\(s\).*load shown, (\d+)/\1 piece")
        self.check("host sees the load shown again on car B", ok, rep)
        ok, rep = self.wait_trunk(self.client, CAR_B, r"tier 2, 15 slot\(s\).*load shown, (\d+)/\1 piece")
        self.check("client sees the load shown on car B", ok, rep)
        time.sleep(2)
        self.shots.append(("client menu, car B load shown", self.client.shot()))
        self.cmd(self.client, "bodyshop close", wait=3000)
        return True

    def final_state(self, g, who):
        ok, rep = self.wait_trunk(g, CAR_A, r"tier 1, 10 slot\(s\).*load hidden", timeout=60)
        self.check("%s: car A has tier 1 with the load hidden" % who, ok, rep)
        ok, rep = self.wait_trunk(g, CAR_B, r"tier 2, 15 slot\(s\).*load shown, (\d+)/\1 piece", timeout=30)
        self.check("%s: car B has tier 2 with the load shown" % who, ok, rep)

    def reload_host(self):
        out = subprocess.run([sys.executable, "-I", os.path.join(HERE, "reload-save.py"), "--slot", self.host.slot, "--save", "5"],
                             capture_output=True, text=True, encoding="utf-8", errors="replace")
        tail = (out.stdout + out.stderr).strip().splitlines()[-3:]
        return self.check("host saves and loads save 5", out.returncode == 0, " | ".join(tail))

    def errors(self, g, log, who):
        bad = [l for l in log.lines() if ERROR.search(l) and OURS.search(l)]
        self.check("%s: no Bodyshop or Chassis error" % who, not bad, bad[0][-300:] if bad else "")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="mptest5")
    parser.add_argument("--client", default="mptest6")
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--joined", action="store_true", help="the client is already in the host's world")
    a = parser.parse_args()
    run = Run(a.host, a.client)
    hlog = Log(run.host)
    if not a.joined:
        if not run.join(a.timeout):
            sys.exit(run.summary())
    clog = Log(run.client)
    run.finish_creator()

    if run.car_a() and run.client_buys_a():
        run.car_b()
    run.final_state(run.host, "host before saving")
    run.errors(run.host, hlog, "host")
    run.errors(run.client, clog, "client")

    if run.reload_host():
        hlog = Log(run.host)
        run.final_state(run.host, "host after loading")
        if run.join(a.timeout):
            clog = Log(run.client)
            run.finish_creator(appears_within=20)
            time.sleep(5)
            run.final_state(run.client, "client after the late join")
            run.errors(run.client, clog, "client after the late join")
        run.errors(run.host, hlog, "host after loading")
    sys.exit(run.summary())


if __name__ == "__main__":
    main()
