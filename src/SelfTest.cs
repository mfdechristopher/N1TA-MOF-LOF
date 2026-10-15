// "N1TA_MOFLOF.exe --selftest": checks the engine against the Navy's published sample
// and the NOAA parsers against sample text. Useful after any edit.
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    public static class SelfTest
    {
        static int fails; static StringBuilder sb = new StringBuilder();
        static void Check(string name, bool ok) { sb.AppendLine((ok ? "PASS  " : "FAIL  ") + name); if (!ok) fails++; }
        static bool Near(double a, double b, double tol) { return Math.Abs(a - b) <= tol; }

        public static int Run()
        {
            // NOSC TD 201 sample: 21N 156.1W -> 38N 122.1W, 17 Oct, SSN 110. Hourly MUF (MHz).
            Cond c = new Cond(); c.Month = 10; c.Day = 17; c.Ssn = 110; c.Ke = 0;
            double[][] exp = { new double[] { 3, 29.9 }, new double[] { 4, 25.0 }, new double[] { 6, 20.9 }, new double[] { 12, 14.6 }, new double[] { 13, 14.1 }, new double[] { 14, 13.7 }, new double[] { 16, 27.6 } };
            foreach (double[] e in exp)
            {
                c.Hour = e[0];
                double m = Prop.PathMof(Prop.Rad(21), Prop.Rad(-156.1), Prop.Rad(38), Prop.Rad(-122.1), false, c);
                Check(string.Format("MINIMUF 3.5 sample, {0:00}Z: expect {1:0.0}, got {2:0.0}", e[0], e[1], m), Near(m, e[1], 0.15));
            }
            Check("SFI 100 -> SSN about 47", Near(Prop.SsnFromFlux(100), 47, 3));
            Check("SFI 60 -> SSN 0", Prop.SsnFromFlux(60) == 0);
            Check("Ap 15 -> K 3", Near(Prop.KFromA(15), 3, 0.01));

            Check("flux parse", Near(Noaa.ParseFlux("{\"Flux\":\"148\",\"TimeStamp\":\"2026-10-14 20:00:00\"}"), 148, 0.001));
            double kp, a;
            bool ok = Noaa.ParseKp("[[\"time_tag\",\"Kp\",\"a_running\",\"station_count\"],[\"2026-10-15 00:00:00.000\",\"2.33\",\"9\",\"8\"],[\"2026-10-15 03:00:00.000\",\"3.00\",\"15\",\"8\"]]", out kp, out a);
            Check("Kp parse (array form)", ok && Near(kp, 3.0, 0.001) && Near(a, 15, 0.001));
            ok = Noaa.ParseKp("[{\"time_tag\":\"2026-10-15T00:00:00\",\"Kp\":2.67,\"a_running\":12,\"station_count\":8}]", out kp, out a);
            Check("Kp parse (object form)", ok && Near(kp, 2.67, 0.001) && Near(a, 12, 0.001));
            string dgd = "# Date  A K...\n2026 10 13    6   1 1 2 1 1 1 2 1     4   0 0 1 1 1 1 1 0     7   2 1 2 2 1 1 2 2\n2026 10 14    9   2 2 2 3 2 1 2 2     8   1 1 2 3 2 2 1 1    11   3 2 2 3 3 2 2 -1\n";
            ok = Noaa.ParseDgd(dgd, out a, out kp);
            Check("DGD parse", ok && Near(a, 11, 0.001) && Near(kp, 2, 0.001));
            List<OutlookRow> o = Noaa.ParseOutlook("# comment\n2026 Oct 15     135          8          3\n2026 Oct 16     138         10          3\n");
            Check("27-day outlook parse", o.Count == 2 && o[1].Date.Day == 16 && Near(o[1].Sfi, 138, 0.001) && Near(o[1].A, 10, 0.001));

            Cond q = new Cond(); q.Month = 6; q.Day = 21; q.Hour = 17; q.Sfi = 130; q.A = 5; q.K = 1; q.Derive();
            double day = Prop.PathMof(Prop.Rad(42), Prop.Rad(-72), Prop.Rad(51), Prop.Rad(0), false, q);
            q.Hour = 5; double lofNight = Prop.PathLof(Prop.Rad(42), Prop.Rad(-72), Prop.Rad(51), Prop.Rad(0), false, q);
            q.Hour = 17; double lofDay = Prop.PathLof(Prop.Rad(42), Prop.Rad(-72), Prop.Rad(51), Prop.Rad(0), false, q);
            Check(string.Format("LOF day ({0:0.0}) higher than LOF at local night ({1:0.0})", lofDay, lofNight), lofDay > lofNight);
            Check(string.Format("MOF sane for FN32->Europe afternoon ({0:0.0})", day), day > 8 && day <= 32);

            sb.AppendLine(); sb.AppendLine(fails == 0 ? "All checks passed." : fails + " check(s) FAILED.");
            Console.WriteLine(sb.ToString());
            if (Environment.GetEnvironmentVariable("N1TA_QUIET") == null)
                MessageBox.Show(sb.ToString(), "N1TA MOF/LOF self-test");
            return fails == 0 ? 0 : 1;
        }
    }
}
