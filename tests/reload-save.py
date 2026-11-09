"""Saves the running game, exits to the menu, loads the save slot again and returns once it is loaded (exit 0),
else exit 1. A load is refused while a world is loaded, so the menu comes first.

    python tests/reload-save.py --slot mptest --save 5
"""
import argparse
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Workspace", "tools", "bridge"))
import s1  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument("--slot", default="mptest")
parser.add_argument("--save", type=int, default=5)
parser.add_argument("--timeout", type=int, default=240)
a = parser.parse_args()

game = s1.Game(a.slot)
print("[reload] save_now: %s" % (game.call("save_now", {}, timeout_ms=30000),))
time.sleep(3)
print("[reload] return_to_menu: %s" % (game.call("return_to_menu", {}, timeout_ms=30000),))

deadline = time.time() + a.timeout
in_menu = False
while time.time() < deadline:
    time.sleep(3)
    try:
        cur = game.call("get_current_save", {}, timeout_ms=10000)
    except Exception as e:  # the bridge drops requests while the scene changes
        print("[reload] get_current_save failed while changing scene: %s" % e)
        continue
    if "'inGame': False" in str(cur) or '"inGame": false' in str(cur):
        in_menu = True
        break
if not in_menu:
    print("[reload] FAIL: still in the world after %d s" % a.timeout)
    sys.exit(1)
print("[reload] in the menu")
time.sleep(3)
print("[reload] load_save %d: %s" % (a.save, game.call("load_save", {"slot": a.save}, timeout_ms=30000)))
time.sleep(10)
ready, last = game.wait_ready(timeout=a.timeout)
print("[reload] %s: %s" % ("ready" if ready else "FAIL not ready", last))
sys.exit(0 if ready else 1)
