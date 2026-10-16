// Maidenhead locator <-> latitude/longitude (east-positive degrees).
using System;
using System.Text.RegularExpressions;

namespace N1TA.MofLof
{
    public static class Grid
    {
        static readonly Regex Rx = new Regex("^[A-Ra-r]{2}[0-9]{2}([A-Xa-x]{2}([0-9]{2})?)?$");

        public static bool TryParse(string s, out double lat, out double lon)
        {
            lat = lon = 0;
            if (s == null) return false;
            s = s.Trim(); if (!Rx.IsMatch(s)) return false;
            s = s.ToUpperInvariant();
            lon = -180 + (s[0] - 'A') * 20 + (s[2] - '0') * 2;
            lat = -90 + (s[1] - 'A') * 10 + (s[3] - '0');
            if (s.Length == 4) { lon += 1; lat += 0.5; return true; }
            double sl = 5.0 / 60, sa = 2.5 / 60;
            lon += (s[4] - 'A') * sl; lat += (s[5] - 'A') * sa;
            if (s.Length == 6) { lon += sl / 2; lat += sa / 2; return true; }
            double el = sl / 10, ea = sa / 10;
            lon += (s[6] - '0') * el; lat += (s[7] - '0') * ea; lon += el / 2; lat += ea / 2;
            return true;
        }

        public static string From(double lat, double lon, int chars)
        {
            double x = lon + 180, y = lat + 90;
            if (x < 0) x = 0; if (x >= 360) x = 359.99999; if (y < 0) y = 0; if (y >= 180) y = 179.99999;
            string g = "" + (char)('A' + (int)(x / 20)) + (char)('A' + (int)(y / 10)) + (char)('0' + (int)((x % 20) / 2)) + (char)('0' + (int)(y % 10));
            if (chars <= 4) return g;
            double lx = (x % 2) * 12, ly = (y % 1) * 24;
            return g + (char)('a' + (int)lx) + (char)('a' + (int)ly);
        }

        // 'FN32pb' normal casing: field letters upper, subsquare lower.
        public static string Normalize(string s)
        {
            s = s.Trim(); if (s.Length < 4) return s;
            string r = s.Substring(0, 2).ToUpperInvariant() + s.Substring(2, 2);
            if (s.Length >= 6) r += s.Substring(4, 2).ToLowerInvariant();
            if (s.Length >= 8) r += s.Substring(6, 2);
            return r;
        }
    }
}
