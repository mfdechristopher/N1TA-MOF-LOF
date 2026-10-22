// "N1TA_MOFLOF.exe --selftest": checks the engine against the Navy's published sample
// and the NOAA parsers against sample text. Useful after any edit.
using System;
using System.IO;
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

            // ---- NOAA parsers ----
            Check("flux: object, quoted", Near(Noaa.ParseFlux("{\"Flux\":\"148\",\"TimeStamp\":\"2026-10-14 20:00:00\"}"), 148, 0.001));
            Check("flux: object, numeric, last wins", Near(Noaa.ParseFlux("[{\"flux\":140},{\"flux\":152.5}]"), 152.5, 0.001));
            Check("flux: array rows", Near(Noaa.ParseFlux("[[\"time_tag\",\"flux\"],[\"2026-10-01\",\"131\"],[\"2026-10-02\",\"135\"]]"), 135, 0.001));
            Check("flux: junk -> NaN, no throw", double.IsNaN(Noaa.ParseFlux("<html>503</html>")) && double.IsNaN(Noaa.ParseFlux(null)));

            string kpObj = "[{\"time_tag\":\"2026-10-15T00:00:00\",\"Kp\":2.33,\"a_running\":9,\"station_count\":8},{\"time_tag\":\"2026-10-15T03:00:00\",\"Kp\":3.0,\"a_running\":null,\"station_count\":8},{\"time_tag\":\"2026-10-15T06:00:00\",\"Kp\":1.67,\"a_running\":6,\"station_count\":8}]";
            List<KpRow> kr = Noaa.ParseKp(kpObj);
            Check("Kp objects (new NOAA layout, null tolerated): 3 rows, last Kp 1.67", kr.Count == 3 && Near(Noaa.LatestKp(kr), 1.67, 0.001));
            Check("A = mean of last 24 h, null filled from Kp (9 + 15 + 6)/3 = 10", Near(Noaa.DailyA(kr), 10, 0.05));
            string kpArr = "[[\"time_tag\",\"Kp\",\"a_running\",\"station_count\"],[\"2026-10-15 00:00:00.000\",\"2.33\",\"9\",\"8\"],[\"2026-10-15 03:00:00.000\",\"3.00\",\"15\",\"8\"]]";
            kr = Noaa.ParseKp(kpArr);
            Check("Kp arrays (old layout)", kr.Count == 2 && Near(Noaa.LatestKp(kr), 3.0, 0.001) && Near(Noaa.DailyA(kr), 12, 0.05));
            Check("Kp: junk -> empty, no throw", Noaa.ParseKp("garbage").Count == 0 && Noaa.ParseKp(null).Count == 0 && double.IsNaN(Noaa.DailyA(new List<KpRow>())));
            Check("Kp 3 -> ap 15, Kp 5 -> ap 48", Near(Noaa.ApFromKp(3), 15, 0.01) && Near(Noaa.ApFromKp(5), 48, 0.01));
            List<OutlookRow> o = Noaa.ParseOutlook("# comment\n2026 Oct 15     135          8          3\n2026 Oct 16     138         10          3\n");
            Check("27-day outlook parse", o.Count == 2 && o[1].Date.Day == 16 && Near(o[1].Sfi, 138, 0.001) && Near(o[1].A, 10, 0.001));
            Check("outlook: junk -> empty", Noaa.ParseOutlook("nothing here").Count == 0);

            // ---- grid squares ----
            double gla, glo;
            Check("grid FN32pb -> about 42.06N 72.71W", Grid.TryParse("FN32pb", out gla, out glo) && Near(gla, 42.06, 0.02) && Near(glo, -72.71, 0.02));
            Check("grid FN32 centre", Grid.TryParse("fn32", out gla, out glo) && Near(gla, 42.5, 0.001) && Near(glo, -73.0, 0.001));
            Check("grid round trip", Grid.From(42.06, -72.71, 6) == "FN32pb" && Grid.From(51.5, -0.12, 6) == "IO91wm");
            Check("grid rejects junk", !Grid.TryParse("ZZ99", out gla, out glo) && !Grid.TryParse("FN3", out gla, out glo) && !Grid.TryParse("", out gla, out glo));
            Check("3000 km is 1864 mi", Near(3000 * 0.621371, 1864, 0.5));

            // ---- bands and colours (original scheme) ----
            Check("band state: 20 m with MOF 24 / LOF 5 OPEN; 15 m MARGINAL; 10 m CLOSED", Bands.State(24, 5, 14.15) == BandState.Open && Bands.State(24, 5, 21.2) == BandState.Marginal && Bands.State(24, 5, 28.5) == BandState.Closed);
            Check("band state: MOF clipped at 32 means 10 m is OPEN", Bands.State(32, 3, 28.5) == BandState.Open);
            Check("colours: 20 & 40 m blue, 15 & 80 m green, 10 & 160 m red", Bands.ColorGroup(14.025) == 0 && Bands.ColorGroup(7.04) == 0 && Bands.ColorGroup(21.3) == 1 && Bands.ColorGroup(3.7) == 1 && Bands.ColorGroup(28.4) == 2 && Bands.ColorGroup(1.9) == 2);
            Check("colours: WARC bands borrow the nearest (60 & 30 m blue, 17 m green, 12 m red)", Bands.ColorGroup(5.35) == 0 && Bands.ColorGroup(10.12) == 0 && Bands.ColorGroup(18.1) == 1 && Bands.ColorGroup(24.94) == 2);
            Check("band lookup: 14.025 -> 20 m, 10.12 -> 30 m, 12.0 -> none", Bands.Name[Bands.IndexOf(14.025)] == "20" && Bands.Name[Bands.IndexOf(10.12)] == "30" && Bands.IndexOf(12.0) < 0);

            // ---- callsign database (AD1C cty.dat, embedded) ----
            double cla, clo; string cw;
            Check("cty.dat loads (version 2026+, hundreds of entities)", CallDb.Lookup("DL1ABC", out cla, out clo, out cw) && CallDb.EntityCount > 300 && CallDb.Version >= 20260101);
            Check("call W1AW -> US call area 1 (43.0N 71.5W)", CallDb.Lookup("W1AW", out cla, out clo, out cw) && cw == "United States (call area 1)" && Near(cla, 43.0, 0.01) && Near(clo, -71.5, 0.01));
            Check("call K6XYZ -> call area 6, W1AW/4 -> call area 4 (digit suffix)", CallDb.Lookup("K6XYZ", out cla, out clo, out cw) && cw.EndsWith("area 6)") && CallDb.Lookup("W1AW/4", out cla, out clo, out cw) && cw.EndsWith("area 4)"));
            Check("call VE3XYZ -> Canada area 3", CallDb.Lookup("VE3XYZ", out cla, out clo, out cw) && cw == "Canada (call area 3)" && Near(cla, 45.0, 0.01));
            Check("call JA1ABC -> Japan, east longitude positive (138E)", CallDb.Lookup("JA1ABC", out cla, out clo, out cw) && cw == "Japan" && clo > 130 && clo < 145);
            Check("call DL1ABC -> Germany (10E), DL1ABC/P same", CallDb.Lookup("DL1ABC", out cla, out clo, out cw) && cw.Contains("Germany") && Near(clo, 10.0, 0.5) && CallDb.Lookup("DL1ABC/P", out cla, out clo, out cw) && cw.Contains("Germany"));
            Check("call EA8/DL1ABC -> Canary Islands (prefix form), KH6ABC -> Hawaii (west longitude negative)", CallDb.Lookup("EA8/DL1ABC", out cla, out clo, out cw) && cw.Contains("Canary") && CallDb.Lookup("KH6ABC", out cla, out clo, out cw) && cw == "Hawaii" && clo < -150);
            Check("unknown callsign is not located, no throw", !CallDb.Lookup("XX0X", out cla, out clo, out cw) && !CallDb.Lookup("", out cla, out clo, out cw) && !CallDb.Lookup(null, out cla, out clo, out cw));
            Check("callsign-shaped tokens: calls yes; 599, 5NN, 05, 100W, MA, MIKE no", LogFile.CallLike("W1AW") && LogFile.CallLike("3B8CF") && LogFile.CallLike("9A1A") && LogFile.CallLike("W1AW/4") && !LogFile.CallLike("599") && !LogFile.CallLike("5NN") && !LogFile.CallLike("05") && !LogFile.CallLike("100W") && !LogFile.CallLike("MA") && !LogFile.CallLike("MIKE"));

            // ---- Cabrillo import ----
            string cab = "START-OF-LOG: 3.0\nCALLSIGN: N1TA\nCONTEST: CQ-WW-CW\nGRID-LOCATOR: FN32pb\n" +
                "QSO: 14025 CW 2024-11-23 0001 N1TA 599 05 W1AW 599 05 0\n" +
                "QSO: 7005 CW 2024-11-23 0105 N1TA 599 05 DL1ABC 599 14 0\n" +
                "QSO: 28025 CW 2024-11-23 0210 N1TA 599 05 JA1ABC 599 25 0\n" +
                "QSO: 144 PH 2024-11-23 0300 N1TA 59 FN32 K1ZZ 59 FN42\n" +
                "QSO: 21025 CW 2024-11-23 1500 N1TA 599 MA K5ZD 599 100\n" +
                "QSO: 14025 CW 2024-11-23 2561 N1TA 599 05 W1AW 599 05\n" +
                "QSO: 7025 CW 2024-11-23 1805 N1TA MIKE MA K5ZD RICK TX\n" +
                "QSO: garbage\nEND-OF-LOG:\n";
            LogData lg = LogFile.Parse(cab);
            Check("Cabrillo header: call N1TA, contest, grid FN32pb", lg.Format == "Cabrillo" && lg.MyCall == "N1TA" && lg.Contest == "CQ-WW-CW" && lg.MyGrid == "FN32pb");
            Check("Cabrillo: 5 QSOs read; 1 VHF, 1 bad time (2561), 1 short line skipped, none fatal", lg.Qsos.Count == 5 && lg.NotHf == 1 && lg.BadTime == 1 && lg.NoCall == 1);
            Check("Cabrillo: HF kHz -> MHz and UTC time (7005 kHz at 01:05Z)", lg.Qsos[1].Call == "DL1ABC" && Near(lg.Qsos[1].Mhz, 7.005, 1e-9) && lg.Qsos[1].Utc == new DateTime(2024, 11, 23, 1, 5, 0));
            Check("Cabrillo: worked call found past a name/state exchange (NAQP style: K5ZD)", lg.Qsos[4].Call == "K5ZD" && lg.Qsos[3].Call == "K5ZD");
            Check("Cabrillo: QSOs are sorted by time", lg.Qsos[0].Utc <= lg.Qsos[1].Utc && lg.Qsos[3].Utc <= lg.Qsos[4].Utc && lg.First == lg.Qsos[0].Utc);
            LogData gx = LogFile.Parse("START-OF-LOG: 3.0\nQSO: 14025 CW 2024-11-23 0001 N1TA 599 FN32 DL1ABC 599 JO62\nEND-OF-LOG:");
            Check("Cabrillo: a grid square in the exchange beats the callsign guess", gx.Qsos.Count == 1 && gx.Qsos[0].Located && gx.Qsos[0].Where == "JO62");

            // ---- ADIF import ----
            string adi = "ADIF Export\n<ADIF_VER:5>3.1.0 <PROGRAMID:4>TEST <EOH>\n" +
                "<CALL:4>W1AW <QSO_DATE:8>20241123 <TIME_ON:4>0301 <BAND:3>20m <MODE:2>CW <GRIDSQUARE:4>FN31 <MY_GRIDSQUARE:6>FN32pb <STATION_CALLSIGN:4>N1TA <EOR>\n" +
                "<CALL:6>DL1ABC <QSO_DATE:8>20241123 <TIME_ON:6>041500 <FREQ:6>7.0125 <MODE:3>SSB <EOR>\n" +
                "<CALL:5>XX9ZZ <QSO_DATE:8>20241123 <TIME_ON:4>0500 <BAND:2>2m <EOR>\n" +
                "<CALL:5>K1ABC <QSO_DATE:8>2024 <TIME_ON:4>0500 <BAND:3>40m <EOR>\n";
            LogData ad = LogFile.Parse(adi);
            Check("ADIF: 2 QSOs read; VHF and bad date skipped", ad.Format == "ADIF" && ad.Qsos.Count == 2 && ad.NotHf == 1 && ad.BadTime == 1);
            Check("ADIF: my call/grid from the record, QSO grid beats callsign lookup (FN31)", ad.MyCall == "N1TA" && ad.MyGrid == "FN32pb" && ad.Qsos[0].Where == "FN31" && ad.Qsos[0].Located);
            Check("ADIF: FREQ in MHz (7.0125) and 6-digit time (04:15:00); BAND 20m -> 14.175 centre", Near(ad.Qsos[1].Mhz, 7.0125, 1e-9) && ad.Qsos[1].Utc == new DateTime(2024, 11, 23, 4, 15, 0) && Near(ad.Qsos[0].Mhz, 14.175, 1e-6));
            bool bad = false; try { LogFile.Parse("this is not a log"); } catch (FormatException) { bad = true; }
            Check("a file that is neither Cabrillo nor ADIF is refused cleanly", bad);

            // ---- look-back evaluation (FN32pb, SFI 150, A 6, K 2) ----
            LogAnalysis.Evaluate(lg, 42.06, -72.71, 150, 6, 2);
            Check("evaluation: 144 km W1AW QSO is too close to judge (NVIS / ground wave), not a 'miss'", lg.TooClose == 1 && !lg.Qsos[0].Evaluated);
            Check("evaluation: 40 m to Germany at 01:05Z OPEN; 15 m to Texas at 15:00Z OPEN", lg.Qsos[1].Evaluated && lg.Qsos[1].State == BandState.Open && lg.Qsos[3].Evaluated && lg.Qsos[3].State == BandState.Open);
            Check("evaluation: 10 m to Japan at 02:10Z (night, SFI 150) is a MISS (CLOSED)", lg.Qsos[2].Evaluated && lg.Qsos[2].State == BandState.Closed);
            int eo, em, ec, eu; LogAnalysis.Count(lg.Qsos, out eo, out em, out ec, out eu);
            Check("counts: 3 open, 0 marginal, 1 closed, 1 not judged", eo == 3 && em == 0 && ec == 1 && eu == 1);
            Check("hour window: 01Z holds exactly the DL1ABC QSO", LogAnalysis.InHour(lg, new DateTime(2024, 11, 23, 1, 0, 0)).Count == 1 && LogAnalysis.InHour(lg, new DateTime(2024, 11, 23, 1, 0, 0))[0].Call == "DL1ABC");
            string rpt = LogAnalysis.Report(lg, "FN32pb", 42.06, -72.71, 150, 6, 2, "test");
            Check("report: has the how-to-read note, overall counts, by-band, by-hour and the miss list", rpt.Contains("HOW TO READ THIS") && rpt.Contains("MISSED  (CLOSED)") && rpt.Contains("BY BAND") && rpt.Contains("BY HOUR (UTC)") && rpt.Contains("LARGEST MISSES") && rpt.Contains("JA1ABC"));
            Check("report: states the 1000 km / 621 mi exclusion and shows km and miles", rpt.Contains("1000 km") && rpt.Contains("621 mi"));

            // ---- callsign database update (cty.dat) ----
            string builtIn = CallDb.BuiltInText();
            CtyDb vdb; string vprob;
            Check("cty validate: the built-in file passes (version, 300+ countries, spot checks)", CallDb.Validate(builtIn, out vdb, out vprob) && vdb.Version >= 20260101 && vdb.Entities >= 300);
            Check("cty validate: rejects empty, tiny and junk files", !CallDb.Validate("", out vdb, out vprob) && !CallDb.Validate("hello", out vdb, out vprob) && !CallDb.Validate(new string('x', 80000), out vdb, out vprob));
            Check("cty validate: rejects a truncated file (too few countries)", !CallDb.Validate(builtIn.Substring(0, builtIn.Length / 5), out vdb, out vprob) && vprob.Length > 0);
            Check("cty validate: rejects a file whose entity names changed (US refinement would silently break)", !CallDb.Validate(builtIn.Replace("United States:", "USA:"), out vdb, out vprob) && vprob.Contains("United States"));
            Check("cty version/age helpers", CallDb.VersionOf("=VER20260714,") == 20260714 && CallDb.VersionOf("nothing") == 0 && CallDb.AgeDays(20000101) > 9000 && CallDb.AgeDays(5) == -1);

            string tmpDir = Path.Combine(Path.GetTempPath(), "n1ta_selftest_" + Environment.TickCount);
            CallDb.DataDirOverride = tmpDir; CallDb.ResetForTest();
            string newer = builtIn.Replace("=VER20260714", "=VER20990101");
            string inst; bool ok1 = CallDb.Install(newer, out inst);
            double ila, ilo; string iw;
            Check("cty install: a valid newer file is saved, becomes the one in use, and lookups still work", ok1 && File.Exists(CallDb.UserFile) && CallDb.Version == 20990101 && CallDb.Source.StartsWith("your") && CallDb.Lookup("DL1ABC", out ila, out ilo, out iw));
            CallDb.ResetForTest(); int vReload = CallDb.Version;
            Check("cty install: the saved copy is picked up again on the next start", vReload == 20990101 && CallDb.Source.StartsWith("your"));
            File.WriteAllText(CallDb.UserFile, "this is not a country file");
            CallDb.ResetForTest(); int vFallback = CallDb.Version;
            Check("cty fallback: a corrupt saved file is ignored and the built-in copy is used", vFallback >= 20260101 && vFallback != 20990101 && CallDb.Source.StartsWith("built") && CallDb.Lookup("DL1ABC", out ila, out ilo, out iw));
            string badInst; Check("cty install: an invalid file is refused and nothing is written over the good one", !CallDb.Install("junk", out badInst) && badInst.Length > 0);
            CallDb.Install(newer, out inst); CallDb.UseBuiltIn();
            Check("cty use built-in: deletes the downloaded copy and goes back", !File.Exists(CallDb.UserFile) && CallDb.Version < 20990101 && CallDb.Source.StartsWith("built"));
            try { Directory.Delete(tmpDir, true); } catch { }
            CallDb.DataDirOverride = null; CallDb.ResetForTest();

            // a real HTTP download against a throwaway local server (a normal user's file comes from country-files.com)
            System.Net.Sockets.TcpListener lsn = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); lsn.Start();
            int sport = ((System.Net.IPEndPoint)lsn.LocalEndpoint).Port; byte[] body = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(builtIn);
            System.Threading.Thread srv = new System.Threading.Thread(delegate ()
            {
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        using (System.Net.Sockets.TcpClient tc = lsn.AcceptTcpClient())
                        {
                            System.Net.Sockets.NetworkStream ns = tc.GetStream(); byte[] rb = new byte[4096]; ns.Read(rb, 0, rb.Length);
                            string head = i == 0 ? "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n" : "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                            byte[] hb = System.Text.Encoding.ASCII.GetBytes(head); ns.Write(hb, 0, hb.Length); if (i == 0) ns.Write(body, 0, body.Length); ns.Flush();
                        }
                    }
                    catch { }
                }
            });
            srv.IsBackground = true; srv.Start();
            Environment.SetEnvironmentVariable("N1TA_CTY_URL", "http://127.0.0.1:" + sport + "/bigcty/cty.dat");
            string derr; string got = CtyUpdate.Download(out derr);
            Check("cty download: fetches the file over HTTP, byte for byte (" + (got == null ? derr : (got.Length + " chars")) + ")", got != null && got == builtIn && CallDb.VersionOf(got) == CallDb.Version);
            string derr2; string got2 = CtyUpdate.Download(out derr2);
            Check("cty download: a 404 gives a plain-language error and no data (" + derr2 + ")", got2 == null && derr2.Contains("404"));
            lsn.Stop();
            string derr3; string got3 = CtyUpdate.Download(out derr3);
            Check("cty download: an unreachable server gives an error and no data", got3 == null && derr3.Length > 0);
            Environment.SetEnvironmentVariable("N1TA_CTY_URL", null);
            Check("cty download: the default address is AD1C's stable 'latest' file", CtyUpdate.Url == "https://www.country-files.com/bigcty/cty.dat");

            // re-placing an open log after the database changes
            LogData rl = new LogData(); Qso r1 = new Qso(); r1.Call = "DL1ABC"; r1.Utc = new DateTime(2025, 11, 29, 12, 0, 0); r1.Mhz = 14.025;
            Qso r2 = new Qso(); r2.Call = "DL1ABC"; r2.Utc = r1.Utc; r2.Mhz = 14.025; r2.Lat = 10; r2.Lon = 20; r2.Located = true; r2.FromGrid = true; r2.Where = "JK00";
            Qso r3 = new Qso(); r3.Call = "XX0X"; r3.Utc = r1.Utc; r3.Mhz = 14.025; rl.Qsos.Add(r1); rl.Qsos.Add(r2); rl.Qsos.Add(r3);
            LogFile.Relocate(rl);
            Check("relocate: callsign QSOs are re-placed, grid-located QSOs keep their grid, unknowns counted", r1.Located && r1.Where.Contains("Germany") && r2.Where == "JK00" && Near(r2.Lat, 10, 1e-9) && !r3.Located && rl.Unlocated == 1);

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
