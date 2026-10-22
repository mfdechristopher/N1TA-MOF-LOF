// Contest / station log import: Cabrillo (v2/v3) and ADIF (.adi).
//
// Both parsers are tolerant: a line they cannot read is counted and skipped, never fatal.
// Cabrillo QSO lines vary by contest, so the worked station's callsign is found by scanning for the first
// callsign-shaped token after our own callsign, rather than by a fixed column.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public class Qso
    {
        public DateTime Utc; public double Mhz; public string Call = "", Mode = "", Where = "";
        public double Lat, Lon; public bool Located, FromGrid;           // location of the worked station, and whether the log itself gave it (a grid square)
        public bool Evaluated; public BandState State; public bool LongPath; public double Mof, Lof, DistKm;   // filled by LogAnalysis
    }

    public class LogData
    {
        public string Format = "", Contest = "", MyCall = "", MyGrid = "";
        public List<Qso> Qsos = new List<Qso>();
        public int NotHf, NoCall, BadTime, Unlocated, TooClose;   // QSOs that could not be used, by reason
        public DateTime First, Last;
        public int Total { get { return Qsos.Count + NotHf + NoCall + BadTime; } }
    }

    public static class LogFile
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        static readonly Regex CallRx = new Regex("^(?:[A-Z0-9]{1,3}/)?[A-Z0-9]{1,3}[0-9][A-Z0-9]*[A-Z](?:/[A-Z0-9]{1,4})?$", RegexOptions.Compiled);
        static readonly Regex GridRx = new Regex("^[A-R]{2}[0-9]{2}([A-X]{2})?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // A callsign has at least one digit and at least two letters, which rules out reports (599), zones (05),
        // powers (100W), states (MA) and similar exchange tokens.
        public static bool CallLike(string tok)
        {
            if (string.IsNullOrEmpty(tok)) return false;
            tok = tok.ToUpperInvariant();
            if (!CallRx.IsMatch(tok)) return false;
            int letters = 0; foreach (char ch in tok) if (ch >= 'A' && ch <= 'Z') letters++;
            return letters >= 2;
        }

        public static LogData Load(string path)
        {
            string text = File.ReadAllText(path, Encoding.GetEncoding("ISO-8859-1"));   // 1 byte = 1 char, so ADIF field lengths are exact
            return Parse(text);
        }

        public static LogData Parse(string text)
        {
            LogData log;
            if (Regex.IsMatch(text, "START-OF-LOG", RegexOptions.IgnoreCase) || Regex.IsMatch(text, "^\\s*QSO:", RegexOptions.IgnoreCase | RegexOptions.Multiline)) log = Cabrillo(text);
            else if (Regex.IsMatch(text, "<eor>|<eoh>", RegexOptions.IgnoreCase)) log = Adif(text);
            else throw new FormatException("This file does not look like a Cabrillo or ADIF log.");
            Finish(log); return log;
        }

        // Places every QSO that was located by callsign again, e.g. after the country file has been updated.
        // QSOs the log itself located (grid squares) are left alone.
        public static void Relocate(LogData log)
        {
            log.Unlocated = 0;
            foreach (Qso q in log.Qsos)
            {
                if (q.FromGrid) continue;
                double la, lo; string w;
                if (CallDb.Lookup(q.Call, out la, out lo, out w)) { q.Lat = la; q.Lon = lo; q.Where = w; q.Located = true; }
                else { q.Located = false; q.Where = ""; log.Unlocated++; }
            }
        }

        static void Finish(LogData log)
        {
            foreach (Qso q in log.Qsos)
            {
                if (q.Located) continue;
                double la, lo; string w;
                if (CallDb.Lookup(q.Call, out la, out lo, out w)) { q.Lat = la; q.Lon = lo; q.Where = w; q.Located = true; } else log.Unlocated++;
            }
            log.Qsos.Sort(delegate (Qso a, Qso b) { return a.Utc.CompareTo(b.Utc); });
            if (log.Qsos.Count > 0) { log.First = log.Qsos[0].Utc; log.Last = log.Qsos[log.Qsos.Count - 1].Utc; }
        }

        // ---------------- Cabrillo ----------------
        static LogData Cabrillo(string text)
        {
            LogData log = new LogData(); log.Format = "Cabrillo";
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim(); if (line.Length == 0) continue;
                if (line.StartsWith("QSO:", StringComparison.OrdinalIgnoreCase)) { CabrilloQso(log, line); continue; }
                int c = line.IndexOf(':'); if (c <= 0) continue;
                string key = line.Substring(0, c).Trim().ToUpperInvariant(), val = line.Substring(c + 1).Trim();
                if (key == "CONTEST" && log.Contest.Length == 0) log.Contest = val;
                else if (key == "CALLSIGN" && log.MyCall.Length == 0) log.MyCall = val.ToUpperInvariant();
                else if (key == "GRID-LOCATOR" && log.MyGrid.Length == 0) { double a, b; if (Grid.TryParse(val, out a, out b)) log.MyGrid = Grid.Normalize(val); }
            }
            return log;
        }

        static void CabrilloQso(LogData log, string line)
        {
            string[] t = Regex.Split(line, "\\s+");
            if (t.Length < 8) { log.NoCall++; return; }
            double f; if (!double.TryParse(t[1], NumberStyles.Float, CI, out f)) { log.NotHf++; return; }
            double mhz;
            if (f >= 1000) mhz = f / 1000.0;            // HF is logged in kHz
            else if (f >= 1.7 && f <= 30) mhz = f;      // some programs write MHz
            else { log.NotHf++; return; }                // 50, 144, 432 ... VHF and up
            if (!Bands.Modelled(mhz)) { log.NotHf++; return; }
            DateTime when;
            if (!ParseTime(t[3], t[4], out when)) { log.BadTime++; return; }
            string mine = t[5].ToUpperInvariant(); int idx = -1;
            for (int i = 6; i < t.Length; i++) if (CallLike(t[i]) && t[i].ToUpperInvariant() != mine) { idx = i; break; }
            if (idx < 0) { log.NoCall++; return; }
            Qso q = new Qso(); q.Utc = when; q.Mhz = mhz; q.Mode = t[2].ToUpperInvariant(); q.Call = t[idx].ToUpperInvariant();
            if (log.MyCall.Length == 0) log.MyCall = mine;
            for (int i = idx + 1; i < t.Length; i++)       // grid-square exchanges (VHF-style contests) beat the callsign guess
                if (GridRx.IsMatch(t[i])) { double la, lo; if (Grid.TryParse(t[i], out la, out lo)) { q.Lat = la; q.Lon = lo; q.Where = Grid.Normalize(t[i]); q.Located = true; q.FromGrid = true; } break; }
            log.Qsos.Add(q);
        }

        static bool ParseTime(string date, string time, out DateTime when)
        {
            when = DateTime.MinValue; DateTime d;
            if (!DateTime.TryParseExact(date, new string[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd" }, CI, DateTimeStyles.None, out d)) return false;
            int n; if (!int.TryParse(time, NumberStyles.None, CI, out n)) return false;
            int hh, mm, ss = 0;
            if (time.Length >= 6) { hh = n / 10000; mm = (n / 100) % 100; ss = n % 100; } else { hh = n / 100; mm = n % 100; }
            if (hh > 23 || mm > 59 || ss > 59) return false;
            when = DateTime.SpecifyKind(d.AddHours(hh).AddMinutes(mm).AddSeconds(ss), DateTimeKind.Utc); return true;
        }

        // ---------------- ADIF ----------------
        static LogData Adif(string text)
        {
            LogData log = new LogData(); log.Format = "ADIF";
            int i = 0; Match eoh = Regex.Match(text, "<eoh>", RegexOptions.IgnoreCase); if (eoh.Success) i = eoh.Index + eoh.Length;
            Dictionary<string, string> rec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (i < text.Length)
            {
                int lt = text.IndexOf('<', i); if (lt < 0) break;
                int gt = text.IndexOf('>', lt); if (gt < 0) break;
                string[] tp = text.Substring(lt + 1, gt - lt - 1).Split(':');
                string name = tp[0].Trim().ToUpperInvariant();
                if (name == "EOR") { AdifRecord(log, rec); rec.Clear(); i = gt + 1; continue; }
                int len;
                if (tp.Length >= 2 && int.TryParse(tp[1], NumberStyles.None, CI, out len))
                {
                    int vs = gt + 1, ve = Math.Min(text.Length, vs + len);
                    rec[name] = text.Substring(vs, ve - vs); i = ve;
                }
                else i = gt + 1;
            }
            return log;
        }

        static string Get(Dictionary<string, string> r, string k) { string v; return r.TryGetValue(k, out v) ? v.Trim() : ""; }

        static void AdifRecord(LogData log, Dictionary<string, string> r)
        {
            string call = Get(r, "CALL").ToUpperInvariant(); if (call.Length == 0) return;
            if (log.Contest.Length == 0) log.Contest = Get(r, "CONTEST_ID");
            if (log.MyCall.Length == 0) log.MyCall = (Get(r, "STATION_CALLSIGN") + Get(r, "OPERATOR")).Length > 0 ? (Get(r, "STATION_CALLSIGN").Length > 0 ? Get(r, "STATION_CALLSIGN") : Get(r, "OPERATOR")).ToUpperInvariant() : "";
            if (log.MyGrid.Length == 0) { double a, b; string g = Get(r, "MY_GRIDSQUARE"); if (g.Length > 0 && Grid.TryParse(g, out a, out b)) log.MyGrid = Grid.Normalize(g); }

            double mhz = double.NaN, f;
            if (double.TryParse(Get(r, "FREQ").Replace(',', '.'), NumberStyles.Float, CI, out f)) mhz = f;
            else
            {
                Match bm = Regex.Match(Get(r, "BAND"), "^(\\d+)\\s*M$", RegexOptions.IgnoreCase);
                if (bm.Success) mhz = Bands.Center(bm.Groups[1].Value);
            }
            if (double.IsNaN(mhz) || !Bands.Modelled(mhz)) { log.NotHf++; return; }
            DateTime when; if (!ParseTime(Get(r, "QSO_DATE"), Get(r, "TIME_ON").Length > 0 ? Get(r, "TIME_ON") : "0000", out when)) { log.BadTime++; return; }

            Qso q = new Qso(); q.Utc = when; q.Mhz = mhz; q.Call = call; q.Mode = Get(r, "MODE").ToUpperInvariant();
            string gs = Get(r, "GRIDSQUARE"); double la, lo;
            if (gs.Length >= 4 && Grid.TryParse(gs, out la, out lo)) { q.Lat = la; q.Lon = lo; q.Where = Grid.Normalize(gs); q.Located = true; q.FromGrid = true; }
            log.Qsos.Add(q);
        }
    }
}
