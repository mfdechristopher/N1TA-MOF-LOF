// Callsign -> approximate location, from AD1C's Country Files (cty.dat, embedded as a resource).
//
// cty.dat gives one point per DXCC entity. For the United States and Canada, one point is far too coarse
// to be useful for propagation, so those two are refined to the centre of the call area (the digit in the
// callsign). Everything else is the entity's own point. This is an approximation and is reported as such.
//
// cty.dat longitudes are positive WEST; this class returns east-positive degrees like the rest of the program.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public class Entity { public string Name = ""; public double Lat, Lon; }

    public static class CallDb
    {
        static Dictionary<string, Entity> exact = new Dictionary<string, Entity>();
        static Dictionary<string, Entity> prefix = new Dictionary<string, Entity>();
        static int maxPrefix = 1; static bool loaded;
        public static int EntityCount, Version;

        static readonly HashSet<string> NoiseSuffix = new HashSet<string> { "P", "M", "MM", "AM", "QRP", "QRPP", "A", "B", "LH", "R", "T", "SK" };

        // US call areas 0-9 (approximate centre of each area)
        static readonly double[][] UsArea = {
            new double[] { 41.5, -97.0 }, new double[] { 43.0, -71.5 }, new double[] { 41.3, -74.3 }, new double[] { 40.0, -76.5 },
            new double[] { 34.0, -83.0 }, new double[] { 32.0, -97.0 }, new double[] { 36.5, -120.0 }, new double[] { 43.5, -114.0 },
            new double[] { 41.0, -83.0 }, new double[] { 41.5, -88.5 } };
        // Canadian call areas 0-9
        static readonly double[][] CaArea = {
            new double[] { 65.0, -95.0 }, new double[] { 45.0, -63.0 }, new double[] { 47.0, -72.0 }, new double[] { 45.0, -80.0 },
            new double[] { 50.0, -98.0 }, new double[] { 52.0, -106.0 }, new double[] { 53.0, -114.0 }, new double[] { 52.0, -124.0 },
            new double[] { 65.0, -115.0 }, new double[] { 46.5, -66.5 } };

        static void EnsureLoaded()
        {
            if (loaded) return; loaded = true;
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("cty.dat"))
                using (StreamReader sr = new StreamReader(st)) Parse(sr.ReadToEnd());
            }
            catch { }
        }

        public static void Parse(string text)
        {
            exact = new Dictionary<string, Entity>(); prefix = new Dictionary<string, Entity>(); maxPrefix = 1; EntityCount = 0; loaded = true;
            Entity cur = null; System.Text.StringBuilder list = new System.Text.StringBuilder();
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (!char.IsWhiteSpace(raw[0]))
                {
                    string[] h = raw.Split(':');
                    double la, lo;
                    if (h.Length >= 8 && double.TryParse(h[4].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out la)
                        && double.TryParse(h[5].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lo))
                    { cur = new Entity(); cur.Name = h[0].Trim(); cur.Lat = la; cur.Lon = -lo; list.Length = 0; EntityCount++; }
                    continue;
                }
                if (cur == null) continue;
                list.Append(raw.Trim());
                if (raw.TrimEnd().EndsWith(";"))
                {
                    string s = list.ToString().TrimEnd(';'); list.Length = 0;
                    foreach (string tok in s.Split(',')) AddToken(tok.Trim(), cur);
                }
            }
            Match v = Regex.Match(text, "=VER(\\d{8})"); if (v.Success) Version = int.Parse(v.Groups[1].Value);
        }

        static void AddToken(string tok, Entity ent)
        {
            if (tok.Length == 0) return;
            bool isExact = tok[0] == '='; if (isExact) tok = tok.Substring(1);
            Entity e = ent;
            Match ll = Regex.Match(tok, "<\\s*(-?[0-9.]+)\\s*/\\s*(-?[0-9.]+)\\s*>");
            if (ll.Success)
            {
                double la, lo;
                if (double.TryParse(ll.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out la) &&
                    double.TryParse(ll.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lo))
                { e = new Entity(); e.Name = ent.Name; e.Lat = la; e.Lon = -lo; }
            }
            tok = Regex.Replace(tok, "[\\(\\[<\\{~].*$", "").Trim().ToUpperInvariant();
            if (tok.StartsWith("*")) tok = tok.Substring(1);
            if (tok.Length == 0) return;
            if (isExact) exact[tok] = e; else { prefix[tok] = e; if (tok.Length > maxPrefix) maxPrefix = tok.Length; }
        }

        static Entity Find(string call)
        {
            Entity e; if (exact.TryGetValue(call, out e)) return e;
            for (int n = Math.Min(call.Length, maxPrefix); n >= 1; n--) if (prefix.TryGetValue(call.Substring(0, n), out e)) return e;
            return null;
        }

        // Looks a callsign up. Handles /P, /M, /QRP, a digit suffix (W1AW/4), and prefix forms (EA8/DL1ABC).
        public static bool Lookup(string rawCall, out double lat, out double lon, out string where)
        {
            EnsureLoaded(); lat = lon = 0; where = "";
            if (string.IsNullOrEmpty(rawCall)) return false;
            string call = rawCall.Trim().ToUpperInvariant();
            Entity whole = null; if (call.Contains("/") && exact.TryGetValue(call, out whole)) { lat = whole.Lat; lon = whole.Lon; where = whole.Name; return true; }

            List<string> parts = new List<string>();
            foreach (string p in call.Split('/')) if (p.Length > 0 && !NoiseSuffix.Contains(p)) parts.Add(p);
            if (parts.Count == 0) return false;
            string digit = null;
            if (parts.Count > 1) { for (int i = 0; i < parts.Count; i++) if (parts[i].Length == 1 && char.IsDigit(parts[i][0])) { digit = parts[i]; parts.RemoveAt(i); break; } }
            string c;
            if (parts.Count >= 2) c = parts[0].Length <= parts[1].Length ? parts[0] : parts[1];   // EA8/DL1ABC: the short part is the prefix
            else c = parts[0];
            if (digit != null) { Match m = Regex.Match(c, "^(.*?)\\d(.*)$"); if (m.Success) c = m.Groups[1].Value + digit + m.Groups[2].Value; }

            Entity ent = Find(c); if (ent == null) return false;
            lat = ent.Lat; lon = ent.Lon; where = ent.Name;
            Match d = Regex.Match(c, "\\d");
            if (d.Success)
            {
                int area = c[d.Index] - '0';
                if (ent.Name == "United States") { lat = UsArea[area][0]; lon = UsArea[area][1]; where = "United States (call area " + area + ")"; }
                else if (ent.Name == "Canada")
                {
                    lat = CaArea[area][0]; lon = CaArea[area][1]; where = "Canada (call area " + area + ")";
                    if (c.StartsWith("VO")) { if (area == 2) { lat = 53.5; lon = -61.0; } else { lat = 48.5; lon = -56.0; } }
                    else if (c.StartsWith("VY") && area == 1) { lat = 63.0; lon = -135.0; }
                }
            }
            return true;
        }
    }
}
