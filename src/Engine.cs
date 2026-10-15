// N1TA MOF/LOF - propagation engine
//
// MOF: MINIMUF 3.5 (Rose & Martin, NOSC TD 201, 1978 - US Navy, public release),
//      re-implemented from the published equations. NOT a copy of K1TTT's code.
// LOF: simple D-layer absorption model (George-style, see README). Heuristic.
// Storm response from A/K: N1TA heuristic, see README. Not from the original.
using System;

namespace N1TA.MofLof
{
    public class Cond
    {
        public double Sfi = 120, A = 8, K = 2;
        public int Month = 1, Day = 1;
        public double Hour = 0;          // UTC, fractional hours
        public double Ssn, Ke;           // derived
        public void Derive()
        {
            Ssn = Prop.SsnFromFlux(Sfi);
            double ka = Prop.KFromA(A);
            Ke = 0.6 * K + 0.4 * ka;
        }
    }

    public static class Prop
    {
        public const double R = 6371.0;
        const double PI = Math.PI, PIH = PI / 2, PID = PI * 2;
        public const double LofThresholdDb = 52.0;

        static double Sgn(double x) { return x == 0 ? 0 : (x > 0 ? 1 : -1); }
        static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static double Rad(double d) { return d * PI / 180.0; }
        public static double Deg(double r) { return r * 180.0 / PI; }

        // Inverse of the Steward/Leftin relation quoted in NOSC TR 1121 eq. 29:
        // flux = 63.7 + 0.728 R + 8.9e-4 R^2
        public static double SsnFromFlux(double f)
        {
            if (f <= 63.7) return 0;
            double a = 8.9e-4, b = 0.728, c = 63.7 - f;
            double r = (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            return Clamp(r, 0, 300);
        }

        // Planetary A -> equivalent K (standard Ap/Kp table, interpolated)
        static readonly double[] ApTab = { 0, 3, 7, 15, 27, 48, 80, 132, 208, 400 };
        public static double KFromA(double a)
        {
            if (a <= 0) return 0;
            for (int i = 1; i < ApTab.Length; i++)
                if (a <= ApTab[i]) return (i - 1) + (a - ApTab[i - 1]) / (ApTab[i] - ApTab[i - 1]);
            return 9;
        }

        // ---------- spherical helpers (lon east-positive, radians) ----------
        public static double Dist(double la1, double lo1, double la2, double lo2)
        {
            double c = Math.Sin(la1) * Math.Sin(la2) + Math.Cos(la1) * Math.Cos(la2) * Math.Cos(lo2 - lo1);
            return Math.Acos(Clamp(c, -1, 1));
        }
        public static double Bearing(double la1, double lo1, double la2, double lo2)
        {
            double y = Math.Sin(lo2 - lo1) * Math.Cos(la2);
            double x = Math.Cos(la1) * Math.Sin(la2) - Math.Sin(la1) * Math.Cos(la2) * Math.Cos(lo2 - lo1);
            if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12) return 0;
            return Math.Atan2(y, x);
        }
        public static void Dest(double la1, double lo1, double brg, double d, out double la2, out double lo2)
        {
            double s = Math.Sin(la1) * Math.Cos(d) + Math.Cos(la1) * Math.Sin(d) * Math.Cos(brg);
            la2 = Math.Asin(Clamp(s, -1, 1));
            lo2 = lo1 + Math.Atan2(Math.Sin(brg) * Math.Sin(d) * Math.Cos(la1),
                                   Math.Cos(d) - Math.Sin(la1) * Math.Sin(la2));
            lo2 = Math.IEEERemainder(lo2, PID);
        }

        // Geomagnetic latitude (dipole), NOSC TR 1121 eq. 17. Longitude west-positive inside.
        public static double MagLat(double lat, double lonE)
        {
            double w = -lonE;
            double s = 0.9792 * Math.Sin(lat) + 0.2028 * Math.Cos(lat) * Math.Cos(w - 1.2043);
            return Math.Asin(Clamp(s, -1, 1));
        }

        // Solar quantities for a given date. y2 = -declination (as in the Navy code).
        static void SolarDay(Cond c, out double y1, out double y2)
        {
            y1 = 0.0172 * (10.0 + (c.Month - 1) * 30.4 + c.Day);
            y2 = 0.409 * Math.Cos(y1);
        }
        // UT (hours) of local solar noon at lonE, with equation-of-time term.
        static double NoonUt(double lonE, double y1)
        {
            double w = Math.IEEERemainder(-lonE, PID); if (w < 0) w += PID;
            double k8 = 3.82 * w + 12.0 + 0.13 * (Math.Sin(y1) + 1.2 * Math.Sin(2 * y1));
            if (k8 > 24) k8 -= 24;
            return k8;
        }

