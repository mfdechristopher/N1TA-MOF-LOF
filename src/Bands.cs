// Band tables, the original blue/green/red colour scheme, and the open/marginal/closed judgement.
//
// A band at frequency f is judged on one path:
//   Closed   : above the MOF (nothing reflects) or below the LOF (D layer eats it)
//   Marginal : inside that window but within 15% of either edge
//   Open     : comfortably inside the window
using System;
using System.Drawing;

namespace N1TA.MofLof
{
    public enum BandState { Closed = 0, Marginal = 1, Open = 2 }

    public static class Bands
    {
        // every HF ham band
        public static readonly string[] Name = { "160", "80", "60", "40", "30", "20", "17", "15", "12", "10" };
        public static readonly double[] Lo = { 1.8, 3.5, 5.25, 7.0, 10.1, 14.0, 18.068, 21.0, 24.89, 28.0 };
        public static readonly double[] Hi = { 2.0, 4.0, 5.45, 7.3, 10.15, 14.35, 18.168, 21.45, 24.99, 29.7 };
        public const double Margin = 0.15;

        public static int IndexOf(double mhz)
        {
            for (int i = 0; i < Lo.Length; i++) if (mhz >= Lo[i] - 0.001 && mhz <= Hi[i] + 0.001) return i;
            return -1;
        }
        public static double Center(string bandName)
        {
            for (int i = 0; i < Name.Length; i++) if (Name[i] == bandName) return (Lo[i] + Hi[i]) / 2;
            return double.NaN;
        }
        public static string Label(double mhz)
        {
            int i = IndexOf(mhz);
            return i >= 0 ? Name[i] + " M" : mhz.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " MHz";
        }
        // The F2 model is meaningful from about 1.7 to 30 MHz.
        public static bool Modelled(double mhz) { return mhz >= 1.7 && mhz <= 30.0; }

        // ---- the original colour scheme ----
        // MOF map: 20 m blue, 15 m green, 10 m red. LOF map: 40 m blue, 80 m green, 160 m red.
        // So 20+40 = blue, 15+80 = green, 10+160 = red. Every other frequency (30, 17, 12, 60 m ...)
        // takes the colour of the nearest of those six bands, by log distance.
        static readonly double[] Six = { 1.85, 3.6, 7.1, 14.15, 21.2, 28.5 };
        static readonly int[] GroupOf = { 2, 1, 0, 0, 1, 2 };
        public static int ColorGroup(double mhz)
        {
            int best = 0; double bd = double.MaxValue;
            for (int i = 0; i < Six.Length; i++) { double d = Math.Abs(Math.Log(mhz / Six[i])); if (d < bd) { bd = d; best = i; } }
            return GroupOf[best];
        }
        public static readonly Color[] Full = { Color.FromArgb(0x28, 0x46, 0xFF), Color.FromArgb(0, 0xBE, 0), Color.FromArgb(0xD2, 0, 0) };
        public static Color ColorOf(double mhz) { return Full[ColorGroup(mhz)]; }

        // ---- judgement ----
        // MINIMUF clips at 32 MHz, so a reading of 32 means "at least 32".
        static double Eff(double mof) { return mof >= 31.99 ? 1e9 : mof; }

        public static BandState State(double mof, double lof, double f)
        {
            double m = Eff(mof);
            if (f > m || f < lof) return BandState.Closed;
            if (f > (1 - Margin) * m || f < (1 + Margin) * lof) return BandState.Marginal;
            return BandState.Open;
        }

        // Best of the short and long path. Returns that path's state and its MOF/LOF.
        public static BandState Eval(double mofS, double lofS, double mofL, double lofL, double f, out bool longPath, out double mof, out double lof)
        {
            BandState s = State(mofS, lofS, f), l = State(mofL, lofL, f);
            longPath = l > s; mof = longPath ? mofL : mofS; lof = longPath ? lofL : lofS;
            return longPath ? l : s;
        }

        // How far outside the window a Closed band is (1.0 = on the edge, 2.0 = twice the MOF, etc).
        public static double MissFactor(double mof, double lof, double f)
        {
            if (f > Eff(mof)) return f / mof;
            if (f < lof) return lof / f;
            return 1.0;
        }
    }
}
