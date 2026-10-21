// Look-back analysis: for every QSO in a log, what did the prediction say about that band, at that time, on that path?
//
// Important limit, stated in the report too: a completed QSO proves a band was open. It cannot prove that the
// bands you did NOT work were closed. So this measures "misses" (the model said closed, but you worked it),
// not false alarms.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace N1TA.MofLof
{
    public static class LogAnalysis
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        const double Km2Mi = 0.621371;
        public const double NearKm = 1000;      // closer than this is ground wave / NVIS, which an F2 model does not describe

        public static void Evaluate(LogData log, double qthLat, double qthLon, double sfi, double a, double k)
        {
            double la1 = Prop.Rad(qthLat), lo1 = Prop.Rad(qthLon); log.TooClose = 0;
            foreach (Qso q in log.Qsos)
            {
                q.Evaluated = false;
                if (!q.Located || !Bands.Modelled(q.Mhz)) continue;
                Cond c = new Cond(); c.Sfi = sfi; c.A = a; c.K = k; c.Month = q.Utc.Month; c.Day = q.Utc.Day; c.Hour = q.Utc.Hour + q.Utc.Minute / 60.0; c.Derive();
                double la2 = Prop.Rad(q.Lat), lo2 = Prop.Rad(q.Lon);
                q.DistKm = Prop.Dist(la1, lo1, la2, lo2) * Prop.R;
                if (q.DistKm < NearKm) { log.TooClose++; continue; }
                double ms = Prop.PathMof(la1, lo1, la2, lo2, false, c), ml = Prop.PathMof(la1, lo1, la2, lo2, true, c);
                double ls = Prop.PathLof(la1, lo1, la2, lo2, false, c), ll = Prop.PathLof(la1, lo1, la2, lo2, true, c);
                bool lp; double mof, lof;
                q.State = Bands.Eval(ms, ls, ml, ll, q.Mhz, out lp, out mof, out lof);
                q.LongPath = lp; q.Mof = mof; q.Lof = lof; q.Evaluated = true;
            }
        }

        public static DateTime HourStart(DateTime t) { return new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, DateTimeKind.Utc); }

        public static List<Qso> InHour(LogData log, DateTime hourStart)
        {
            List<Qso> l = new List<Qso>(); DateTime end = hourStart.AddHours(1);
            foreach (Qso q in log.Qsos) { if (q.Utc >= end) break; if (q.Utc >= hourStart) l.Add(q); }
            return l;
        }

        public static void Count(List<Qso> qs, out int open, out int marginal, out int closed, out int unplotted)
        {
            open = marginal = closed = unplotted = 0;
            foreach (Qso q in qs)
            {
                if (!q.Evaluated) { unplotted++; continue; }
                if (q.State == BandState.Open) open++; else if (q.State == BandState.Marginal) marginal++; else closed++;
            }
        }

        static string Pct(int n, int d) { return d == 0 ? "   -  " : (100.0 * n / d).ToString("0.0", CI).PadLeft(5) + "%"; }

        public static string Report(LogData log, string qthGrid, double qthLat, double qthLon, double sfi, double a, double k, string solarNote)
        {
            StringBuilder s = new StringBuilder(); string nl = "\r\n";
            List<Qso> ev = new List<Qso>(); foreach (Qso q in log.Qsos) if (q.Evaluated) ev.Add(q);
            int open, marg, closed, unp; Count(log.Qsos, out open, out marg, out closed, out unp);
            int n = ev.Count;

            s.Append("LOG ANALYSIS: HOW ACCURATE WAS THE PREDICTION?" + nl + nl);
            s.Append("Log        " + (log.MyCall.Length > 0 ? log.MyCall + "  " : "") + (log.Contest.Length > 0 ? log.Contest + "  " : "") + "(" + log.Format + ")" + nl);
            if (log.Qsos.Count > 0)
                s.Append("Period     " + log.First.ToString("yyyy-MM-dd HH:mm", CI) + "Z  to  " + log.Last.ToString("yyyy-MM-dd HH:mm", CI) + "Z" + nl);
            s.Append("Location   " + qthGrid + "  (" + qthLat.ToString("0.00", CI) + ", " + qthLon.ToString("0.00", CI) + ")" + nl);
            s.Append("Solar data " + "SFI " + sfi.ToString("0.#", CI) + "  A " + a.ToString("0.#", CI) + "  K " + k.ToString("0.#", CI) + "  (" + solarNote + ")" + nl);
            s.Append("QSOs      " + log.Total.ToString("N0", CI) + " read, " + n.ToString("N0", CI) + " analysed" + nl);
            List<string> parts = new List<string>();
            if (log.TooClose > 0) parts.Add(log.TooClose + " under " + NearKm.ToString("0") + " km / " + (NearKm * Km2Mi).ToString("0") + " mi");
            if (log.Unlocated > 0) parts.Add(log.Unlocated + " callsigns not located");
            if (log.NotHf > 0) parts.Add(log.NotHf + " not HF");
            if (log.NoCall > 0) parts.Add(log.NoCall + " no callsign found");
            if (log.BadTime > 0) parts.Add(log.BadTime + " bad date/time");
            if (parts.Count > 0)
            {
                string ln = "Left out   ";
                foreach (string p in parts)
                {
                    if (ln.Length + p.Length > 84) { s.Append(ln.TrimEnd(',', ' ') + nl); ln = "           "; }
                    ln += p + ", ";
                }
                s.Append(ln.TrimEnd(',', ' ') + nl);
            }
            s.Append(nl);
            s.Append("HOW TO READ THIS" + nl);
            s.Append("A completed QSO proves the band was open. It cannot prove that the bands you did not work were" + nl);
            s.Append("closed. So this measures MISSES (the prediction said CLOSED but you worked it), not false alarms." + nl);
            s.Append("Worked stations are placed by callsign: the DXCC entity point, or the call-area centre for the US" + nl);
            s.Append("and Canada, or the grid square if the log has one. Distances are therefore approximate. QSOs closer" + nl);
            s.Append("than " + NearKm.ToString("0") + " km (" + (NearKm * Km2Mi).ToString("0") + " mi) are left out: ground wave and NVIS are not part of an F2-layer model." + nl + nl);

            if (n == 0) { s.Append("No QSOs could be analysed." + nl); return s.ToString(); }

            s.Append("OVERALL" + nl);
            s.Append("  Agreed  (OPEN)      " + open.ToString().PadLeft(6) + "  " + Pct(open, n) + nl);
            s.Append("  Marginal            " + marg.ToString().PadLeft(6) + "  " + Pct(marg, n) + nl);
            s.Append("  MISSED  (CLOSED)    " + closed.ToString().PadLeft(6) + "  " + Pct(closed, n) + nl + nl);

            s.Append("BY BAND" + nl);
            s.Append("  Band    QSOs    Open  Marginal  Missed   Missed%" + nl);
            for (int b = 0; b < Bands.Name.Length; b++)
            {
                int t = 0, o = 0, m = 0, c = 0;
                foreach (Qso q in ev) if (Bands.IndexOf(q.Mhz) == b) { t++; if (q.State == BandState.Open) o++; else if (q.State == BandState.Marginal) m++; else c++; }
                if (t == 0) continue;
                s.Append("  " + (Bands.Name[b] + " M").PadRight(6) + t.ToString().PadLeft(6) + o.ToString().PadLeft(8) + m.ToString().PadLeft(10) + c.ToString().PadLeft(8) + Pct(c, t).PadLeft(10) + nl);
            }
            s.Append(nl + "BY HOUR (UTC)" + nl);
            s.Append("  Hour             QSOs    Open  Marginal  Missed" + nl);
            DateTime h = HourStart(log.First), endH = HourStart(log.Last);
            for (; h <= endH; h = h.AddHours(1))
            {
                int hn, ho, hm, hc, hu; Count(InHour(log, h), out ho, out hm, out hc, out hu); hn = ho + hm + hc;
                s.Append("  " + h.ToString("yyyy-MM-dd HH", CI) + "Z" + hn.ToString().PadLeft(8) + ho.ToString().PadLeft(8) + hm.ToString().PadLeft(10) + hc.ToString().PadLeft(8) + nl);
            }

            List<Qso> misses = new List<Qso>(); foreach (Qso q in ev) if (q.State == BandState.Closed) misses.Add(q);
            misses.Sort(delegate (Qso x, Qso y) { return Bands.MissFactor(y.Mof, y.Lof, y.Mhz).CompareTo(Bands.MissFactor(x.Mof, x.Lof, x.Mhz)); });
            if (misses.Count > 0)
            {
                s.Append(nl + "LARGEST MISSES (the prediction was furthest from what actually happened)" + nl);
                s.Append("  Time (UTC)        Call        MHz      Distance                 Predicted MOF / LOF" + nl);
                for (int i = 0; i < misses.Count && i < 12; i++)
                {
                    Qso q = misses[i];
                    s.Append("  " + q.Utc.ToString("MM-dd HH:mm", CI) + "Z   " + q.Call.PadRight(11) + q.Mhz.ToString("0.000", CI).PadLeft(7) + "  " +
                        (q.DistKm.ToString("0", CI) + " km / " + (q.DistKm * Km2Mi).ToString("0", CI) + " mi").PadRight(22) +
                        (q.Mof >= 31.99 ? ">=32" : q.Mof.ToString("0.0", CI)) + " / " + q.Lof.ToString("0.0", CI) + (q.LongPath ? "  (long path)" : "") + nl);
                }
            }
            return s.ToString();
        }
    }
}
