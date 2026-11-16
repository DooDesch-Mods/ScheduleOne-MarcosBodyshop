"""Makes the Personnel NPC packs of a co-op pair identical, and puts the client's own packs back afterwards.

Different packs on host and client hang the join on "Spawning player...". `--apply` moves the client's
UserData/Personnel/Packs aside (Packs.bodyshop-backup) and copies the host's packs in; `--restore` deletes the copy and
moves the client's own packs back. Without a flag it only compares. Run it while both games are stopped.

    python tests/coop-packs.py --host mptest5 --client mptest6 --apply
    python tests/coop-packs.py --host mptest5 --client mptest6 --restore
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SLOTS_PY = os.path.join(HERE, "..", "..", "Workspace", "tools", "slots", "slots.py")
BACKUP = "Packs.bodyshop-backup"


def roots():
    out = subprocess.run([sys.executable, "-I", SLOTS_PY, "table"], capture_output=True, text=True, check=True).stdout
    return {name: entry["root"] for name, entry in json.loads(out).items()}


def packs(root):
    folder = os.path.join(root, "UserData", "Personnel", "Packs")
    found = {}
    if not os.path.isdir(folder):
        return found
    for name in sorted(os.listdir(folder)):
        pack = os.path.join(folder, name)
        if not os.path.isdir(pack):
            continue
        digest = hashlib.md5()
        for base, _, files in sorted(os.walk(pack)):
            for file in sorted(files):
                path = os.path.join(base, file)
                digest.update(os.path.relpath(path, pack).encode("utf-8"))
                with open(path, "rb") as handle:
                    digest.update(handle.read())
        found[name] = digest.hexdigest()
    return found


def compare(host_root, client_root):
    host, client = packs(host_root), packs(client_root)
    differ = sorted(n for n in set(host) | set(client) if host.get(n) != client.get(n))
    if differ:
        print("FAIL  packs differ: %s (host %d, client %d)" % (", ".join(differ), len(host), len(client)))
        return False
    print("PASS  host and client load the same NPC packs (%d)" % len(host))
    return True


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="mptest5")
    parser.add_argument("--client", default="mptest6")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--restore", action="store_true")
    a = parser.parse_args()
    table = roots()
    host_root, client_root = table[a.host], table[a.client]
    personnel = os.path.join(client_root, "UserData", "Personnel")
    live, backup = os.path.join(personnel, "Packs"), os.path.join(personnel, BACKUP)

    if a.apply:
        if os.path.isdir(backup):
            print("[packs] %s already holds the client's own packs - not overwritten." % backup)
        elif os.path.isdir(live):
            os.rename(live, backup)
            print("[packs] client packs moved to %s" % backup)
        source = os.path.join(host_root, "UserData", "Personnel", "Packs")
        if os.path.isdir(live):
            shutil.rmtree(live)
        if os.path.isdir(source):
            shutil.copytree(source, live)
            print("[packs] host packs copied to %s" % live)
        else:
            os.makedirs(live, exist_ok=True)
            print("[packs] the host has no packs; the client gets an empty Packs folder")
    elif a.restore:
        if not os.path.isdir(backup):
            print("[packs] no %s - nothing to restore." % backup)
            sys.exit(1)
        if os.path.isdir(live):
            shutil.rmtree(live)
        os.rename(backup, live)
        print("[packs] client packs restored from %s" % backup)
        sys.exit(0)
    sys.exit(0 if compare(host_root, client_root) else 1)


if __name__ == "__main__":
    main()
