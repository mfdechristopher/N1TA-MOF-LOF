// N1TA MOF/LOF - NOAA SWPC data fetch + parse.
// Endpoints are public, no key needed. Parsers are tolerant: if NOAA changes a format
// the program falls back to manual values rather than crashing.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public class SpaceWx
    {
        public double Sfi = double.NaN, A = double.NaN, K = double.NaN;
        public string Note = "";
        public bool Complete { get { return !double.IsNaN(Sfi) && !double.IsNaN(A) && !double.IsNaN(K); } }
    }
    public class OutlookRow { public DateTime Date; public double Sfi, A, K; }

    public static class Noaa
    {
        const string Base = "https://services.swpc.noaa.gov/";
        public const string UrlFlux = Base + "products/summary/10cm-flux.json";
        public const string UrlKp = Base + "products/noaa-planetary-k-index.json";
        public const string UrlDgd = Base + "text/daily-geomagnetic-indices.txt";
        public const string UrlOutlook = Base + "text/27-day-outlook.txt";

        static string Get(string url)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192); } catch { }
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.Timeout = 12000; rq.ReadWriteTimeout = 12000;
            rq.UserAgent = "N1TA-MOFLOF/1.0 (amateur radio propagation tool; github.com/mfdechristopher/N1TA-MOF-LOF)";
            using (WebResponse rs = rq.GetResponse())
            using (StreamReader sr = new StreamReader(rs.GetResponseStream()))
                return sr.ReadToEnd();
        }
        static double D(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }

        // ---- parsers (public so the self-test can hit them) ----
        public static double ParseFlux(string json)
        {
            Match m = Regex.Match(json, "\"Flux\"\\s*:\\s*\"?([0-9.]+)");
            return m.Success ? D(m.Groups[1].Value) : double.NaN;
        }
        // Returns last Kp and a_running. Handles array-of-arrays (current) and array-of-objects (older).
        public static bool ParseKp(string json, out double kp, out double a)
        {
            kp = double.NaN; a = double.NaN;
            MatchCollection rows = Regex.Matches(json, "\\[\\s*\"[0-9T:\\- .Z]+\"\\s*,\\s*\"?([0-9.]+)\"?\\s*,\\s*\"?([0-9.]+)\"?");
            if (rows.Count > 0)
            {
                Match m = rows[rows.Count - 1];
                kp = D(m.Groups[1].Value); a = D(m.Groups[2].Value); return true;
            }
            MatchCollection ks = Regex.Matches(json, "\"[Kk]p(?:_index)?\"\\s*:\\s*\"?([0-9.]+)");
            if (ks.Count > 0) { kp = D(ks[ks.Count - 1].Groups[1].Value); }
            MatchCollection aa = Regex.Matches(json, "\"a_running\"\\s*:\\s*\"?([0-9.]+)");
            if (aa.Count > 0) { a = D(aa[aa.Count - 1].Groups[1].Value); }
            return !double.IsNaN(kp);
        }
        // Daily geomagnetic indices: last row with a valid planetary A. Returns A and last valid Kp slot.
        public static bool ParseDgd(string txt, out double a, out double kp)
        {
            a = double.NaN; kp = double.NaN;
            string[] lines = txt.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string ln = lines[i].Trim();
                if (!Regex.IsMatch(ln, "^\\d{4}\\s+\\d{2}\\s+\\d{2}\\s")) continue;
                string[] t = Regex.Split(ln, "\\s+");
                if (t.Length < 30) continue;
                int pa = int.Parse(t[21]);
                if (pa < 0) continue;
                a = pa;
                for (int k = 29; k >= 22; k--) { int kv = int.Parse(t[k]); if (kv >= 0) { kp = kv; break; } }
                return true;
            }
            return false;
        }
        public static List<OutlookRow> ParseOutlook(string txt)
        {
            List<OutlookRow> rows = new List<OutlookRow>();
            foreach (string ln in txt.Split('\n'))
            {
                Match m = Regex.Match(ln.Trim(), "^(\\d{4})\\s+([A-Za-z]{3})\\s+(\\d{1,2})\\s+(\\d+)\\s+(\\d+)\\s+(\\d+)");
                if (!m.Success) continue;
                DateTime dt;
                if (!DateTime.TryParseExact(m.Groups[1].Value + " " + m.Groups[2].Value + " " + m.Groups[3].Value,
                    "yyyy MMM d", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;
                OutlookRow r = new OutlookRow();
                r.Date = dt; r.Sfi = D(m.Groups[4].Value); r.A = D(m.Groups[5].Value); r.K = D(m.Groups[6].Value);
                rows.Add(r);
            }
            return rows;
        }

        // ---- network ----
        public static SpaceWx FetchCurrent()
        {
            SpaceWx w = new SpaceWx();
            List<string> errs = new List<string>();
            try { w.Sfi = ParseFlux(Get(UrlFlux)); } catch (Exception e) { errs.Add("flux: " + e.Message); }
            double kp = double.NaN, a1 = double.NaN;
            try { ParseKp(Get(UrlKp), out kp, out a1); } catch (Exception e) { errs.Add("Kp: " + e.Message); }
            double a2 = double.NaN, k2 = double.NaN;
            try { ParseDgd(Get(UrlDgd), out a2, out k2); } catch (Exception e) { errs.Add("A: " + e.Message); }
            w.K = !double.IsNaN(kp) ? kp : k2;
            w.A = !double.IsNaN(a2) ? a2 : a1;
            w.Note = errs.Count == 0 ? "NOAA SWPC" : string.Join("; ", errs.ToArray());
            return w;
        }
        public static List<OutlookRow> FetchOutlook()
        {
            return ParseOutlook(Get(UrlOutlook));
        }
    }
}
