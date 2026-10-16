// The one input screen: where you are, when you want the map for, and which solar numbers to use.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    public class StartDialog : Form
    {
        TextBox tGrid, tSfi, tA, tK; Label lGridInfo, lDateNote, lSolarNote;
        RadioButton rLive, rFixed, rAuto, rManual; DateTimePicker dtp; NumericUpDown nHr, nMin;
        SpaceWx cur; List<OutlookRow> outlook;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        // results
        public string GridText; public double Lat, Lon, Sfi, A, K; public DateTime When; public bool Auto, Live;
        public SpaceWx FetchedCurrent; public List<OutlookRow> FetchedOutlook;

        Label Lbl(string t, int x, int y, int w) { Label l = new Label(); l.Text = t; l.Left = x; l.Top = y; l.Width = w; l.AutoSize = (w == 0); Controls.Add(l); return l; }
        TextBox Box(int x, int y, int w, string v) { TextBox t = new TextBox(); t.Left = x; t.Top = y; t.Width = w; t.Text = v; Controls.Add(t); return t; }
        RadioButton Rad(string t, int x, int y, int w, bool chk) { RadioButton r = new RadioButton(); r.Text = t; r.Left = x; r.Top = y; r.Width = w; r.Checked = chk; Controls.Add(r); return r; }
        Button Btn(string t, int x, int y, int w, EventHandler h) { Button b = new Button(); b.Text = t; b.Left = x; b.Top = y; b.Width = w; b.Click += h; Controls.Add(b); return b; }
        static string F(double v) { return double.IsNaN(v) ? "" : v.ToString("0.##", CI); }
        static bool Good(double v) { return !double.IsNaN(v); }

        public StartDialog(string grid, DateTime t, bool live, bool auto, double sfi, double a, double k, SpaceWx noaa, List<OutlookRow> outl, bool startup)
        {
            Text = startup ? "N1TA MOF/LOF - Start" : "Location, date and solar data";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(500, 452);
            cur = noaa; outlook = outl;

            Lbl("Your location", 14, 12, 200).Font = new Font(Font, FontStyle.Bold);
            Lbl("Maidenhead grid square", 14, 38, 150);
            tGrid = Box(170, 35, 90, grid); tGrid.MaxLength = 8; tGrid.TextChanged += delegate { GridChanged(); };
            lGridInfo = Lbl("", 270, 38, 220);
            Lbl("4, 6 or 8 characters, e.g. FN32 or FN32pb. Remembered from last time.", 14, 62, 480);

            Lbl("Date and time (UTC)", 14, 96, 200).Font = new Font(Font, FontStyle.Bold);
            rLive = Rad("Live: follow the UTC clock", 14, 118, 220, live); rFixed = Rad("This date and time:", 14, 142, 150, !live);
            dtp = new DateTimePicker(); dtp.Format = DateTimePickerFormat.Custom; dtp.CustomFormat = "yyyy-MM-dd"; dtp.Left = 170; dtp.Top = 140; dtp.Width = 110;
            dtp.MinDate = new DateTime(1990, 1, 1); dtp.MaxDate = new DateTime(2100, 12, 31); dtp.Value = t.Date; Controls.Add(dtp);
            nHr = new NumericUpDown(); nHr.Left = 290; nHr.Top = 140; nHr.Width = 44; nHr.Minimum = 0; nHr.Maximum = 23; nHr.Value = t.Hour; Controls.Add(nHr);
            Lbl(":", 337, 143, 8);
            nMin = new NumericUpDown(); nMin.Left = 348; nMin.Top = 140; nMin.Width = 44; nMin.Minimum = 0; nMin.Maximum = 59; nMin.Value = t.Minute; Controls.Add(nMin);
            Lbl("Z", 396, 143, 14);
            Btn("Now", 420, 138, 60, delegate { DateTime n = DateTime.UtcNow; dtp.Value = n.Date; nHr.Value = n.Hour; nMin.Value = n.Minute; rFixed.Checked = true; });
            lDateNote = Lbl("", 30, 168, 460); lDateNote.Height = 44;

            Lbl("Solar and geomagnetic data", 14, 222, 250).Font = new Font(Font, FontStyle.Bold);
            rAuto = Rad("Automatic from NOAA SWPC", 14, 244, 230, auto); rManual = Rad("Manual (type the values)", 250, 244, 230, !auto);
            Lbl("Solar flux (SFI)", 30, 276, 100); tSfi = Box(135, 273, 60, F(sfi));
            Lbl("A index", 215, 276, 60); tA = Box(280, 273, 60, F(a));
            Lbl("K index", 355, 276, 50); tK = Box(410, 273, 60, F(k));
            Btn("Fetch from NOAA now", 30, 308, 150, delegate { FetchNow(); });
            lSolarNote = Lbl("", 30, 340, 460); lSolarNote.Height = 56;

            Button ok = Btn("OK", 314, 410, 80, delegate { if (Collect()) { DialogResult = DialogResult.OK; Close(); } });
            Button cancel = Btn(startup ? "Quit / Skip" : "Cancel", 404, 410, 80, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = ok; CancelButton = cancel;

            EventHandler upd = delegate { UpdateState(); };
            rLive.CheckedChanged += upd; rFixed.CheckedChanged += upd; rAuto.CheckedChanged += upd; rManual.CheckedChanged += upd;
            dtp.ValueChanged += upd;
            GridChanged(); UpdateState();
        }

        void GridChanged()
        {
            double la, lo;
            if (Grid.TryParse(tGrid.Text, out la, out lo)) lGridInfo.Text = string.Format(CI, "= {0:0.00}{1}  {2:0.00}{3}", Math.Abs(la), la >= 0 ? "N" : "S", Math.Abs(lo), lo >= 0 ? "E" : "W");
            else lGridInfo.Text = tGrid.Text.Length == 0 ? "" : "(not a valid grid square)";
        }

        DateTime ChosenDate() { return rLive.Checked ? DateTime.UtcNow.Date : dtp.Value.Date; }

        // Which numbers would automatic mode use for the chosen date?
        void AutoValues(out double sfi, out double a, out double k, out string src)
        {
            sfi = a = k = double.NaN; src = "";
            DateTime d = ChosenDate(), today = DateTime.UtcNow.Date;
            if (d > today && outlook != null)
                foreach (OutlookRow r in outlook) if (r.Date.Date == d) { sfi = r.Sfi; a = r.A; k = r.K; src = "NOAA 27-day outlook for " + d.ToString("dd MMM yyyy"); return; }
            if (cur != null && cur.Any) { sfi = cur.Sfi; a = cur.A; k = cur.K; src = "NOAA current values"; }
        }

        void UpdateState()
        {
            bool live = rLive.Checked, auto = rAuto.Checked;
            dtp.Enabled = nHr.Enabled = nMin.Enabled = !live;
            if (live) { DateTime n = DateTime.UtcNow; dtp.Value = n.Date; nHr.Value = n.Hour; nMin.Value = n.Minute; }
            tSfi.ReadOnly = tA.ReadOnly = tK.ReadOnly = auto;
            tSfi.BackColor = tA.BackColor = tK.BackColor = auto ? SystemColors.Control : SystemColors.Window;

            DateTime d = ChosenDate(), today = DateTime.UtcNow.Date;
            if (live || d == today) lDateNote.Text = "Today. Automatic mode uses NOAA's current values.";
            else if (d < today) lDateNote.Text = "Past date. Automatic mode can only supply today's NOAA values. For a replay of that day, choose Manual and enter its SFI / A / K.";
            else if ((d - today).TotalDays <= 27) lDateNote.Text = "Future date. Automatic mode uses NOAA's 27-day outlook (SFI, A, K) for that day.";
            else lDateNote.Text = "More than 27 days ahead, beyond NOAA's outlook. Automatic mode would fall back to today's values; choose Manual to enter the SFI / A / K you expect.";

            if (auto)
            {
                double s, a, k; string src; AutoValues(out s, out a, out k, out src);
                tSfi.Text = F(s); tA.Text = F(a); tK.Text = F(k);
                lSolarNote.Text = src == "" ? "No NOAA data yet. Press \"Fetch from NOAA now\", or just press OK and the map will fill in once NOAA answers." : "Using: " + src + ". Switch to Manual to override these numbers.";
            }
            else lSolarNote.Text = "Typing your own values. SFI is the 10.7 cm flux (about 65-300), A is the daily planetary A index, K is the planetary K (0-9). Press \"Fetch from NOAA now\" to start from today's numbers.";
        }

        void FetchNow()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                SpaceWx w = Noaa.FetchCurrent();
                if (w.Any) { cur = w; FetchedCurrent = w; }
                if (ChosenDate() > DateTime.UtcNow.Date) { List<OutlookRow> o = Noaa.FetchOutlook(); if (o.Count > 0) { outlook = o; FetchedOutlook = o; } }
                bool wasManual = rManual.Checked;
                if (wasManual && w.Any) { tSfi.Text = F(w.Sfi); tA.Text = F(w.A); tK.Text = F(w.K); }
                UpdateState();
                if (wasManual && w.Any) { tSfi.Text = F(w.Sfi); tA.Text = F(w.A); tK.Text = F(w.K); }
                if (!w.Complete) lSolarNote.Text = "NOAA: " + w.Note + ". Details under Help > NOAA diagnostics. " + (wasManual ? "" : "Anything missing is filled from your manual values.");
            }
            finally { Cursor = Cursors.Default; }
        }

        bool Collect()
        {
            try
            {
                if (!Grid.TryParse(tGrid.Text, out Lat, out Lon)) throw new Exception("Please enter a valid Maidenhead grid square, such as FN32 or FN32pb.");
                GridText = Grid.Normalize(tGrid.Text);
                Live = rLive.Checked; Auto = rAuto.Checked;
                When = DateTime.SpecifyKind(dtp.Value.Date.AddHours((double)nHr.Value).AddMinutes((double)nMin.Value), DateTimeKind.Utc);
                if (Live) When = DateTime.UtcNow;
                double v;
                Sfi = double.TryParse(tSfi.Text, NumberStyles.Float, CI, out v) ? v : double.NaN;
                A = double.TryParse(tA.Text, NumberStyles.Float, CI, out v) ? v : double.NaN;
                K = double.TryParse(tK.Text, NumberStyles.Float, CI, out v) ? v : double.NaN;
                if (!Auto)
                {
                    if (!Good(Sfi) || Sfi < 50 || Sfi > 400) throw new Exception("Solar flux (SFI) must be a number between 50 and 400.");
                    if (!Good(A) || A < 0 || A > 400) throw new Exception("A index must be a number between 0 and 400.");
                    if (!Good(K) || K < 0 || K > 9) throw new Exception("K index must be a number between 0 and 9.");
                }
                else { if (!Good(Sfi)) Sfi = 120; if (!Good(A)) A = 8; if (!Good(K)) K = 2; }   // only used if NOAA is unreachable
                return true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Please check your entries"); return false; }
        }
    }
}
