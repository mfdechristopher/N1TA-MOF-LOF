// "Calculations > Start..." dialog. Same idea as the original: pick place, date, and solar numbers.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    public class StartDialog : Form
    {
        TextBox tGrid, tLat, tLon, tMon, tDay, tHr, tMin, tSfi, tA, tK;
        CheckBox cAuto, cLive;
        public double Lat, Lon, Sfi, A, K; public DateTime When; public bool Auto, Live; public SpaceWx Fetched;

        TextBox Field(string label, int x, int y, int w, string val)
        {
            Label l = new Label(); l.Text = label; l.Left = x; l.Top = y + 3; l.AutoSize = true; Controls.Add(l);
            TextBox t = new TextBox(); t.Left = x + 100; t.Top = y; t.Width = w; t.Text = val; Controls.Add(t); return t;
        }
        static string F(double v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

        public StartDialog(double lat, double lon, DateTime t, bool auto, bool live, double sfi, double a, double k, SpaceWx noaa)
        {
            Text = "Start"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 330);
            bool use = auto && noaa.Complete;
            tGrid = Field("Grid square", 12, 12, 70, ""); tGrid.Leave += delegate { GridToLatLon(); };
            tLat = Field("Latitude (+N)", 12, 40, 90, F(lat)); tLon = Field("Longitude (+E)", 12, 68, 90, F(lon));
            tMon = Field("UTC month", 12, 106, 50, t.Month.ToString()); tDay = Field("UTC day", 12, 134, 50, t.Day.ToString());
            tHr = Field("UTC hour", 12, 162, 50, t.Hour.ToString()); tMin = Field("UTC minute", 12, 190, 50, t.Minute.ToString());
            tSfi = Field("Solar flux (SFI)", 215, 106, 60, F(use ? noaa.Sfi : sfi));
            tA = Field("A index", 215, 134, 60, F(use ? noaa.A : a)); tK = Field("K index", 215, 162, 60, F(use ? noaa.K : k));
            cAuto = new CheckBox(); cAuto.Text = "Get SFI / A / K from NOAA automatically"; cAuto.Left = 12; cAuto.Top = 228; cAuto.Width = 370; cAuto.Checked = auto; Controls.Add(cAuto);
            cLive = new CheckBox(); cLive.Text = "Live: follow the UTC clock"; cLive.Left = 12; cLive.Top = 252; cLive.Width = 370; cLive.Checked = live; Controls.Add(cLive);
            Btn("Now (UTC)", 12, 288, 80, delegate { DateTime n = DateTime.UtcNow; tMon.Text = n.Month.ToString(); tDay.Text = n.Day.ToString(); tHr.Text = n.Hour.ToString(); tMin.Text = n.Minute.ToString(); });
            Btn("Get NOAA", 98, 288, 80, delegate { GetNoaa(); });
            Button ok = Btn("OK", 236, 288, 70, delegate { if (Parse()) { DialogResult = DialogResult.OK; Close(); } });
            Button cancel = Btn("Cancel", 312, 288, 70, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = ok; CancelButton = cancel;
        }
        Button Btn(string text, int x, int y, int w, EventHandler h)
        {
            Button b = new Button(); b.Text = text; b.Left = x; b.Top = y; b.Width = w; b.Click += h; Controls.Add(b); return b;
        }

        void GridToLatLon()
        {
            string g = tGrid.Text.Trim().ToUpperInvariant(); if (g.Length < 4) return;
            try
            {
                double lo = (g[0] - 'A') * 20 - 180 + (g[2] - '0') * 2, la = (g[1] - 'A') * 10 - 90 + (g[3] - '0');
                if (g.Length >= 6) { lo += (g[4] - 'A') * (2.0 / 24) + 1.0 / 24; la += (g[5] - 'A') * (1.0 / 24) + 0.5 / 24; }
                else { lo += 1; la += 0.5; }
                tLat.Text = F(Math.Round(la, 3)); tLon.Text = F(Math.Round(lo, 3));
            }
            catch { }
        }
        void GetNoaa()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                SpaceWx w = Noaa.FetchCurrent();
                if (!double.IsNaN(w.Sfi)) tSfi.Text = F(w.Sfi); if (!double.IsNaN(w.A)) tA.Text = F(w.A); if (!double.IsNaN(w.K)) tK.Text = F(w.K);
                if (w.Complete) Fetched = w;
                else MessageBox.Show(this, "NOAA returned incomplete data:\r\n" + w.Note, "NOAA");
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not reach NOAA SWPC:\r\n" + ex.Message + "\r\n\r\nEnter the values by hand instead.", "NOAA"); }
            finally { Cursor = Cursors.Default; }
        }
        bool Parse()
        {
            try
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                Lat = double.Parse(tLat.Text, ci); Lon = double.Parse(tLon.Text, ci);
                if (Lat < -90 || Lat > 90 || Lon < -180 || Lon > 360) throw new Exception("Latitude must be -90..90 and longitude -180..180.");
                if (Lon > 180) Lon -= 360;
                int mo = int.Parse(tMon.Text), dy = int.Parse(tDay.Text), hr = int.Parse(tHr.Text), mi = int.Parse(tMin.Text);
                Sfi = double.Parse(tSfi.Text, ci); A = double.Parse(tA.Text, ci); K = double.Parse(tK.Text, ci);
                if (Sfi < 50 || Sfi > 400) throw new Exception("SFI should be between 50 and 400.");
                if (K < 0 || K > 9) throw new Exception("K index must be 0..9.");
                if (A < 0 || A > 400) throw new Exception("A index must be 0..400.");
                DateTime today = DateTime.UtcNow.Date; int yr = today.Year; DateTime d;
                try { d = new DateTime(yr, mo, dy); } catch { throw new Exception("That date does not exist."); }
                if (d < today.AddDays(-30)) d = d.AddYears(1);
                if (d > today.AddDays(335)) d = d.AddYears(-1);
                When = DateTime.SpecifyKind(d.AddHours(hr).AddMinutes(mi), DateTimeKind.Utc);
                if (hr < 0 || hr > 23 || mi < 0 || mi > 59) throw new Exception("Time must be 0-23 : 0-59.");
                Auto = cAuto.Checked; Live = cLive.Checked;
                return true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message.StartsWith("Input") ? "One of the fields is not a number." : ex.Message, "Check your entries"); return false; }
        }
    }
}
