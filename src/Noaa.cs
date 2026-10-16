// N1TA MOF/LOF - NOAA SWPC data fetch + parse.
//
// Design rules (learned the hard way, see CHANGELOG 1.1.0):
//  * Parsers never throw. They return NaN / empty on anything unexpected.
//  * Every request and parse result is written to Noaa.Log, shown in Help > NOAA diagnostics,
//    so a format change can be diagnosed from a copy-paste instead of a guess.
//  * The A index is derived from the planetary Kp feed (mean of the last 24 h of 3-hourly
//    a values), so only two feeds are needed for the three numbers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public class SpaceWx
    {
        public double Sfi = double.NaN, A = double.NaN, K = double.NaN;
        public string Note = "";
        public bool Complete { get { return !double.IsNaN(Sfi) && !double.IsNaN(A) && !double.IsNaN(K); } }
        public bool Any { get { return !double.IsNaN(Sfi) || !double.IsNaN(A) || !double.IsNaN(K); } }
    }
    public class OutlookRow { public DateTime Date; public double Sfi, A, K; }
    public class KpRow { public double Kp = double.NaN, A = double.NaN; }

    public static class Noaa
    {
        const string Base = "https://services.swpc.noaa.gov/";
        public static readonly string[] UrlFlux = {
            Base + "products/summary/10cm-flux.json",
            Base + "products/10cm-flux-30-day.json",
            Base + "json/f107_cm_flux.json" };
        public const string UrlKp = Base + "products/noaa-planetary-k-index.json";
        public const string UrlOutlook = Base + "text/27-day-outlook.txt";

        public static readonly StringBuilder Log = new StringBuilder();
        static void L(string s) { lock (Log) { if (Log.Length > 30000) Log.Remove(0, 10000); Log.AppendLine(DateTime.UtcNow.ToString("HH:mm:ss") + "Z  " + s); } }
        static string Snip(string s) { s = Regex.Replace(s, "\\s+", " ").Trim(); return s.Length > 260 ? s.Substring(0, 260) + "..." : s; }

        static string Get(string url)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192); } catch { }
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.Timeout = 8000; rq.ReadWriteTimeout = 8000;
            rq.UserAgent = "N1TA-MOFLOF/1.1 (amateur radio propagation tool; github.com/mfdechristopher/N1TA-MOF-LOF)";
            using (WebResponse rs = rq.GetResponse())
            using (StreamReader sr = new StreamReader(rs.GetResponseStream()))
                return sr.ReadToEnd();
        }
        static bool Num(string s, out double v)
        {
            return double.TryParse(s.Trim().Trim('"', '\''), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        // ---------------- parsers (all total functions) ----------------

        // Last plausible solar flux in the body. Accepts {"Flux":"148"}, {"flux":148}, and [[hdr],[time,148],...].
        public static double ParseFlux(string body)
        {
            double best = double.NaN, v;
            if (string.IsNullOrEmpty(body)) return best;
            foreach (Match m in Regex.Matches(body, "\"[Ff]lux\"\\s*:\\s*\"?(-?[0-9.]+)"))
                if (Num(m.Groups[1].Value, out v) && v >= 50 && v <= 400) best = v;
            if (!double.IsNaN(best)) return best;
            foreach (Match m in Regex.Matches(body, "\\[([^\\[\\]]+)\\]"))
            {
                string[] f = m.Groups[1].Value.Split(',');
                if (f.Length >= 2 && Num(f[1], out v) && v >= 50 && v <= 400) best = v;
            }
            return best;
        }

        // Planetary K feed. Handles objects ({"time_tag":..,"Kp":1.67,"a_running":6,..}) and
        // arrays (["time_tag","Kp","a_running",..] header followed by rows). Nulls are tolerated.
        public static List<KpRow> ParseKp(string body)
        {
            List<KpRow> rows = new List<KpRow>(); double v;
            if (string.IsNullOrEmpty(body)) return rows;
            foreach (Match o in Regex.Matches(body, "\\{[^{}]*\\}"))
            {
                Match k = Regex.Match(o.Value, "\"[Kk]p(?:_index)?\"\\s*:\\s*\"?(-?[0-9.]+)");
                if (!k.Success || !Num(k.Groups[1].Value, out v)) continue;
                KpRow r = new KpRow(); r.Kp = v;
                Match a = Regex.Match(o.Value, "\"a_running\"\\s*:\\s*\"?(-?[0-9.]+)");
                if (a.Success && Num(a.Groups[1].Value, out v) && v >= 0) r.A = v;
                rows.Add(r);
            }
            if (rows.Count > 0) return rows;
            foreach (Match m in Regex.Matches(body, "\\[([^\\[\\]]+)\\]"))
            {
                string[] f = m.Groups[1].Value.Split(',');
                if (f.Length < 2 || !Regex.IsMatch(f[0], "[0-9]{4}-[0-9]{2}") || !Num(f[1], out v)) continue;
                KpRow r = new KpRow(); r.Kp = v;
                if (f.Length > 2 && Num(f[2], out v) && v >= 0) r.A = v;
                rows.Add(r);
            }
            return rows;
        }

        // Kp -> 3-hourly equivalent amplitude "ap" (standard table, linear between entries).
        static readonly double[] KpT = { 0, 0.33, 0.67, 1, 1.33, 1.67, 2, 2.33, 2.67, 3, 3.33, 3.67, 4, 4.33, 4.67, 5, 5.33, 5.67, 6, 6.33, 6.67, 7, 7.33, 7.67, 8, 8.33, 8.67, 9 };
        static readonly double[] ApT = { 0, 2, 3, 4, 5, 6, 7, 9, 12, 15, 18, 22, 27, 32, 39, 48, 56, 67, 80, 94, 111, 132, 154, 179, 207, 236, 300, 400 };
        public static double ApFromKp(double kp)
        {
            if (kp <= 0) return 0; if (kp >= 9) return 400;
            for (int i = 1; i < KpT.Length; i++)
                if (kp <= KpT[i]) return ApT[i - 1] + (kp - KpT[i - 1]) / (KpT[i] - KpT[i - 1]) * (ApT[i] - ApT[i - 1]);
            return 400;
        }
        // Daily-style A: mean of the last 8 three-hourly values (24 h).
        public static double DailyA(List<KpRow> rows)
        {
            double sum = 0; int n = 0;
            for (int i = rows.Count - 1; i >= 0 && n < 8; i--)
            {
                double a = !double.IsNaN(rows[i].A) ? rows[i].A : ApFromKp(rows[i].Kp);
                sum += a; n++;
            }
            return n == 0 ? double.NaN : Math.Round(sum / n, 1);
        }
        public static double LatestKp(List<KpRow> rows) { return rows.Count == 0 ? double.NaN : rows[rows.Count - 1].Kp; }

        public static List<OutlookRow> ParseOutlook(string txt)
        {
            List<OutlookRow> rows = new List<OutlookRow>();
            if (string.IsNullOrEmpty(txt)) return rows;
            foreach (string ln in txt.Split('\n'))
            {
                Match m = Regex.Match(ln.Trim(), "^(\\d{4})\\s+([A-Za-z]{3})\\s+(\\d{1,2})\\s+(\\d+)\\s+(\\d+)\\s+(\\d+)");
                if (!m.Success) continue;
                DateTime dt;
                if (!DateTime.TryParseExact(m.Groups[1].Value + " " + m.Groups[2].Value + " " + m.Groups[3].Value,
                    "yyyy MMM d", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;
                OutlookRow r = new OutlookRow(); r.Date = dt; double v;
                Num(m.Groups[4].Value, out v); r.Sfi = v; Num(m.Groups[5].Value, out v); r.A = v; Num(m.Groups[6].Value, out v); r.K = v;
                rows.Add(r);
            }
            return rows;
        }

        // ---------------- network ----------------
        public static SpaceWx FetchCurrent()
        {
            SpaceWx w = new SpaceWx(); List<string> missing = new List<string>();
            foreach (string url in UrlFlux)
            {
                try
                {
                    string body = Get(url); double f = ParseFlux(body);
                    L("GET " + url + " -> " + body.Length + " bytes, flux=" + (double.IsNaN(f) ? "NOT FOUND  start: " + Snip(body) : f.ToString(CultureInfo.InvariantCulture)));
                    if (!double.IsNaN(f)) { w.Sfi = f; break; }
                }
                catch (Exception e) { L("GET " + url + " -> ERROR " + e.Message); }
            }
            try
            {
                string body = Get(UrlKp); List<KpRow> rows = ParseKp(body);
                double k = LatestKp(rows), a = DailyA(rows);
                L("GET " + UrlKp + " -> " + body.Length + " bytes, " + rows.Count + " rows, K=" + k + " A(24h)=" + a + (rows.Count == 0 ? "  start: " + Snip(body) : ""));
                w.K = k; w.A = a;
            }
            catch (Exception e) { L("GET " + UrlKp + " -> ERROR " + e.Message); }
            if (double.IsNaN(w.Sfi)) missing.Add("SFI");
            if (double.IsNaN(w.K) || double.IsNaN(w.A)) missing.Add("K/A");
            w.Note = missing.Count == 0 ? "ok" : "no " + string.Join(" or ", missing.ToArray());
            return w;
        }
        public static List<OutlookRow> FetchOutlook()
        {
            try
            {
                string body = Get(UrlOutlook); List<OutlookRow> rows = ParseOutlook(body);
                L("GET " + UrlOutlook + " -> " + body.Length + " bytes, " + rows.Count + " day rows" + (rows.Count == 0 ? "  start: " + Snip(body) : ""));
                return rows;
            }
            catch (Exception e) { L("GET " + UrlOutlook + " -> ERROR " + e.Message); return new List<OutlookRow>(); }
        }
        public static string LogText() { lock (Log) { return Log.Length == 0 ? "(nothing fetched yet)" : Log.ToString(); } }
    }
}
