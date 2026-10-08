// =============================================================================
// ControllerForm.cs - Virtual Xbox 360 pad (xbdm autoinput) for XCE Atlas
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XCETools
{
    /// <summary>
    /// On-screen gamepad + optional PC XInput forwarding to the devkit via XDCKIT.
    /// </summary>
    internal sealed class ControllerForm : Form
    {
        private readonly XboxConsole _console;
        private XboxVirtualController _pad;
        private readonly Timer _pollTimer = new Timer { Interval = 16 };

        private readonly ComboBox _userCombo = new ComboBox();
        private readonly CheckBox _chkPcPad = new CheckBox();
        private readonly Label _status = new Label();
        private readonly TrackBar _lt = new TrackBar();
        private readonly TrackBar _rt = new TrackBar();
        private readonly Button _btnConnect = new Button();
        private readonly Button _btnDisconnect = new Button();
        private readonly Button _btnRecord = new Button();
        private readonly Button _btnStopRecord = new Button();
        private readonly Button _btnSaveMacro = new Button();
        private readonly Button _btnPlayMacro = new Button();

        private XboxInputMacro _macro = new XboxInputMacro();
        private bool _recording;
        private int _recordOrigin;
        private XBOX_AUTOMATION_GAMEPAD _lastRecorded;

        private readonly System.Collections.Generic.Dictionary<XboxAutomationButtonFlags, Button> _buttons
            = new System.Collections.Generic.Dictionary<XboxAutomationButtonFlags, Button>();

        public ControllerForm(XboxConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            Text = "Virtual Controller";
            ClientSize = new Size(420, 480);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(8, 6, 8, 0),
                WrapContents = true,
                BackColor = BackColor,
            };

            top.Controls.Add(new Label { Text = "User:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _userCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _userCombo.Items.AddRange(new object[] { "0", "1", "2", "3" });
            _userCombo.SelectedIndex = 0;
            _userCombo.Width = 48;
            _userCombo.Enabled = false;
            _userCombo.Margin = new Padding(0, 4, 12, 0);
            top.Controls.Add(_userCombo);

            StyleBtn(_btnConnect, "Connect", true);
            _btnConnect.Margin = new Padding(0, 2, 8, 0);
            _btnConnect.Click += OnConnect;
            top.Controls.Add(_btnConnect);

            StyleBtn(_btnDisconnect, "Disconnect", false);
            _btnDisconnect.Enabled = false;
            _btnDisconnect.Margin = new Padding(0, 2, 8, 0);
            _btnDisconnect.Click += OnDisconnect;
            top.Controls.Add(_btnDisconnect);

            _chkPcPad.Text = "PC gamepad (XInput)";
            _chkPcPad.AutoSize = true;
            _chkPcPad.ForeColor = ForeColor;
            _chkPcPad.Margin = new Padding(8, 6, 0, 0);
            top.Controls.Add(_chkPcPad);

            StyleBtn(_btnRecord, "Record", true);
            _btnRecord.Margin = new Padding(0, 4, 6, 0);
            _btnRecord.Enabled = false;
            _btnRecord.Click += OnRecordMacro;
            top.Controls.Add(_btnRecord);

            StyleBtn(_btnStopRecord, "Stop", false);
            _btnStopRecord.Margin = new Padding(0, 4, 6, 0);
            _btnStopRecord.Enabled = false;
            _btnStopRecord.Click += OnStopRecordMacro;
            top.Controls.Add(_btnStopRecord);

            StyleBtn(_btnSaveMacro, "Save .xcepad", false);
            _btnSaveMacro.Margin = new Padding(0, 4, 6, 0);
            _btnSaveMacro.Click += OnSaveMacro;
            top.Controls.Add(_btnSaveMacro);

            StyleBtn(_btnPlayMacro, "Play .xcepad", false);
            _btnPlayMacro.Margin = new Padding(0, 4, 6, 0);
            _btnPlayMacro.Click += OnPlayMacro;
            top.Controls.Add(_btnPlayMacro);

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Padding = new Padding(8, 4, 0, 0);
            _status.ForeColor = Color.FromArgb(180, 180, 180);
            _status.Text = "Not connected to console.";

            var padPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = BackColor,
            };

            int y = 8;
            AddButtonRow(padPanel, "D-Pad", ref y,
                (XboxAutomationButtonFlags.DPadUp, "Up"),
                (XboxAutomationButtonFlags.DPadDown, "Down"),
                (XboxAutomationButtonFlags.DPadLeft, "Left"),
                (XboxAutomationButtonFlags.DPadRight, "Right"));

            AddButtonRow(padPanel, "Face", ref y,
                (XboxAutomationButtonFlags.Y_Button, "Y"),
                (XboxAutomationButtonFlags.X_Button, "X"),
                (XboxAutomationButtonFlags.B_Button, "B"),
                (XboxAutomationButtonFlags.A_Button, "A"));

            AddButtonRow(padPanel, "Shoulder", ref y,
                (XboxAutomationButtonFlags.LeftShoulderButton, "LB"),
                (XboxAutomationButtonFlags.RightShoulderButton, "RB"),
                (XboxAutomationButtonFlags.LeftThumbButton, "LS"),
                (XboxAutomationButtonFlags.RightThumbButton, "RS"));

            AddButtonRow(padPanel, "System", ref y,
                (XboxAutomationButtonFlags.StartButton, "Start"),
                (XboxAutomationButtonFlags.BackButton, "Back"),
                (XboxAutomationButtonFlags.Xbox360_Button, "Guide"),
                (XboxAutomationButtonFlags.Bind_Button, "Bind"));

            var trigLbl = new Label { Text = "Triggers (LT / RT)", Left = 8, Top = y, AutoSize = true };
            padPanel.Controls.Add(trigLbl);
            y += 22;

            ConfigureTrigger(padPanel, _lt, 8, y, "LT");
            ConfigureTrigger(padPanel, _rt, 220, y, "RT");
            y += 56;

            var hint = new Label
            {
                Text = "Hold buttons on screen, or enable PC gamepad. Sticks come from XInput when enabled.",
                Left = 8,
                Top = y,
                Width = 380,
                Height = 36,
                ForeColor = Color.FromArgb(140, 140, 140),
            };
            padPanel.Controls.Add(hint);

            Controls.Add(padPanel);
            Controls.Add(top);
            Controls.Add(_status);

            _pollTimer.Tick += OnPollTick;
            FormClosing += OnFormClosing;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private void ConfigureTrigger(Panel parent, TrackBar bar, int left, int top, string name)
        {
            bar.Left = left;
            bar.Top = top;
            bar.Width = 180;
            bar.Minimum = 0;
            bar.Maximum = (int)XboxVirtualController.TriggerMax;
            bar.TickFrequency = 32;
            bar.Value = 0;
            bar.Enabled = false;
            bar.Scroll += (_, __) => SyncTriggersFromUi();
            parent.Controls.Add(bar);
            parent.Controls.Add(new Label { Text = name, Left = left, Top = top - 14, AutoSize = true, ForeColor = ForeColor });
        }

        private void AddButtonRow(Panel parent, string title, ref int y,
            params (XboxAutomationButtonFlags flag, string label)[] cols)
        {
            parent.Controls.Add(new Label
            {
                Text = title,
                Left = 8,
                Top = y,
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 160, 160),
            });
            y += 20;
            int x = 8;
            foreach (var col in cols)
            {
                var btn = new Button
                {
                    Text = col.label,
                    Left = x,
                    Top = y,
                    Width = 88,
                    Height = 32,
                };
                StyleBtn(btn, col.label, false);
                btn.Tag = col.flag;
                btn.MouseDown += OnPadButtonDown;
                btn.MouseUp += OnPadButtonUp;
                btn.MouseLeave += OnPadButtonUp;
                parent.Controls.Add(btn);
                _buttons[col.flag] = btn;
                x += 96;
            }
            y += 40;
        }

        private static void StyleBtn(Button btn, string text, bool primary)
        {
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = primary
                ? Color.FromArgb(16, 124, 65)
                : Color.FromArgb(80, 80, 80);
            btn.BackColor = primary ? Color.FromArgb(16, 124, 65) : Color.FromArgb(63, 63, 70);
            btn.ForeColor = Color.WhiteSmoke;
            btn.Cursor = Cursors.Hand;
        }

        private UserIndex SelectedUser => (UserIndex)_userCombo.SelectedIndex;

        private void OnConnect(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first (File → Open Process).", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _pad = _console.Automation.VirtualController(SelectedUser);
                _pad.Attach();
                _btnConnect.Enabled = false;
                _btnDisconnect.Enabled = true;
                _userCombo.Enabled = false;
                _lt.Enabled = _rt.Enabled = true;
                SetPadButtonsEnabled(true);
                _pollTimer.Start();
                _btnRecord.Enabled = true;
                _status.Text = $"Virtual pad attached (user {(int)SelectedUser}).";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnDisconnect(object sender, EventArgs e)
        {
            StopPad();
            _status.Text = _console.Connected ? "Pad detached." : "Not connected to console.";
        }

        private void StopPad()
        {
            if (_recording) OnStopRecordMacro(null, EventArgs.Empty);
            _pollTimer.Stop();
            try { _pad?.Detach(); } catch { /* ignore */ }
            _pad = null;
            _btnConnect.Enabled = _console.Connected;
            _btnDisconnect.Enabled = false;
            _btnRecord.Enabled = false;
            _userCombo.Enabled = true;
            _lt.Enabled = _rt.Enabled = false;
            _lt.Value = _rt.Value = 0;
            SetPadButtonsEnabled(false);
            foreach (var btn in _buttons.Values)
                btn.BackColor = Color.FromArgb(63, 63, 70);
        }

        private void SetPadButtonsEnabled(bool on)
        {
            foreach (var btn in _buttons.Values)
                btn.Enabled = on;
        }

        private void OnPadButtonDown(object sender, MouseEventArgs e)
        {
            if (_pad == null || sender is not Button b || b.Tag is not XboxAutomationButtonFlags flag) return;
            _pad.SetButton(flag, true);
            b.BackColor = Color.FromArgb(16, 124, 65);
        }

        private void OnPadButtonUp(object sender, EventArgs e)
        {
            if (sender is not Button b || b.Tag is not XboxAutomationButtonFlags flag) return;
            if (_pad != null)
                _pad.SetButton(flag, false);
            b.BackColor = _pad != null ? Color.FromArgb(63, 63, 70) : Color.FromArgb(50, 50, 50);
        }

        private void SyncTriggersFromUi()
        {
            if (_pad == null) return;
            _pad.SetTriggers((uint)_lt.Value, (uint)_rt.Value);
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (_pad == null || !_console.Connected) return;

            try
            {
                if (_chkPcPad.Checked && XInputReader.TryRead(0,
                    out ushort buttons, out byte lt, out byte rt,
                    out short lx, out short ly, out short rx, out short ry))
                {
                    _pad.ImportXInput(buttons, lt, rt, lx, ly, rx, ry);
                    MergeOnScreenButtons();
                    _lt.Value = Math.Min(_lt.Maximum, lt);
                    _rt.Value = Math.Min(_rt.Maximum, rt);
                }
                else
                {
                    SyncTriggersFromUi();
                }

                _pad.Apply();
                CaptureMacroFrame();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
                StopPad();
            }
        }

        private void CaptureMacroFrame()
        {
            if (!_recording || _pad == null) return;
            var s = _pad.State;
            if (_macro.Frames.Count > 0 && !PadChanged(s, _lastRecorded)) return;
            int t = Environment.TickCount - _recordOrigin;
            _macro.AddFrame(s, t);
            _lastRecorded = s;
        }

        private static bool PadChanged(XBOX_AUTOMATION_GAMEPAD a, XBOX_AUTOMATION_GAMEPAD b)
            => a.Buttons != b.Buttons || a.LeftTrigger != b.LeftTrigger || a.RightTrigger != b.RightTrigger
               || a.LeftThumbX != b.LeftThumbX || a.LeftThumbY != b.LeftThumbY
               || a.RightThumbX != b.RightThumbX || a.RightThumbY != b.RightThumbY;

        private void OnRecordMacro(object sender, EventArgs e)
        {
            if (_pad == null)
            {
                MessageBox.Show(this, "Connect the virtual pad first.", Text);
                return;
            }
            _macro = new XboxInputMacro { UserSlot = _userCombo.SelectedIndex };
            _macro.Frames.Clear();
            _recording = true;
            _recordOrigin = Environment.TickCount;
            _lastRecorded = default;
            _btnRecord.Enabled = false;
            _btnStopRecord.Enabled = true;
            _status.Text = "Recording macro…";
        }

        private void OnStopRecordMacro(object sender, EventArgs e)
        {
            _recording = false;
            _btnRecord.Enabled = _pad != null;
            _btnStopRecord.Enabled = false;
            _status.Text = $"Recorded {_macro.Frames.Count} frame(s).";
        }

        private void OnSaveMacro(object sender, EventArgs e)
        {
            if (_macro.Frames.Count == 0)
            {
                MessageBox.Show(this, "Record or load a macro first.", Text);
                return;
            }
            using (var sfd = new SaveFileDialog
            {
                Filter = $"XCE pad macro (*{XboxInputMacro.FileExtension})|*{XboxInputMacro.FileExtension}|All files (*.*)|*.*",
                DefaultExt = XboxInputMacro.FileExtension.TrimStart('.'),
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                _macro.UserSlot = _userCombo.SelectedIndex;
                _macro.Save(sfd.FileName);
                _status.Text = "Macro saved.";
            }
        }

        private void OnPlayMacro(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Console not connected.", Text);
                return;
            }
            using (var ofd = new OpenFileDialog
            {
                Filter = $"XCE pad macro (*{XboxInputMacro.FileExtension})|*{XboxInputMacro.FileExtension}|All files (*.*)|*.*",
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var macro = XboxInputMacro.Load(ofd.FileName);
                    var user = (UserIndex)Math.Max(0, Math.Min(3, macro.UserSlot));
                    macro.Play(_console.Automation, user);
                    _status.Text = $"Playing {macro.Frames.Count} frame(s) on user {(int)user}…";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text);
                }
            }
        }

        private void MergeOnScreenButtons()
        {
            XboxAutomationButtonFlags merged = XboxAutomationButtonFlags.None;
            foreach (var kv in _buttons)
            {
                if (kv.Value.BackColor == Color.FromArgb(16, 124, 65))
                    merged |= kv.Key;
            }
            _pad.State.Buttons = (_pad.State.Buttons & ~GetAllUiFlags()) | merged;
        }

        private static XboxAutomationButtonFlags GetAllUiFlags()
        {
            return XboxAutomationButtonFlags.DPadUp | XboxAutomationButtonFlags.DPadDown
                | XboxAutomationButtonFlags.DPadLeft | XboxAutomationButtonFlags.DPadRight
                | XboxAutomationButtonFlags.A_Button | XboxAutomationButtonFlags.B_Button
                | XboxAutomationButtonFlags.X_Button | XboxAutomationButtonFlags.Y_Button
                | XboxAutomationButtonFlags.LeftShoulderButton | XboxAutomationButtonFlags.RightShoulderButton
                | XboxAutomationButtonFlags.LeftThumbButton | XboxAutomationButtonFlags.RightThumbButton
                | XboxAutomationButtonFlags.StartButton | XboxAutomationButtonFlags.BackButton
                | XboxAutomationButtonFlags.Xbox360_Button | XboxAutomationButtonFlags.Bind_Button;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e) => StopPad();

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_pad == null) return;
            switch (e.KeyCode)
            {
                case Keys.W: _pad.SetLeftThumb(0, XboxVirtualController.ThumbMax); break;
                case Keys.S: _pad.SetLeftThumb(0, -XboxVirtualController.ThumbMax); break;
                case Keys.A: _pad.SetLeftThumb(-XboxVirtualController.ThumbMax, 0); break;
                case Keys.D: _pad.SetLeftThumb(XboxVirtualController.ThumbMax, 0); break;
                case Keys.Up: _pad.SetButton(XboxAutomationButtonFlags.DPadUp, true); break;
                case Keys.Down: _pad.SetButton(XboxAutomationButtonFlags.DPadDown, true); break;
                case Keys.Left: _pad.SetButton(XboxAutomationButtonFlags.DPadLeft, true); break;
                case Keys.Right: _pad.SetButton(XboxAutomationButtonFlags.DPadRight, true); break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (_pad == null) return;
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.S:
                case Keys.A:
                case Keys.D:
                    _pad.SetLeftThumb(0, 0);
                    break;
                case Keys.Up: _pad.SetButton(XboxAutomationButtonFlags.DPadUp, false); break;
                case Keys.Down: _pad.SetButton(XboxAutomationButtonFlags.DPadDown, false); break;
                case Keys.Left: _pad.SetButton(XboxAutomationButtonFlags.DPadLeft, false); break;
                case Keys.Right: _pad.SetButton(XboxAutomationButtonFlags.DPadRight, false); break;
            }
        }
    }
}
