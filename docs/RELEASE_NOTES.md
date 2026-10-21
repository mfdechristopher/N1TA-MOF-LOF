## N1TA MOF/LOF 1.2.0

**New: log analysis, to look back at how the prediction held up.**

- Import a **Cabrillo or ADIF** log (Log menu, command line, drag onto the exe, or "Open with").
- Every QSO is plotted on the map as a dot in its band's colour (20 & 40 m blue, 15 & 80 m green, 10 & 160 m red). A white **X** marks a QSO the prediction called closed at that time.
- Step through the contest hour by hour (Enter / Backspace), animate it (A), jump to the first or last QSO (Home / End), hover a dot for its details.
- **Log > Analysis report** gives overall, per-band and per-hour accuracy and the largest misses, and saves as text.
- Includes a clearly labelled synthetic sample log (`docs/sample-log-synthetic.cbr`) for trying it.

**Please note**
- The solar flux, A and K for the log's own period have to be entered; NOAA's automatic data only covers today.
- A QSO proves a band was open but can't prove the others were closed, so the report counts misses, not false alarms.
- Worked stations are placed by callsign (country point, or call-area centre for the US and Canada), or by grid square when the log has one. That is approximate. QSOs under 1000 km (621 mi) are not judged.

**Download** `N1TA_MOFLOF-v1.2.0.zip`, unzip, run `N1TA_MOFLOF.exe`. Needs .NET Framework 4.x (built into Windows 10/11). Unsigned, so SmartScreen will complain; `SHA256SUMS.txt` is attached.

**Known limits**
- Not yet tried with real contest logs from real contests; please report anything that imports oddly.
- Not yet exercised on real Windows hardware or against NOAA's live servers by the author.
- LOF model and A/K storm corrections are heuristics, not the original K1TTT method.

Concept and display conventions after Dave, K1TTT's MOF/LOF program. No K1TTT code used.
