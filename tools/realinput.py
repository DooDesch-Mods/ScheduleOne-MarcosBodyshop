"""Real OS input into one game window: the measurement a synthetic UI click cannot make.

Brings the window of the given process to the front, plays a list of steps through SendInput, then hands the
foreground back to the window that had it. Every step is printed with its result, so a step that did nothing is
visible in the output.

    python -I realinput.py <pid> move 0.15 0.30   wait 300   click   key down   key enter   key esc

Steps:
    move fx fy    pointer to a fraction of the client area (0..1, from the top left)
    click         left button down and up where the pointer is
    rclick        right button down and up
    key NAME      up, down, left, right, enter, esc
    wait MS       sleep that many milliseconds
"""
import ctypes
import sys
import time
from ctypes import wintypes

user32 = ctypes.WinDLL("user32", use_last_error=True)

INPUT_MOUSE, INPUT_KEYBOARD = 0, 1
KEYEVENTF_EXTENDEDKEY, KEYEVENTF_KEYUP, KEYEVENTF_SCANCODE = 0x1, 0x2, 0x8
MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP = 0x2, 0x4
MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP = 0x8, 0x10

# Scan codes; the arrows are extended keys.
KEYS = {"up": (0x48, True), "down": (0x50, True), "left": (0x4B, True), "right": (0x4D, True),
        "enter": (0x1C, False), "esc": (0x01, False)}


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("mouseData", wintypes.DWORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD), ("dwExtraInfo", ctypes.c_size_t)]


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ctypes.c_size_t)]


class _U(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 32)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("u", _U)]


def send(*inputs):
    arr = (INPUT * len(inputs))(*inputs)
    n = user32.SendInput(len(inputs), arr, ctypes.sizeof(INPUT))
    if n != len(inputs):
        print(f"  SendInput placed {n} of {len(inputs)} events (error {ctypes.get_last_error()})")
    return n == len(inputs)


def mouse(flags):
    i = INPUT(type=INPUT_MOUSE)
    i.u.mi = MOUSEINPUT(0, 0, 0, flags, 0, 0)
    return i


def key(scan, extended, up):
    flags = KEYEVENTF_SCANCODE | (KEYEVENTF_EXTENDEDKEY if extended else 0) | (KEYEVENTF_KEYUP if up else 0)
    i = INPUT(type=INPUT_KEYBOARD)
    i.u.ki = KEYBDINPUT(0, scan, flags, 0, 0)
    return i


def window_of(pid):
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(hwnd, _):
        p = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True

    user32.EnumWindows(cb, 0)
    return found[0] if found else None


def bring_front(hwnd):
    # Windows refuses SetForegroundWindow to a process that did not receive the last input; a tapped Alt counts.
    send(key(0x38, False, False), key(0x38, False, True))
    user32.ShowWindow(hwnd, 9)
    ok = user32.SetForegroundWindow(hwnd)
    time.sleep(0.4)
    return ok and user32.GetForegroundWindow() == hwnd


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    pid = int(argv[0])
    hwnd = window_of(pid)
    if not hwnd:
        print(f"no visible window for pid {pid}")
        return 1

    rect = wintypes.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(rect))
    origin = wintypes.POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(origin))
    w, h = rect.right, rect.bottom
    print(f"window {hwnd:#x}, client {w}x{h} at screen ({origin.x}, {origin.y})")

    previous = user32.GetForegroundWindow()
    if not bring_front(hwnd):
        print("could not bring the game window to the front - stopping, nothing was sent")
        return 1
    print("game window is in front")

    steps = argv[1:]
    i = 0
    try:
        while i < len(steps):
            s = steps[i]
            if s == "move":
                fx, fy = float(steps[i + 1]), float(steps[i + 2])
                x, y = origin.x + int(fx * w), origin.y + int(fy * h)
                ok = user32.SetCursorPos(x, y)
                print(f"move to ({fx:.3f}, {fy:.3f}) = screen ({x}, {y}): {'ok' if ok else 'FAILED'}")
                i += 3
            elif s == "click":
                print(f"click: {'ok' if send(mouse(MOUSEEVENTF_LEFTDOWN), mouse(MOUSEEVENTF_LEFTUP)) else 'FAILED'}")
                i += 1
            elif s == "rclick":
                print(f"right click: {'ok' if send(mouse(MOUSEEVENTF_RIGHTDOWN), mouse(MOUSEEVENTF_RIGHTUP)) else 'FAILED'}")
                i += 1
            elif s == "key":
                name = steps[i + 1]
                scan, ext = KEYS[name]
                down_ok = send(key(scan, ext, False))
                time.sleep(0.06)
                up_ok = send(key(scan, ext, True))
                print(f"key {name}: {'ok' if down_ok and up_ok else 'FAILED'}")
                i += 2
            elif s == "wait":
                time.sleep(int(steps[i + 1]) / 1000.0)
                i += 2
            else:
                print(f"unknown step '{s}' - stopping")
                return 2
            time.sleep(0.15)
    finally:
        if previous and previous != hwnd:
            user32.SetForegroundWindow(previous)
            print("foreground handed back")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
