## N1TA MOF/LOF 1.1.0

Fixes the NOAA problem from 1.0.0 and adds the things testers asked for.

**Fixed**
- Automatic NOAA data now loads without the "input string was not in a correct format" error. The bad parser is gone; the A index is derived from NOAA's planetary Kp feed, and all parsers tolerate layout changes. **Help > NOAA diagnostics** shows exactly what NOAA returned.

**New**
- One Start screen at every launch: Maidenhead grid square (remembered), date and time, and solar data.
- Manual SFI / A / K entry that is easy to find, with automatic fall-back per field.
- Predictions for any date. Future dates up to 27 days ahead use NOAA's 27-day outlook.
- Distances shown in both kilometers and miles everywhere, plus the grid square under the cursor.

**Download** `N1TA_MOFLOF-v1.1.0.zip`, unzip, run `N1TA_MOFLOF.exe`. Needs .NET Framework 4.x (built into Windows 10/11). Unsigned, so SmartScreen will complain; `SHA256SUMS.txt` is attached.

**Known limits**
- Not yet exercised on real Windows hardware or against NOAA's live servers by the author; please report anything odd, ideally with the text from Help > NOAA diagnostics.
- LOF model and A/K storm corrections are heuristics, not the original K1TTT method.

Concept and display conventions after Dave, K1TTT's MOF/LOF program. No K1TTT code used.
