using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    /// <summary>
    /// Shows xbdm <c>walkmem</c> output (DmWalkCommittedMemory) in a grid, similar to
    /// classic X360 tools' region walk listings.
    /// </summary>
    internal sealed class WalkMemoryForm : Form
    {
        private readonly XboxConsole _console;
        private readonly ListView _lv = new ListView();
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _lbl = new ToolStripStatusLabel();

        public WalkMemoryForm(XboxConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            Text = "Committed memory (walkmem)";
            ClientSize = new Size(780, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9F);

            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.GridLines = false;
            _lv.HideSelection = false;
            _lv.BackColor = Color.FromArgb(30, 30, 30);
            _lv.ForeColor = Color.WhiteSmoke;
            _lv.Font = new Font("Consolas", 9F);
            _lv.Columns.Add("#", 40);
            _lv.Columns.Add("Base", 110);
            _lv.Columns.Add("End", 110);
            _lv.Columns.Add("Size", 90);
            _lv.Columns.Add("Protect", 120);
            _lv.Columns.Add("Flags", 220);
            _lv.DoubleClick += OnDoubleClick;

            _status.Dock = DockStyle.Bottom;
            _status.Items.Add(_lbl);
            _lbl.Spring = true;
            _lbl.TextAlign = ContentAlignment.MiddleLeft;

            Controls.Add(_lv);
            Controls.Add(_status);

            Shown += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _lbl.Text = "Querying console…";
            List<XboxMemoryRegion> rows;
            try
            {
                rows = await Task.Run(() => _console.WalkCommittedMemory());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "walkmem", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                _lbl.Text = "Error.";
                return;
            }

            _lv.BeginUpdate();
            _lv.Items.Clear();
            int i = 0;
            foreach (var r in rows.OrderBy(x => x.BaseAddress))
            {
                uint end = unchecked(r.BaseAddress + Math.Max(1u, r.Size) - 1);
                var item = new ListViewItem((++i).ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add("0x" + r.BaseAddress.ToString("X8"));
                item.SubItems.Add("0x" + end.ToString("X8"));
                item.SubItems.Add("0x" + r.Size.ToString("X8"));
                item.SubItems.Add("0x" + ((uint)r.Flags).ToString("X8"));
                item.SubItems.Add(FormatProtectFlags(r.Flags));
                item.Tag = r.BaseAddress;
                _lv.Items.Add(item);
            }
            _lv.EndUpdate();
            _lbl.Text = rows.Count == 0 ? "No regions returned (is the target stopped / xbdm ready?)." : $"{rows.Count:N0} region(s). Double-click a row to open Memory View at base.";
        }

        private static string FormatProtectFlags(XboxMemoryRegionFlags f)
        {
            uint u = (uint)f;
            if (u == 0) return "(none)";
            var parts = new List<string>();
            foreach (XboxMemoryRegionFlags bit in Enum.GetValues(typeof(XboxMemoryRegionFlags)))
            {
                uint b = (uint)bit;
                if (b != 0 && (u & b) == b)
                    parts.Add(bit.ToString());
            }
            return parts.Count == 0 ? "0x" + u.ToString("X8", CultureInfo.InvariantCulture) : string.Join(", ", parts);
        }

        private void OnDoubleClick(object sender, EventArgs e)
        {
            if (_lv.SelectedItems.Count == 0) return;
            if (!(_lv.SelectedItems[0].Tag is uint addr)) return;
            using (var mv = new MemoryViewForm(_console, addr))
                mv.ShowDialog(this);
        }
    }
}
