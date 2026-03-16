# CodeGamified.TUI

Shared terminal UI framework for educational programming games.  
Row-based monospace layout → TMP rich-text → Unity Canvas.

## Architecture

```
TUI Primitives (pure string helpers, no MonoBehaviour)
├── TUIColors      Color32 palette + Fg(), Bold(), Dimmed()
├── TUIGlyphs      Box-drawing, blocks, spinners, status icons
├── TUIConfig      Brand gradient stops, Load(json), Reset()
├── TUIGradient    Lerp(stops,t), Sample(t), MakeLoop()
├── TUIText        StripTags(), VisibleLength(), Truncate()
├── TUIEasing      Smoothstep(t), Smootherstep(t)
├── TUIEffects     ScrambleText(), GradientColorize()
├── TUILayout      CenterText(), RightAlign()
├── TUIWidgets     ProgressBar(), SpinnerFrame(), Divider(), Box()
├── TUIAnimation   DecodeRows(), FadeoutRows(), ProgressFrame()
└── TUIFormat      Duration(), TimeColor(), ColoredDuration()

Runtime Components (MonoBehaviour, Unity UI)
├── TerminalWindow   Abstract base: row grid, resize, scrollback, Render()
├── TerminalRow      Single row: text + optional dual/triple/three-panel columns
│                    + optional slider/button overlays (opt-in)
└── TUIEdgeDragger   Draggable edge handle for panel resize
```

## Files

| File | Purpose |
|------|---------|
| `TUIColors.cs` | Color32 palette, `Fg(color, text)`, `Bold()`, `Dimmed()` |
| `TUIGlyphs.cs` | Box-drawing, block elements, spinner sequences, scramble charset |
| `TUIConfig.cs` | Brand `Gradient` (Color32[]), `Load(json)`, `Reset()` |
| `TUIGradient.cs` | `Lerp(stops,t)`, `Sample(t)`, `MakeLoop()` |
| `TUIText.cs` | `StripTags()`, `VisibleLength()`, `Truncate()`, `SanitizeEmoji()` |
| `TUIEasing.cs` | `Smoothstep(t)`, `Smootherstep(t)` |
| `TUIEffects.cs` | `ScrambleText(target,age)`, `GradientColorize(text)` |
| `TUILayout.cs` | `CenterText(text,w)`, `RightAlign(text,w)` |
| `TUIWidgets.cs` | `ProgressBar()`, `SpinnerFrame()`, `Divider()`, `HeaderLine()`, `Box()` |
| `TUIAnimation.cs` | `DecodeRows()`, `FadeoutRows()`, `ScrambleRevealFrame()`, `ProgressFrame()` |
| `TUIFormat.cs` | `Duration(s)`, `TimeColor(s)`, `ColoredDuration(s)` |
| `TerminalWindow.cs` | Abstract base — row grid, dynamic resize, scrollback, `Render()` |
| `TerminalRow.cs` | Row component — text, column modes, optional slider/button overlays |
| `TUIEdgeDragger.cs` | Draggable panel edge for resize |

## Integration Pattern

### 1. Subclass `TerminalWindow`

```csharp
public class ShipTerminal : TerminalWindow
{
    protected override void Awake()
    {
        base.Awake();
        windowTitle = "SHIP LOG";
        totalRows = 20;
    }

    protected override void Render()
    {
        RenderHeader();
        SetRow(ROW_SEP_TOP, Separator());
        RenderScrollback();
        SetRow(RowSepBot, Separator());
        SetRow(RowActions, TUIColors.Dimmed("  [ESC] close"));
    }
}
```

### 2. Use TUI Primitives

```csharp
using CodeGamified.TUI;

// Scramble-reveal animation
string frame = TUIEffects.ScrambleText("LOADING...", age);
label.text = TUIEffects.GradientColorize(frame);

// Progress bar with gradient fill
string bar = TUIWidgets.ProgressBar(0.73f, length: 16);

// Duration with magnitude color
string uptime = TUIFormat.ColoredDuration(3661f); // "1h 01m 01s" in yellow
```

### 3. Column Modes (opt-in)

```csharp
// Dual-column (list + detail)
InitializeDualColumns(splitRatio: 0.5f);
rows[3].SetBothTexts("Left content", "Right content");

// Three-panel (code debugger)
rows[3].SetThreePanelMode(true, col2Start, col3Start);
rows[3].SetThreePanelTexts("SOURCE", "MACHINE", "REGISTERS");

// Triple-column (status bar: left/center/right justified)
rows[0].SetTripleColumnMode(true);
rows[0].SetTripleTexts("Ship Name", "12:00:00", "Day 42");
```

### 4. Slider/Button Overlays (opt-in, BitNaughts)

