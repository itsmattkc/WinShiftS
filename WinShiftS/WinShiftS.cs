using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace WinShiftS
{
    public class WinShiftS : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool dragging = false;
        private Point drag_start;
        private Point drag_end;

        private Rectangle last_drawn;

        private Bitmap full_shot;

        public WinShiftS()
        {
            // Registers Win+Shift+S
            RegisterHotKey(this.Handle, 0, 0xC, (uint) Keys.S.GetHashCode());

            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            this.Paint += new PaintEventHandler(this.ScreenshotPaint);
            this.MouseDown += new MouseEventHandler(this.UserMouseDown);
            this.MouseMove += new MouseEventHandler(this.UserMouseMove);
            this.MouseUp += new MouseEventHandler(this.UserMouseRelease);
            this.VisibleChanged += new EventHandler(this.CleanUpImage);
            this.FormClosing += new FormClosingEventHandler(this.OverrideClose);

            this.Cursor = Cursors.Cross;
        }

        ~WinShiftS()
        {
            UnregisterHotKey(this.Handle, 0);
        }

        private void OverrideClose(object sender, FormClosingEventArgs e)
        {
            // Closing the form will delete it and unregister the hotkey which is probably undesirable behavior,
            // instead we override it with hiding
            e.Cancel = true;
            Hide();
        }

        private void UpdateChanged()
        {
            Invalidate(last_drawn);
            last_drawn = DragCoordsToRectangle();
            
            // We draw a 1px border around the rectangle that needs to be invalidated graphically too
            last_drawn.X--;
            last_drawn.Y--;
            last_drawn.Width += 2;
            last_drawn.Height += 2;

            Invalidate(last_drawn);
        }

        private Rectangle DragCoordsToRectangle()
        {
            int drag_left = Math.Min(drag_start.X, drag_end.X);
            int drag_top = Math.Min(drag_start.Y, drag_end.Y);
            int drag_right = Math.Max(drag_start.X, drag_end.X);
            int drag_bottom = Math.Max(drag_start.Y, drag_end.Y);
            int drag_width = drag_right - drag_left;
            int drag_height = drag_bottom - drag_top;

            return new Rectangle(drag_left, drag_top, drag_width, drag_height);
        }

        private void ScreenshotPaint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            g.DrawImage(full_shot, 0, 0);

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
                Hide();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UserMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            dragging = true;
            drag_start = e.Location;
            drag_end = e.Location;
        }

        private void UserMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (dragging)
            {
                drag_end = e.Location;

                UpdateChanged();
            }
        }

        private void UserMouseRelease(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (dragging)
            {
                if (drag_start.X != drag_end.X && drag_start.Y != drag_end.Y)
                {
                    Bitmap cropped = full_shot.Clone(DragCoordsToRectangle(), full_shot.PixelFormat);

                    Clipboard.SetImage(cropped);

                    Hide();

                    NotifyIcon icon = new NotifyIcon();
                    icon.Icon = SystemIcons.Exclamation;
                    icon.ShowBalloonTip(1000, "Win+Shift+S", "Screenshot copied to clipboard", ToolTipIcon.Error);
                }

                dragging = false;
            }
        }

        private void CleanUpImage(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                // Attempt to clear the bitmap's memory if the form was hidden
                full_shot = null;
            }
        }

        private void TakeScreenshot()
        {
            int screenLeft = SystemInformation.VirtualScreen.Left;
            int screenTop = SystemInformation.VirtualScreen.Top;
            int screenWidth = SystemInformation.VirtualScreen.Width;
            int screenHeight = SystemInformation.VirtualScreen.Height;

            full_shot = new Bitmap(screenWidth, screenHeight, PixelFormat.Format24bppRgb);

            using (Graphics graphics = Graphics.FromImage(full_shot))
            {
                graphics.CopyFromScreen(screenLeft, screenTop, 0, 0, full_shot.Size);
            }

            Show();
            Location = new Point(screenLeft, screenTop);
            Size = full_shot.Size;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        
            if (m.Msg == 0x0312)
            {
                // Keeping these here just in case we end up using more hotkeys later

                //Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                //KeyModifier modifier = (KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                //int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.
        
                TakeScreenshot();
            }
        }

        [STAThread]
        static void Main()
        {
            // Ensure only one instance of this app is open at once
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Win+Shift+S", out createdNew))
            {
                if (createdNew)  
                {
                    WinShiftS entity = new WinShiftS();

                    Application.Run();  
                }
            }
        }
    }
}
