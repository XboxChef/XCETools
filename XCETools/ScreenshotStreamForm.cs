// =============================================================================
// ScreenshotStreamForm.cs - Live console view (xbdm screenshot polling)
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class ScreenshotStreamForm : Form
    {
        private readonly XboxConsole _console;
        private readonly XboxScreenshotStream _stream;
        private readonly PictureBox _view = new PictureBox();
        private readonly Button _btnStart = new Button();
        private readonly Button _btnStop = new Button();
        private readonly Button _btnSnapshot = new Button();
        private readonly NumericUpDown _interval = new NumericUpDown();
        private readonly Label _status = new Label();
        private bool _handlingFrame;

        public ScreenshotStreamForm(XboxConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _stream = console.LiveView;
            Text = "Live View (screenshot stream)";
            ClientSize = new Size(960, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.Black;
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(8, 6, 8, 6),
            };

            _btnStart.Text = "Start";
            _btnStart.Width = 72;
            _btnStart.Height = 28;
            _btnStart.FlatStyle = FlatStyle.Flat;
            _btnStart.BackColor = Color.FromArgb(16, 124, 65);
            _btnStart.ForeColor = Color.White;
            _btnStart.Click += OnStart;

            _btnStop.Text = "Stop";
            _btnStop.Width = 72;
            _btnStop.Height = 28;
            _btnStop.Left = 80;
            _btnStop.Enabled = false;
            _btnStop.FlatStyle = FlatStyle.Flat;
            _btnStop.BackColor = Color.FromArgb(63, 63, 70);
            _btnStop.ForeColor = Color.WhiteSmoke;
            _btnStop.Click += OnStop;

            _btnSnapshot.Text = "Save frame";
            _btnSnapshot.Width = 88;
            _btnSnapshot.Height = 28;
            _btnSnapshot.Left = 160;
            _btnSnapshot.FlatStyle = FlatStyle.Flat;
            _btnSnapshot.BackColor = Color.FromArgb(63, 63, 70);
            _btnSnapshot.ForeColor = Color.WhiteSmoke;
            _btnSnapshot.Click += OnSaveFrame;

            var lblInterval = new Label
            {
                Text = "Interval (ms):",
                AutoSize = true,
                Left = 260,
                Top = 8,
                ForeColor = Color.FromArgb(180, 180, 180),
            };
            _interval.Left = 350;
            _interval.Top = 4;
            _interval.Width = 64;
            _interval.Minimum = 33;
            _interval.Maximum = 2000;
            _interval.Value = 100;
            _interval.ValueChanged += (_, __) =>
            {
                if (!_stream.IsRunning)
                    _stream.IntervalMs = (int)_interval.Value;
            };

            top.Controls.AddRange(new Control[] { _btnStart, _btnStop, _btnSnapshot, lblInterval, _interval });

            _view.Dock = DockStyle.Fill;
            _view.SizeMode = PictureBoxSizeMode.Zoom;
            _view.BackColor = Color.Black;

            _status.Dock = DockStyle.Bottom;
            _status.Height = 22;
            _status.Padding = new Padding(8, 4, 0, 0);
            _status.ForeColor = Color.FromArgb(160, 160, 160);
            _status.Text = "Stopped — press Start to stream via xbdm screenshot.";

            Controls.Add(_view);
            Controls.Add(top);
            Controls.Add(_status);

            _stream.FrameReady += OnFrameReady;
            _stream.FrameError += OnFrameError;
            FormClosing += OnFormClosing;
        }

        private void OnStart(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_console.ScanInProgress)
            {
                MessageBox.Show(this, "Cancel the active memory scan (Esc) before live view.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _stream.IntervalMs = (int)_interval.Value;
                _interval.Enabled = false;
                _btnStart.Enabled = false;
                _btnStop.Enabled = true;
                _stream.Start();
                _status.Text = "Streaming…";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnStop(object sender, EventArgs e) => StopStream();

        private void StopStream()
        {
            _stream.Stop();
            _btnStart.Enabled = _console.Connected;
            _btnStop.Enabled = false;
            _interval.Enabled = true;
            if (!_status.Text.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                _status.Text = "Stopped.";
        }

        private void OnFrameReady(object sender, ScreenshotFrameEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) { e.Bitmap.Dispose(); return; }

            try
            {
                BeginInvoke(new Action(() => ApplyFrame(e)));
            }
            catch
            {
                e.Bitmap.Dispose();
            }
        }

        private void ApplyFrame(ScreenshotFrameEventArgs e)
        {
            if (IsDisposed) { e.Bitmap.Dispose(); return; }
            if (_handlingFrame) { e.Bitmap.Dispose(); return; }
            _handlingFrame = true;
            try
            {
                var old = _view.Image;
                _view.Image = e.Bitmap;
                old?.Dispose();
                _status.Text = $"Streaming  {e.Info.Width}×{e.Info.Height}  frame #{e.FrameIndex}  ~{e.FramesPerSecond:F1} FPS";
            }
            finally { _handlingFrame = false; }
        }

        private void OnFrameError(object sender, string message)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _status.Text = "Error: " + message;
                }));
            }
            catch { /* form closing */ }
        }

        private void OnSaveFrame(object sender, EventArgs e)
        {
            if (_view.Image == null)
            {
                MessageBox.Show(this, "No frame to save yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var sfd = new SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png",
                FileName = $"xbox-live-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _view.Image.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    _status.Text = "Saved " + sfd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _stream.FrameReady -= OnFrameReady;
            _stream.FrameError -= OnFrameError;
            StopStream();
            _view.Image?.Dispose();
            _view.Image = null;
        }
    }
}
