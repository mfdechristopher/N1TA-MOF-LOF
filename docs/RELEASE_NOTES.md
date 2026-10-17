## N1TA MOF/LOF 1.2.0

**New: a very visible Best Band box, and it can follow your radio.**

- **BEST BAND banner** above the map: the highest band open to a good share of the world right now, with all six contest bands (160-10 m) shown as green / yellow / grey cells. Click the map, or press T, to get the best band to one specific target, with distance in km and miles.
- **Follow your radio over UDP.** Tick it on the Start screen and the map shows only the band your radio is on, and the banner tells you if a better band exists. Supports N1MM Logger+, DXLog.net, WSJT-X, other N1MM-style senders, with SO2R focus, split, and a station filter for multi-op networks.
- **Single band view** (key B, or Display menu) for any HF band, with or without a logger.
- **Help > Radio feed diagnostics** shows every packet received and how it was read.

**Download** `N1TA_MOFLOF-v1.2.0.zip`, unzip, run `N1TA_MOFLOF.exe`. Needs .NET Framework 4.x (built into Windows 10/11). Unsigned, so SmartScreen will complain; `SHA256SUMS.txt` is attached.

**Known limits**
- Not yet tried against a live N1MM+, DXLog.net or WSJT-X; the packet parsers were checked against the loggers' documented samples and a real UDP socket. Please report anything odd, ideally with the text from Help > Radio feed diagnostics.
- Not yet exercised on real Windows hardware or against NOAA's live servers by the author.
- "Best band" is a prediction from a median-ionosphere model (MINIMUF 3.5 plus a simple absorption model), not a measurement.

Concept and display conventions after Dave, K1TTT's MOF/LOF program. No K1TTT code used.
