"""
═══════════════════════════════════════════════════════════
 TUI.py — Python source of truth for CodeGamified.TUI
 C# static classes are 1:1 ports of these functions.
 Edit here first → port to C# → both games get updates.
═══════════════════════════════════════════════════════════
"""
import math, re, time
from typing import List, Tuple, Optional

# ═══════════════════════════════════════════════════════════
# § Colors
# ═══════════════════════════════════════════════════════════

class C:
    """Named color palette (ANSI 256-color approximations)."""
    WHITE   = (255, 255, 255)
    CYAN    = (0,   255, 255)
    GREEN   = (0,   255, 0)
    YELLOW  = (255, 255, 0)
    RED     = (255, 0,   0)
    MAGENTA = (255, 0,   255)
    DIM     = (180, 180, 180)

def fg(r: int, g: int, b: int, text: str) -> str:
    """Wrap text in ANSI 24-bit foreground color."""
    return f"\033[38;2;{r};{g};{b}m{text}\033[0m"

def bold(text: str) -> str:
    return f"\033[1m{text}\033[0m"

def dimmed(text: str) -> str:
    return fg(*C.DIM, text)

# ═══════════════════════════════════════════════════════════
# § Glyphs
# ═══════════════════════════════════════════════════════════

BOX_H, BOX_V = "─", "│"
BOX_TL, BOX_TR, BOX_BL, BOX_BR = "┌", "┐", "└", "┘"
BOX_TEE_R, BOX_TEE_L = "├", "┤"
BOX_DBL_H, BOX_DBL_V = "═", "║"
BOX_DBL_TL, BOX_DBL_TR = "╔", "╗"
BOX_DBL_BL, BOX_DBL_BR = "╚", "╝"

BLOCK_FULL, BLOCK_LIGHT = "█", "░"
BLOCK_MEDIUM, BLOCK_DARK = "▒", "▓"
BLOCK_EIGHTHS = [" ", "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█"]

DIAMOND_EMPTY, DIAMOND_FILLED, DIAMOND_DOT = "◇", "◆", "◈"
CIRCLE_EMPTY, CIRCLE_FILLED, CIRCLE_DOT = "○", "●", "◉"
CHECK, CROSS, WARN, INFO = "✓", "✗", "⚠", "ⓘ"
ARROW_R, ARROW_L, ARROW_U, ARROW_D = "→", "←", "↑", "↓"

BRAILLE_SPIN = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"]
BLOCK_SPIN = ["▖", "▘", "▝", "▗"]
PULSE_BOX = ["·", "▪", "■", "▪"]
PULSE_DIAMOND = ["·", "◇", "◈", "◆", "◈", "◇"]
PULSE_CIRCLE = ["·", "○", "◉", "●", "◉", "○"]
RADAR_SWEEP = ["◜", "◝", "◞", "◟"]

SCRAMBLE_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*<>░▒▓│┤╡╢╖╕╣║╗╝╜╛┐└┴┬├─┼╞╟╚╔╩╦╠═╬"

# ═══════════════════════════════════════════════════════════
# § Config
# ═══════════════════════════════════════════════════════════

MSFT_GRADIENT = [
    (0,   164, 239),  # Blue
    (127, 186, 0),    # Green
    (255, 185, 0),    # Yellow
    (242, 80,  34),   # Red
]

gradient = list(MSFT_GRADIENT)

def load_settings(json_str: str) -> None:
    """Load gradient from JSON accents array. Falls back to defaults."""
    import json
    global gradient
    try:
        data = json.loads(json_str)
        accents = data.get("accents", [])
        if len(accents) >= 2:
            gradient = [tuple(int(x) for x in a.split(",")) for a in accents]
            return
    except Exception:
        pass
    gradient = list(MSFT_GRADIENT)

def reset_settings() -> None:
    global gradient
    gradient = list(MSFT_GRADIENT)

# ═══════════════════════════════════════════════════════════
# § Gradient
# ═══════════════════════════════════════════════════════════

