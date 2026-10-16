# CLAUDE.md

Guidance for Claude (or any AI assistant / new contributor) working in this repository.

## What this is
N1TA MOF/LOF: a deliberately plain Windows desktop program that draws HF MOF and LOF maps and pulls SFI/A/K from NOAA SWPC. It is a **clean-room rewrite of the idea** behind K1TTT's MOF/LOF program. No K1TTT code was used or consulted, and none must be added.

## Hard rules
1. **The UI stays bare-bones.** Classic menu bar, black map, status bar, system colours. No gradients, themes, icons-in-menus, splash screens, animations beyond the hour-step animator. This is a product requirement, not a style preference.
2. **No third-party dependencies.** One `.exe`, .NET Framework 4.x only (WinForms, GDI+). No NuGet, no external DLLs, no installer.
3. **No K1TTT code.** Behaviour and look are fair game; source is not. Credit stays in README/THIRD_PARTY.md/About.
4. **Never claim more than is verified.** MOF is verified against the Navy sample (see below). LOF and the A/K storm factors are heuristics and must be described as such in docs and UI text.
5. **Offline must work.** Every NOAA failure falls back to manual values. Never throw to the user over a network or parse error.
6. **Parsers never throw.** NOAA formats change. Parse with tolerant regexes, return NaN/empty on surprises, log to `Noaa.Log` (Help > NOAA diagnostics), and add the new sample to `SelfTest.cs`. Do not assume column positions in any NOAA text file; v1.0.0 did and shipped broken.
7. **Never guess the user's location.** The grid square is asked at every start (pre-filled from `settings.ini`). No hard-coded default location.
8. **Every distance shown to the user shows both km and miles.** Status readout, mode names, help text, README.

## Layout
- `src/Engine.cs`: all propagation maths. Pure functions, no UI, no I/O.
- `src/Noaa.cs`: HTTP fetch, tolerant parsers, diagnostics log. Feeds used: 10cm flux summary (+2 fallbacks), planetary Kp feed (A = mean of last 8 3-hourly a values), 27-day outlook.
- `src/Grid.cs`: Maidenhead conversion.
- `src/MainForm.cs`: window, menu, per-pixel inverse-projection renderer, settings (`%APPDATA%\N1TA-MOFLOF\settings.ini`: grid, auto, live, manual sfi/a/k, mode, proj, step, overlays).
- `src/StartDialog.cs`: the single input screen (grid, live/fixed date-time, automatic/manual solar data, fetch button). Shown at startup and via Ctrl+S.
- `src/SelfTest.cs`: `--selftest` regression checks.
- `res/coast.txt`: Natural Earth 110m coastline. One polyline per line, space-separated `lon10,lat10` integer pairs (degrees x 10). Embedded as resource named `coast.txt`.
- `build.sh`: the only build step.

## Build and test
```sh
./build.sh                                       # needs mono-mcs + mono-devel
N1TA_QUIET=1 mono dist/N1TA_MOFLOF.exe --selftest
```
**The self-test must pass before any commit that touches `Engine.cs` or `Noaa.cs`.** It checks MINIMUF 3.5 against the Navy's published sample (NOSC TD 201, Fig. 1: 21N 156.1W to 38N 122.1W, 17 Oct, SSN 110) to within 0.15 MHz, plus parser samples.

The compiler is Mono's `mcs` (C# up to about 6/7 features). Avoid newer syntax (`?.` is fine, but no records, pattern-matching switch expressions, `using var`). Target is `-sdk:4.5`.

## Gotchas
- **Longitude conventions.** The public API and UI are **east-positive**. MINIMUF's original code is west-positive; `Prop.NoonUt` and `MagLat` convert internally (`w = -lonE`). Do not "simplify" this; the Navy sample check will catch you.
- `y2 = 0.409*cos(y1)` is **minus** the solar declination. Several formulas depend on that sign.
- MINIMUF clips to 2..32 MHz (`Prop.Clip`). Colour thresholds are constants in `MainForm.cs` (`MofLevel`, `LofLevel`) and the legend text in `UpdateInfo()`; change them together.
- Long path: same great circle, other way round (bearing + 180°, arc = 2π - d). Control points for paths over ~4000 km are 1/(2*1.59*arc) from each end (two points); hop angle for M-factor is arc/2 (the Navy BASIC), not the constant used in some C ports.
- Rendering is per-pixel inverse projection → cell lookup, so every projection shares one code path. Add projections by implementing `Forward` and `Inverse` only.
- `Legend.Labels` (not `Text`) holds legend strings; `Text` would hide `Control.Text`.
- NOAA endpoints are in `Noaa.cs`. The build environment cannot reach NOAA, so live responses have only been checked against sample layouts (the Kp feed switched from arrays to objects with two-decimal Kp in Nov 2022; both are handled). Ask users for the text from Help > NOAA diagnostics.
- Mono's DateTimePicker ignores typed digits, so date entry cannot be driven by xdotool in the Linux test rig.
- Windows quirks not covered by Mono testing: DPI scaling (no manifest, so Windows bitmap-scales), SmartScreen on the unsigned exe.

## Release process
1. Update `HelpText.Version` in `MainForm.cs`, `src/AssemblyInfo.cs`, `CHANGELOG.md`, `docs/RELEASE_NOTES.md`.
2. `./build.sh` and run the self-test.
3. `git tag vX.Y.Z && git push --tags`. The GitHub Action builds, self-tests, zips, checksums and attaches the release.
4. Do **not** commit `dist/` or `*.exe` (see `.gitignore`).

## Style
Plain C#, no frameworks, explicit over clever. Comments explain *why* (especially anywhere the Navy algorithm is mirrored), not what. README tone is dry and a little droll; error messages and dialogs are plain and polite.
