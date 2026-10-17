# Changelog

## 1.2.0
- **Added: BEST BAND banner.** Large flat-colour box above the map naming the best contest band (highest band open to at least a third of 18 distant regions), with all six contest bands at a glance. Click the map, or press T, for the best band to one specific target; shows distance in km and miles and which path.
- **Added: follow the radio.** Listens for the logging program's UDP broadcast (N1MM Logger+, DXLog.net, WSJT-X, N1MM-style senders such as MacLoggerDX and DXLab Commander, ADIF fallback) and shows only the band the radio is on. SO2R focus handling, split uses transmit frequency, optional station filter, port sharing advice. Configured on the Start screen.
- **Added: single band view** (Display menu, key B): green open / yellow marginal / black closed for one band, any HF band 160-10 m including 60, 30, 17 and 12 m. Works with or without a logger.
- **Added:** Help > Radio feed diagnostics.
- Parsers never throw: a malformed packet cannot stop the listener (a self-test caught and fixed one such gap during development).
- Self-test expanded to 65 checks, including a real UDP socket test.

## 1.1.0
Fixes and changes after first field testing.
- **Fixed:** NOAA automatic mode reported "unreachable" and "input string was not in a correct format". Cause: a parser for NOAA's daily-indices text file assumed a column layout that does not match the real file. That file is no longer used. The A index is now the mean of the last 24 h of 3-hourly values from the planetary Kp feed, and every NOAA parser is non-throwing and layout-tolerant.
- **Added:** Help > NOAA diagnostics (every request, response summary and parse result).
- **Added:** one Start screen for location, date/time and solar data, shown at every launch.
- **Added:** manual SFI / A / K entry, clearly visible on that screen, with per-field fallback if NOAA is partly unavailable.
- **Changed:** location is a Maidenhead grid square, asked at startup and remembered (previously a hard-coded default location, which the program had no business choosing).
- **Added:** full date (including year) input for predictions; future dates use NOAA's 27-day outlook; the screen explains what happens for past dates and dates beyond 27 days.
- **Added:** every distance is shown in kilometers and miles; grid square shown under the cursor.
- **Changed:** readout moved to a two-line panel above the status bar; status bar shows data source and is clickable.
- Self-test expanded to 28 checks (parsers incl. garbage input, grid conversion, units).

## 1.0.0 - first release
- Windows desktop program (single .exe, .NET Framework 4.x, no installer, no dependencies).
- MOF via MINIMUF 3.5, verified against the Navy's published sample output.
- LOF via D-layer absorption model.
- Short path, long path and composite maps, plus 3000 km single-hop maps.
- Rectangular, north-polar and QTH-centred projections; sunrise/sunset lines; great-circle radials.
- Automatic SFI / A / K from NOAA SWPC, with a manual override.
- Future dates use NOAA's 27-day outlook when available.
- `--selftest` switch.
