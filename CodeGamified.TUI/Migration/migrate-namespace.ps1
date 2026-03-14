#!/usr/bin/env pwsh
# ═══════════════════════════════════════════════════════════
#  migrate-namespace.ps1 — Migrate BNUI/SRUI → CodeGamified.TUI
#  Run from the game's Assets/ folder.
#
#  Usage:
#    ./migrate-namespace.ps1 -Path "Assets/Scripts"
#    ./migrate-namespace.ps1 -Path "Assets/Scripts" -WhatIf
# ═══════════════════════════════════════════════════════════
param(
    [Parameter(Mandatory)][string]$Path,
    [switch]$WhatIf
)

$replacements = @(
    # ── Namespace imports ────────────────────────────────────
    @{ Old = 'using SeaRauber\.TUI;';         New = 'using CodeGamified.TUI;' }
    @{ Old = 'using SeaRauber\.UI;';          New = 'using CodeGamified.TUI;' }
    @{ Old = 'using BitNaughtsAI\.UI;';       New = 'using CodeGamified.TUI;' }

    # ── Namespace declarations (game-specific UI stays in game) ──
    # These are NOT changed — game terminals stay in their own namespace.
    # Only the shared TUI primitives and base classes migrate.

    # ── TextAnimUtils → TUI primitives ───────────────────────
    @{ Old = 'TextAnimUtils\.GetScrambledText';    New = 'TUIEffects.ScrambleText' }
    @{ Old = 'TextAnimUtils\.GetProgressBar';      New = 'TUIWidgets.ProgressBar' }
    @{ Old = 'TextAnimUtils\.GetBrailleSpinner';   New = 'TUIWidgets.SpinnerFrame' }
    @{ Old = 'TextAnimUtils\.GetPulseDiamond';     New = "TUIWidgets.SpinnerFrame(age, TUIGlyphs.PulseDiamond" }
    @{ Old = 'TextAnimUtils\.GetPulseCircle';      New = "TUIWidgets.SpinnerFrame(age, TUIGlyphs.PulseCircle" }
    @{ Old = 'TextAnimUtils\.GetRadarSweep';       New = "TUIWidgets.SpinnerFrame(age, TUIGlyphs.RadarSweep" }
    @{ Old = 'TextAnimUtils\.GetDivider';          New = 'TUIWidgets.Divider' }
    @{ Old = 'TextAnimUtils\.GetHeader';           New = 'TUIWidgets.HeaderLine' }
    @{ Old = 'TextAnimUtils\.GetBlinkingText';     New = 'TUIEffects.BlinkingText' }
    @{ Old = 'TextAnimUtils\.GetTypewriterText';   New = 'TUIEffects.TypewriterText' }
    @{ Old = 'TextAnimUtils\.GetCursor';           New = 'TUIEffects.Cursor' }
    @{ Old = 'TextAnimUtils\.GetSignalStrength';   New = 'TUIWidgets.SignalStrength' }
    @{ Old = 'TextAnimUtils\.GetBatteryIndicator'; New = 'TUIWidgets.BatteryIndicator' }
    @{ Old = 'TextAnimUtils\.GetTemperatureGauge'; New = 'TUIWidgets.TemperatureGauge' }

    # ── TerminalStyle constants → TUIConstants ───────────────
    @{ Old = 'TerminalStyle\.INDENT';              New = 'TUIConstants.INDENT' }
    @{ Old = 'TerminalStyle\.PROGRESS_BAR_WIDTH';  New = 'TUIConstants.PROGRESS_BAR_WIDTH' }
    @{ Old = 'TerminalStyle\.TAB_BUTTON_WIDTH';    New = 'TUIConstants.TAB_BUTTON_WIDTH' }
    @{ Old = 'TerminalStyle\.COLUMN_DIVIDER';      New = 'TUIConstants.COLUMN_DIVIDER' }
    @{ Old = 'TerminalStyle\.Colorize';            New = 'TUIColors.Fg' }

    # ── BLINK marker ─────────────────────────────────────────
    @{ Old = 'BLINK_MARKER';   New = 'M_BLINK' }
    @{ Old = 'SPIN_MARKER';    New = 'M_SPIN' }
    @{ Old = 'PROG_MARKER';    New = 'M_PROG' }
    @{ Old = 'PULSE_MARKER';   New = 'M_PULSE' }
)

$files = Get-ChildItem -Path $Path -Recurse -Include "*.cs"
$totalChanges = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $modified = $content
    $fileChanges = 0

    foreach ($r in $replacements) {
        $count = ([regex]::Matches($modified, $r.Old)).Count
        if ($count -gt 0) {
            $modified = $modified -replace $r.Old, $r.New
            $fileChanges += $count
        }
    }

    if ($fileChanges -gt 0) {
        $totalChanges += $fileChanges
        if ($WhatIf) {
            Write-Host "[WhatIf] $($file.Name): $fileChanges replacements"
        } else {
            Set-Content -Path $file.FullName -Value $modified -NoNewline
            Write-Host "[OK] $($file.Name): $fileChanges replacements"
        }
    }
}

Write-Host "`n=== Total: $totalChanges replacements across $($files.Count) files ==="
