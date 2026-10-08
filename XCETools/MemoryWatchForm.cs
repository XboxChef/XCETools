// =============================================================================
// MemoryWatchForm.cs - Trainer-style address watch / freeze
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class MemoryWatchForm : Form
    {
        private readonly XboxConsole _console;
        private readonly ListView _list = new ListView();
        private readonly Timer _timer = new Timer { Interval = 250 };
        private readonly TextBox _addr = new TextBox();
        private readonly ComboBox _type = new ComboBox();
        private readonly CheckBox _freeze = new CheckBox();

        public MemoryWatchForm(XboxConsole console)
        {
            _console = console;
            Text = "Memory Watch";
            ClientSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);

            var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BackColor };
            _addr.Width = 120; _addr.Left = 8; _addr.Top = 8;
            _addr.BackColor = Color.FromArgb(30, 30, 30); _addr.ForeColor = Color.WhiteSmoke;
            _type.Left = 136; _type.Top = 6; _type.Width = 80;
            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Items.AddRange(new object[] { "UInt32", "Int32", "Float", "UInt16", "Byte" });
            _type.SelectedIndex = 0;
            var btnAdd = Btn("Add", 224, 6);
            btnAdd.Click += OnAdd;
            var btnFreeze = Btn("Freeze all", 290, 6);
            btnFreeze.Click += (_, __) => { _console.MemoryWatch.FreezeAll(); RefreshList(); };
            var btnUnfreeze = Btn("Unfreeze", 380, 6);
            btnUnfreeze.Click += (_, __) => { _console.MemoryWatch.UnfreezeAll(); RefreshList(); };
            top.Controls.AddRange(new Control[] { _addr, _type, btnAdd, btnFreeze, btnUnfreeze });

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.BackColor = Color.FromArgb(30, 30, 30);
            _list.ForeColor = Color.WhiteSmoke;
            _list.Columns.Add("Address", 100);
            _list.Columns.Add("Type", 60);
            _list.Columns.Add("Value", 100);
            _list.Columns.Add("Freeze", 50);
            _list.Columns.Add("Description", 160);

            Controls.Add(_list);
            Controls.Add(top);

            _timer.Tick += (_, __) => { _console.MemoryWatch.Poll(); RefreshList(); };
            FormClosing += (_, __) => _timer.Stop();
            _timer.Start();
            RefreshList();
        }

        private static Button Btn(string t, int l, int y) => new Button
        {
            Text = t, Left = l, Top = y, Width = 86, Height = 26,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(63, 63, 70), ForeColor = Color.WhiteSmoke
        };

        private void OnAdd(object sender, EventArgs e)
        {
            if (!_console.Connected) return;
            string s = _addr.Text?.Trim() ?? "";
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint addr))
            {
                MessageBox.Show(this, "Invalid hex address.", Text);
                return;
            }
            var t = ParseType(_type.SelectedItem?.ToString());
            _console.MemoryWatch.Add(addr, t, freeze: false);
            RefreshList();
        }

        private static MemoryWatchType ParseType(string name)
        {
            switch (name)
            {
                case "Int32": return MemoryWatchType.Int32;
                case "Float": return MemoryWatchType.Float;
                case "UInt16": return MemoryWatchType.UInt16;
                case "Byte": return MemoryWatchType.Byte;
                default: return MemoryWatchType.UInt32;
            }
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var e in _console.MemoryWatch.Entries)
            {
                var item = new ListViewItem($"0x{e.Address:X8}");
                item.SubItems.Add(e.Type.ToString());
                item.SubItems.Add(FormatValue(e.LastValue));
                item.SubItems.Add(e.Freeze ? "Yes" : "");
                item.SubItems.Add(e.Description ?? "");
                item.Tag = e;
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }

        private static string FormatValue(object v)
        {
            if (v == null) return "";
            if (v is uint u) return u.ToString("X8");
            if (v is int i) return i.ToString();
            if (v is float f) return f.ToString("G9");
            return v.ToString();
        }
    }
}
