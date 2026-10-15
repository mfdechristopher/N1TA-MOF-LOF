# N1TA MOF/LOF

**A bare-bones HF propagation map for Windows. One `.exe`, no installer, no account, no cloud, no dashboard, no "insights."**

It paints the world in three colours showing where your signal can go (MOF, the *maximum* observed frequency) and where the D-layer is going to eat it (LOF, the *lowest* observed frequency), then pulls today's solar numbers from NOAA so you don't have to.

It is a from-scratch rewrite of the idea behind [K1TTT's MOF/LOF program](https://wiki.k1ttt.net), which has been quietly useful since the DOS era. Credit for the concept and the look belongs to Dave, K1TTT. No K1TTT code was used. Go thank him.

![Short-path MOF map](docs/screenshot-mof.png)

*Short-path MOF from FN32. Blue = 20 m open, green = 15 m, red = 10 m, black = go outside. The white line is the day/night terminator. The white square is you.*

---

## Get it

1. Grab `N1TA_MOFLOF-v1.0.0.zip` from the [Releases](../../releases) page.
2. Unzip. Double-click `N1TA_MOFLOF.exe`.
3. Windows SmartScreen will say it doesn't recognise the app. It is correct: I did not pay for a code-signing certificate to draw coastlines. Click *More info → Run anyway*, or build it yourself (below) and trust your own compiler.

Needs .NET Framework 4.x, which is already on Windows 10 and 11. Windows 7/8 users may need to install it. Nothing is installed; settings live in `%APPDATA%\N1TA-MOFLOF\settings.ini`. Delete the folder and it never happened.

## First five minutes

1. **Calculations → Start…** (Ctrl+S). Type your grid square (it fills in lat/lon) or the coordinates directly. Longitude is **+East / −West**.
2. Leave "Get SFI / A / K from NOAA automatically" ticked. Leave "Live" ticked. Click OK.
3. Look at the map. Press **Enter** to jump one hour ahead, **Backspace** to go back, **A** to animate the day.
4. Press **4** to flip from MOF to LOF. Press **3** or **6** for the composite short/long path view.
5. Hover the mouse over anywhere on the map. The status bar gives you distance, bearing, MOF and LOF for both paths, and the 3000 km single-hop numbers.

No internet? **Calculations → NOAA data: manual** and type SFI, A and K into the Start box. Works fine in a basement, a field day tent, or a bunker.

## Keys

| Key | What it does |
|---|---|
| `1` `2` `3` | MOF: short path / long path / composite (best of both) |
| `4` `5` `6` | LOF: short path / long path / composite |
| `7` `8` | MOF / LOF for a 3000 km single hop centred on each map point (the "online tool" view) |
| `Enter` `Space` `→` | Next hour |
| `Backspace` `←` | Previous hour |
| `A` | Animate (about one hour per second) |
| `L` | Live: follow the UTC clock |
| `R` `X` `G` | Rectangular / north polar / polar centred on your QTH |
| `S` | Sunrise (solid) and sunset (dashed) lines |
| `P` | Great-circle radials from your QTH, every 30° |
| `F5` | Re-fetch NOAA data now |
| `Ctrl+S` | Start dialog |

**Striped cells are the long path.** In the composite views, stripes mean the long way round beats the short way.

## What the colours mean

| MOF map | LOF map |
|---|---|
| blue: MOF ≥ 14.15 MHz (20 m is open) | blue: LOF ≤ 7.1 MHz (40 m is usable) |
| green: MOF ≥ 21.2 MHz (15 m) | green: LOF ≤ 3.6 MHz (80 m) |
| red: MOF ≥ 28.5 MHz (10 m) | red: LOF ≤ 1.85 MHz (160 m) |

Higher bands imply the lower ones. Red on a MOF map means 20 and 15 are open too. A band is only really workable if the MOF is above it *and* the LOF is below it, so flip between the two maps. Thresholds are constants near the top of `MainForm.cs` if you disagree with my band centres.

## Where the numbers come from

* **Solar flux, K and A** come from NOAA's Space Weather Prediction Center public services (`services.swpc.noaa.gov`). No key, no login. Refreshed every 30 minutes while Live. If NOAA is unreachable the status bar says so and the program carries on with whatever you last typed in.
* **Future dates** in automatic mode use NOAA's 27-day outlook for that day, if it has one. Beyond 27 days it falls back to today's values and the status bar says so ("using NOAA now").
* **MOF** is [MINIMUF 3.5](https://apps.dtic.mil/sti/pdfs/ADA066256.pdf), the US Navy's 1978 algorithm, implemented from the published equations. It reproduces the Navy's own sample output (`N1TA_MOFLOF.exe --selftest` checks this). Control points, M-factor, the effective-zenith-angle lag, the winter/high-latitude/transequatorial fudge factors: all in `src/Engine.cs`.
* **LOF** is a standard D-layer absorption model (George-style, as used by ITU-R P.533) with a 52 dB total absorption budget. That budget is my tuning, not a law of nature.
* **A and K** do not appear anywhere in MINIMUF, so I invented something modest: K above 3 trims MOF at mid and high geomagnetic latitudes, and raises auroral absorption (so LOF goes up) as the oval expands equatorward. These are heuristics. They are honest about it in the Notes box. So am I.

## How it differs from K1TTT's original

| | K1TTT | N1TA |
|---|---|---|
| MOF engine | adaptation of MINIMUF-85 | MINIMUF 3.5 (the part I could verify) |
| LOF engine | Piggott / George / Samuel / Bradley, via Radio Netherlands | simple absorption model, tuned by me |
| Solar data | type it in | NOAA fetch, or type it in |
| Contest-log overlay (MMPRINT), replay files | yes | no |
| Runs on | DOS / Win16/32 | anything with .NET Framework 4 |

**Expect the maps to look *similar*, not identical.** If they disagree, the original has a decade of contest logs behind its tuning and I have a verified Navy algorithm and a good attitude. Use both.

## Honest caveats

* Propagation prediction is statistical. This predicts a median ionosphere. Yours will be having its own day.
* MINIMUF is an F-region model. It knows nothing about sporadic E, grey-line enhancement, or that one guy with the 4-element 160 m array.
* Daily flux is noisy. NOAA's "now" value drives the map; a 3-to-30-day average is a steadier planning input, which is what the Start box is for.
* Tested: the engine against the Navy's published sample; the NOAA parsers against sample text in the documented formats; the full GUI under Mono on a virtual display (all eight map modes, three projections, the Start dialog). **Not yet tested:** on a real Windows machine, and against NOAA's live servers (the build environment couldn't reach them). If the NOAA light stays red on your machine, open an issue with the status-bar text and I'll fix the parser. The program degrades to manual entry rather than crashing.

## Build it yourself

Needs [Mono](https://www.mono-project.com/) (`mono-mcs`, `mono-devel`). Yes, Mono, on any OS; the output is a normal Windows executable.

```sh
./build.sh                                  # produces dist/N1TA_MOFLOF.exe
N1TA_QUIET=1 mono dist/N1TA_MOFLOF.exe --selftest
```

On Windows you can run `N1TA_MOFLOF.exe --selftest` directly and it pops up the results.

Layout:

```
src/Engine.cs       propagation maths (MINIMUF 3.5, LOF, storm tweaks, sun position)
src/Noaa.cs         SWPC fetch + tolerant parsers
src/MainForm.cs     the window, the map renderer, settings
src/StartDialog.cs  the Start box
src/SelfTest.cs     --selftest
res/coast.txt       Natural Earth 110m coastline, reduced
build.sh            one-line build
```

Pushing a tag like `v1.0.0` makes GitHub Actions build, self-test, zip and attach the release automatically.

## Support

As the original README said: don't expect much, but feel free to ask. Issues and pull requests welcome. Please keep the UI plain. If your PR contains a gradient, I will find you.

## Credits and licence

MIT licensed (see `LICENSE`). Full credits for K1TTT, the US Navy's MINIMUF authors, Natural Earth and NOAA are in [THIRD_PARTY.md](THIRD_PARTY.md).

73 de N1TA