def lerp_gradient(stops: list, t: float) -> Tuple[int, int, int]:
    """Interpolate RGB gradient at position t ∈ [0,1]."""
    if not stops:
        return (255, 255, 255)
    if len(stops) == 1:
        return stops[0]
    t = max(0.0, min(1.0, t))
    seg = t * (len(stops) - 1)
    i = min(int(seg), len(stops) - 2)
    f = seg - i
    a, b = stops[i], stops[i + 1]
    return (
        int(a[0] + (b[0] - a[0]) * f),
        int(a[1] + (b[1] - a[1]) * f),
        int(a[2] + (b[2] - a[2]) * f),
    )

def gradient_rgb(t: float) -> Tuple[int, int, int]:
    """Sample the brand gradient at t ∈ [0,1]."""
    return lerp_gradient(gradient, t)

def make_loop(stops: list) -> list:
    """Append first stop at end for perimeter sweeps."""
    return stops + [stops[0]] if stops else stops

# ═══════════════════════════════════════════════════════════
# § Text
# ═══════════════════════════════════════════════════════════

_ANSI_RE = re.compile(r"\033\[[0-9;]*m")

def strip_ansi(text: str) -> str:
    """Strip ANSI escape sequences."""
    return _ANSI_RE.sub("", text) if text else text

def visible_length(text: str) -> int:
    return len(strip_ansi(text)) if text else 0

def truncate_ansi(text: str, max_vis: int, ellipsis: str = "…") -> str:
    """Truncate ANSI-colored text to max visible characters."""
    if not text or max_vis <= 0:
        return ""
    plain = strip_ansi(text)
    if len(plain) <= max_vis:
        return text
    # Simple truncation (strips ANSI for now)
    return plain[:max_vis - len(ellipsis)] + dimmed(ellipsis)

def strip_html(text: str) -> str:
    return re.sub(r"<[^>]+>", "", text) if text else text

def sanitize_emoji(text: str) -> str:
    """Remove variation selectors and ZWJ."""
    if not text:
        return text
    return "".join(c for c in text if c not in "\uFE0E\uFE0F\u200D")

# ═══════════════════════════════════════════════════════════
# § Easing
# ═══════════════════════════════════════════════════════════

def smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)

def smootherstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)

# ═══════════════════════════════════════════════════════════
# § Effects
# ═══════════════════════════════════════════════════════════

def scramble_text(target: str, age: float,
                  char_rate: float = 0.02, scramble_rate: float = 0.05) -> str:
    """Scramble-reveal: characters resolve left-to-right over time."""
    length = len(target)
    resolved = int(age / char_rate)
    if resolved >= length:
        return target
    step_t = int(age / scramble_rate)
    chars = SCRAMBLE_CHARS
    n_chars = len(chars)
    result = list(target[:resolved])
    for i in range(resolved, length):
        c = target[i]
        if c in " \n\t":
            result.append(c)
        else:
            idx = (i + step_t * 13 + ord(c) * 7) % n_chars
            result.append(chars[idx])
    return "".join(result)

def gradient_colorize(text: str) -> str:
    """Per-character horizontal gradient coloring."""
    if not text:
        return text
    n = len(text)
    parts = []
    for i, ch in enumerate(text):
        if ch in " \t\n":
            parts.append(ch)
        else:
            t = i / max(n - 1, 1)
            r, g, b = gradient_rgb(t)
            parts.append(fg(r, g, b, ch))
    return "".join(parts)

def is_resolved(target: str, age: float, char_rate: float = 0.02) -> bool:
    return age / char_rate >= len(target)

def blinking_text(text: str, age: float, interval: float = 0.5) -> str:
    visible = int(age / interval) % 2 == 0
    return text if visible else " " * len(text)

def typewriter_text(text: str, age: float, char_delay: float = 0.05) -> str:
    n = int(age / char_delay)
    return text[:min(n, len(text))]

def cursor(age: float, blink_speed: float = 0.5) -> str:
    return BLOCK_FULL if int(age / blink_speed) % 2 == 0 else " "

