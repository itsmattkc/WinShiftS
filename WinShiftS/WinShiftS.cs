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

        private bool dragging;
        private Point drag_start;
        private Point drag_end;

        private Rectangle last_drawn;

        private Bitmap full_shot;

        private NotifyIcon systray_icon;

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

            systray_icon = new NotifyIcon()
            {
                Icon = SystemIcons.Exclamation,
                ContextMenu = new ContextMenu(new MenuItem[]{
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
            ShowNotification("Win+Shift+S enabled");
        }

        ~WinShiftS()
        {
            UnregisterHotKey(this.Handle, 0);
        }

        private void Exit(object sender, EventArgs e)
        {
            // Ensure system tray icon is hidden before we close
            systray_icon.Visible = false;

            Application.Exit();
        }

        private void OverrideClose(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Closing the form will delete it and unregister the hotkey, which in the case of UserClosing (usually
                // Alt+F4) is probably not the user's intention. We allow all other closes however.
                e.Cancel = true;
                this.Hide();
            }
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
                this.Hide();
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

        private void ShowNotification(string text)
        {
            systray_icon.ShowBalloonTip(1000, "Win+Shift+S", text, ToolTipIcon.Info);
        }

        private void UserMouseRelease(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (dragging)
            {
                if (drag_start.X != drag_end.X && drag_start.Y != drag_end.Y)
                {
                    Bitmap cropped = full_shot.Clone(DragCoordsToRectangle(), full_shot.PixelFormat);

                    Clipboard.SetImage(cropped);

                    this.Hide();

                    ShowNotification("Screenshot copied to clipboard");
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

            this.Show();
            this.Location = new Point(screenLeft, screenTop);
            this.Size = full_shot.Size;

            // Set some default valuse
            dragging = false;
            drag_start = new Point(0, 0);
            drag_end = new Point(0, 0);
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