        static double StormMofFactor(double lat, double lonE, double ke)
        {
            if (ke <= 3) return 1.0;
            double ml = Math.Abs(Deg(MagLat(lat, lonE)));
            double t = Clamp((ml - 30.0) / 30.0, 0, 1);
            double sm = t * t * (3 - 2 * t);
            return 1.0 - 0.035 * (ke - 3.0) * sm;
        }

        // ---------- MOF ----------
        // F2 MUF at one control point. hop = angular length of one hop (rad).
        public static double ControlMuf(double lat, double lonE, double hop, double tePathFactor, Cond c)
        {
            double y1, y2; SolarDay(c, out y1, out y2);
            double k8 = NoonUt(lonE, y1), hour = c.Hour;
            double k9, g0;

            if (Math.Cos(lat + y2) <= -0.26) { k9 = 0; g0 = 0; }
            else
            {
                double f = (-0.26 + Math.Sin(y2) * Math.Sin(lat)) / (Math.Cos(y2) * Math.Cos(lat) + 0.001);
                k9 = 12.0 - Math.Atan(f / Math.Sqrt(Math.Abs(1 - f * f))) * 7.639437;
                double ts = k8 - k9 / 2 + 12.0 * (1 - Sgn(k8 - k9 / 2)) * Sgn(Math.Abs(k8 - k9 / 2));
                double te = k8 + k9 / 2 - 12.0 * (1 + Sgn(k8 + k9 / 2 - 24)) * Sgn(Math.Abs(k8 + k9 / 2 - 24));
                double c0 = Math.Abs(Math.Cos(lat + y2));
                double t9 = 9.7 * Math.Pow(c0, 9.6); if (t9 < 0.1) t9 = 0.1;
                double g8 = PI * t9 / k9;
                bool night = (te < ts && (hour - te) * (ts - hour) > 0) ||
                             (te >= ts && (hour - ts) * (te - hour) <= 0);
                if (night)
                {
                    double h = hour + 12.0 * (1 + Sgn(te - hour)) * Sgn(Math.Abs(te - hour));
                    g0 = c0 * (g8 * (Math.Exp(-k9 / t9) + 1)) * Math.Exp((te - h) / 2) / (1 + g8 * g8);
                }
                else
                {
                    double h = hour + 12.0 * (1 + Sgn(ts - hour)) * Sgn(Math.Abs(ts - hour));
                    double ph = PI * (h - ts) / k9;
                    g0 = c0 * (Math.Sin(ph) + g8 * (Math.Exp((ts - h) / t9) - Math.Cos(ph))) / (1 + g8 * g8);
                    double floor = c0 * (g8 * (Math.Exp(-k9 / t9) + 1)) * Math.Exp((k9 - 24) / 2) / (1 + g8 * g8);
                    if (g0 < floor) g0 = floor;
                }
            }
            if (g0 < 0) g0 = 0;

            double s = Math.Sin(Math.Min(2.5 * hop, PIH));
            double m = 1.0 + 2.5 * s * Math.Sqrt(s);
            double muf = (1 + c.Ssn / 250.0) * m * Math.Sqrt(6.0 + 58.0 * Math.Sqrt(g0));
            muf *= 1.0 - 0.1 * Math.Exp((k9 - 24) / 3.0);
            muf *= tePathFactor;
            muf *= 1.0 - 0.1 * (1 + Sgn(Math.Abs(Math.Sin(lat)) - Math.Cos(lat)));
            muf *= StormMofFactor(lat, lonE, c.Ke);
            return muf;
        }

        public static double Clip(double v) { return Clamp(v, 2.0, 32.0); }

        // Path MOF, short or long way round.
        public static double PathMof(double la1, double lo1, double la2, double lo2, bool longPath, Cond c)
        {
            double d = Dist(la1, lo1, la2, lo2);
            double arc = longPath ? PID - d : d;
            double brg = Bearing(la1, lo1, la2, lo2) + (longPath ? PI : 0);
            double k6 = 1.59 * arc; if (k6 < 1) k6 = 1;
            double hop = k6 > 1 ? 0.5 * arc : arc;
            double te = 1.0 + 0.1 * (1.0 - Sgn(la1) * Sgn(la2));
            double muf = 100;
            double[] ks = k6 > 1 ? new double[] { 1.0 / (2 * k6), 1.0 - 1.0 / (2 * k6) } : new double[] { 0.5 };
            foreach (double k1 in ks)
            {
                double la, lo; Dest(la1, lo1, brg, arc * k1, out la, out lo);
                double m = ControlMuf(la, lo, hop, te, c);
                if (m < muf) muf = m;
            }
            return Clip(muf);
        }

        // 3000 km single hop centred on a point
        public static double HopMof(double lat, double lonE, Cond c)
        {
            return Clip(ControlMuf(lat, lonE, 3000.0 / R, 1.0, c));
        }

