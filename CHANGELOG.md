# Changelog

## 1.3.0
- **Added: update the callsign database with one click.** Log > Update callsign database (cty.dat)... shows the version in use and its age, and downloads AD1C's latest Big CTY file from country-files.com when you click the button. Never contacts the site otherwise.
- The download is validated before use (size, format, 300+ countries, spot checks including that the US/Canada entity names still match); a bad file changes nothing. Kept in the settings folder, preferred over the built-in copy, with automatic fallback to the built-in copy if it is missing or damaged, and a "use the built-in copy" button.
- An open log is re-placed and re-analysed immediately after an update.
- The built-in copy is now the full Big CTY (version 20260714) instead of the smaller standard file.
- **Fixed: QSO dots were hard to read.** A dot whose band colour matched the map region behind it (a blue 40 m dot on the blue 20 m area) looked like an empty ring, and the documentation screenshot and caption did not match. Dots are now a band-coloured disc with a black edge, a white ring and a black outline, and **the map is drawn in darker shades of the same three colours while a log is open** so the bright dots stand out. The screenshot and caption were redone from the actual program output.
- Self-test expanded to 74 checks (validation, install, fallback, a real HTTP download against a local server, re-placing a log).

## 1.2.0
- **Added: log analysis.** Import a Cabrillo or ADIF log and plot its QSOs on the map hour by hour, in the program's blue / green / red band colours, marking QSOs the prediction called closed. Step with Enter/Backspace, animate with A, jump with Home/End, hover a dot for details, and open an analysis report (overall, by band, by hour, largest misses) that can be saved as text.
- **Added:** callsign-to-location from AD1C's country files (embedded), with call-area centres for the US and Canada; grid squares in the log are used when present.
- **Added:** QSOs closer than 1000 km (621 mi) are not judged (ground wave / NVIS are outside an F2 model).
- **Added:** a log can be named on the command line (also "Open with" and drag onto the exe).
- **Added:** the program offers to use the log's own station location, and restores yours when the log is closed.
- Self-test expanded to 59 checks (callsign database, Cabrillo and ADIF parsing, evaluation, report).

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
