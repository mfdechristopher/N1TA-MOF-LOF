// Callsign -> approximate location, from AD1C's Country Files (cty.dat).
//
// Two sources, in this order:
//   1. a downloaded copy in the settings folder (Log > Update callsign database), if it is present and valid
//   2. the copy built into the program (embedded resource)
// A bad or missing downloaded file silently falls back to the built-in one.
//
// cty.dat gives one point per DXCC entity. For the United States and Canada, one point is far too coarse
// to be useful for propagation, so those two are refined to the centre of the call area (the digit in the
// callsign). Everything else is the entity's own point. This is an approximation and is reported as such.
//
// cty.dat longitudes are positive WEST; this class returns east-positive degrees like the rest of the program.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public class Entity { public string Name = ""; public double Lat, Lon; }

    public class CtyDb
    {
        public Dictionary<string, Entity> Exact = new Dictionary<string, Entity>();
        public Dictionary<string, Entity> Prefix = new Dictionary<string, Entity>();
        public int MaxPrefix = 1, Entities, Version;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        public static CtyDb Build(string text)
        {
            CtyDb db = new CtyDb(); Entity cur = null; StringBuilder list = new StringBuilder();
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (!char.IsWhiteSpace(raw[0]))
                {
                    string[] h = raw.Split(':'); double la, lo;
                    if (h.Length >= 8 && double.TryParse(h[4].Trim(), NumberStyles.Float, CI, out la) && double.TryParse(h[5].Trim(), NumberStyles.Float, CI, out lo))
                    { cur = new Entity(); cur.Name = h[0].Trim(); cur.Lat = la; cur.Lon = -lo; list.Length = 0; db.Entities++; }
                    continue;
                }
                if (cur == null) continue;
                list.Append(raw.Trim());
                if (raw.TrimEnd().EndsWith(";"))
                {
                    foreach (string tok in list.ToString().TrimEnd(';').Split(',')) db.Add(tok.Trim(), cur);
                    list.Length = 0;
                }
            }
            Match v = Regex.Match(text, "=VER(\\d{8})"); if (v.Success) db.Version = int.Parse(v.Groups[1].Value, CI);
            return db;
        }

        void Add(string tok, Entity ent)
        {
            if (tok.Length == 0) return;
            bool isExact = tok[0] == '='; if (isExact) tok = tok.Substring(1);
            Entity e = ent;
            Match ll = Regex.Match(tok, "<\\s*(-?[0-9.]+)\\s*/\\s*(-?[0-9.]+)\\s*>");
            if (ll.Success)
            {
                double la, lo;
                if (double.TryParse(ll.Groups[1].Value, NumberStyles.Float, CI, out la) && double.TryParse(ll.Groups[2].Value, NumberStyles.Float, CI, out lo))
                { e = new Entity(); e.Name = ent.Name; e.Lat = la; e.Lon = -lo; }
            }
            tok = Regex.Replace(tok, "[\\(\\[<\\{~].*$", "").Trim().ToUpperInvariant();
            if (tok.StartsWith("*")) tok = tok.Substring(1);
            if (tok.Length == 0) return;
            if (isExact) Exact[tok] = e; else { Prefix[tok] = e; if (tok.Length > MaxPrefix) MaxPrefix = tok.Length; }
        }

        public Entity Find(string call)
        {
            Entity e; if (Exact.TryGetValue(call, out e)) return e;
            for (int n = Math.Min(call.Length, MaxPrefix); n >= 1; n--) if (Prefix.TryGetValue(call.Substring(0, n), out e)) return e;
            return null;
        }
    }

    public static class CallDb
    {
        static CtyDb cur;
        public static string Source = "built into the program";
        public static string DataDirOverride;          // tests only; normally the settings folder

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

        public static string UserFile
        {
            get
            {
                string d = DataDirOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "N1TA-MOFLOF");
                return Path.Combine(d, "cty.dat");
            }
        }

        public static string BuiltInText()
        {
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("cty.dat"))
            using (StreamReader sr = new StreamReader(st, Encoding.GetEncoding("ISO-8859-1"))) return sr.ReadToEnd();
        }

        static void EnsureLoaded()
        {
            if (cur != null) return;
            try
            {
                if (File.Exists(UserFile))
                {
                    CtyDb db; string problem;
                    if (Validate(File.ReadAllText(UserFile, Encoding.GetEncoding("ISO-8859-1")), out db, out problem)) { cur = db; Source = "your downloaded copy"; return; }
                }
            }
            catch { }
            try { cur = CtyDb.Build(BuiltInText()); Source = "built into the program"; } catch { cur = new CtyDb(); }
        }

        public static void ResetForTest() { cur = null; Source = "built into the program"; }

        public static int EntityCount { get { EnsureLoaded(); return cur.Entities; } }
        public static int Version { get { EnsureLoaded(); return cur.Version; } }

        public static int VersionOf(string text)
        {
            Match v = Regex.Match(text ?? "", "=VER(\\d{8})"); return v.Success ? int.Parse(v.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }
        public static int AgeDays(int version)
        {
            DateTime d; if (!DateTime.TryParseExact(version.ToString(CultureInfo.InvariantCulture), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return -1;
            return (int)(DateTime.UtcNow.Date - d).TotalDays;
        }
        public static string Describe()
        {
            EnsureLoaded(); int age = AgeDays(cur.Version);
            return "Version " + cur.Version + (age >= 0 ? " (" + age + " day" + (age == 1 ? "" : "s") + " old)" : "") + ", " + cur.Entities + " countries and territories.\r\nSource: " + Source + ".";
        }

        // Is this text a usable country file? It is checked before anything is replaced.
        public static bool Validate(string text, out CtyDb db, out string problem)
        {
            db = null; problem = "";
            if (string.IsNullOrEmpty(text) || text.Length < 50000) { problem = "the file is empty or too small to be a country file"; return false; }
            if (text.Length > 8 * 1024 * 1024) { problem = "the file is far larger than a country file"; return false; }
            CtyDb d;
            try { d = CtyDb.Build(text); } catch (Exception e) { problem = "it could not be read (" + e.Message + ")"; return false; }
            if (d.Entities < 300) { problem = "it lists only " + d.Entities + " countries (expected 300 or more)"; return false; }
            if (d.Version <= 0) { problem = "it has no version entry"; return false; }
            // Spot checks: well-known callsigns must resolve to the expected place, or the program's US/Canada
            // refinement (which keys on the entity names) would silently stop working.
            double la, lo; string w;
            if (!Lookup(d, "W1AW", out la, out lo, out w) || !w.StartsWith("United States")) { problem = "W1AW no longer resolves to the United States (the entity names may have changed; this program may need an update)"; return false; }
            if (!Lookup(d, "VE3XYZ", out la, out lo, out w) || !w.StartsWith("Canada")) { problem = "VE3 no longer resolves to Canada (the entity names may have changed; this program may need an update)"; return false; }
            if (!Lookup(d, "JA1ABC", out la, out lo, out w) || !w.Contains("Japan")) { problem = "JA1ABC does not resolve to Japan"; return false; }
            if (!Lookup(d, "DL1ABC", out la, out lo, out w) || !w.Contains("Germany")) { problem = "DL1ABC does not resolve to Germany"; return false; }
            if (!Lookup(d, "VK2ABC", out la, out lo, out w) || !w.Contains("Australia")) { problem = "VK2ABC does not resolve to Australia"; return false; }
            db = d; return true;
        }

        // Validates, then saves the file (via a temp file so a failure cannot leave a half-written copy) and switches to it.
        public static bool Install(string text, out string problem)
        {
            CtyDb db; if (!Validate(text, out db, out problem)) return false;
            try
            {
                string path = UserFile; Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp"; File.WriteAllText(tmp, text, Encoding.GetEncoding("ISO-8859-1"));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e) { problem = "it could not be saved (" + e.Message + ")"; return false; }
            cur = db; Source = "your downloaded copy"; return true;
        }

        // Goes back to the copy built into the program (deletes the downloaded one).
        public static void UseBuiltIn()
        {
            try { if (File.Exists(UserFile)) File.Delete(UserFile); } catch { }
            cur = null; EnsureLoaded();
        }

        public static bool Lookup(string rawCall, out double lat, out double lon, out string where)
        {
            EnsureLoaded(); return Lookup(cur, rawCall, out lat, out lon, out where);
        }

        // Looks a callsign up. Handles /P, /M, /QRP, a digit suffix (W1AW/4), and prefix forms (EA8/DL1ABC).
        static bool Lookup(CtyDb db, string rawCall, out double lat, out double lon, out string where)
        {
            lat = lon = 0; where = "";
            if (string.IsNullOrEmpty(rawCall)) return false;
            string call = rawCall.Trim().ToUpperInvariant();
            Entity whole = null; if (call.Contains("/") && db.Exact.TryGetValue(call, out whole)) { lat = whole.Lat; lon = whole.Lon; where = whole.Name; return true; }

            List<string> parts = new List<string>();
            foreach (string p in call.Split('/')) if (p.Length > 0 && !NoiseSuffix.Contains(p)) parts.Add(p);
            if (parts.Count == 0) return false;
            string digit = null;
            if (parts.Count > 1) { for (int i = 0; i < parts.Count; i++) if (parts[i].Length == 1 && char.IsDigit(parts[i][0])) { digit = parts[i]; parts.RemoveAt(i); break; } }
            string c;
            if (parts.Count >= 2) c = parts[0].Length <= parts[1].Length ? parts[0] : parts[1];   // EA8/DL1ABC: the short part is the prefix
            else c = parts[0];
            if (digit != null) { Match m = Regex.Match(c, "^(.*?)\\d(.*)$"); if (m.Success) c = m.Groups[1].Value + digit + m.Groups[2].Value; }

            Entity ent = db.Find(c); if (ent == null) return false;
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
