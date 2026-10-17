// "Best Band" decision logic. Pure functions, no UI.
//
// A band is judged on one frequency (its usual contest operating frequency) per path:
//   Closed   : above MOF (nothing reflects) or below LOF (D layer eats it)
//   Marginal : inside the window but within 15% of either edge (flutter / weak)
//   Open     : comfortably inside the window
// Best band = the HIGHEST Open band (the contest rule of thumb: higher is quieter and has
// less absorption). If nothing is Open, the highest Marginal one, else none.
using System;
using System.Collections.Generic;

namespace N1TA.MofLof
{
    public enum BandState { Closed = 0, Marginal = 1, Open = 2 }

    public class Region { public string Code, Name; public double Lat, Lon; public Region(string c, string n, double la, double lo) { Code = c; Name = n; Lat = la; Lon = lo; } }

    public static class Bands
    {
        public static readonly string[] Name = { "160", "80", "40", "20", "15", "10" };
        public static readonly double[] Freq = { 1.85, 3.6, 7.1, 14.15, 21.2, 28.5 };
        public const double Margin = 0.15;
        public const double MinRegionKm = 1500;      // closer than this is local/NVIS territory, not DX

        public static readonly Region[] Regions = {
            new Region("EU-W", "Western Europe", 48.5, 6), new Region("EU-E", "Eastern Europe", 52, 30),
            new Region("AF-N", "North Africa", 30, 31), new Region("AF-S", "Southern Africa", -26, 28),
            new Region("ME", "Middle East", 30, 48), new Region("AS-C", "India", 22, 78),
            new Region("JA", "Japan", 36, 138), new Region("CN", "China", 32, 114), new Region("SEA", "SE Asia", 13, 101),
            new Region("VK", "Australia", -33, 150), new Region("ZL", "New Zealand", -40, 175), new Region("KH6", "Hawaii", 21, -157),
            new Region("PY", "Brazil", -23, -46), new Region("LU", "Argentina", -34, -60), new Region("SA-W", "Andes / Peru", -12, -77),
            new Region("W-E", "US East", 40, -75), new Region("W-W", "US West", 37, -120), new Region("CAR", "Caribbean", 18, -68) };

        // ---- extended table: every HF ham band, used when following a radio or picking a single band ----
        public static readonly string[] ExtName = { "160", "80", "60", "40", "30", "20", "17", "15", "12", "10" };
        public static readonly double[] ExtLo = { 1.8, 3.5, 5.25, 7.0, 10.1, 14.0, 18.068, 21.0, 24.89, 28.0 };
        public static readonly double[] ExtHi = { 2.0, 4.0, 5.45, 7.3, 10.15, 14.35, 18.168, 21.45, 24.99, 29.7 };
        public static readonly double[] ExtCenter = { 1.85, 3.6, 5.35, 7.1, 10.12, 14.15, 18.1, 21.2, 24.94, 28.5 };

        // Index into the Ext* tables, or -1 if the frequency is not inside a ham band.
        public static int ExtIndex(double mhz)
        {
            for (int i = 0; i < ExtLo.Length; i++) if (mhz >= ExtLo[i] - 0.001 && mhz <= ExtHi[i] + 0.001) return i;
            return -1;
        }
        // Index into the six contest bands (Name/Freq), or -1.
        public static int ContestIndex(double mhz)
        {
            int e = ExtIndex(mhz); if (e < 0) return -1;
            for (int i = 0; i < Name.Length; i++) if (Name[i] == ExtName[e]) return i;
            return -1;
        }
        // The F2 model is meaningful from about 1.7 to 30 MHz. 6 m and up (sporadic E etc.) is not modelled.
        public static bool Modelled(double mhz) { return mhz >= 1.7 && mhz <= 30.0; }
        public static string Label(double mhz)
        {
            int e = ExtIndex(mhz);
            if (e >= 0) return ExtName[e] + " M";
            if (mhz >= 50 && mhz <= 54) return "6 M";
            return mhz.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " MHz";
        }
        public static string Describe(double mhz)
        {
            return Label(mhz) + " (" + mhz.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " MHz)";
        }

        // MINIMUF clips at 32 MHz, so a reading of 32 means "at least 32".
        static double Eff(double mof) { return mof >= 31.99 ? 1e9 : mof; }

        public static BandState State(double mof, double lof, double f)
        {
            double m = Eff(mof);
            if (f > m || f < lof) return BandState.Closed;
            if (f > (1 - Margin) * m || f < (1 + Margin) * lof) return BandState.Marginal;
            return BandState.Open;
        }

        // Best of short and long path for one band.
        public static BandState Eval(double mofS, double lofS, double mofL, double lofL, double f, out bool longPath)
        {
            BandState s = State(mofS, lofS, f), l = State(mofL, lofL, f);
            longPath = l > s; return longPath ? l : s;
        }

        // Highest Open band; else highest Marginal; else -1.
        public static int Best(BandState[] st)
        {
            for (int i = st.Length - 1; i >= 0; i--) if (st[i] == BandState.Open) return i;
            for (int i = st.Length - 1; i >= 0; i--) if (st[i] == BandState.Marginal) return i;
            return -1;
        }

        public static int NeededRegions(int n) { return Math.Max(2, (int)Math.Ceiling(n / 3.0)); }

        // Summary over regions: highest band that is Open to at least a third of them (min 2).
        // Falls back to the highest band Open to any region, then Marginal to any.
        public static int BestByCount(int[] open, int[] any, int n, out bool strong)
        {
            int need = NeededRegions(n); strong = false;
            for (int b = open.Length - 1; b >= 0; b--) if (open[b] >= need) { strong = true; return b; }
            for (int b = open.Length - 1; b >= 0; b--) if (open[b] >= 1) return b;
            for (int b = any.Length - 1; b >= 0; b--) if (any[b] >= 1) return b;
            return -1;
        }
    }
}
