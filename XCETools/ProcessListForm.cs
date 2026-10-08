using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class ProcessListForm : Form
    {
        private readonly XboxConsole _console;
        private readonly TabControl _tabs = new TabControl();
        private readonly ListView _lvApps = NewListView();
        private readonly ListView _lvAll  = NewListView();
        private readonly ListView _lvWin  = NewListView();
        private readonly Label _status   = new Label();

        private readonly Button _open    = MakeBtn("Open");
        private readonly Button _cancel  = MakeBtn("Cancel");
        private readonly Button _attach  = MakeBtn("Attach debugger to process");
        private readonly Button _network = MakeBtn("Network");

        private List<ModuleInfo> _modules = new List<ModuleInfo>();
        private string _runningName = string.Empty;

        public string SelectedModuleName { get; private set; }
        public uint   SelectedModuleBase { get; private set; }
        public uint   SelectedModuleSize { get; private set; }

        public ProcessListForm(XboxConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            Text = "Process List";
            ClientSize = new Size(360, 540);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;

            // File "menu" hint (CE has a single File label above tabs).
            var fileHint = new Label
            {
                Left = 8, Top = 6, AutoSize = true, Text = "File",
                ForeColor = Color.Gainsboro,
            };

            _tabs.Left = 6;
            _tabs.Top  = 26;
            _tabs.Width = ClientSize.Width - 12;
            _tabs.Height = 340;
            _tabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.DrawItem += OnTabDraw;

            var tpApps = new TabPage("Applications") { BackColor = Color.FromArgb(30, 30, 30) };
            var tpAll  = new TabPage("Processes")    { BackColor = Color.FromArgb(30, 30, 30) };
            var tpWin  = new TabPage("Windows")      { BackColor = Color.FromArgb(30, 30, 30) };

            tpApps.Controls.Add(_lvApps); _lvApps.Dock = DockStyle.Fill;
            tpAll .Controls.Add(_lvAll ); _lvAll .Dock = DockStyle.Fill;
            tpWin .Controls.Add(_lvWin ); _lvWin .Dock = DockStyle.Fill;

            _tabs.TabPages.Add(tpApps);
            _tabs.TabPages.Add(tpAll);
            _tabs.TabPages.Add(tpWin);

            foreach (var lv in new[] { _lvApps, _lvAll, _lvWin })
            {
                lv.SmallImageList = BuildModuleIcons();
                lv.Columns.Add("", 230);
                lv.Columns.Add("", 100);
                lv.DoubleClick += (s, e) => CommitSelection();
                lv.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                        ShowContextMenu(lv, e.Location);
                };
            }

            _status.Left = 8; _status.Top = _tabs.Bottom + 4;
            _status.AutoSize = false;
            _status.Width = ClientSize.Width - 16;
            _status.Height = 18;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.Silver;
            _status.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _status.Text = "Loading modules...";

            int btnY1 = ClientSize.Height - 80;
            int btnY2 = ClientSize.Height - 44;
            _open   .SetBounds(   8, btnY1, 168, 30);
            _cancel .SetBounds( 184, btnY1, 168, 30);
            _attach .SetBounds(   8, btnY2, 220, 30);
            _network.SetBounds( 232, btnY2, 120, 30);
            foreach (var b in new[] { _open, _cancel, _attach, _network })
                b.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

            _open   .Click += (s, e) => CommitSelection();
            _cancel .Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _attach .Click += OnAttachDebugger;
            _network.Click += OnNetwork;

            Controls.AddRange(new Control[] { fileHint, _tabs, _status, _open, _cancel, _attach, _network });

            Shown += async (s, e) => await RefreshAsync();
        }

        // ---------------------- module loading -----------------------------

        private async Task RefreshAsync()
        {
            _status.Text = _console.Connected
                ? "Loading modules from " + _console.IPAddress + "..."
                : "Not connected — use Network... to enter an IP.";

            if (!_console.Connected)
            {
                FillLists(new List<ModuleInfo>());
                    return;
            }

            List<ModuleInfo> mods = null;
            string running = null;
            try
            {
                mods    = await Task.Run(() => _console.GetModules() ?? new List<ModuleInfo>());
                running = await Task.Run(() => SafeGetRunningName());
            }
            catch (Exception ex)
            {
                _status.Text = "modules failed: " + ex.Message;
                FillLists(new List<ModuleInfo>());
                    return;
            }

            _modules = mods.OrderBy(m => m.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList();
            _runningName = running ?? string.Empty;
            FillLists(_modules);

            _status.Text = _console.IPAddress + " — " + _modules.Count + " module(s) loaded";
        }

        private string SafeGetRunningName()
        {
            try
            {
                var raw = _console.GetXbeInfo();
                if (string.IsNullOrEmpty(raw)) return null;
                int i = raw.IndexOf("name=", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return null;
                int q1 = raw.IndexOf('"', i);
                int q2 = q1 >= 0 ? raw.IndexOf('"', q1 + 1) : -1;
                if (q1 < 0 || q2 < 0) return null;
                string path = raw.Substring(q1 + 1, q2 - q1 - 1);
                int slash = path.LastIndexOfAny(new[] { '\\', '/' });
                return slash >= 0 ? path.Substring(slash + 1) : path;
            }
            catch { return null; }
        }

        private void FillLists(List<ModuleInfo> mods)
        {
            _lvAll .BeginUpdate(); _lvAll .Items.Clear();
            _lvApps.BeginUpdate(); _lvApps.Items.Clear();
            _lvWin .BeginUpdate(); _lvWin .Items.Clear();

            foreach (var m in mods)
            {
                bool isXex = !string.IsNullOrEmpty(m.Name) &&
                             m.Name.EndsWith(".xex", StringComparison.OrdinalIgnoreCase);
                bool isRunning = !string.IsNullOrEmpty(_runningName) &&
                                 !string.IsNullOrEmpty(m.Name) &&
                                 m.Name.Equals(_runningName, StringComparison.OrdinalIgnoreCase);

                var item = new ListViewItem(BuildRowTitle(m, isRunning))
                {
                    ImageIndex = isXex ? 0 : 1,
                    Tag = m,
                    ToolTipText = string.Format(
                        "Name: {0}\nBase: 0x{1:X8}\nSize: 0x{2:X}\nChecksum: 0x{3:X8}\nTimestamp: {4:yyyy-MM-dd HH:mm}",
                        m.Name ?? "?", m.BaseAddress, m.Size, m.Checksum, m.TimeStamp),
                };
                item.SubItems.Add(string.Format("{0,8} KB", (m.Size + 1023) / 1024));

                _lvAll.Items.Add(item);
                if (isXex || isRunning) _lvApps.Items.Add((ListViewItem)item.Clone());
            }

            _lvAll .EndUpdate();
            _lvApps.EndUpdate();
            _lvWin .EndUpdate();

            // Pre-select something useful when the dialog opens.
            if (_lvApps.Items.Count > 0) { _tabs.SelectedIndex = 0; _lvApps.Items[0].Selected = true; _lvApps.Items[0].EnsureVisible(); _lvApps.Focus(); }
            else if (_lvAll.Items.Count > 0) { _tabs.SelectedIndex = 1; _lvAll.Items[0].Selected = true; _lvAll.Items[0].EnsureVisible(); _lvAll.Focus(); }
        }

        private static string BuildRowTitle(ModuleInfo m, bool isRunning)
        {
            string id = string.Format("{0:X8}", m.BaseAddress);
            string nm = m.Name ?? "(unknown)";
            return id + (isRunning ? " * " : " - ") + nm;
        }

        // ---------------------- buttons ------------------------------------

        private void CommitSelection()
        {
            var lv = ActiveList();
            if (lv == null || lv.SelectedItems.Count == 0)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }
            var mi = lv.SelectedItems[0].Tag as ModuleInfo;
            SelectedModuleName = mi != null ? mi.Name : null;
            SelectedModuleBase = mi != null ? mi.BaseAddress : 0u;
            SelectedModuleSize = mi != null ? mi.Size : 0u;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnAttachDebugger(object sender, EventArgs e)
        {
            try
            {
                if (!_console.Connected) { _status.Text = "Not connected."; return; }
                _console.AttachDebugger("XCE Atlas", Environment.UserName);
                _status.Text = "Debugger attached.";
            }
            catch (Exception ex) { _status.Text = "Attach failed: " + ex.Message; }
        }

        private async void OnNetwork(object sender, EventArgs e)
        {
            using (var p = new Form())
            {
                p.Text = "Network";
                p.ClientSize = new Size(320, 110);
                p.StartPosition = FormStartPosition.CenterParent;
                p.BackColor = Color.FromArgb(45, 45, 48);
                p.ForeColor = Color.WhiteSmoke;
                p.Font = Font;
                p.FormBorderStyle = FormBorderStyle.FixedDialog;
                p.MinimizeBox = false; p.MaximizeBox = false; p.ShowInTaskbar = false;

                var lbl = new Label { Left = 10, Top = 12, AutoSize = true, Text = "Console IP (blank = LAN auto-discover):" };
                var tb  = new TextBox
                {
                    Left = 10, Top = 36, Width = 300,
                    BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9F),
                    Text = _console.Connected ? _console.IPAddress : string.Empty,
                };
                var ok = MakeBtn("Connect"); ok.SetBounds(150, 72, 75, 26); ok.DialogResult = DialogResult.OK;
                var no = MakeBtn("Cancel");  no.SetBounds(235, 72, 75, 26); no.DialogResult = DialogResult.Cancel;
                p.AcceptButton = ok; p.CancelButton = no;
                p.Controls.AddRange(new Control[] { lbl, tb, ok, no });

                if (p.ShowDialog(this) != DialogResult.OK) return;

                string ip = (tb.Text ?? string.Empty).Trim();
                _status.Text = ip.Length == 0 ? "Discovering on LAN..." : "Connecting to " + ip + "...";

                bool connected = await Task.Run(() =>
                {
                    try
                    {
                        if (_console.Connected) _console.Disconnect();
                        if (ip.Length == 0) return _console.Connect();
                        int colon = ip.LastIndexOf(':');
                        if (colon > 0 && int.TryParse(ip.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                            return _console.Connect(ip.Substring(0, colon), port);
                        return _console.Connect(ip);
                    }
                    catch { return false; }
                });

                if (!connected) { _status.Text = "Connection failed."; return; }
                await RefreshAsync();
            }
        }

        // ---------------------- list helpers -------------------------------

        private ListView ActiveList()
        {
            if (_tabs.SelectedTab == null) return null;
            return _tabs.SelectedTab.Controls.Count > 0
                ? _tabs.SelectedTab.Controls[0] as ListView
                : null;
        }

        private void ShowContextMenu(ListView lv, Point at)
        {
            var menu = new ContextMenuStrip { BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.WhiteSmoke };
            menu.Items.Add("Refresh", null, async (s, e) => await RefreshAsync());
            menu.Items.Add("Copy name", null, (s, e) =>
            {
                if (lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag is ModuleInfo mi && !string.IsNullOrEmpty(mi.Name))
                    try { Clipboard.SetText(mi.Name); } catch { /* clipboard busy */ }
            });
            menu.Items.Add("Copy base address", null, (s, e) =>
            {
                if (lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag is ModuleInfo mi)
                    try { Clipboard.SetText("0x" + mi.BaseAddress.ToString("X8")); } catch { }
            });
            menu.Show(lv, at);
        }

        private static void OnTabDraw(object sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender;
            var page = tc.TabPages[e.Index];
            var bg = (e.State & DrawItemState.Selected) != 0
                ? Color.FromArgb(63, 63, 70) : Color.FromArgb(45, 45, 48);

            using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
            using (var fg = new SolidBrush(Color.WhiteSmoke))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                e.Graphics.DrawString(page.Text, e.Font ?? tc.Font, fg, e.Bounds, sf);
        }

        private static ListView NewListView() => new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.None,
            GridLines = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.WhiteSmoke,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F),
            ShowItemToolTips = true,
        };

        private static Button MakeBtn(string text) => new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(63, 63, 70),
            ForeColor = Color.WhiteSmoke,
            UseVisualStyleBackColor = false,
        };

        private static ImageList BuildModuleIcons()
        {
            var il = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            il.Images.Add(DrawModuleGlyph(Color.FromArgb(255, 196, 64), Color.FromArgb(160, 100, 0)));    // .xex - yellow folder
            il.Images.Add(DrawModuleGlyph(Color.FromArgb(120, 180, 220), Color.FromArgb(40, 80, 130)));   // .dll - blue chip
            return il;
        }

        private static Bitmap DrawModuleGlyph(Color fill, Color border)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(fill)) g.FillRectangle(br, 2, 4, 12, 10);
                using (var bp = new Pen(border, 1)) g.DrawRectangle(bp, 2, 4, 12, 10);
                using (var bp = new Pen(border, 1)) g.DrawLine(bp, 2, 7, 14, 7);
            }
            return bmp;
        }
    }
}
