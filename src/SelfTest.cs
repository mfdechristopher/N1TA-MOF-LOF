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
        static byte[] Enc(string s) { return System.Text.Encoding.UTF8.GetBytes(s); }
        static byte[] Concat(byte[] a) { return a; }
        static string RI(string st, int radio, int freq10Hz, int focus)
        {
            return "<RadioInfo><StationName>" + st + "</StationName><RadioNr>" + radio + "</RadioNr><Freq>" + freq10Hz + "</Freq><TXFreq>" + freq10Hz + "</TXFreq><IsSplit>False</IsSplit><FocusRadioNr>" + focus + "</FocusRadioNr><ActiveRadioNr>" + focus + "</ActiveRadioNr></RadioInfo>";
        }
        // Qt QDataStream layout used by WSJT-X: magic, schema, type, id (utf8), dial frequency (u64, Hz). All big-endian.
        static byte[] WsjtxStatus(string id, ulong hz)
        {
            List<byte> b = new List<byte>(); byte[] ib = Enc(id);
            b.AddRange(new byte[] { 0xAD, 0xBC, 0xCB, 0xDA, 0, 0, 0, 2, 0, 0, 0, 1 });
            b.AddRange(new byte[] { (byte)(ib.Length >> 24), (byte)(ib.Length >> 16), (byte)(ib.Length >> 8), (byte)ib.Length }); b.AddRange(ib);
            for (int i = 7; i >= 0; i--) b.Add((byte)(hz >> (8 * i)));
            return b.ToArray();
        }
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

            // ---- bands ----
            Check("band state: 15 m with MOF 24 / LOF 5 is MARGINAL (21.2 > 0.85*24)", Bands.State(24, 5, 21.2) == BandState.Marginal);
            Check("band state: 20 m with MOF 24 / LOF 5 is OPEN", Bands.State(24, 5, 14.15) == BandState.Open);
            Check("band state: 10 m above MOF is CLOSED; 80 m below LOF is CLOSED", Bands.State(24, 5, 28.5) == BandState.Closed && Bands.State(24, 5, 3.6) == BandState.Closed);
            Check("band state: MOF clipped at 32 means 10 m is OPEN", Bands.State(32, 3, 28.5) == BandState.Open);
            Check("band state: 40 m just above LOF is MARGINAL (7.1 < 1.15*6.5)", Bands.State(24, 6.5, 7.1) == BandState.Marginal);
            BandState[] bs = { BandState.Open, BandState.Open, BandState.Closed, BandState.Open, BandState.Marginal, BandState.Closed };
            Check("best band = highest OPEN (20 m here, index 3)", Bands.Best(bs) == 3);
            Check("best band falls back to highest MARGINAL", Bands.Best(new BandState[] { BandState.Closed, BandState.Closed, BandState.Marginal, BandState.Closed, BandState.Closed, BandState.Closed }) == 2);
            Check("best band NONE when all closed", Bands.Best(new BandState[6]) == -1);
            bool strongFlag;
            Check("summary: highest band open to >= a third of regions (6 of 16)", Bands.BestByCount(new int[] { 9, 8, 7, 6, 3, 0 }, new int[] { 9, 9, 9, 9, 9, 2 }, 16, out strongFlag) == 3 && strongFlag);
            Check("summary: weak fallback flagged", Bands.BestByCount(new int[] { 0, 0, 0, 1, 0, 0 }, new int[] { 0, 0, 0, 1, 2, 0 }, 16, out strongFlag) == 3 && !strongFlag);
            bool lpx;
            Check("path pair: long path wins when better", Bands.Eval(10, 5, 24, 5, 14.15, out lpx) == BandState.Open && lpx);
            Check("band lookup 14.025 -> 20 m, 7.04 -> 40 m, 10.12 -> 30 m, 12.0 -> none, 50.1 -> 6 M", Bands.ExtName[Bands.ExtIndex(14.025)] == "20" && Bands.ExtName[Bands.ExtIndex(7.04)] == "40" && Bands.ExtName[Bands.ExtIndex(10.12)] == "30" && Bands.ExtIndex(12.0) < 0 && Bands.Label(50.1) == "6 M");
            Check("contest index: 21.3 MHz -> 15 m; 18.1 (17 m) not a contest band", Bands.Name[Bands.ContestIndex(21.3)] == "15" && Bands.ContestIndex(18.1) < 0);
            Check("F2 model range: 14 MHz yes, 50 MHz no", Bands.Modelled(14.0) && !Bands.Modelled(50.1));

            // ---- logger UDP feeds (samples taken from the N1MM+ and DXLog.net documentation) ----
            Feed f;
            f = FeedParser.Parse(Enc("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<RadioInfo>\n <app>N1MM</app>\n <StationName>CW-80m</StationName>\n <RadioNr>1</RadioNr>\n <Freq>352211</Freq>\n <TXFreq>352211</TXFreq>\n <Mode>CW</Mode>\n <IsSplit>False</IsSplit>\n <FocusRadioNr>1</FocusRadioNr>\n <ActiveRadioNr>1</ActiveRadioNr>\n</RadioInfo>"), -1);
            Check("N1MM RadioInfo: 352211 = 3.52211 MHz (80 m), radio 1", f != null && f.Radio && Near(f.RxMHz, 3.52211, 1e-6) && f.RadioNr == 1 && f.FocusNr == 1 && f.Station == "CW-80m");
            f = FeedParser.Parse(Enc("<?xml version = \"1.0\"?>\n<RadioInfo>\n    <app>DXLog</app>\n    <logger>DXLog v2.4.13</logger>\n    <Station>STN1</Station>\n    <RadioNr>1</RadioNr>\n    <Freq>704000</Freq>\n    <TXFreq>704000</TXFreq>\n    <InactiveFreq>702500</InactiveFreq>\n    <Mode>CW</Mode>\n    <IsSplit>False</IsSplit>\n    <FocusRadioNr>1</FocusRadioNr>\n    <ActiveRadioNr>1</ActiveRadioNr>\n    <IFFrequency>8215000</IFFrequency>\n</RadioInfo>"), -1);
            Check("DXLog.net RadioInfo: 704000 = 7.04 MHz (40 m); IF and inactive VFO ignored", f != null && Near(f.RxMHz, 7.04, 1e-6) && f.Station == "STN1" && Bands.ExtName[Bands.ExtIndex(f.RxMHz)] == "40");
            f = FeedParser.Parse(Enc("<contactinfo><band>3.5</band><txfreq>352376</txfreq><rxfreq>352376</rxfreq><runqso>True</local><newqso>True</newqso></contactinfo>"), -1);
            Check("DXLog contactinfo with mismatched tags still parses: 3.52376 MHz, band 3.5", f != null && !f.Radio && Near(f.TxMHz, 3.52376, 1e-6) && Near(f.BandMHz, 3.5, 1e-9));
            f = FeedParser.Parse(Enc("<lookupinfo><band>20</band><txfreq>1400200</txfreq><call>E7DX</call></lookupinfo>"), -1);
            Check("lookupinfo: 14.002 MHz", f != null && Near(f.TxMHz, 14.002, 1e-6));
            f = FeedParser.Parse(Enc("<spot><dxcall>E7DX</dxcall><frequency>14022.3</frequency></spot>"), -1);
            Check("spot packets are recognised but never used as the radio frequency", f != null && !f.HasFreq && !f.Radio);
            f = FeedParser.Parse(Enc("<Rotator><frequency>2800200</frequency><azimuth>252</azimuth></Rotator>"), -1);
            Check("rotator packets ignored", f != null && !f.HasFreq);
            f = FeedParser.Parse(Enc("<RadioInfo><RadioNr>1</RadioNr><Freq>14.250</Freq></RadioInfo>"), -1);
            Check("frequency already in MHz (decimal point) is accepted", f != null && Near(f.RxMHz, 14.25, 1e-9));
            Check("garbage / empty / null -> no feed, no throw", FeedParser.Parse(Enc("hello"), -1) == null && FeedParser.Parse(new byte[0], 0) == null && FeedParser.Parse(null, 0) == null);
            f = FeedParser.ParseAdif("<CALL:4>W1AW <BAND:3>20m <FREQ:6>14.025 <EOR>");
            Check("ADIF fallback: 14.025 MHz", f != null && Near(f.RxMHz, 14.025, 1e-9));
            f = FeedParser.Parse(WsjtxStatus("WSJT-X", 14074000UL), -1);
            Check("WSJT-X status packet: dial 14074000 Hz = 14.074 MHz", f != null && f.Radio && Near(f.RxMHz, 14.074, 1e-9));
            Check("WSJT-X non-status packet ignored", FeedParser.Parse(Concat(new byte[] { 0xAD, 0xBC, 0xCB, 0xDA, 0, 0, 0, 2, 0, 0, 0, 0 }), -1).HasFreq == false);

            // ---- tracker logic (no sockets) ----
            UdpTracker u = new UdpTracker();
            u.Handle(Enc(RI("S1", 1, 704000, 2)), -1, "t"); u.Handle(Enc(RI("S1", 2, 1402500, 2)), -1, "t");
            Check("SO2R: follows the FOCUSED radio (radio 2 = 14.025)", Near(u.FreqMHz, 14.025, 1e-6));
            u.Handle(Enc(RI("S1", 1, 705000, 2)), -1, "t");
            Check("SO2R: update on the unfocused radio does not move the tracked frequency", Near(u.FreqMHz, 14.025, 1e-6));
            u.RadioSel = 1; u.Handle(Enc(RI("S1", 1, 705000, 2)), -1, "t");
            Check("fixed to radio 1 -> 7.05", Near(u.FreqMHz, 7.05, 1e-6));
            u = new UdpTracker(); u.StationFilter = "MINE";
            u.Handle(Enc(RI("OTHER", 1, 2100000, 1)), -1, "t");
            Check("station filter ignores other stations", double.IsNaN(u.FreqMHz));
            u.Handle(Enc(RI("mine", 1, 1420000, 1)), -1, "t");
            Check("station filter accepts a match, case-insensitive", Near(u.FreqMHz, 14.2, 1e-6));
            u = new UdpTracker();
            u.Handle(Enc("<contactinfo><txfreq>2103200</txfreq><band>21</band></contactinfo>"), -1, "t");
            Check("no RadioInfo at all: logged-QSO frequency is used as a fallback (21.032)", Near(u.FreqMHz, 21.032, 1e-6));
            u.Handle(Enc(RI("S", 1, 1420000, 1)), -1, "t"); u.Handle(Enc("<contactinfo><txfreq>2103200</txfreq></contactinfo>"), -1, "t");
            Check("once live RadioInfo is flowing, QSO events no longer override it", Near(u.FreqMHz, 14.2, 1e-6));
            u = new UdpTracker(); u.Handle(Enc("<RadioInfo><RadioNr>1</RadioNr><Freq>1420000</Freq><TXFreq>1425000</TXFreq><IsSplit>True</IsSplit></RadioInfo>"), -1, "t");
            Check("split operation tracks the TRANSMIT frequency (14.25)", Near(u.FreqMHz, 14.25, 1e-6));
            u.Handle(Enc("not xml at all"), -1, "t");
            Check("unrecognised packet is counted and logged, not fatal", u.Packets == 2 && u.Recognized == 1 && u.LogText().Contains("UNRECOGNISED"));

            // ---- real UDP socket on loopback ----
            int port = 47000 + (Environment.TickCount % 1000);
            UdpTracker sk = new UdpTracker(); string err;
            bool started = sk.Start(port, out err);
            Check("UDP listener binds (port " + port + ")" + (started ? "" : ": " + err), started);
            if (started)
            {
                using (System.Net.Sockets.UdpClient cl = new System.Net.Sockets.UdpClient())
                {
                    byte[] pk = Enc(RI("LOOP", 1, 2122500, 1));
                    cl.Send(pk, pk.Length, "127.0.0.1", port);
                    for (int i = 0; i < 40 && !sk.HasFreq; i++) System.Threading.Thread.Sleep(50);
                }
                Check("UDP loopback delivery: 21.225 MHz received and band = 15 m", sk.HasFreq && Near(sk.FreqMHz, 21.225, 1e-6) && Bands.Label(sk.FreqMHz) == "15 M");
                sk.Stop(); Check("listener stops cleanly", !sk.Running);
            }

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