# ═══════════════════════════════════════════════════════════
# § Layout
# ═══════════════════════════════════════════════════════════

def center_text(text: str, card_width: int) -> str:
    usable = max(0, card_width - 4)
    vis = visible_length(text)
    pad = max(0, (usable - vis) // 2)
    return " " * pad + text if pad > 0 else text

def right_align(text: str, card_width: int) -> str:
    usable = max(0, card_width - 4)
    vis = visible_length(text)
    pad = max(0, usable - vis)
    return " " * pad + text if pad > 0 else text

# ═══════════════════════════════════════════════════════════
# § Widgets
# ═══════════════════════════════════════════════════════════

def progress_bar(progress: float, length: int = 20, show_pct: bool = True) -> str:
    """Smooth sub-character progress bar using Unicode eighths."""
    progress = max(0.0, min(1.0, progress))
    filled_exact = progress * length
    filled_full = int(filled_exact)
    frac = filled_exact - filled_full
    eighth = int(frac * 8)

    parts = []
    for i in range(length):
        t = i / max(length - 1, 1)
        r, g, b = gradient_rgb(t)
        if i < filled_full:
            parts.append(fg(r, g, b, BLOCK_FULL))
        elif i == filled_full and eighth > 0:
            parts.append(fg(r, g, b, BLOCK_EIGHTHS[eighth]))
        else:
            parts.append(dimmed(BLOCK_LIGHT))

    bar = "".join(parts)
    if show_pct:
        return f"{dimmed('[')}{bar}{dimmed(']')} {int(progress * 100):3d}%"
    return f"{dimmed('[')}{bar}{dimmed(']')}"

def spinner_frame(age: float, frames: list = None, speed: float = 0.08) -> str:
    frames = frames or BRAILLE_SPIN
    idx = int(age / speed) % len(frames)
    return frames[idx]

def divider(length: int, left: str = None, mid: str = None, right: str = None) -> str:
    left = left or BOX_TEE_R
    mid = mid or BOX_H
    right = right or BOX_TEE_L
    if length <= 2:
        return mid * length
    return dimmed(f"{left}{mid * (length - 2)}{right}")

def header_line(text: str, width: int) -> str:
    pad = max(0, (width - len(text) - 4) // 2)
    lp = BOX_H * pad
    rp = BOX_H * (width - len(text) - 4 - pad)
    r, g, b = gradient[0]
    ac = fg(r, g, b, DIAMOND_FILLED + lp)
    return f"{ac} {bold(text)} {fg(r, g, b, rp + DIAMOND_FILLED)}"

def signal_strength(bars: int) -> str:
    bars = max(0, min(4, bars))
    return ["▁   ", "▁▃  ", "▁▃▅ ", "▁▃▅▇", "▁▃▅█"][bars]

def battery_indicator(level: float) -> str:
    level = max(0.0, min(1.0, level))
    if level > 0.75: return "█▌"
    if level > 0.50: return "▓▌"
    if level > 0.25: return "▒▌"
    if level > 0.10: return "░▌"
    return " ▌"

def temperature_gauge(normalized: float, length: int = 5) -> str:
    normalized = max(0.0, min(1.0, normalized))
    filled = int(normalized * length)
    ch = "▓" if normalized > 0.8 else ("▒" if normalized > 0.5 else "░")
    return f"[{ch * filled}{' ' * (length - filled)}]"

# ═══════════════════════════════════════════════════════════
# § Animation
# ═══════════════════════════════════════════════════════════

def decode_rows(final_lines: List[str], phase_age: float,
                decode_seconds: float, row_delay: float = 0.08) -> Tuple[List[str], bool]:
    """Rows enter top-to-bottom with scramble-decode."""
    n = len(final_lines)
    plains = [strip_ansi(l) for l in final_lines]
    longest = max((len(p) for p in plains if p.strip()), default=1)
    char_rate = max(0.005, decode_seconds / max(longest, 1))
    visible_n = min(n, int(phase_age / row_delay) + 1) if row_delay > 0 else n
    all_resolved = True
    content = []
    for ri in range(visible_n):
        plain = plains[ri]
        row_age = max(0.0, phase_age - ri * row_delay)
        if not plain.strip():
            content.append(final_lines[ri])
        elif row_age / char_rate >= len(plain):
            content.append(final_lines[ri])
        else:
            all_resolved = False
            revealed = scramble_text(plain, row_age, char_rate)
            content.append(gradient_colorize(revealed))
    return content, all_resolved

def fadeout_rows(final_lines: List[str], phase_age: float,
                 fadeout_seconds: float, row_delay: float = -1.0) -> List[str]:
    """Rows retract bottom-to-top, re-scrambling."""
    n = len(final_lines)
    plains = [strip_ansi(l) for l in final_lines]
    longest = max((len(p) for p in plains if p.strip()), default=1)
    if row_delay < 0:
        row_delay = fadeout_seconds * 0.35 / max(n - 1, 1)
    scramble_time = fadeout_seconds * 0.65
    char_rate = max(0.005, scramble_time / max(longest, 1))
    content = []
    for ri in range(n):
        plain = plains[ri]
        row_start = (n - 1 - ri) * row_delay
        row_age = max(0.0, phase_age - row_start)
        if not plain.strip():
            if row_age < scramble_time * 0.5:
                content.append(final_lines[ri])
            continue
        full_time = len(plain) * char_rate
        virtual_age = full_time - row_age
        if virtual_age <= 0:
            continue
        if virtual_age >= full_time:
            content.append(final_lines[ri])
        else:
            revealed = scramble_text(plain, virtual_age, char_rate)
            content.append(gradient_colorize(revealed))
    return content

def progress_frame(label: str, age: float,
                   duration: float = 1.2, bar_width: int = 24) -> Tuple[str, bool]:
    """Progress bar animation frame."""
    prog = max(0.0, min(1.0, age / duration))
    spin = spinner_frame(age)
    bar = progress_bar(prog, bar_width)
    done = prog >= 1.0
    if done:
        icon = fg(*C.GREEN, CHECK)
        suffix = dimmed("done")
    else:
        icon = fg(*C.MAGENTA, spin)
        suffix = bar
    line = f"  {icon} {dimmed(label)}  {suffix}"
    return line, done

def step_frame(label: str, age: float, ok: bool = True,
               total_frames: int = 12, frame_time: float = 0.06) -> Tuple[str, bool]:
    """Step indicator: radar sweep then check/cross."""
    total_duration = total_frames * frame_time
    done = age >= total_duration
    if done:
        mark = fg(*C.GREEN, CHECK) if ok else fg(*C.RED, CROSS)
        return f"  {mark} {label}", True
    frame = int(age / frame_time)
    sweep = RADAR_SWEEP[frame % len(RADAR_SWEEP)]
    return f"  {fg(*C.YELLOW, sweep)} {dimmed(label)}", False

# ═══════════════════════════════════════════════════════════
# § Format
# ═══════════════════════════════════════════════════════════

def fmt_duration(seconds: float) -> str:
    """Human-readable duration."""
    s = int(seconds)
    if s < 60:
        return f"{s}s"
    m, s = divmod(s, 60)
    if m < 60:
        return f"{m}m {s:02d}s"
    h, m = divmod(m, 60)
    if h < 24:
        return f"{h}h {m:02d}m {s:02d}s"
    d, h = divmod(h, 24)
    return f"{d}d {h}h {m:02d}m"

def time_color(seconds: float) -> Tuple[int, int, int]:
    """Color by time magnitude using brand gradient."""
    s = int(seconds)
    if s < 60:
        idx = 0
    elif s < 3600:
        idx = 1
    elif s < 86400:
        idx = 2
    else:
        idx = 3
    return gradient[min(idx, len(gradient) - 1)]

def colored_duration(seconds: float) -> str:
    r, g, b = time_color(seconds)
    return fg(r, g, b, fmt_duration(seconds))
