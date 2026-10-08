using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class BreakpointsForm : Form
    {
        private readonly XboxConsole _console;
        private readonly TextBox _addr = new TextBox();
        private readonly ListBox _list = new ListBox();

        public BreakpointsForm(XboxConsole console)
        {
            _console = console;
            Text = "Breakpoints";
            ClientSize = new Size(360, 320);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);

            var lbl = new Label { Left = 12, Top = 14, AutoSize = true, Text = "Address (hex):" };
            _addr.Left = 110; _addr.Top = 10; _addr.Width = 150;
            _addr.BackColor = Color.FromArgb(30, 30, 30); _addr.ForeColor = Color.WhiteSmoke;
            _addr.BorderStyle = BorderStyle.FixedSingle; _addr.Font = new Font("Consolas", 9F);

            var btnSet = Btn("Set", 270, 8);
            btnSet.Click += (s, e) => SafeCall(() =>
            {
                var a = ParseAddr();
                _console.SetBreakpoint(a);
                if (!_list.Items.Cast<string>().Any(x => x.Equals($"0x{a:X8}", StringComparison.OrdinalIgnoreCase)))
                    _list.Items.Add($"0x{a:X8}");
            });
            var btnClear = Btn("Clear", 270, 38);
            btnClear.Click += (s, e) => SafeCall(() =>
            {
                var a = ParseAddr();
                _console.ClearBreakpoint(a);
                for (int k = 0; k < _list.Items.Count; k++)
                {
                    if (_list.Items[k].ToString().Equals($"0x{a:X8}", StringComparison.OrdinalIgnoreCase))
                    {
                        _list.Items.RemoveAt(k);
                        break;
                    }
                }
            });
            var btnClearAll = Btn("Clear All", 270, 68);
            btnClearAll.Click += (s, e) => SafeCall(() => { _console.ClearAllBreakpoints(); _list.Items.Clear(); });
            var btnAttach = Btn("Attach", 270, 108);
            btnAttach.Click += (s, e) => SafeCall(() => _console.AttachDebugger("XCE Atlas", Environment.UserName));
            var btnDetach = Btn("Detach", 270, 138);
            btnDetach.Click += (s, e) => SafeCall(() => _console.DetachDebugger());
            var btnClose = Btn("Close", 270, 280);
            btnClose.Click += (s, e) => Close();

            _list.Left = 12; _list.Top = 44; _list.Width = 248; _list.Height = 260;
            _list.BackColor = Color.FromArgb(30, 30, 30); _list.ForeColor = Color.WhiteSmoke;
            _list.BorderStyle = BorderStyle.FixedSingle; _list.Font = new Font("Consolas", 9F);
            _list.DoubleClick += (s, e) =>
            {
                if (_list.SelectedItem is string sel && sel.StartsWith("0x"))
                    _addr.Text = sel.Substring(2);
            };

            Controls.AddRange(new Control[] { lbl, _addr, btnSet, btnClear, btnClearAll, btnAttach, btnDetach, btnClose, _list });
        }

        private static Button Btn(string text, int left, int top) => new Button
        {
            Text = text, Left = left, Top = top, Width = 80, Height = 26,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(63, 63, 70), ForeColor = Color.WhiteSmoke
        };

        private uint ParseAddr()
        {
            string s = _addr.Text?.Trim() ?? string.Empty;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private void SafeCall(Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Breakpoints", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
