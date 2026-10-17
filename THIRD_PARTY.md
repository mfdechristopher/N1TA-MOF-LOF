# Third-party material and credits

| What | Where it's used | Terms |
|---|---|---|
| **K1TTT MOF/LOF** (Dave, K1TTT) | The idea, the display conventions (band colours, striped long path, replay keys). **No code was used or consulted**; this is a clean rewrite. | His original is freeware, "not for sale, may be freely copied". Go say thanks: <https://wiki.k1ttt.net> |
| **MINIMUF 3.5**, R.B. Rose & J.N. Martin, NOSC TD 201, 1978 | `src/Engine.cs` implements the published equations for MOF | US Navy report, approved for public release (DTIC AD-A066 256) |
| **MINIMUF-85** report, Sailors/Sprague/Rix, NOSC TR 1121, 1986 | SFI-to-SSN relation (eq. 29) and geomagnetic-latitude equation (eq. 17) | US Navy report, public release (DTIC ADA178305) |
| **Natural Earth 1:110m coastline** | `res/coast.txt` (reduced to 0.1 degree integers) | Public domain, <https://www.naturalearthdata.com> |
| **N1MM Logger+** and **DXLog.net** UDP broadcast formats | `src/Udp.cs`: the `<RadioInfo>`, `<contactinfo>` and `<lookupinfo>` layouts, read from the loggers' public documentation. No code from either program. | Documentation only |
| **WSJT-X** UDP message layout | `src/Udp.cs`: status-packet decoding from the published network message format. No code used. | Documentation only |
| **NOAA SWPC** public data services | Fetched at runtime, never stored | US Government work, public domain |

The LOF absorption model follows the widely published George (1971) / ITU-R P.533 style
D-layer absorption formula, with N1TA tuning constants. The K/A storm corrections are
N1TA heuristics. Both are described honestly in README.md.