        // ---------- LOF ----------
        static double AbsIndex(double lat, double lonE, Cond c, double y1, double y2)
        {
            double decl = -y2;
            double h = (c.Hour - NoonUt(lonE, y1)) * PI / 12.0;
            double cz = Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(h);
            double chi = Math.Acos(Clamp(cz, -1, 1));
            double term = 0;
            double arg = 0.881 * chi;
            if (arg < PIH) term = Math.Pow(Math.Cos(arg), 1.3);
            double idx = (1 + 0.0067 * c.Ssn) * Math.Max(term, 0.015);
            double ml = Math.Abs(Deg(MagLat(lat, lonE)));
            double w = 1.0 / (1.0 + Math.Exp(-(ml - (68.0 - 2.5 * c.Ke)) / 3.0));
            idx += (0.04 + 0.05 * c.Ke) * w;
            return idx;
        }
        static double FL(double lat, double lonE)
        {
            double ml = MagLat(lat, lonE);
            return 0.75 * 0.7578 * Math.Sqrt(1 + 3 * Math.Sin(ml) * Math.Sin(ml));
        }
        static double SecPhi(double hopAng)
        {
            double half = hopAng / 2, rf = R / (R + 300.0);
            double beta = (half < 1e-6) ? PIH : Math.Atan2(Math.Cos(half) - rf, Math.Sin(half));
            if (beta < Rad(3)) beta = Rad(3);
            double sp = R / (R + 100.0) * Math.Cos(beta);
            return 1.0 / Math.Sqrt(1 - sp * sp);
        }
        static double SolveLof(double[] w, double[] fl)
        {
            // w[i] = 677.2 * I * sec ; loss(f) = sum w/((f+fl)^1.98+10.2)
            Func<double, double> loss = delegate (double f)
            {
                double s = 0;
                for (int i = 0; i < w.Length; i++) s += w[i] / (Math.Pow(f + fl[i], 1.98) + 10.2);
                return s;
            };
            if (loss(1.0) <= LofThresholdDb) return 1.0;
            if (loss(40.0) > LofThresholdDb) return 40.0;
            double lo = 1.0, hi = 40.0;
            for (int i = 0; i < 28; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (loss(mid) > LofThresholdDb) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        public static double PathLof(double la1, double lo1, double la2, double lo2, bool longPath, Cond c)
        {
            double y1, y2; SolarDay(c, out y1, out y2);
            double d = Dist(la1, lo1, la2, lo2);
            double arc = longPath ? PID - d : d;
            double brg = Bearing(la1, lo1, la2, lo2) + (longPath ? PI : 0);
            int n = (int)Math.Ceiling(arc * R / 4000.0); if (n < 1) n = 1;
            double hopAng = arc / n, sec = SecPhi(hopAng);
            double[] w = new double[n], fl = new double[n];
            for (int i = 0; i < n; i++)
            {
                double la, lo; Dest(la1, lo1, brg, arc * (i + 0.5) / n, out la, out lo);
                w[i] = 677.2 * AbsIndex(la, lo, c, y1, y2) * sec;
                fl[i] = FL(la, lo);
            }
            return SolveLof(w, fl);
        }

        public static double HopLof(double lat, double lonE, Cond c)
        {
            double y1, y2; SolarDay(c, out y1, out y2);
            double[] w = { 677.2 * AbsIndex(lat, lonE, c, y1, y2) * SecPhi(3000.0 / R) };
            double[] fl = { FL(lat, lonE) };
            return SolveLof(w, fl);
        }

        // ---------- Sun ----------
        public static void SubSolar(Cond c, out double latDeg, out double lonDeg)
        {
            double y1, y2; SolarDay(c, out y1, out y2);
            latDeg = Deg(-y2);
            // noon UT at lon: 12 - lon*12/pi + eqt  =>  lon at which noon == Hour
            double eqt = 0.13 * (Math.Sin(y1) + 1.2 * Math.Sin(2 * y1));
            double lon = (12.0 + eqt - c.Hour) * 15.0;
            lonDeg = ((lon + 540) % 360) - 180;
        }
        // Latitude (rad) of the terminator at lonE, and whether it is a sunrise line there.
        public static double TermLat(Cond c, double lonE, out bool sunrise)
        {
            double y1, y2; SolarDay(c, out y1, out y2);
            double decl = -y2; if (Math.Abs(decl) < 0.0005) decl = decl < 0 ? -0.0005 : 0.0005;
            double h = (c.Hour - (12.0 - lonE * 12.0 / PI + 0.13 * (Math.Sin(y1) + 1.2 * Math.Sin(2 * y1)))) * PI / 12.0;
            sunrise = Math.Sin(h) < 0;
            return Math.Atan(-Math.Cos(h) / Math.Tan(decl));
        }
    }
}
