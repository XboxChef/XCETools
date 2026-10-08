// =============================================================================
// QuickFindForm.cs - Quick value / pattern search in a memory range
// =============================================================================
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class QuickFindForm : Form
    {
        private readonly XboxConsole _console;
        private readonly string _defaultStart;
        private readonly string _defaultStop;
        private readonly ListView _results = new ListView();
        private readonly TextBox _value = new TextBox();
        private readonly ComboBox _mode = new ComboBox();

        public QuickFindForm(XboxConsole console, string defaultStart, string defaultStop)
        {
            _console = console;
            _defaultStart = defaultStart;
            _defaultStop = defaultStop;
            Text = "Quick memory find";
            ClientSize = new Size(400, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;

            var top = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = BackColor };
            var lbl = new Label { Text = "Value (hex uint / pattern with ??):", Left = 8, Top = 8, AutoSize = true, ForeColor = Color.WhiteSmoke };
            _value.Left = 8; _value.Top = 28; _value.Width = 280;
            _value.BackColor = Color.FromArgb(30, 30, 30); _value.ForeColor = Color.WhiteSmoke;
            _mode.Left = 8; _mode.Top = 52; _mode.Width = 120;
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.AddRange(new object[] { "UInt32", "Hex pattern" });
            _mode.SelectedIndex = 0;
            var btn = new Button
            {
                Text = "Find", Left = 300, Top = 26, Width = 80, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 124, 65), ForeColor = Color.White
            };
            btn.Click += OnFind;
            top.Controls.AddRange(new Control[] { lbl, _value, _mode, btn });

            _results.Dock = DockStyle.Fill;
            _results.View = View.Details;
            _results.FullRowSelect = true;
            _results.BackColor = Color.FromArgb(30, 30, 30);
            _results.ForeColor = Color.WhiteSmoke;
            _results.Columns.Add("Address", 120);
            _results.Columns.Add("Note", 240);
            _results.DoubleClick += (_, __) =>
            {
                if (_results.SelectedItems.Count > 0 && _results.SelectedItems[0].Text.StartsWith("0x"))
                    Clipboard.SetText(_results.SelectedItems[0].Text);
            };

            Controls.Add(_results);
            Controls.Add(top);
        }

        private void OnFind(object sender, EventArgs e)
        {
            if (!_console.Connected) return;
            if (!TryParseRange(_defaultStart, _defaultStop, out uint start, out uint length))
            {
                MessageBox.Show(this, "Set valid Start/Stop in the main scan options first.", Text);
                return;
            }

            string raw = _value.Text?.Trim() ?? "";
            MemorySearchResult r;
            try
            {
                if (_mode.SelectedIndex == 1)
                    r = _console.FindHexPattern(start, length, raw);
                else
                {
                    if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(2);
                    if (!uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
                    {
                        MessageBox.Show(this, "Enter a hex value (e.g. DEADBEEF).", Text);
                        return;
                    }
                    r = _console.FindUInt32(start, length, v);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text);
                return;
            }

            _results.BeginUpdate();
            _results.Items.Clear();
            foreach (uint a in r.Addresses)
            {
                var item = new ListViewItem($"0x{a:X8}");
                item.SubItems.Add("");
                _results.Items.Add(item);
            }
            if (r.Truncated)
            {
                var item = new ListViewItem("(truncated)");
                item.SubItems.Add($">{XboxMemorySearch.DefaultMaxHits} hits");
                _results.Items.Add(item);
            }
            _results.EndUpdate();
            Text = $"Quick find — {r.Addresses.Count} hit(s), {r.BytesScanned:N0} bytes scanned";
        }

        private static bool TryParseRange(string startHex, string stopHex, out uint start, out uint length)
        {
            start = length = 0;
            string sh = startHex?.Trim() ?? "";
            string eh = stopHex?.Trim() ?? "";
            if (sh.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) sh = sh.Substring(2);
            if (eh.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) eh = eh.Substring(2);
            if (!uint.TryParse(sh, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out start)) return false;
            if (!uint.TryParse(eh, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint stop)) return false;
            if (stop <= start) return false;
            length = stop - start;
            return true;
        }
    }
}
