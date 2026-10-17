// N1TA MOF/LOF - main window. Deliberately plain: menu bar, a map, a status bar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    public enum Mode { MofShort, MofLong, MofComp, LofShort, LofLong, LofComp, Mof3k, Lof3k }
    public enum Proj { Rect, North, Centered }
    public enum BandView { Off, Follow, Manual }

    public class MapData
    {
        public int Nx, Ny; public double Step;
        public float[] MofS, MofL, LofS, LofL, Mof3, Lof3;
    }

    public class Legend : Control
    {
        public string[] Labels = new string[0]; public Color[] Col = new Color[0];
        public Legend() { SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true); Height = 22; Dock = DockStyle.Bottom; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(SystemColors.Control);
            int x = 6;
            for (int i = 0; i < Labels.Length; i++)
            {
                if (Col[i] != Color.Empty)
                {
                    using (Brush b = new SolidBrush(Col[i])) e.Graphics.FillRectangle(b, x, 5, 11, 11);
                    e.Graphics.DrawRectangle(Pens.Black, x, 5, 11, 11);
                    x += 15;
                }
                SizeF sz = e.Graphics.MeasureString(Labels[i], Font);
                e.Graphics.DrawString(Labels[i], Font, SystemBrushes.ControlText, x, 3);
                x += (int)sz.Width + 12;
            }
        }
    }

    public class MapView : Control
    {
        public Bitmap Bmp;
        public MapView() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); BackColor = Color.Black; Dock = DockStyle.Fill; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            if (Bmp != null) e.Graphics.DrawImageUnscaled(Bmp, 0, 0);
        }
    }

    public class MainForm : Form
    {
        // ---- state ----
        string qGrid = ""; double qLat, qLon;          // set from the grid square the user enters at startup
        const double MiPerKm = 0.621371;
        DateTime T = DateTime.UtcNow;
        bool live = true, auto = true;
        double manSfi = 120, manA = 8, manK = 2;
        SpaceWx noaa = new SpaceWx(); DateTime noaaWhen = DateTime.MinValue; string noaaStatus = "NOAA: not yet fetched";
        List<OutlookRow> outlook;
        Mode mode = Mode.MofShort; Proj proj = Proj.Rect;
        bool ovSun = true, ovRadials = false, ovGrid = true;
        double step = 4.0;
        Cond cond = new Cond(); string dataSrc = "";
        bool hasTarget = false; double tLat, tLon; string tGrid = "";
        BestBandView best = new BestBandView();
        UdpTracker udp = new UdpTracker(); bool udpOn = false; int udpPort = 12060, udpRadio = 0; string udpStation = "";
        BandView bandView = BandView.Off; int manualBand = 5; int seenVer = -1; bool seenHas = false;
        System.Windows.Forms.Timer udpPoll = new System.Windows.Forms.Timer(); MenuItem[] miBandView;
        MapData md; List<double[]> coast = new List<double[]>();

        // ---- ui ----
        MapView map = new MapView(); Label info = new Label(); Legend legend = new Legend();
        StatusBar sb = new StatusBar(); StatusBarPanel pNoaa = new StatusBarPanel(); Label readout = new Label();
        System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer(), anim = new System.Windows.Forms.Timer(), rsz = new System.Windows.Forms.Timer();
        MenuItem miLive, miAnim; MenuItem[] miMode, miProj, miRes; MenuItem miSun, miRad, miGrid;
        // layout of last render
        int ox, oy, mw, mh, cx, cy; double rad;
        DateTime lastNoaaTry = DateTime.MinValue;

        public MainForm()
        {
            Text = "N1TA MOF/LOF"; ClientSize = new Size(900, 560); MinimumSize = new Size(560, 380);
            KeyPreview = true; StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            LoadSettings(); LoadCoast();

            info.Dock = DockStyle.Top; info.Height = 20; info.Font = new Font("Courier New", 9f); info.TextAlign = ContentAlignment.MiddleLeft;
            readout.Dock = DockStyle.Bottom; readout.Height = 36; readout.Font = new Font("Courier New", 9f); readout.Padding = new Padding(4, 2, 0, 0);
            sb.ShowPanels = true; pNoaa.AutoSize = StatusBarPanelAutoSize.Spring; sb.Panels.Add(pNoaa);
            Controls.Add(map); Controls.Add(legend); Controls.Add(readout); Controls.Add(best); Controls.Add(info); Controls.Add(sb);
            map.BringToFront();
            BuildMenu();
            map.MouseMove += OnMouse; map.MouseLeave += delegate { readout.Text = ""; };
            map.MouseDown += delegate { map.Focus(); };
            map.MouseClick += delegate (object so, MouseEventArgs me)
            {
                if (me.Button == MouseButtons.Right) { ClearTarget(); return; }
                double la, lo; if (md != null && Inverse(me.X + 0.5, me.Y + 0.5, out la, out lo)) SetTarget(la, lo);
            };
            map.SizeChanged += delegate { rsz.Stop(); rsz.Start(); };
            sb.PanelClick += delegate { ShowStart(false); };
            rsz.Interval = 120; rsz.Tick += delegate { rsz.Stop(); Redraw(); };
            tick.Interval = 15000; tick.Tick += OnTick; tick.Start();
            anim.Interval = 700; anim.Tick += delegate { Advance(1); };
            udpPoll.Interval = 1000; udpPoll.Tick += delegate { PollUdp(); }; udpPoll.Start();
            Shown += delegate { BeginInvoke(new MethodInvoker(Startup)); };
            FormClosing += delegate { SaveSettings(); udp.Stop(); };
        }

        // Ask for the grid square every time the program opens (pre-filled with last time's answer).
        void Startup()
        {
            if (live) T = DateTime.UtcNow;
            if (udpOn) ApplyUdp(false);
            ShowStart(true);
        }

        // ================= menu =================
        void BuildMenu()
        {
            MainMenu m = new MainMenu();
            MenuItem file = new MenuItem("&File");
            file.MenuItems.Add(new MenuItem("&Save map as BMP...", delegate { SaveBmp(); }));
            file.MenuItems.Add(new MenuItem("-"));
            file.MenuItems.Add(new MenuItem("E&xit", delegate { Close(); }));

            MenuItem calc = new MenuItem("&Calculations");
            calc.MenuItems.Add(new MenuItem("&Location, date and solar data...", delegate { ShowStart(false); }, Shortcut.CtrlS));
            calc.MenuItems.Add(new MenuItem("&Target grid square...\tT", delegate { PromptTarget(); }));
            calc.MenuItems.Add(new MenuItem("Clear target\tEsc", delegate { ClearTarget(); }));
            calc.MenuItems.Add(new MenuItem("-"));
            calc.MenuItems.Add(new MenuItem("&Next hour\tEnter", delegate { Advance(1); }));
            calc.MenuItems.Add(new MenuItem("&Previous hour\tBackspace", delegate { Advance(-1); }));
            miAnim = new MenuItem("&Animate\tA", delegate { ToggleAnim(); }); calc.MenuItems.Add(miAnim);
            miLive = new MenuItem("L&ive (follow UTC clock)\tL", delegate { SetLive(!live); }); calc.MenuItems.Add(miLive);
            calc.MenuItems.Add(new MenuItem("-"));
            calc.MenuItems.Add(new MenuItem("&Refresh NOAA data now\tF5", delegate { FetchAsync(); }));

            MenuItem disp = new MenuItem("&Display");
            string[] names = { "1  Short path MOF", "2  Long path MOF", "3  Composite MOF", "4  Short path LOF", "5  Long path LOF", "6  Composite LOF", "7  3000 km (1864 mi) single-hop MOF", "8  3000 km (1864 mi) single-hop LOF" };
            miMode = new MenuItem[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int k = i; miMode[i] = new MenuItem(names[i], delegate { SetMode((Mode)k); }); miMode[i].RadioCheck = true; disp.MenuItems.Add(miMode[i]);
            }
            disp.MenuItems.Add(new MenuItem("-"));
            string[] pn = { "&Rectangular\tR", "&North polar\tX", "Polar, &centered on QTH\tG" };
            miProj = new MenuItem[3];
            for (int i = 0; i < 3; i++) { int k = i; miProj[i] = new MenuItem(pn[i], delegate { SetProj((Proj)k); }); miProj[i].RadioCheck = true; disp.MenuItems.Add(miProj[i]); }
            disp.MenuItems.Add(new MenuItem("-"));
            miSun = new MenuItem("&Sunrise / sunset lines\tS", delegate { ovSun = !ovSun; Redraw(); SyncMenu(); });
            miRad = new MenuItem("Great-circle &radials\tP", delegate { ovRadials = !ovRadials; Redraw(); SyncMenu(); });
            miGrid = new MenuItem("&Lat/lon grid", delegate { ovGrid = !ovGrid; Redraw(); SyncMenu(); });
            disp.MenuItems.Add(miSun); disp.MenuItems.Add(miRad); disp.MenuItems.Add(miGrid);
            disp.MenuItems.Add(new MenuItem("-"));
            MenuItem res = new MenuItem("Resolution");
            double[] steps = { 2, 4, 6, 9 }; miRes = new MenuItem[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                double s = steps[i]; miRes[i] = new MenuItem(string.Format("{0} degree cells", s), delegate { step = s; Recompute(); SyncMenu(); }); miRes[i].RadioCheck = true; res.MenuItems.Add(miRes[i]);
            }
            disp.MenuItems.Add(res);
            disp.MenuItems.Add(new MenuItem("-"));
            MenuItem bv = new MenuItem("Single &band view");
            miBandView = new MenuItem[2 + Bands.ExtName.Length];
            miBandView[0] = new MenuItem("Off (normal maps)", delegate { SetBandView(BandView.Off, 0); });
            miBandView[1] = new MenuItem("Follow the radio (UDP)	B", delegate { SetBandView(BandView.Follow, 0); });
            for (int i = 0; i < Bands.ExtName.Length; i++)
            {
                int k = i; miBandView[2 + i] = new MenuItem(Bands.ExtName[i] + " m   (" + Bands.ExtCenter[i].ToString("0.0#", CultureInfo.InvariantCulture) + " MHz)", delegate { SetBandView(BandView.Manual, k); });
            }
            for (int i = 0; i < miBandView.Length; i++) { miBandView[i].RadioCheck = true; bv.MenuItems.Add(miBandView[i]); }
            disp.MenuItems.Add(bv);

            MenuItem help = new MenuItem("&Help");
            help.MenuItems.Add(new MenuItem("&Notes", delegate { MessageBox.Show(this, HelpText.Notes, "N1TA MOF/LOF - Notes"); }));
            help.MenuItems.Add(new MenuItem("NOAA &diagnostics", delegate { ShowDiag(); }));
            help.MenuItems.Add(new MenuItem("&Radio feed diagnostics", delegate { ShowRadioDiag(); }));
            help.MenuItems.Add(new MenuItem("&About", delegate { MessageBox.Show(this, HelpText.About, "About N1TA MOF/LOF"); }));
            m.MenuItems.AddRange(new MenuItem[] { file, calc, disp, help });
            Menu = m; SyncMenu();
        }
        void SyncMenu()
        {
            for (int i = 0; i < miMode.Length; i++) miMode[i].Checked = (int)mode == i;
            for (int i = 0; i < miProj.Length; i++) miProj[i].Checked = (int)proj == i;
            double[] steps = { 2, 4, 6, 9 }; for (int i = 0; i < miRes.Length; i++) miRes[i].Checked = steps[i] == step;
            miSun.Checked = ovSun; miRad.Checked = ovRadials; miGrid.Checked = ovGrid;
            miLive.Checked = live; miAnim.Checked = anim.Enabled;
            for (int i = 0; i < miBandView.Length; i++)
                miBandView[i].Checked = (i == 0 && bandView == BandView.Off) || (i == 1 && bandView == BandView.Follow) || (i >= 2 && bandView == BandView.Manual && manualBand == i - 2);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys k = e.KeyCode; bool h = true;
            if (k >= Keys.D1 && k <= Keys.D8) SetMode((Mode)(k - Keys.D1));
            else if (k >= Keys.NumPad1 && k <= Keys.NumPad8) SetMode((Mode)(k - Keys.NumPad1));
            else if (k == Keys.Enter || k == Keys.Space || k == Keys.Right) Advance(1);
            else if (k == Keys.Back || k == Keys.Left) Advance(-1);
            else if (k == Keys.R) SetProj(Proj.Rect); else if (k == Keys.X) SetProj(Proj.North); else if (k == Keys.G) SetProj(Proj.Centered);
            else if (k == Keys.S) { ovSun = !ovSun; Redraw(); SyncMenu(); }
            else if (k == Keys.P) { ovRadials = !ovRadials; Redraw(); SyncMenu(); }
            else if (k == Keys.A) ToggleAnim();
            else if (k == Keys.L) SetLive(!live);
            else if (k == Keys.F5) FetchAsync();
            else if (k == Keys.T) PromptTarget();
            else if (k == Keys.B) ToggleBandView();
            else if (k == Keys.Escape) ClearTarget();
            else h = false;
            if (h) e.Handled = true; else base.OnKeyDown(e);
        }

        // ================= actions =================
        void SetMode(Mode m) { mode = m; bandView = BandView.Off; UpdateBest(); Redraw(); SyncMenu(); }
        void SetProj(Proj p) { proj = p; Redraw(); SyncMenu(); }
        void SetLive(bool v) { live = v; if (live) { T = DateTime.UtcNow; Recompute(); } SyncMenu(); }
        void ToggleAnim() { anim.Enabled = !anim.Enabled; if (anim.Enabled) live = false; SyncMenu(); }
        void Advance(int hours) { live = false; T = T.AddHours(hours); Recompute(); SyncMenu(); }
        void OnTick(object s, EventArgs e)
        {
            if (!live) return;
            DateTime n = DateTime.UtcNow;
            if (n.Minute != T.Minute || n.Hour != T.Hour || n.Date != T.Date) { T = n; Recompute(); }
            if (auto && (DateTime.UtcNow - noaaWhen).TotalMinutes > 30 && (DateTime.UtcNow - lastNoaaTry).TotalMinutes > 5) FetchAsync();
        }
        bool ShowStart(bool startup)
        {
            using (StartDialog d = new StartDialog(qGrid, T, live, auto, manSfi, manA, manK, noaa, outlook, startup, udpOn, udpPort, udpRadio, udpStation, udp.Status))
            {
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    if (startup)
                    {
                        if (qGrid.Length == 0) { Close(); return false; }    // first run and no location: nothing to draw
                        Recompute(); if (auto) FetchAsync();
                    }
                    return false;
                }
                qGrid = d.GridText; qLat = d.Lat; qLon = d.Lon; T = d.When; auto = d.Auto; live = d.Live;
                manSfi = d.Sfi; manA = d.A; manK = d.K;
                udpOn = d.UdpOn; udpPort = d.UdpPort; udpRadio = d.UdpRadio; udpStation = d.UdpStation; ApplyUdp(true);
                if (d.FetchedCurrent != null) { MergeNoaa(d.FetchedCurrent); }
                if (d.FetchedOutlook != null) outlook = d.FetchedOutlook;
                SaveSettings();
                Recompute(); SyncMenu();
                if (auto) FetchAsync();
                return true;
            }
        }
        void ShowDiag()
        {
            using (Form f = new Form())
            {
                f.Text = "NOAA diagnostics"; f.ClientSize = new Size(760, 400); f.StartPosition = FormStartPosition.CenterParent;
                TextBox t = new TextBox(); t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Both; t.WordWrap = false; t.Dock = DockStyle.Fill; t.Font = new Font("Courier New", 9f);
                t.Text = "Copy this into a GitHub issue if NOAA data does not load.\r\n\r\nCurrent: SFI " + F(noaa.Sfi) + "  A " + F(noaa.A) + "  K " + F(noaa.K) + "   status: " + noaaStatus + "\r\n\r\n" + Noaa.LogText().Replace("\n", "\r\n");
                f.Controls.Add(t); t.Select(0, 0); f.ShowDialog(this);
            }
        }
        static string F(double v) { return double.IsNaN(v) ? "-" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        void MergeNoaa(SpaceWx w)
        {
            if (!double.IsNaN(w.Sfi)) noaa.Sfi = w.Sfi; if (!double.IsNaN(w.A)) noaa.A = w.A; if (!double.IsNaN(w.K)) noaa.K = w.K;
            noaa.Note = w.Note;
            if (w.Complete) { noaaWhen = DateTime.UtcNow; noaaStatus = "NOAA: ok " + noaaWhen.ToString("HH:mm") + "Z"; }
            else noaaStatus = "NOAA: " + w.Note;
        }
        void SaveBmp()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Bitmap (*.bmp)|*.bmp|PNG (*.png)|*.png"; d.FileName = "N1TA_MOFLOF_" + T.ToString("yyyyMMdd_HHmm") + "Z";
                if (d.ShowDialog(this) != DialogResult.OK || map.Bmp == null) return;
                map.Bmp.Save(d.FileName, d.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Png : ImageFormat.Bmp);
            }
        }

        // ================= NOAA =================
        void FetchAsync()
        {
            lastNoaaTry = DateTime.UtcNow;
            pNoaa.Text = "NOAA: fetching..."; bool needOutlook = T.Date > DateTime.UtcNow.Date || live == false;
            Thread t = new Thread(delegate ()
            {
                SpaceWx w = null; List<OutlookRow> o = null; string err = null;
                try { w = Noaa.FetchCurrent(); } catch (Exception ex) { err = ex.Message; }
                if (needOutlook) { try { o = Noaa.FetchOutlook(); } catch (Exception ex) { if (err == null) err = "outlook: " + ex.Message; } }
                try { BeginInvoke(new MethodInvoker(delegate { FetchDone(w, o, err); })); } catch { }
            });
            t.IsBackground = true; t.Start();
        }
        void FetchDone(SpaceWx w, List<OutlookRow> o, string err)
        {
            if (w != null) MergeNoaa(w); else noaaStatus = "NOAA: unreachable";
            if (o != null && o.Count > 0) outlook = o;
            Recompute();
        }
        // Pick the numbers for the time being displayed.
        void ChooseConditions(out double sfi, out double a, out double k, out string src)
        {
            sfi = manSfi; a = manA; k = manK; src = "manual values";
            if (!auto) return;
            double ns = noaa.Sfi, na = noaa.A, nk = noaa.K; src = "NOAA now";
            if (T.Date > DateTime.UtcNow.Date && outlook != null)
                foreach (OutlookRow r in outlook) if (r.Date.Date == T.Date) { ns = r.Sfi; na = r.A; nk = r.K; src = "NOAA 27-day outlook"; break; }
            int got = 0;
            if (!double.IsNaN(ns)) { sfi = ns; got++; } if (!double.IsNaN(na)) { a = na; got++; } if (!double.IsNaN(nk)) { k = nk; got++; }
            if (got == 0) src = "manual values (no NOAA data yet)"; else if (got < 3) src = src + " + manual fill";
            if (T.Date < DateTime.UtcNow.Date && got > 0) src += " (past date: today's values)";
            else if (src == "NOAA now" && T.Date > DateTime.UtcNow.Date.AddDays(27)) src += " (beyond 27-day outlook)";
        }

        // ================= single band view + radio feed =================
        double ViewFreq
        {
            get
            {
                if (bandView == BandView.Follow) return udp.HasFreq ? udp.FreqMHz : double.NaN;
                if (bandView == BandView.Manual) return Bands.ExtCenter[manualBand];
                return double.NaN;
            }
        }
        bool BandActive { get { double f = ViewFreq; return bandView != BandView.Off && !double.IsNaN(f) && Bands.Modelled(f); } }
        string ModeName(string[] mn)
        {
            if (BandActive) return Bands.Describe(ViewFreq).ToUpperInvariant() + " BAND VIEW" + (bandView == BandView.Follow ? " - FOLLOWING RADIO" : "");
            return mn[(int)mode];
        }
        string RadioStatusSuffix()
        {
            if (!udpOn) return "";
            return "  |  Radio: " + (!udp.Running ? "feed not running" : (udp.HasFreq ? Bands.Label(udp.FreqMHz) : "waiting for data on UDP " + udp.Port));
        }
        void SetBandView(BandView v, int idx)
        {
            if (v == BandView.Follow && !udp.Running) { ShowStart(false); return; }
            bandView = v; if (v == BandView.Manual) manualBand = idx;
            UpdateBest(); Redraw(); SyncMenu();
        }
        void ToggleBandView() { if (bandView != BandView.Off) SetBandView(BandView.Off, 0); else if (udp.Running) SetBandView(BandView.Follow, 0); else SetBandView(BandView.Manual, manualBand); }
        void ApplyUdp(bool userAction)
        {
            udp.Stop();
            if (!udpOn) { if (bandView == BandView.Follow) bandView = BandView.Off; UpdateInfoSafe(); return; }
            udp.RadioSel = udpRadio; udp.StationFilter = udpStation ?? "";
            string err;
            if (!udp.Start(udpPort, out err))
            {
                MessageBox.Show(this, "Could not listen on UDP port " + udpPort + ":\r\n" + err + "\r\n\r\nAnother program may already be using that port. Choose a different port here, and add it as an extra destination (for example 127.0.0.1:12061) in your logging program's broadcast settings.", "Radio feed");
                return;
            }
            if (userAction || bandView == BandView.Off) bandView = BandView.Follow;
            seenVer = -1; seenHas = false; UpdateInfoSafe();
        }
        void UpdateInfoSafe() { if (md != null) { UpdateBest(); Redraw(); } SyncMenu(); }
        void PollUdp()
        {
            bool has = udp.HasFreq;
            if (udp.Version != seenVer || has != seenHas) { seenVer = udp.Version; seenHas = has; if (md != null && bandView == BandView.Follow) { UpdateBest(); Redraw(); } }
        }
        void ShowRadioDiag()
        {
            using (Form f = new Form())
            {
                f.Text = "Radio feed diagnostics"; f.ClientSize = new Size(760, 400); f.StartPosition = FormStartPosition.CenterParent;
                TextBox t = new TextBox(); t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Both; t.WordWrap = false; t.Dock = DockStyle.Fill; t.Font = new Font("Courier New", 9f);
                t.Text = "Copy this into a GitHub issue if your logger is not recognised.\r\n\r\n" + udp.Status() + "\r\nPackets: " + udp.Packets + " received, " + udp.Recognized + " recognised\r\n\r\n" + udp.LogText();
                f.Controls.Add(t); t.Select(0, 0); f.ShowDialog(this);
            }
        }

        // ================= target + best band =================
        static string StateWord(BandState s) { return s == BandState.Open ? "OPEN" : (s == BandState.Marginal ? "MARGINAL" : "CLOSED"); }
        static string BandLabel(int i) { return i < 0 ? "NONE" : Bands.Name[i] + " M"; }
        class PathVals { public double Ms, Ml, Ls, Ll; }
        PathVals Pv(double la, double lo)
        {
            double a1 = Prop.Rad(qLat), o1 = Prop.Rad(qLon), a2 = Prop.Rad(la), o2 = Prop.Rad(lo); PathVals p = new PathVals();
            p.Ms = Prop.PathMof(a1, o1, a2, o2, false, cond); p.Ml = Prop.PathMof(a1, o1, a2, o2, true, cond);
            p.Ls = Prop.PathLof(a1, o1, a2, o2, false, cond); p.Ll = Prop.PathLof(a1, o1, a2, o2, true, cond); return p;
        }
        void SetTarget(double la, double lo) { hasTarget = true; tLat = la; tLon = lo; tGrid = Grid.From(la, lo, 6); UpdateBest(); Redraw(); }
        void ClearTarget() { if (!hasTarget) return; hasTarget = false; UpdateBest(); Redraw(); }
        void PromptTarget()
        {
            using (Form f = new Form())
            {
                f.Text = "Target"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = f.MinimizeBox = false; f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(330, 112);
                Label l = new Label(); l.Text = "Target grid square (e.g. JN58 or JN58qm)"; l.Left = 12; l.Top = 12; l.Width = 300; f.Controls.Add(l);
                TextBox t = new TextBox(); t.Left = 12; t.Top = 36; t.Width = 120; t.Text = hasTarget ? tGrid : ""; t.MaxLength = 8; f.Controls.Add(t);
                Label msg = new Label(); msg.Left = 145; msg.Top = 39; msg.Width = 175; msg.ForeColor = Color.DarkRed; f.Controls.Add(msg);
                Button ok = new Button(); ok.Text = "OK"; ok.Left = 12; ok.Top = 72; ok.Width = 80;
                ok.Click += delegate { double la, lo; if (Grid.TryParse(t.Text, out la, out lo)) { f.DialogResult = DialogResult.OK; f.Close(); } else msg.Text = "Not a valid grid square."; };
                Button clr = new Button(); clr.Text = "Clear target"; clr.Left = 100; clr.Top = 72; clr.Width = 100; clr.Click += delegate { f.DialogResult = DialogResult.Abort; f.Close(); };
                Button cn = new Button(); cn.Text = "Cancel"; cn.Left = 208; cn.Top = 72; cn.Width = 80; cn.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
                f.Controls.Add(ok); f.Controls.Add(clr); f.Controls.Add(cn); f.AcceptButton = ok; f.CancelButton = cn;
                DialogResult r = f.ShowDialog(this);
                if (r == DialogResult.OK) { double la, lo; if (Grid.TryParse(t.Text, out la, out lo)) SetTarget(la, lo); }
                else if (r == DialogResult.Abort) ClearTarget();
            }
        }

        void UpdateBest()
        {
            if (cond == null) return;
            double ft = ViewFreq; bool tracked = bandView != BandView.Off && !double.IsNaN(ft) && Bands.Modelled(ft);
            int tIdx = tracked ? Bands.ContestIndex(ft) : -1;
            string note = "";
            if (bandView == BandView.Follow)
            {
                if (!udp.Running) note = "Radio feed is not running. ";
                else if (!udp.HasFreq) note = "Waiting for radio data on UDP " + udp.Port + ". ";
                else if (!Bands.Modelled(udp.FreqMHz)) note = "Radio is on " + Bands.Label(udp.FreqMHz) + ", outside the HF model. ";
            }
            BandState[] st = new BandState[6]; best.Caption = "BEST BAND";
            for (int i = 0; i < 6; i++) best.Sub[i] = "";
            string capt = bandView == BandView.Follow ? "RADIO ON" : "BAND";
            double la1 = Prop.Rad(qLat), lo1 = Prop.Rad(qLon);

            if (hasTarget)
            {
                PathVals tv = Pv(tLat, tLon); bool[] lp = new bool[6];
                for (int i = 0; i < 6; i++) { st[i] = Bands.Eval(tv.Ms, tv.Ls, tv.Ml, tv.Ll, Bands.Freq[i], out lp[i]); best.Sub[i] = st[i] == BandState.Closed ? "closed" : (lp[i] ? "long path" : "short path"); }
                int b = Bands.Best(st); double km = Prop.Dist(la1, lo1, Prop.Rad(tLat), Prop.Rad(tLon)) * Prop.R;
                string dist = string.Format(CultureInfo.InvariantCulture, "To {0}: {1:0} km / {2:0} mi", tGrid, km, km * MiPerKm);
                string nvis = km < 1000 ? " Under 1000 km: NVIS / ground wave are not modelled." : "";
                best.St = st;
                if (tracked)
                {
                    bool tl; BandState ts = Bands.Eval(tv.Ms, tv.Ls, tv.Ml, tv.Ll, ft, out tl);
                    best.Caption = capt; best.Big = Bands.Label(ft); best.BigColor = BestBandView.StateColor(ts); best.Best = tIdx;
                    if (b >= 0) best.Sub[b] = "BEST";
                    best.Text1 = dist + ". " + Bands.Label(ft) + " is " + StateWord(ts) + (ts != BandState.Closed ? (tl ? ", long path." : ", short path.") : ".");
                    best.Text2 = note + (b < 0 ? "No contest band looks usable to this target right now." : (b == tIdx ? "This is the best band for this target." : "Best band for this target: " + BandLabel(b) + " (" + StateWord(st[b]) + ").")) + nvis;
                }
                else
                {
                    best.Best = b; best.Big = BandLabel(b); best.BigColor = b < 0 ? Color.Gray : BestBandView.StateColor(st[b]);
                    string next = ""; int c = 0;
                    for (int i = b - 1; i >= 0 && c < 2; i--) if (st[i] == BandState.Open) { next += (next.Length > 0 ? ", " : "") + Bands.Name[i] + " M"; c++; }
                    best.Text1 = dist + (b >= 0 ? (st[b] == BandState.Open ? ". Open" : ". Marginal") + (Bands.Eval(tv.Ms, tv.Ls, tv.Ml, tv.Ll, Bands.Freq[b], out lp[b]) != BandState.Closed && lp[b] ? ", long path." : ", short path.") : ".");
                    best.Text2 = note + (b < 0 ? "No band looks usable to this target right now." : (next.Length > 0 ? "Also good: " + next + "." : "")) + nvis + "  Right-click or Esc for the world summary.";
                }
            }
            else
            {
                int n = 0; int[] open = new int[6], any = new int[6]; List<string>[] who = new List<string>[6];
                for (int i = 0; i < 6; i++) who[i] = new List<string>();
                int openT = 0, anyT = 0; List<string> whoT = new List<string>();
                foreach (Region r in Bands.Regions)
                {
                    if (Prop.Dist(la1, lo1, Prop.Rad(r.Lat), Prop.Rad(r.Lon)) * Prop.R < Bands.MinRegionKm) continue;
                    n++; PathVals p = Pv(r.Lat, r.Lon); bool l;
                    for (int i = 0; i < 6; i++)
                    {
                        BandState s = Bands.Eval(p.Ms, p.Ls, p.Ml, p.Ll, Bands.Freq[i], out l);
                        if (s == BandState.Open) { open[i]++; who[i].Add(r.Code); }
                        if (s != BandState.Closed) any[i]++;
                    }
                    if (tracked) { BandState s = Bands.Eval(p.Ms, p.Ls, p.Ml, p.Ll, ft, out l); if (s == BandState.Open) { openT++; whoT.Add(r.Code); } if (s != BandState.Closed) anyT++; }
                }
                int need = Bands.NeededRegions(n); bool strong; int b = Bands.BestByCount(open, any, n, out strong);
                for (int i = 0; i < 6; i++) { st[i] = open[i] >= need ? BandState.Open : (any[i] > 0 ? BandState.Marginal : BandState.Closed); best.Sub[i] = n == 0 ? "" : open[i] + " / " + n; }
                best.St = st;
                string next = ""; int c = 0;
                for (int i = b - 1; i >= 0 && c < 2; i--) if (open[i] >= need) { next += (next.Length > 0 ? ", " : "") + Bands.Name[i] + " M (" + open[i] + "/" + n + ")"; c++; }
                if (tracked)
                {
                    BandState ts = openT >= need ? BandState.Open : (anyT > 0 ? BandState.Marginal : BandState.Closed);
                    best.Caption = capt; best.Big = Bands.Label(ft); best.BigColor = BestBandView.StateColor(ts); best.Best = tIdx;
                    if (b >= 0) best.Sub[b] = "BEST " + open[b] + "/" + n;
                    best.Text1 = "Radio on " + Bands.Describe(ft) + ": " + (whoT.Count > 0 ? "open to " + string.Join(" ", whoT.ToArray()) : (anyT > 0 ? "marginal only" : "closed to all distant regions")) + " (" + openT + "/" + n + ").";
                    best.Text2 = note + (b < 0 ? "No contest band reaches distant regions right now." : (b == tIdx ? "This is the best band right now." : "Best now: " + BandLabel(b) + " (" + open[b] + "/" + n + ")" + (b > tIdx && strong ? ". Consider moving up." : "."))) + "  Click the map for a specific target.";
                }
                else
                {
                    best.Best = b; best.Big = BandLabel(b); best.BigColor = b < 0 ? Color.Gray : (strong ? BestBandView.StateColor(BandState.Open) : BestBandView.StateColor(BandState.Marginal));
                    best.Text1 = b < 0 ? "No band reaches distant regions right now." : (strong ? "Open to: " : "Weak. Open to only: ") + (who[b].Count > 0 ? string.Join(" ", who[b].ToArray()) : "(marginal only)");
                    best.Text2 = note + "Highest band open to at least " + need + " of " + n + " distant regions (over " + Bands.MinRegionKm.ToString("0") + " km / " + (Bands.MinRegionKm * MiPerKm).ToString("0") + " mi)." + (next.Length > 0 ? " Next: " + next + "." : "") + "  Click map for a target.";
                }
            }
            best.Invalidate();
        }

        // ================= compute =================
        void Recompute()
        {
            double s, a, k; ChooseConditions(out s, out a, out k, out dataSrc);
            if (auto && T.Date > DateTime.UtcNow.Date && outlook == null && (DateTime.UtcNow - lastNoaaTry).TotalSeconds > 20) FetchAsync();
            cond = new Cond(); cond.Sfi = s; cond.A = a; cond.K = k; cond.Month = T.Month; cond.Day = T.Day; cond.Hour = T.Hour + T.Minute / 60.0; cond.Derive();
            ComputeGrid(); UpdateBest(); Redraw();
        }
        void ComputeGrid()
        {
            MapData d = new MapData(); d.Step = step; d.Nx = (int)(360 / step); d.Ny = (int)(180 / step);
            int n = d.Nx * d.Ny; d.MofS = new float[n]; d.MofL = new float[n]; d.LofS = new float[n]; d.LofL = new float[n]; d.Mof3 = new float[n]; d.Lof3 = new float[n];
            double la1 = Prop.Rad(qLat), lo1 = Prop.Rad(qLon);
            for (int j = 0; j < d.Ny; j++)
            {
                double la = Prop.Rad(90 - (j + 0.5) * step);
                for (int i = 0; i < d.Nx; i++)
                {
                    double lo = Prop.Rad(-180 + (i + 0.5) * step); int x = j * d.Nx + i;
                    d.MofS[x] = (float)Prop.PathMof(la1, lo1, la, lo, false, cond);
                    d.MofL[x] = (float)Prop.PathMof(la1, lo1, la, lo, true, cond);
                    d.LofS[x] = (float)Prop.PathLof(la1, lo1, la, lo, false, cond);
                    d.LofL[x] = (float)Prop.PathLof(la1, lo1, la, lo, true, cond);
                    d.Mof3[x] = (float)Prop.HopMof(la, lo, cond);
                    d.Lof3[x] = (float)Prop.HopLof(la, lo, cond);
                }
            }
            md = d;
        }

        // ================= drawing =================
        static readonly int[] Pal = { unchecked((int)0xFF000000), unchecked((int)0xFF2846FF), unchecked((int)0xFF00BE00), unchecked((int)0xFFD20000), unchecked((int)0xFFE6C800) };
        static int MofLevel(double v) { return v >= 28.5 ? 3 : (v >= 21.2 ? 2 : (v >= 14.15 ? 1 : 0)); }
        static int LofLevel(double v) { return v <= 1.85 ? 3 : (v <= 3.6 ? 2 : (v <= 7.1 ? 1 : 0)); }
        bool IsMof { get { return mode == Mode.MofShort || mode == Mode.MofLong || mode == Mode.MofComp || mode == Mode.Mof3k; } }

        void CellColor(int idx, out int level, out bool striped)
        {
            striped = false;
            switch (mode)
            {
                case Mode.MofShort: level = MofLevel(md.MofS[idx]); break;
                case Mode.MofLong: level = MofLevel(md.MofL[idx]); striped = true; break;
                case Mode.MofComp: { bool l = md.MofL[idx] > md.MofS[idx]; level = MofLevel(l ? md.MofL[idx] : md.MofS[idx]); striped = l; break; }
                case Mode.LofShort: level = LofLevel(md.LofS[idx]); break;
                case Mode.LofLong: level = LofLevel(md.LofL[idx]); striped = true; break;
                case Mode.LofComp: { bool l = md.LofL[idx] < md.LofS[idx]; level = LofLevel(l ? md.LofL[idx] : md.LofS[idx]); striped = l; break; }
                case Mode.Mof3k: level = MofLevel(md.Mof3[idx]); break;
                default: level = LofLevel(md.Lof3[idx]); break;
            }
        }

        bool Forward(double latDeg, double lonDeg, out float x, out float y)
        {
            x = y = 0;
            if (proj == Proj.Rect) { x = (float)(ox + (lonDeg + 180) / 360 * mw); y = (float)(oy + (90 - latDeg) / 180 * mh); return true; }
            if (proj == Proj.North)
            {
                double cl = (90 - latDeg) / 180, lr = Prop.Rad(lonDeg);
                x = (float)(cx + rad * cl * Math.Sin(lr)); y = (float)(cy + rad * cl * Math.Cos(lr)); return true;
            }
            double la = Prop.Rad(latDeg), lo = Prop.Rad(lonDeg), la0 = Prop.Rad(qLat), lo0 = Prop.Rad(qLon);
            double c = Prop.Dist(la0, lo0, la, lo) / Math.PI, az = Prop.Bearing(la0, lo0, la, lo);
            x = (float)(cx + rad * c * Math.Sin(az)); y = (float)(cy - rad * c * Math.Cos(az)); return true;
        }
        bool Inverse(double x, double y, out double latDeg, out double lonDeg)
        {
            latDeg = lonDeg = 0;
            if (proj == Proj.Rect)
            {
                double u = (x - ox) / mw, v = (y - oy) / mh; if (u < 0 || u >= 1 || v < 0 || v >= 1) return false;
                lonDeg = u * 360 - 180; latDeg = 90 - v * 180; return true;
            }
            double dx = x - cx, dy = y - cy, r = Math.Sqrt(dx * dx + dy * dy) / rad; if (r > 1) return false;
            if (proj == Proj.North) { latDeg = 90 - r * 180; lonDeg = Prop.Deg(Math.Atan2(dx, dy)); return true; }
            double la, lo; Prop.Dest(Prop.Rad(qLat), Prop.Rad(qLon), Math.Atan2(dx, -dy), r * Math.PI, out la, out lo);
            latDeg = Prop.Deg(la); lonDeg = Prop.Deg(lo); return true;
        }

        void Redraw()
        {
            if (md == null) return;
            int W = map.ClientSize.Width, H = map.ClientSize.Height; if (W < 40 || H < 40) return;
            if (proj == Proj.Rect) { mw = Math.Min(W, 2 * H); mh = mw / 2; ox = (W - mw) / 2; oy = (H - mh) / 2; }
            else { cx = W / 2; cy = H / 2; rad = Math.Min(W, H) / 2 - 3; }
            byte[] bl = null; bool[] bst = null;
            if (BandActive)
            {
                int nc = md.Nx * md.Ny; bl = new byte[nc]; bst = new bool[nc]; double bf = ViewFreq;
                for (int ii = 0; ii < nc; ii++)
                {
                    bool lp; BandState bs = Bands.Eval(md.MofS[ii], md.LofS[ii], md.MofL[ii], md.LofL[ii], bf, out lp);
                    bl[ii] = (byte)(bs == BandState.Open ? 2 : (bs == BandState.Marginal ? 4 : 0)); bst[ii] = lp && bs != BandState.Closed;
                }
            }
            int[] px = new int[W * H]; for (int i = 0; i < px.Length; i++) px[i] = Pal[0];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    double la, lo; if (!Inverse(x + 0.5, y + 0.5, out la, out lo)) continue;
                    int ci = (int)((lo + 180) / md.Step); if (ci < 0) ci = 0; if (ci >= md.Nx) ci = md.Nx - 1;
                    int cj = (int)((90 - la) / md.Step); if (cj < 0) cj = 0; if (cj >= md.Ny) cj = md.Ny - 1;
                    int lvl; bool st; int cidx = cj * md.Nx + ci; if (bl != null) { lvl = bl[cidx]; st = bst[cidx]; } else CellColor(cidx, out lvl, out st);
                    if (lvl == 0) continue;
                    if (st && ((x / 2) & 1) == 1) continue;
                    px[y * W + x] = Pal[lvl];
                }
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, bd.Scan0, px.Length); bmp.UnlockBits(bd);
            using (Graphics g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.None; Overlays(g, W, H); }
            map.Bmp = bmp; map.Invalidate();
            UpdateInfo();
        }

        void Line(Graphics g, Pen p, List<PointF> pts) { if (pts.Count > 1) g.DrawLines(p, pts.ToArray()); pts.Clear(); }
        double Jump { get { return proj == Proj.Rect ? mw / 2.5 : rad * 0.5; } }
        void Poly(Graphics g, Pen p, IEnumerable<double[]> ll)
        {
            List<PointF> pts = new List<PointF>(); float px = 0, py = 0; bool have = false;
            foreach (double[] q in ll)
            {
                float x, y; Forward(q[1], q[0], out x, out y);
                if (have && Math.Abs(x - px) + Math.Abs(y - py) > Jump) Line(g, p, pts);
                pts.Add(new PointF(x, y)); px = x; py = y; have = true;
            }
            Line(g, p, pts);
        }
        void Overlays(Graphics g, int W, int H)
        {
            if (ovGrid)
            {
                using (Pen gp = new Pen(Color.FromArgb(70, 70, 70)))
                {
                    for (int la = -60; la <= 60; la += 30) { List<double[]> l = new List<double[]>(); for (int lo = -180; lo <= 180; lo += 3) l.Add(new double[] { lo, la }); Poly(g, gp, l); }
                    for (int lo = -180; lo <= 150; lo += 30) { List<double[]> l = new List<double[]>(); for (int la = -90; la <= 90; la += 3) l.Add(new double[] { lo, la }); Poly(g, gp, l); }
                }
            }
            if (proj != Proj.Rect) g.DrawEllipse(Pens.Gray, (float)(cx - rad), (float)(cy - rad), (float)(2 * rad), (float)(2 * rad));
            else g.DrawRectangle(Pens.Gray, ox, oy, mw - 1, mh - 1);
            foreach (double[] c in coast)
            {
                List<double[]> l = new List<double[]>(); for (int i = 0; i < c.Length; i += 2) l.Add(new double[] { c[i], c[i + 1] });
                Poly(g, Pens.White, l);
            }
            if (ovRadials)
            {
                using (Pen rp = new Pen(Color.FromArgb(170, 170, 170)))
                    for (int b = 0; b < 360; b += 30)
                    {
                        List<double[]> l = new List<double[]>();
                        for (double d = 0; d <= Math.PI; d += 0.04) { double la, lo; Prop.Dest(Prop.Rad(qLat), Prop.Rad(qLon), Prop.Rad(b), d, out la, out lo); l.Add(new double[] { Prop.Deg(lo), Prop.Deg(la) }); }
                        Poly(g, rp, l);
                    }
            }
            if (ovSun)
            {
                using (Pen dash = new Pen(Color.White)) using (Pen solid = new Pen(Color.White, 2))
                {
                    dash.DashStyle = DashStyle.Dash;
                    List<double[]> run = new List<double[]>(); bool runRise = false, first = true;
                    for (int lo = -180; lo <= 180; lo += 2)
                    {
                        bool rise; double la = Prop.TermLat(cond, Prop.Rad(lo), out rise);
                        if (!first && rise != runRise) { Poly(g, runRise ? solid : dash, run); double[] last = run[run.Count - 1]; run.Clear(); run.Add(last); }
                        run.Add(new double[] { lo, Prop.Deg(la) }); runRise = rise; first = false;
                    }
                    Poly(g, runRise ? solid : dash, run);
                }
            }
            if (hasTarget)
            {
                using (Pen tp = new Pen(Color.FromArgb(255, 230, 0), 2))
                {
                    double d = Prop.Dist(Prop.Rad(qLat), Prop.Rad(qLon), Prop.Rad(tLat), Prop.Rad(tLon)), br = Prop.Bearing(Prop.Rad(qLat), Prop.Rad(qLon), Prop.Rad(tLat), Prop.Rad(tLon));
                    List<double[]> l = new List<double[]>();
                    for (int i = 0; i <= 60; i++) { double la, lo; Prop.Dest(Prop.Rad(qLat), Prop.Rad(qLon), br, d * i / 60.0, out la, out lo); l.Add(new double[] { Prop.Deg(lo), Prop.Deg(la) }); }
                    Poly(g, tp, l);
                    float tx, ty; Forward(tLat, tLon, out tx, out ty);
                    g.DrawEllipse(tp, tx - 6, ty - 6, 12, 12); g.DrawLine(tp, tx - 9, ty, tx + 9, ty); g.DrawLine(tp, tx, ty - 9, tx, ty + 9);
                }
            }
            float qx, qy; Forward(qLat, qLon, out qx, out qy);
            g.FillRectangle(Brushes.White, qx - 4, qy - 4, 8, 8);
        }

        void UpdateInfo()
        {
            string[] mn = { "SHORT PATH MOF", "LONG PATH MOF", "COMPOSITE MOF", "SHORT PATH LOF", "LONG PATH LOF", "COMPOSITE LOF", "3000 KM (1864 MI) HOP MOF", "3000 KM (1864 MI) HOP LOF" };
            info.Text = string.Format(" {0}  {1:dd MMM yyyy HH:mm}Z {2}  SFI {3:0}  A {4:0}  K {5:0.#}  SSN {6:0}  QTH {7} {8}",
                ModeName(mn), T, live ? "LIVE" : "FIXED", cond.Sfi, cond.A, cond.K, cond.Ssn, qGrid, LatLon(qLat, qLon));
            pNoaa.Text = (auto ? noaaStatus + "  |  " : "NOAA automatic update is off  |  ") + "using " + dataSrc + "   (click to change)";
            pNoaa.ToolTipText = "Click to change location, date and solar data. Help > NOAA diagnostics shows what NOAA returned.";
            bool mof = IsMof;
            string suffix = (mode == Mode.MofLong || mode == Mode.LofLong) ? "  (striped = long path)" : (mode == Mode.MofComp || mode == Mode.LofComp ? "  (striped = long path is better)" : "");
            if (BandActive) { legend.Labels = new string[] { Bands.Label(ViewFreq) + " open", "marginal", "black = closed", "white square = QTH, yellow = target" + (suffix.Length > 0 ? "" : "  (striped = long path)") }; legend.Col = new Color[] { Color.FromArgb(0, 0xBE, 0), Color.FromArgb(0xE6, 0xC8, 0), Color.Empty, Color.Empty }; legend.Invalidate(); pNoaa.Text += RadioStatusSuffix(); return; }
            if (mof) { legend.Labels = new string[] { "20M  MOF>=14.15", "15M  MOF>=21.2", "10M  MOF>=28.5", "white square = QTH, yellow = target" + suffix }; }
            else { legend.Labels = new string[] { "40M  LOF<=7.1", "80M  LOF<=3.6", "160M  LOF<=1.85", "white square = QTH, yellow = target" + suffix }; }
            legend.Col = new Color[] { Color.FromArgb(0x28, 0x46, 0xFF), Color.FromArgb(0, 0xBE, 0), Color.FromArgb(0xD2, 0, 0), Color.Empty };
            legend.Invalidate(); pNoaa.Text += RadioStatusSuffix();
        }
        static string LatLon(double la, double lo) { return string.Format(CultureInfo.InvariantCulture, "{0:0.00}{1} {2:0.00}{3}", Math.Abs(la), la >= 0 ? "N" : "S", Math.Abs(lo), lo >= 0 ? "E" : "W"); }

        void OnMouse(object s, MouseEventArgs e)
        {
            double la, lo; if (md == null || !Inverse(e.X + 0.5, e.Y + 0.5, out la, out lo)) { readout.Text = ""; return; }
            int ci = Math.Min(md.Nx - 1, Math.Max(0, (int)((lo + 180) / md.Step))), cj = Math.Min(md.Ny - 1, Math.Max(0, (int)((90 - la) / md.Step))); int x = cj * md.Nx + ci;
            double km = Prop.Dist(Prop.Rad(qLat), Prop.Rad(qLon), Prop.Rad(la), Prop.Rad(lo)) * Prop.R;
            double lkm = 2 * Math.PI * Prop.R - km;
            double b = (Prop.Deg(Prop.Bearing(Prop.Rad(qLat), Prop.Rad(qLon), Prop.Rad(la), Prop.Rad(lo))) + 360) % 360;
            readout.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} {1}  short {2:0} km / {3:0} mi  long {4:0} km / {5:0} mi  bearing {6:0} deg\r\nMOF short {7:0.0} long {8:0.0}   LOF short {9} long {10}   3000 km (1864 mi) hop: MOF {11:0.0} LOF {12}",
                Grid.From(la, lo, 6), LatLon(la, lo), km, km * MiPerKm, lkm, lkm * MiPerKm, b, md.MofS[x], md.MofL[x], Lf(md.LofS[x]), Lf(md.LofL[x]), md.Mof3[x], Lf(md.Lof3[x]));
        }
        static string Lf(float v) { return v <= 1.0001f ? "<1.0" : v.ToString("0.0", CultureInfo.InvariantCulture); }

        // ================= resources / settings =================
        void LoadCoast()
        {
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("coast.txt"))
                using (StreamReader sr = new StreamReader(st))
                {
                    string ln;
                    while ((ln = sr.ReadLine()) != null)
                    {
                        string[] pts = ln.Split(' '); double[] a = new double[pts.Length * 2];
                        for (int i = 0; i < pts.Length; i++) { string[] xy = pts[i].Split(','); a[2 * i] = int.Parse(xy[0]) / 10.0; a[2 * i + 1] = int.Parse(xy[1]) / 10.0; }
                        coast.Add(a);
                    }
                }
            }
            catch { }
        }
        static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.Combine("N1TA-MOFLOF", "settings.ini")); } }
        void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (string ln in File.ReadAllLines(SettingsPath))
                {
                    int e = ln.IndexOf('='); if (e < 0) continue; string k = ln.Substring(0, e).Trim(), v = ln.Substring(e + 1).Trim();
                    double d; bool dv = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                    switch (k)
                    {
                        case "grid": { double la, lo; if (Grid.TryParse(v, out la, out lo)) { qGrid = Grid.Normalize(v); qLat = la; qLon = lo; } break; }
                        case "auto": auto = v == "1"; break; case "live": live = v == "1"; break;
                        case "sfi": if (dv) manSfi = d; break; case "a": if (dv) manA = d; break; case "k": if (dv) manK = d; break;
                        case "mode": if (dv && d >= 0 && d < 8) mode = (Mode)(int)d; break; case "proj": if (dv && d >= 0 && d < 3) proj = (Proj)(int)d; break;
                        case "step": if (dv && d >= 1) step = d; break;
                        case "udp": udpOn = v == "1"; break; case "udpport": if (dv && d >= 1 && d <= 65535) udpPort = (int)d; break;
                        case "udpradio": if (dv && d >= 0 && d <= 4) udpRadio = (int)d; break; case "udpstation": udpStation = v; break;
                        case "sun": ovSun = v == "1"; break; case "rad": ovRadials = v == "1"; break; case "gridlines": ovGrid = v == "1"; break;
                    }
                }
            }
            catch { }
        }
        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                CultureInfo ci = CultureInfo.InvariantCulture;
                File.WriteAllLines(SettingsPath, new string[] {
                    "grid=" + qGrid, "auto=" + (auto ? 1 : 0), "live=" + (live ? 1 : 0),
                    "sfi=" + manSfi.ToString(ci), "a=" + manA.ToString(ci), "k=" + manK.ToString(ci),
                    "mode=" + (int)mode, "proj=" + (int)proj, "step=" + step.ToString(ci),
                    "udp=" + (udpOn ? 1 : 0), "udpport=" + udpPort, "udpradio=" + udpRadio, "udpstation=" + udpStation,
                    "sun=" + (ovSun ? 1 : 0), "rad=" + (ovRadials ? 1 : 0), "gridlines=" + (ovGrid ? 1 : 0) });
            }
            catch { }
        }
    }

    public static class HelpText
    {
        public static string Version = "1.2.0";
        public static string About =
            "N1TA MOF/LOF " + Version + "\r\n\r\n" +
            "A from-scratch rewrite of the idea behind K1TTT's MOF/LOF\r\n" +
            "propagation program. Credit for the concept belongs to\r\n" +
            "Dave, K1TTT. No K1TTT code was used.\r\n\r\n" +
            "MOF: MINIMUF 3.5 (US Navy, NOSC TD 201, 1978)\r\n" +
            "LOF: simple D-layer absorption model (see Notes)\r\n" +
            "Data: NOAA Space Weather Prediction Center\r\n\r\n" +
            "No warranty. Predictions are statistical; the ionosphere\r\n" +
            "has not read this program.\r\n\r\n" +
            "github.com/mfdechristopher/N1TA-MOF-LOF";
        public static string Notes =
            "Distances are shown in kilometers and miles everywhere (1 km = 0.6214 mi).\r\n\r\nBEST BAND banner: for the whole world it names the highest band that is OPEN to at least a\nthird of 18 distant regions (over 1500 km / 932 mi). Click the map (or press T) to get\nthe best band to one specific target; right-click or Esc clears it. OPEN = well between LOF\nand MOF on the short or long path, MARGINAL = within 15% of either edge, closed = outside.\nContest bands are judged at 1.85, 3.6, 7.1, 14.15, 21.2 and 28.5 MHz.\n\nRADIO FOLLOWING: on the Start screen, tick the UDP option and set the port. The map then\nshows only the band your radio is on (green open, yellow marginal), and the banner says\nwhether a better band exists. Press B to switch single-band view on and off.\n\nKeys: 1-8 display, Enter/Space next hour, Backspace previous,\r\n" +
            "R/X/G projection, S sun lines, P radials, A animate, L live, F5 refresh NOAA, Ctrl+S location/date/solar data, T target, Esc clear target, B single-band view.\r\n\r\n" +
            "Map colours: MOF - blue 20M, green 15M, red 10M.\r\n" +
            "             LOF - blue 40M, green 80M, red 160M.\r\n" +
            "Striped cells are the long path.\r\n\r\n" +
            "MOF engine is MINIMUF 3.5 and is checked against the Navy's published\r\n" +
            "test case. The original K1TTT program adapted MINIMUF-85 and used a\r\n" +
            "different LOF method, so numbers will differ somewhat.\r\n\r\n" +
            "A and K indices do not enter MINIMUF. This program applies its own simple\r\n" +
            "storm corrections: lower MOF at mid/high geomagnetic latitudes when K is\r\n" +
            "above 3, and extra D-layer/auroral absorption (higher LOF) when K is high.\r\n" +
            "These are heuristics, not published models.\r\n\r\n" +
            "Future dates in automatic mode use NOAA's 27-day outlook when available (up to 27 days ahead).\r\nBeyond that, or for past dates, choose Manual and enter the SFI/A/K you want.\r\nA is the mean of the last 24 hours of NOAA 3-hourly values.";
    }
}
