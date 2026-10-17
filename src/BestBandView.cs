// The big "BEST BAND" banner. Flat colours, no decoration: it exists to be read from across the shack.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace N1TA.MofLof
{
    public class BestBandView : Control
    {
        public string Caption = "BEST BAND", Big = "--", Text1 = "", Text2 = ""; public Color BigColor = Color.Gray;
        public string[] Sub = new string[6]; public BandState[] St = new BandState[6]; public int Best = -1;
        static readonly Color COpen = Color.FromArgb(0, 210, 0), CMarg = Color.FromArgb(255, 215, 0), CClosed = Color.FromArgb(105, 105, 105);
        public static Color StateColor(BandState s) { return s == BandState.Open ? COpen : (s == BandState.Marginal ? CMarg : CClosed); }

        public BestBandView() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); Dock = DockStyle.Top; Height = 80; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(SystemColors.Control);
            int h = Height - 8;
            Rectangle blk = new Rectangle(4, 4, 168, h);
            using (Brush b = new SolidBrush(BigColor)) g.FillRectangle(b, blk);
            g.DrawRectangle(new Pen(Color.Black, 2), blk);
            Color tc = (BigColor == CClosed || BigColor == Color.Gray) ? Color.White : Color.Black;
            StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center; c.LineAlignment = StringAlignment.Center;
            using (Brush tb = new SolidBrush(tc))
            {
                using (Font f1 = new Font("Arial", 9f, FontStyle.Bold)) g.DrawString(Caption, f1, tb, new RectangleF(blk.X, blk.Y + 2, blk.Width, 16), c);
                using (Font f2 = new Font("Arial", Big.Length > 4 ? 22f : 30f, FontStyle.Bold)) g.DrawString(Big, f2, tb, new RectangleF(blk.X, blk.Y + 16, blk.Width, blk.Height - 16), c);
            }
            int cw = Math.Max(44, Math.Min(84, (Width - 180 - 230) / 6)); int cx0 = Width - 6 * cw - 4;
            for (int i = 0; i < 6; i++)
            {
                Rectangle r = new Rectangle(cx0 + i * cw, 4, cw - 5, h);
                using (Brush b = new SolidBrush(StateColor(St[i]))) g.FillRectangle(b, r);
                g.DrawRectangle(new Pen(Color.Black, i == Best ? 4 : 1), r);
                Color fc = St[i] == BandState.Closed ? Color.FromArgb(215, 215, 215) : Color.Black;
                using (Brush fb = new SolidBrush(fc))
                {
                    using (Font fn = new Font("Arial", 15f, FontStyle.Bold)) g.DrawString(Bands.Name[i], fn, fb, new RectangleF(r.X, r.Y + 4, r.Width, 26), c);
                    using (Font fs = new Font("Arial", 8f, FontStyle.Regular)) g.DrawString(Sub[i] ?? "", fs, fb, new RectangleF(r.X, r.Y + 30, r.Width, h - 32), c);
                }
            }
            RectangleF tr = new RectangleF(180, 4, Math.Max(40, cx0 - 190), h);
            StringFormat left = new StringFormat(); left.Trimming = StringTrimming.EllipsisWord;
            using (Font fb = new Font("Arial", 10f, FontStyle.Bold)) g.DrawString(Text1, fb, Brushes.Black, new RectangleF(tr.X, tr.Y, tr.Width, 36), left);
            using (Font fr = new Font("Arial", 9f, FontStyle.Regular)) g.DrawString(Text2, fr, Brushes.Black, new RectangleF(tr.X, tr.Y + 38, tr.Width, h - 38), left);
        }
    }
}
