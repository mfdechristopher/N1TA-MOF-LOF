## N1TA MOF/LOF 1.0.0

First release. A plain Windows program that maps HF MOF and LOF, with automatic solar data from NOAA SWPC.

**Download** `N1TA_MOFLOF-v1.0.0.zip`, unzip, run `N1TA_MOFLOF.exe`. Needs .NET Framework 4.x (built into Windows 10/11). The exe is unsigned, so SmartScreen will complain; `SHA256SUMS.txt` is attached if you like checking.

**Highlights**
- Short path, long path and composite MOF/LOF maps, plus 3000 km single-hop views
- Rectangular, north-polar and QTH-centred projections; sunrise/sunset lines; great-circle radials
- SFI / A / K fetched from NOAA automatically, or entered by hand
- 27-day-outlook values for future dates
- MOF engine verified against the US Navy's published MINIMUF 3.5 sample output (`--selftest`)

**Known limits**
- Not yet exercised on real Windows hardware or against NOAA's live servers by the author; please report anything odd.
- LOF model and A/K storm corrections are heuristics, not the original K1TTT method.

Concept and display conventions after Dave, K1TTT's MOF/LOF program. No K1TTT code used.
