// Follow the band of the radio, from the logging software's UDP broadcasts.
//
// Verified against the loggers' own documentation:
//  * N1MM Logger+ and DXLog.net (N1MM-style): XML <RadioInfo> datagrams, default UDP port 12060.
//    Freq / TXFreq are integers in units of 10 Hz (704000 = 7040.00 kHz). FocusRadioNr says which
//    radio the operator is on (SO2R). IsSplit says whether TXFreq differs.
//  * <contactinfo> and <lookupinfo> carry txfreq (10 Hz units) and band, used only as a fallback.
//  * WSJT-X status datagrams (binary, Qt big-endian, magic 0xADBCCBDA, type 1): dial frequency in Hz.
//  * ADIF text records (<FREQ:..>, <BAND:..>), fallback only.
// DXLog.net's own sample XML contains mismatched closing tags, so nothing here uses an XML parser:
// tolerant regexes only. Nothing in this file throws on bad input.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace N1TA.MofLof
{
    public class Feed
    {
        public string Kind = "", Station = "";
        public int RadioNr, FocusNr, ActiveNr;
        public double RxMHz = double.NaN, TxMHz = double.NaN, BandMHz = double.NaN;
        public bool Split, Radio;       // Radio = a live radio-state report (as opposed to a logged QSO event)
        public bool HasFreq { get { return !double.IsNaN(RxMHz) || !double.IsNaN(TxMHz) || !double.IsNaN(BandMHz); } }
    }

    public static class FeedParser
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        static string Tag(string s, string name)
        {
            Match m = Regex.Match(s, "<\\s*" + name + "\\s*>(.*?)<\\s*/\\s*" + name + "\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
        static bool Num(string t, out double v)
        {
            v = double.NaN; if (t == null) return false;
            return double.TryParse(t.Trim().Replace(',', '.'), NumberStyles.Float, CI, out v);
        }
        // Logger frequency field -> MHz. Integers are 10 Hz units; anything with a decimal point is taken as MHz already.
        static double FreqField(string t)
        {
            double v; if (!Num(t, out v) || v <= 0) return double.NaN;
            double mhz = t.Contains(".") || t.Contains(",") ? v : v / 100000.0;
            return (mhz >= 0.1 && mhz <= 1000) ? mhz : double.NaN;
        }
        static int Int(string t) { double v; return Num(t, out v) ? (int)v : 0; }

        public static Feed Parse(byte[] d, int n)
        {
            if (d == null) return null; if (n < 0) n = d.Length; if (n < 4) return null;
            try
            {
                if (n >= 12 && d[0] == 0xAD && d[1] == 0xBC && d[2] == 0xCB && d[3] == 0xDA) return Wsjtx(d, n);
                string text = Encoding.UTF8.GetString(d, 0, n);
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                Match r = Regex.Match(text, "<\\s*([A-Za-z_][A-Za-z0-9_]*)\\b[^>]*>");
                if (!r.Success) return null;
                string root = r.Groups[1].Value; string rl = root.ToLowerInvariant();
                Feed f = new Feed(); f.Kind = root;
                if (rl == "radioinfo")
                {
                    f.Radio = true;
                    f.Station = Tag(text, "StationName") ?? Tag(text, "Station") ?? "";
                    f.RadioNr = Int(Tag(text, "RadioNr")); f.FocusNr = Int(Tag(text, "FocusRadioNr")); f.ActiveNr = Int(Tag(text, "ActiveRadioNr"));
                    f.RxMHz = FreqField(Tag(text, "Freq")); f.TxMHz = FreqField(Tag(text, "TXFreq"));
                    string sp = Tag(text, "IsSplit"); f.Split = sp != null && sp.Trim().ToLowerInvariant() == "true";
                    return f;
                }
                if (rl == "contactinfo" || rl == "lookupinfo")
                {
                    f.Station = Tag(text, "StationName") ?? Tag(text, "stationid") ?? "";
                    f.RxMHz = FreqField(Tag(text, "rxfreq")); f.TxMHz = FreqField(Tag(text, "txfreq"));
                    double b; if (Num(Tag(text, "band"), out b) && b > 0 && b < 1000) f.BandMHz = b;   // N1MM: 21, 3.5 ...
                    return f;
                }
                if (rl == "spot" || rl == "rotator" || rl == "appinfo" || rl == "dynamicresults" || rl == "radio_setfrequency" || rl == "score") return f;   // recognised, irrelevant
                return null;
            }
            catch { return null; }
        }

        // Also handles plain ADIF text that is not XML.
        public static Feed ParseAdif(string text)
        {
            if (text == null) return null;
            Match fm = Regex.Match(text, "<FREQ:\\d+[^>]*>\\s*([0-9]+[.,]?[0-9]*)", RegexOptions.IgnoreCase);
            Match bm = Regex.Match(text, "<BAND:\\d+[^>]*>\\s*([0-9]+)\\s*m\\b", RegexOptions.IgnoreCase);
            if (!fm.Success && !bm.Success) return null;
            Feed f = new Feed(); f.Kind = "ADIF";
            double v; if (fm.Success && Num(fm.Groups[1].Value, out v) && v >= 0.1 && v <= 1000) f.RxMHz = v;
            if (double.IsNaN(f.RxMHz) && bm.Success)
                for (int i = 0; i < Bands.ExtName.Length; i++) if (Bands.ExtName[i] == bm.Groups[1].Value) f.BandMHz = Bands.ExtCenter[i];
            return f.HasFreq ? f : null;
        }

        static uint U32(byte[] d, int o) { return (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]); }
        static Feed Wsjtx(byte[] d, int n)
        {
            uint type = U32(d, 8); Feed f = new Feed(); f.Kind = "WSJT-X"; if (type != 1) return f;     // 1 = Status
            int o = 12; if (o + 4 > n) return f;
            uint len = U32(d, o); o += 4; if (len != 0xFFFFFFFF) { if (o + len > n) return f; f.Station = Encoding.UTF8.GetString(d, o, (int)len); o += (int)len; }
            if (o + 8 > n) return f;
            ulong hz = ((ulong)U32(d, o) << 32) | U32(d, o + 4);
            double mhz = hz / 1e6; if (mhz >= 0.1 && mhz <= 1000) { f.RxMHz = mhz; f.TxMHz = mhz; f.Radio = true; f.RadioNr = 1; f.FocusNr = 1; }
            return f;
        }
    }

    public class UdpTracker
    {
        Socket sock; Thread th; volatile bool run;
        readonly Dictionary<string, double> radios = new Dictionary<string, double>();
        readonly Dictionary<string, int> focus = new Dictionary<string, int>();
        readonly List<string> log = new List<string>();
        public int Port, RadioSel; public string StationFilter = "";
        public double FreqMHz = double.NaN; public DateTime FreqWhen = DateTime.MinValue; public string FreqSource = "";
        public int Version, Packets, Recognized; public DateTime LastPacket = DateTime.MinValue; DateTime lastRadioState = DateTime.MinValue;
        public string LastError = "";
        public bool Running { get { return run; } }
        public bool HasFreq { get { return !double.IsNaN(FreqMHz) && (DateTime.UtcNow - FreqWhen).TotalSeconds < 300; } }

        void L(string s) { lock (log) { log.Add(DateTime.UtcNow.ToString("HH:mm:ss") + "Z  " + s); if (log.Count > 200) log.RemoveAt(0); } }
        public string LogText() { lock (log) { return log.Count == 0 ? "(no packets received)" : string.Join("\r\n", log.ToArray()); } }

        public bool Start(int port, out string error)
        {
            Stop(); error = ""; Port = port; Packets = Recognized = 0; FreqMHz = double.NaN; lock (radios) { radios.Clear(); focus.Clear(); }
            try
            {
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                sock.Bind(new IPEndPoint(IPAddress.Any, port)); sock.ReceiveTimeout = 500;
            }
            catch (Exception e) { error = e.Message; LastError = e.Message; try { if (sock != null) sock.Close(); } catch { } sock = null; L("cannot listen on UDP " + port + ": " + e.Message); return false; }
            LastError = ""; run = true; L("listening on UDP " + port);
            th = new Thread(Loop); th.IsBackground = true; th.Start(); return true;
        }
        public void Stop()
        {
            run = false; try { if (sock != null) sock.Close(); } catch { } sock = null;
            if (th != null) { try { th.Join(800); } catch { } th = null; }
        }
        void Loop()
        {
            byte[] buf = new byte[65536]; Socket s = sock;
            while (run && s != null)
            {
                try
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = s.ReceiveFrom(buf, ref ep);
                    Handle(buf, n, ep.ToString());
                }
                catch (SocketException e) { if (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock) continue; if (!run) break; Thread.Sleep(200); }
                catch (ObjectDisposedException) { break; }
                catch { Thread.Sleep(200); }
            }
        }

        static string Snip(byte[] d, int n) { string s = Encoding.UTF8.GetString(d, 0, Math.Max(0, Math.Min(n, 160))); s = Regex.Replace(s, "[\\r\\n\\t ]+", " ").Trim(); return s; }

        // Public so the self-test can drive it without sockets.
        public void Handle(byte[] d, int n, string from)
        {
            try { HandleInner(d, n, from); }
            catch (Exception e) { L(from + "  packet caused an internal error (ignored): " + e.Message); }
        }
        void HandleInner(byte[] d, int n, string from)
        {
            if (d == null) return; if (n < 0 || n > d.Length) n = d.Length;
            Packets++; LastPacket = DateTime.UtcNow;
            Feed f = FeedParser.Parse(d, n);
            if (f == null) { try { f = FeedParser.ParseAdif(Encoding.UTF8.GetString(d, 0, n)); } catch { f = null; } }
            if (f == null) { L(from + "  UNRECOGNISED (" + n + " bytes): " + Snip(d, n)); return; }
            Recognized++;
            if (StationFilter.Length > 0 && f.Station.Length > 0 && !f.Station.Equals(StationFilter, StringComparison.OrdinalIgnoreCase)) { L(from + "  " + f.Kind + " from station '" + f.Station + "' ignored (filter '" + StationFilter + "')"); return; }
            if (!f.HasFreq) { L(from + "  " + f.Kind + " (no frequency, ignored)"); return; }

            double now = double.NaN; string src = f.Kind;
            if (f.Radio)
            {
                string st = f.Station ?? ""; int rn = f.RadioNr > 0 ? f.RadioNr : 1;
                double fr = (f.Split && !double.IsNaN(f.TxMHz)) ? f.TxMHz : (!double.IsNaN(f.RxMHz) ? f.RxMHz : f.TxMHz);
                if (double.IsNaN(fr)) return;
                lock (radios)
                {
                    radios[st + "#" + rn] = fr;
                    int fo = f.FocusNr > 0 ? f.FocusNr : rn; focus[st] = fo;
                    int want = RadioSel > 0 ? RadioSel : fo;
                    double w; if (radios.TryGetValue(st + "#" + want, out w)) now = w;
                }
                lastRadioState = DateTime.UtcNow;
                if (double.IsNaN(now)) { L(from + "  " + f.Kind + " radio " + rn + " " + fr.ToString("0.000", CultureInfo.InvariantCulture) + " MHz (not the selected radio)"); return; }
            }
            else
            {
                if ((DateTime.UtcNow - lastRadioState).TotalSeconds < 120) { L(from + "  " + f.Kind + " event ignored (live radio data is current)"); return; }
                now = !double.IsNaN(f.TxMHz) ? f.TxMHz : (!double.IsNaN(f.RxMHz) ? f.RxMHz : f.BandMHz);
            }
            bool changed = double.IsNaN(FreqMHz) || Math.Abs(now - FreqMHz) > 0.001;
            FreqMHz = now; FreqWhen = DateTime.UtcNow; FreqSource = src;
            if (changed) { Version++; L(from + "  " + src + " -> " + now.ToString("0.000", CultureInfo.InvariantCulture) + " MHz (" + Bands.Label(now) + ")"); }
        }

        public string Status()
        {
            if (!run) return LastError.Length > 0 ? "Cannot listen: " + LastError : "Not listening.";
            string h = "Listening on UDP " + Port + ". ";
            if (Packets == 0) return h + "No packets yet.";
            double age = (DateTime.UtcNow - LastPacket).TotalSeconds;
            if (HasFreq) return h + "Radio: " + Bands.Describe(FreqMHz) + " via " + FreqSource + ", " + (age < 90 ? age.ToString("0") + " s ago." : ((int)(age / 60)) + " min ago.");
            return h + Packets + " packet(s), none with a usable frequency (Help > Radio feed diagnostics).";
        }
    }
}
