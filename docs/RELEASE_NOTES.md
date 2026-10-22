## N1TA MOF/LOF 1.3.0

**New: update the callsign database with one click.**

- **Log > Update callsign database (cty.dat)...** shows which version of AD1C's Country Files is in use and how old it is, and a button downloads the latest from country-files.com. The program contacts the site **only when you click**.
- The download is **checked before use**; a bad or truncated file is refused and nothing changes. It is saved in your settings folder and used from then on, with automatic fallback to the built-in copy, and a button to go back to it.
- An open log is re-placed and re-analysed right away.
- The built-in copy is now the full Big CTY (version 20260714).

**Fixed: log-analysis dots.** A dot whose colour matched the map region behind it looked like an empty ring, and the screenshot in the README did not match its caption. Dots are redrawn, and the map is now drawn in darker shades of the same three colours while a log is open, so the bright dots stand out. The README screenshot and caption were redone from the real program.

**From 1.2.0:** import a Cabrillo or ADIF log and plot every QSO on the map, hour by hour, marking those the prediction called closed; Log > Analysis report summarises how well the prediction held up.

**Download** `N1TA_MOFLOF-v1.3.0.zip`, unzip, run `N1TA_MOFLOF.exe`. Needs .NET Framework 4.x (built into Windows 10/11). Unsigned, so SmartScreen will complain; `SHA256SUMS.txt` is attached.

**Known limits**
- The download button has been tested against a local stand-in server, not yet against country-files.com itself (the build environment cannot reach it). If it fails for you, the message says why; please report it.
- Not yet tried with real contest logs from real contests.
- Not yet exercised on real Windows hardware or against NOAA's live servers by the author.

Concept and display conventions after Dave, K1TTT's MOF/LOF program. No K1TTT code used.
