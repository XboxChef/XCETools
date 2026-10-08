using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace XCETools
{
    /// <summary>
    /// Streams discovered consoles into a list as they answer (UDP name service + TCP probe).
    /// </summary>
    internal sealed class DiscoveryPickerForm : Form
    {
        private readonly ListView _lv;
        private readonly Button _ok;
        private readonly Button _cancel;
        private readonly Label _info;
        private readonly SynchronizationContext _ui;
        private CancellationTokenSource _externalCts;

        public string PickedIP { get; private set; }

        public DiscoveryPickerForm(SynchronizationContext ui)
        {
            _ui = ui ?? SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            Text = "Discover xbdm Consoles";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(440, 340);
            MinimumSize = new Size(360, 240);

            _info = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(8, 4, 0, 0),
                Text = "Scanning… consoles appear below as they answer.",
                ForeColor = Color.FromArgb(190, 190, 190),
            };

            _lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                GridLines = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Consolas", 9F),
            };
            _lv.Columns.Add("Name", 160);
            _lv.Columns.Add("IP", 180);
            _lv.Columns.Add("Source", 70);
            _lv.DoubleClick += (s, ev) => Commit();

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 38,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(45, 45, 48),
            };
            _ok = new Button
            {
                Text = "Connect", Width = 90, Enabled = false, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(63, 63, 70), ForeColor = Color.WhiteSmoke
            };
            _cancel = new Button
            {
                Text = "Cancel", Width = 90, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(63, 63, 70), ForeColor = Color.WhiteSmoke
            };
            _ok.Click += (s, ev) => Commit();
            _cancel.Click += (s, ev) => { _externalCts?.Cancel(); DialogResult = DialogResult.Cancel; Close(); };
            bottom.Controls.Add(_ok);
            bottom.Controls.Add(_cancel);

            Controls.Add(_lv);
            Controls.Add(bottom);
            Controls.Add(_info);
            AcceptButton = _ok;
            CancelButton = _cancel;

            _lv.SelectedIndexChanged += (s, ev) => _ok.Enabled = _lv.SelectedItems.Count > 0;
        }

        public void WireCancel(CancellationTokenSource cts) => _externalCts = cts;

        public void AddHit(XboxClient.DiscoveredConsole hit)
        {
            if (hit == null || string.IsNullOrEmpty(hit.IPAddress)) return;
            _ui.Post(_ =>
            {
                if (IsDisposed) return;
                foreach (ListViewItem existing in _lv.Items)
                {
                    if (string.Equals(existing.SubItems[1].Text, hit.IPAddress, StringComparison.Ordinal))
                    {
                        if (!string.IsNullOrEmpty(hit.Name)) existing.SubItems[0].Text = hit.Name;
                        return;
                    }
                }
                var item = new ListViewItem(hit.Name ?? "(unknown)");
                item.SubItems.Add(hit.IPAddress);
                item.SubItems.Add(string.IsNullOrEmpty(hit.Name) ? "tcp" : "name");
                item.Tag = hit.IPAddress;
                _lv.Items.Add(item);
                if (_lv.SelectedItems.Count == 0) item.Selected = true;
                _info.Text = $"Found {_lv.Items.Count} console(s). Double-click to connect.";
            }, null);
        }

        private void Commit()
        {
            if (_lv.SelectedItems.Count == 0) return;
            PickedIP = (_lv.SelectedItems[0].Tag as string) ?? _lv.SelectedItems[0].SubItems[1].Text;
            _externalCts?.Cancel();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