```csharp
var slider = rows[6].CreateSliderOverlay(startChar: 10, widthChars: 14);
slider.onValueChanged.AddListener(v => OnAltitudeChanged(v));

rows[3].CreateButtonOverlay("ACCEPT", charPos: 2, width: 10, onClick: AcceptContract);
```

### 5. Glassmorphic Blur (zero-config, URP only)

Acrylic frosted-glass effect on panel backgrounds. Auto-enabled on Ultra quality.

**Zero setup required.** Having `CodeGamified.TUI.Blur` in your project does everything:

1. **Editor** (`TUIBlurEditorSetup`, `[InitializeOnLoad]`):
   - Creates material assets in `Assets/CodeGamified.TUI.Blur.Generated/`
   - Adds `TUIBlurFeature` to the active URP renderer

2. **Runtime** (`TUIBlurManager`, `[RuntimeInitializeOnLoadMethod]`):
   - Loads the UI blur material from Resources
   - Assigns it to `TerminalWindow.SharedBlurMaterial`
   - Listens to `QualityBridge.OnTierChanged` — blur ON at Ultra, OFF otherwise

All terminals (including ones created later) auto-pick-up the shared blur state.

**Manual override** (per-terminal):

```csharp
// Force blur on/off for a specific terminal, regardless of quality tier:
terminal.SetBlurEnabled(true);

// Or assign a custom blur material via Inspector (overrides shared):
// TerminalWindow → Blur Material field
```

**How it works:**

- `TUIBlurFeature` (render feature) captures the opaque scene after rendering,
  downsamples it, and runs 4 iterations of Kawase blur → `_TUIBlurTexture` global
- `UIBackgroundBlur` (UI shader) samples `_TUIBlurTexture` at screen UV,
  dims it (`_BlurBrightness`), composites with Image.color tint
- When blur is disabled or no material exists, falls back to solid `(0,0,0,0.92)`

**Tuning:**

| Parameter | Where | Default | Effect |
|-----------|-------|---------|--------|
| Iterations | TUIBlurFeature (Inspector) | 4 | More = blurrier, ~0.07ms each |
| Downsample | TUIBlurFeature (Inspector) | 2 | Higher = cheaper, softer |
| `_BlurBrightness` | UIBackgroundBlur material | 0.12 | How much blur shows through |
| `Image.color` | TerminalWindow | `(0,0,0,0.85)` | Tint RGB + overall opacity |

**Assembly isolation:**

| Assembly | Dependencies | Platform |
|----------|-------------|----------|
| `CodeGamified.TUI.Blur` | URP, TUI, Quality | All |
| `CodeGamified.TUI.Blur.Editor` | TUI.Blur, URP | Editor only |

Core TUI has zero URP dependency — blur is fully opt-in.

## Submodule Usage

```bash
# In your game repo:
git submodule add <tui-repo-url> Assets/CodeGamified.TUI
```

Both BitNaughts and SeaRauber import this as a submodule.  
TUI improvements propagate to both via `git submodule update`.

## Refinements Over Original Implementations

| # | What | Effect |
|---|------|--------|
| 1 | Unified `TerminalWindow` | Merges BN's column layout + SR's dynamic resize |
| 2 | Unified `TerminalRow` | SR's clean factory + BN's slider/button overlays |
| 3 | `CodeGamified.TUI` namespace | Replaces both `SeaRauber.TUI` and `BitNaughtsAI.UI` |
| 4 | `TextAnimUtils` absorbed | BN's static helpers → already in TUI primitives |
| 5 | `TerminalStyle` SO eliminated | Constants + TUI primitives replace ScriptableObject |
| 6 | `TUIEdgeDragger` deduplicated | Single copy instead of identical files in BNUI + SRUI |
| 7 | Assembly definition | `CodeGamified.TUI.asmdef` for Unity project isolation |
| 8 | 5 identical terminals deduplicated | Code/Crew/Nav/Ship terminals + dragger were copy-pasted |
| 9 | Python source of truth preserved | `py/TUI.py` → C# 1:1 static classes pattern unchanged |

## vs Original Implementations

| BitNaughts (BNUI) | SeaRauber (SRUI) | CodeGamified.TUI |
|---|---|---|
| `TextAnimUtils` static class | `TUIEffects + TUIWidgets` | TUI primitives (11 classes) |
| `TerminalStyle` ScriptableObject | `TUIConfig + TUIColors` | `TUIConfig` static + gradient |
| Slider/button overlays per row | No overlays | Overlays opt-in per row |
| Dual/triple column modes | Three-panel mode | All column modes unified |
| `BitNaughtsAI.UI` namespace | `SeaRauber.UI` namespace | `CodeGamified.TUI` namespace |
| `BLINK_MARKER` animation | `M_SPIN/M_PROG/M_PULSE` | Shared marker set |
