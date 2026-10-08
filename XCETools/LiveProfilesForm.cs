// =============================================================================
// LiveProfilesForm.cs - XEDK profile list + in-house LIVE shortcuts
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class LiveProfilesForm : Form
    {
        private readonly XboxConsole _console;
        private readonly ListBox _profiles = new ListBox();
        private readonly Label _status = new Label();

        public LiveProfilesForm(XboxConsole console)
        {
            _console = console;
            Text = "Xbox LIVE / profiles";
            ClientSize = new Size(420, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;

            _status.Dock = DockStyle.Top;
            _status.Height = 48;
            _status.Padding = new Padding(8);
            _status.ForeColor = Color.WhiteSmoke;
            RefreshStatus();

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor,
                Padding = new Padding(6, 4, 6, 4)
            };
            bar.Controls.Add(MkBtn("Quick sign-in", OnQuickSignIn));
            bar.Controls.Add(MkBtn("Friends", (_, __) => XboxLiveUi.OpenFriends(_console)));
            bar.Controls.Add(MkBtn("Party", (_, __) => XboxLiveUi.OpenParty(_console)));
            bar.Controls.Add(MkBtn("Refresh XEDK list", OnRefreshXedk));

            _profiles.Dock = DockStyle.Fill;
            _profiles.BackColor = Color.FromArgb(30, 30, 30);
            _profiles.ForeColor = Color.WhiteSmoke;

            Controls.Add(_profiles);
            Controls.Add(bar);
            Controls.Add(_status);

            OnRefreshXedk(null, EventArgs.Empty);
        }

        void RefreshStatus()
        {
            if (_console == null || !_console.Connected)
            {
                _status.Text = "Not connected.";
                return;
            }
            var state = XboxLiveUi.GetSigninState(_console);
            string xedk = XboxLiveUi.XedkProfilesAvailable
                ? $"XEDK: {XboxLiveUi.XedkRoot}"
                : "XEDK Profiles.dll not found (in-house shortcuts only).";
            _status.Text = $"Sign-in state (user 0): {state}\r\n{xedk}";
        }

        void OnQuickSignIn(object s, EventArgs e)
        {
            try
            {
                XboxLiveUi.QuickSignIn(_console);
                RefreshStatus();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        void OnRefreshXedk(object s, EventArgs e)
        {
            _profiles.Items.Clear();
            if (!XboxLiveUi.XedkProfilesAvailable)
            {
                _profiles.Items.Add("(Install Xbox 360 XEDK for profile enumeration)");
                return;
            }
            string name = _console?.Name;
            var list = XboxLiveUi.TryEnumerateXedkProfiles(name, out string err);
            if (list == null)
            {
                _profiles.Items.Add(err ?? "Could not open XEDK console.");
                return;
            }
            if (list.Count == 0)
                _profiles.Items.Add("(no profiles)");
            else
                foreach (string p in list)
                    _profiles.Items.Add(p);
        }

        static Button MkBtn(string text, EventHandler click)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 124, 65),
                ForeColor = Color.White,
                Margin = new Padding(4, 0, 4, 0)
            };
            b.Click += click;
            return b;
        }

        void ShowError(Exception ex) =>
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
