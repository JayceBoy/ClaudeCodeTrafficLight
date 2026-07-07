using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TrafficLight
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var context = new TrafficLightContext();
            Application.Run(context);
        }
    }

    sealed class TrafficLightContext : ApplicationContext
    {
        private const int TimeoutSec = 60;

        private readonly NotifyIcon _trayIcon = new();
        private readonly System.Windows.Forms.Timer _tickTimer = new();
        private readonly Icon[] _icons;             // static: idle(0), red(1), yellow(2), green(3)
        private bool _flashOn;                       // toggles for red flash
        private HttpListener? _httpListener;
        private bool _disposed;

        private string _currentStatus = "idle";
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly object _statusLock = new();
        private int _lastIconIdx = -1;              // guards visibility-toggle flicker

        // --- constructor ---

        public TrafficLightContext()
        {
            _icons = new[]
            {
                LoadIcon("idle"),
                LoadIcon("red"),
                LoadIcon("yellow"),
                LoadIcon("green"),
            };

            _trayIcon.Text = "Claude Code-空闲";
            _trayIcon.Icon = _icons[0];
            _lastIconIdx = 0;

            var menu = new ContextMenuStrip();
            menu.Items.Add("关于", null, (_, _) => ShowAbout());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.MouseDoubleClick += OnDoubleClick;
            _trayIcon.Visible = true;

            // 500 ms tick — balances responsive icon changes with breathing smoothness
            _tickTimer.Interval = 500;
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            ThreadPool.QueueUserWorkItem(_ => StartHttpListener());

            DebugLog("TrafficLight started.");
        }

        // --- SetStatus (HTTP thread) ---

        public void SetStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return;

            status = status.ToLowerInvariant();

            lock (_statusLock)
            {
                _currentStatus = status;
                _lastUpdate = DateTime.UtcNow;
                DebugLog($"SetStatus => '{_currentStatus}'");
            }
        }

        // --- exit / double-click ---

        private void ExitApp()
        {
            _tickTimer.Stop();
            _trayIcon.Visible = false;
            Application.Exit();
        }

        // --- about dialog ---

        private static void ShowAbout()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string verStr = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

            const string url = "https://github.com/JayceBoy/ClaudeCodeTrafficLight";

            var openBtn = new TaskDialogButton("打开 GitHub");

            var page = new TaskDialogPage
            {
                Caption = "关于",
                Heading = "Claude Code TrafficLight",
                Text = $"作者：飞翔的秋秋\n版本：{verStr}\n\n{url}",
                Icon = TaskDialogIcon.Information,
                Buttons = { openBtn, TaskDialogButton.OK },
            };

            if (TaskDialog.ShowDialog(page) == openBtn)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            }
        }

        private void OnDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) SetStatus("idle");
        }

        // --- 500 ms tick (UI thread) ---

        private void OnTick(object? sender, EventArgs e)
        {
            string status;
            lock (_statusLock)
            {
                if (_currentStatus != "idle" &&
                    (DateTime.UtcNow - _lastUpdate).TotalSeconds >= TimeoutSec)
                {
                    _currentStatus = "idle";
                    _lastUpdate = DateTime.UtcNow;
                    DebugLog("Timeout → idle");
                }
                status = _currentStatus;
            }

            int idx = status switch
            {
                "waiting" or "confirm"     => 1,
                "thinking" or "processing" => 2,
                "completed" or "done"      => 3,
                _                          => 0,
            };

            string text = status switch
            {
                "waiting" or "confirm"     => "Claude Code-需确认",
                "thinking" or "processing" => "Claude Code-执行中",
                "completed" or "done"      => "Claude Code-已完成",
                _                          => "Claude Code-空闲",
            };

            if (idx == 1)
            {
                // Red → flash: alternate between red and idle (gray) every 500 ms
                _flashOn = !_flashOn;
                ShowFlash(_flashOn ? _icons[1] : _icons[0], idx, text);
            }
            else
            {
                _flashOn = false;          // reset when leaving red
                ShowStatic(_icons[idx], idx, text);
            }
        }

        // --- icon switching helpers ---

        /// <summary>Switch to a different-status icon (uses visibility toggle).</summary>
        private void ShowStatic(Icon icon, int idx, string text)
        {
            if (idx == _lastIconIdx) return;
            _lastIconIdx = idx;

            DebugLog($"Icon set to '{text}'");
            _trayIcon.Visible = false;
            _trayIcon.Icon = icon;
            _trayIcon.Text = text;
            _trayIcon.Visible = true;
        }

        /// <summary>Flash the red icon (alternate between red and gray without visibility toggle).</summary>
        private void ShowFlash(Icon icon, int idx, string text)
        {
            if (idx != _lastIconIdx)
            {
                // Just entered red — use full toggle to force the transition
                _lastIconIdx = idx;
                DebugLog("Icon → red (flash start)");
                _trayIcon.Visible = false;
                _trayIcon.Icon = icon;
                _trayIcon.Text = text;
                _trayIcon.Visible = true;
                return;
            }

            // Flash — direct set, no hide/show
            _trayIcon.Icon = icon;
        }

        // --- load embedded PNG → Icon ---

        private static Icon LoadIcon(string name)
        {
            using var bmp32 = LoadPng32(name);
            return MakeIcon(bmp32);
        }

        /// <summary>Load an embedded PNG as a 32bpp ARGB Bitmap.</summary>
        private static Bitmap LoadPng32(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resource = $"TrafficLight.images.{name}.png";

            using var stream = asm.GetManifestResourceStream(resource);
            if (stream == null)
            {
                var names = string.Join("\n  ", asm.GetManifestResourceNames());
                throw new FileNotFoundException(
                    $"Resource '{resource}' not found.\nAvailable:\n  {names}");
            }

            using var src = new Bitmap(stream);
            var bmp32 = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp32))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(src, 0, 0);
            }
            return bmp32;
        }

        private static Icon MakeIcon(Bitmap bmp32)
        {
            int w = bmp32.Width;
            int h = bmp32.Height;

            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp32.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[w * h * 4];
            Marshal.Copy(bd.Scan0, pixels, 0, pixels.Length);
            bmp32.UnlockBits(bd);

            int dibHeight = h * 2;
            int andStride = ((w + 31) / 32) * 4;
            int andSize = andStride * h;
            int imgSize = 40 + pixels.Length + andSize;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)1);

            bw.Write((byte)(w >= 256 ? 0 : w));
            bw.Write((byte)(h >= 256 ? 0 : h));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(imgSize);
            bw.Write(22);

            bw.Write(40);
            bw.Write(w);
            bw.Write(dibHeight);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);
            bw.Write(pixels.Length);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            bw.Write(pixels);
            bw.Write(new byte[andSize]);

            bw.Flush();
            ms.Seek(0, SeekOrigin.Begin);
            return new Icon(ms);
        }

        // --- HTTP listener ---

        private void StartHttpListener()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:9876/");
                _httpListener.Start();
                DebugLog("HTTP listener started on port 9876");

                while (_httpListener.IsListening)
                {
                    try
                    {
                        var ctx = _httpListener.GetContext();
                        string status = ctx.Request.QueryString["status"] ?? "";
                        DebugLog($"HTTP request: status='{status}'");
                        SetStatus(status);

                        byte[] body = Encoding.UTF8.GetBytes("OK");
                        ctx.Response.ContentType = "text/plain";
                        ctx.Response.ContentLength64 = body.Length;
                        ctx.Response.OutputStream.Write(body, 0, body.Length);
                        ctx.Response.OutputStream.Close();
                    }
                    catch (HttpListenerException ex)
                    {
                        DebugLog($"HTTP listener error: {ex.Message}");
                        break;
                    }
                    catch (ObjectDisposedException) { break; }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"HTTP listener failed: {ex.Message}");
            }
        }

        // --- debug logging ---

        private static void DebugLog(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "TrafficLight-debug.log"),
                    line + Environment.NewLine);
            }
            catch { }
        }

        // --- disposal ---

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _tickTimer.Stop();
                _tickTimer.Dispose();

                if (_httpListener != null)
                {
                    try { _httpListener.Stop(); } catch { }
                    ((IDisposable)_httpListener).Dispose();
                }

                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                foreach (var ic in _icons) ic?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
