using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinShiftS
{
    public class WinShiftS : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public class DisplayForm : Form
        {
            static bool dragging = false;
            static Point drag_start;
            static Point drag_end;

            public event EventHandler RectDragged;

            private Rectangle last_drawn;

            public DisplayForm()
            {
                TopMost = true;
                FormBorderStyle = FormBorderStyle.None;
                DoubleBuffered = true;

                Paint += new PaintEventHandler(this.ScreenshotPaint);
                MouseDown += new MouseEventHandler(this.UserMouseDown);
                MouseMove += new MouseEventHandler(this.UserMouseMove);
                MouseUp += new MouseEventHandler(this.UserMouseRelease);

                Cursor = Cursors.Cross;
            }

            public void ForceUpdate(object sender, EventArgs e)
            {
                Invalidate(Expanded(last_drawn));
                last_drawn = DragCoordsToRectangle();
                Invalidate(Expanded(last_drawn));
            }

            private Rectangle Expanded(Rectangle input)
            {
                return new Rectangle(input.X - 1, input.Y - 1, input.Width + 2, input.Height + 2);
            }

            private Rectangle DragCoordsToRectangle()
            {
                int drag_left = Math.Min(drag_start.X, drag_end.X);
                int drag_top = Math.Min(drag_start.Y, drag_end.Y);
                int drag_right = Math.Max(drag_start.X, drag_end.X);
                int drag_bottom = Math.Max(drag_start.Y, drag_end.Y);
                int drag_width = drag_right - drag_left;
                int drag_height = drag_bottom - drag_top;

                return RectangleToClient(new Rectangle(drag_left, drag_top, drag_width, drag_height));
            }

            private void ScreenshotPaint(object sender, System.Windows.Forms.PaintEventArgs e)
            {
                Graphics g = e.Graphics;

                SolidBrush overlay = new SolidBrush(Color.FromArgb(128, 255, 255, 255));

                if (dragging)
                {
                    Rectangle r = DragCoordsToRectangle();

                    g.FillRectangle(overlay, 0, 0, Width - (Width - r.Left), Height);
                    g.FillRectangle(overlay, r.Right, 0, Width - r.Right, Height);
                    g.FillRectangle(overlay, r.Left, 0, r.Width, Height - (Height - r.Top));
                    g.FillRectangle(overlay, r.Left, r.Bottom, r.Width, Height - r.Bottom);

                    g.DrawRectangle(new Pen(SystemColors.Highlight), r.Left, r.Top, r.Width, r.Height);
                }
                else
                {
                    g.FillRectangle(overlay, 0, 0, Width, Height);
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == (Keys.Escape))
                {
                    Close();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            private void UserMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                dragging = true;
                drag_start = PointToScreen(e.Location);
                drag_end = PointToScreen(e.Location);
            }

            private void UserMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                if (dragging)
                {
                    drag_end = PointToScreen(e.Location);

                    EventHandler local_copy = RectDragged;
                    if (local_copy != null)
                    {
                        local_copy(this, EventArgs.Empty);
                    }
                }
            }

            private void UserMouseRelease(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                if (dragging)
                {
                    if (drag_start.X != drag_end.X && drag_start.Y != drag_end.Y)
                    {
                        Bitmap cropped = ((Bitmap)BackgroundImage).Clone(DragCoordsToRectangle(), BackgroundImage.PixelFormat);

                        Clipboard.SetImage(cropped);

                        Close();
                    }

                    dragging = false;
                }
            }
        }

        private Bitmap[] captures = null;
        private DisplayForm[] forms = null;

        public WinShiftS()
        {
            RegisterHotKey(this.Handle, 0, 0xC, (uint) Keys.S.GetHashCode());

            TakeScreenshot();
        }

        ~WinShiftS()
        {
            UnregisterHotKey(this.Handle, 0);
        }

        private void TakeScreenshot()
        {
            if (Screen.AllScreens.Length == 0)
            {
                return;
            }

            captures = new Bitmap[Screen.AllScreens.Length];
            forms = new DisplayForm[Screen.AllScreens.Length];

            for (int i=0;i<Screen.AllScreens.Length;i++)
            {
                Rectangle screen_rect = Screen.AllScreens[i].Bounds;

                Bitmap b = new Bitmap(screen_rect.Width, screen_rect.Height, PixelFormat.Format24bppRgb);
                captures[i] = b;

                Graphics graphics = Graphics.FromImage(b);
                graphics.CopyFromScreen(screen_rect.Left, screen_rect.Top, 0, 0, screen_rect.Size);

                DisplayForm f = new DisplayForm();
                f.BackgroundImage = b;
                f.Visible = true;
                f.Location = screen_rect.Location;
                f.Size = screen_rect.Size;
                f.FormClosing += new FormClosingEventHandler(this.FormAboutToClose);
                forms[i] = f;
            }

            for (int i=0;i<Screen.AllScreens.Length;i++)
            {
                for (int j=0;j<Screen.AllScreens.Length;j++)
                {
                    forms[i].RectDragged += new EventHandler(forms[j].ForceUpdate);
                }
            }
        }

        private void FormAboutToClose(object sender, FormClosingEventArgs e)
        {
            if (forms != null)
            {
                for (int i=0;i<forms.Length;i++)
                {
                    if (forms[i] != sender)
                    {
                        forms[i].Close();
                    }
                }

                forms = null;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        
            if (m.Msg == 0x0312)
            {
                /* Note that the three lines below are not needed if you only want to register one hotkey.
                 * The below lines are useful in case you want to register multiple keys, which you can use a switch with the id as argument, or if you want to know which key/modifier was pressed for some particular reason. */
        
                //Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                //KeyModifier modifier = (KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                //int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.
        
        
                //MessageBox.Show("Hotkey has been pressed!");
                // do something

                TakeScreenshot();
            }
        }

        [STAThread]
        static void Main()
        {
            WinShiftS entity = new WinShiftS();

            Application.Run();
        }
    }
}
