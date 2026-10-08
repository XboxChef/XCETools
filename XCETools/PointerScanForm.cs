using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    /// <summary>
    /// Level-1 pointer scan: every 4-byte aligned big-endian <see cref="uint"/>
    /// in a range that equals the target address (classic “find what points to
    /// this address” / static reference hunt on PPC).
    /// </summary>
    internal sealed class PointerScanForm : Form
    {
        private static readonly Color Bg      = Color.FromArgb(45, 45, 48);
        private static readonly Color Panel = Color.FromArgb(30, 30, 30);
        private static readonly Color Fg    = Color.WhiteSmoke;
        private static readonly Color Dim   = Color.FromArgb(180, 180, 180);

        private readonly XboxConsole _console;
        private readonly ScanHitPageStore _hits = new ScanHitPageStore("pointer-scans");
        private CancellationTokenSource _cts;

        private readonly TextBox _targetBox = new TextBox();
        private readonly TextBox _startBox = new TextBox();
        private readonly TextBox _stopBox  = new TextBox();
        private readonly Button  _btnScan  = new Button();
        private readonly Button  _btnCancel = new Button();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _status = new Label();
        private readonly ListView _lv = new ListView();

        private readonly CheckBox _activeOnly = new CheckBox();

        public PointerScanForm(XboxConsole console, uint? targetGuess = null, string startHex = null, string stopHex = null)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            Text = "Pointer scan (level 1)";
            ClientSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(520, 400);
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.Sizable;

            var tip = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(12, 8, 12, 4),
                ForeColor = Dim,
                Text = "Finds every 4-byte aligned address whose value (big-endian, PPC) equals the target. " +
                       "Results are paged to disk; the list shows the first 5 000 hits."
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(12, 4, 12, 8),
                BackColor = Bg
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            void Row(int r, string label, Control field)
            {
                var lb = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Dim, Margin = new Padding(0, 6, 8, 0) };
                grid.Controls.Add(lb, 0, r);
                grid.SetColumnSpan(field, 3);
                field.Dock = DockStyle.Fill;
                field.Margin = new Padding(0, 2, 0, 4);
                field.BackColor = Panel;
                field.ForeColor = Fg;
                field.Font = new Font("Consolas", 10F);
                grid.Controls.Add(field, 1, r);
            }

            _targetBox.Text = targetGuess.HasValue ? $"{targetGuess.Value:X8}" : string.Empty;
            _startBox.Text  = string.IsNullOrEmpty(startHex)  ? "C2000000" : startHex;
            _stopBox.Text   = string.IsNullOrEmpty(stopHex)   ? "E0000000" : stopHex;

            Row(0, "Target", _targetBox);
            Row(1, "Start", _startBox);
            Row(2, "Stop",  _stopBox);

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(12, 0, 12, 8),
                BackColor = Bg
            };
            _btnScan.Text = "Scan";
            _btnScan.AutoSize = true;
            _btnScan.Padding = new Padding(16, 6, 16, 6);
            _btnScan.BackColor = Color.FromArgb(63, 63, 70);
            _btnScan.ForeColor = Fg;
            _btnScan.FlatStyle = FlatStyle.Flat;
            _btnScan.Click += async (_, __) => await RunScanAsync();

            _btnCancel.Text = "Cancel";
            _btnCancel.AutoSize = true;
            _btnCancel.Enabled = false;
            _btnCancel.Padding = new Padding(12, 6, 12, 6);
            _btnCancel.BackColor = Color.FromArgb(63, 63, 70);
            _btnCancel.ForeColor = Fg;
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.Margin = new Padding(12, 0, 0, 0);
            _btnCancel.Click += (_, __) => { try { _cts?.Cancel(); } catch { /* ignore */ } };

            bar.Controls.Add(_btnScan);
            bar.Controls.Add(_btnCancel);

            _activeOnly.Text = "Active memory only (walkmem)";
            _activeOnly.AutoSize = true;
            _activeOnly.ForeColor = Dim;
            _activeOnly.Checked = true;
            _activeOnly.Margin = new Padding(16, 6, 0, 0);
            bar.Controls.Add(_activeOnly);

            _progress.Dock = DockStyle.Top;
            _progress.Height = 18;
            _progress.Margin = new Padding(12, 0, 12, 4);
            _progress.Style = ProgressBarStyle.Continuous;

            _status.Dock = DockStyle.Top;
            _status.Height = 22;
            _status.Padding = new Padding(12, 0, 12, 4);
            _status.ForeColor = Dim;
            _status.Text = "Idle.";

            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.HideSelection = false;
            _lv.BackColor = Panel;
            _lv.ForeColor = Fg;
            _lv.Font = new Font("Consolas", 9.5F);
            _lv.BorderStyle = BorderStyle.FixedSingle;
            _lv.Columns.Add("Pointer @", 160);
            _lv.Columns.Add("Value (BE u32)", 140);
            _lv.DoubleClick += OnListDoubleClick;

            Controls.Add(_lv);
            Controls.Add(_status);
            Controls.Add(_progress);
            Controls.Add(bar);
            Controls.Add(grid);
            Controls.Add(tip);

            FormClosing += (_, e) =>
            {
                try { _cts?.Cancel(); } catch { /* ignore */ }
                _hits.Dispose();
            };
        }

        private void OnListDoubleClick(object sender, EventArgs e)
        {
            if (_lv.SelectedItems.Count == 0) return;
            if (_lv.SelectedItems[0].Tag is uint addr)
            {
                using (var mv = new MemoryViewForm(_console, addr))
                    mv.ShowDialog(this);
            }
        }

        private static bool TryParseHexU32(string s, out uint v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }

        private static uint ReadU32BE(byte[] buf, int i)
            => ((uint)buf[i] << 24) | ((uint)buf[i + 1] << 16) | ((uint)buf[i + 2] << 8) | buf[i + 3];

        private static void ScanChunkForTarget(byte[] tmp, int n, uint target, uint baseVa, ScanHitPageStore hits)
        {
            for (int i = 0; i + 4 <= n; i += 4)
            {
                if (ReadU32BE(tmp, i) == target)
                    hits.Append(baseVa + (uint)i);
            }
        }

        private async Task RunScanAsync()
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Not connected to a console.", "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryParseHexU32(_targetBox.Text, out uint target))
            {
                MessageBox.Show(this, "Target must be 32-bit hex (e.g. 82001234).", "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryParseHexU32(_startBox.Text, out uint start) || !TryParseHexU32(_stopBox.Text, out uint stopU) || stopU <= start)
            {
                MessageBox.Show(this, "Start/Stop must be hex with Stop > Start.", "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ulong len64 = (ulong)stopU - start;
            if (len64 < 4)
            {
                MessageBox.Show(this, "Range too small.", "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            const ulong HardCap = 0x20000000ul;
            if (len64 > HardCap)
            {
                MessageBox.Show(this,
                    $"Range is {len64 / (1024.0 * 1024.0):N0} MiB; max is {HardCap / (1024 * 1024)} MiB.",
                    "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            uint rangeStart = (start + 3u) & ~3u;
            if (rangeStart >= stopU)
            {
                MessageBox.Show(this, "No 4-byte aligned window in range.", "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnScan.Enabled = false;
            _btnCancel.Enabled = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _progress.Value = 0;
            _lv.Items.Clear();
            _status.Text = "Scanning…";

            try
            {
                _hits.StartNewSession();

                await Task.Run(() =>
                {
                    var tmp = new byte[0x01000000];
                    ulong denom = (ulong)stopU - rangeStart;

                    void BumpProgress(uint pos)
                    {
                        ulong done = (ulong)(pos - rangeStart);
                        int pct = denom == 0 ? 100 : (int)Math.Min(100, done * 100 / denom);
                        BeginInvoke(new Action(() => _progress.Value = pct));
                    }

                    if (_activeOnly.Checked)
                    {
                        var segs = XboxMemoryScanHelpers.BuildMergedCommittedIntervals(_console, start, stopU - start);
                        if (segs.Count == 0)
                        {
                            BeginInvoke(new Action(() =>
                                MessageBox.Show(this, "walkmem returned no regions in this range (or failed). Uncheck Active memory only to scan linearly.",
                                    "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                            return;
                        }

                        foreach (var (segStart, segLen) in segs)
                        {
                            uint segEnd = segStart + segLen;
                            uint pos = segStart < rangeStart ? rangeStart : segStart;
                            pos = (pos + 3u) & ~3u;
                            if (pos >= stopU || pos >= segEnd)
                                continue;

                            while (pos + 4 <= stopU && pos < segEnd)
                            {
                                token.ThrowIfCancellationRequested();

                                uint ch = XboxMemoryScanHelpers.ChunkSize(pos);
                                uint room = Math.Min(segEnd - pos, stopU - pos);
                                uint want = ch < room ? ch : room;
                                want = (want / 4) * 4;
                                if (want == 0)
                                    break;

                                if (tmp.Length < want)
                                    tmp = new byte[want];

                                uint got = 0;
                                try
                                {
                                    _console.GetMemory(pos, want, tmp, out got);
                                }
                                catch
                                {
                                    got = 0;
                                }

                                if (got < want)
                                    Array.Clear(tmp, (int)got, (int)(want - got));

                                int n = (int)(want / 4) * 4;
                                ScanChunkForTarget(tmp, n, target, pos, _hits);

                                pos += want;
                                BumpProgress(pos);
                            }
                        }
                    }
                    else
                    {
                        uint pos = rangeStart;
                        while (pos + 4 <= stopU)
                        {
                            token.ThrowIfCancellationRequested();

                            uint ch = XboxMemoryScanHelpers.ChunkSize(pos);
                            uint span = stopU - pos;
                            uint want = ch < span ? ch : span;
                            want = (want / 4) * 4;
                            if (want == 0) break;

                            if (tmp.Length < want)
                                tmp = new byte[want];

                            uint got = 0;
                            try
                            {
                                _console.GetMemory(pos, want, tmp, out got);
                            }
                            catch
                            {
                                got = 0;
                            }

                            if (got < want)
                                Array.Clear(tmp, (int)got, (int)(want - got));

                            int n = (int)(want / 4) * 4;
                            ScanChunkForTarget(tmp, n, target, pos, _hits);

                            pos += want;
                            BumpProgress(pos);
                        }
                    }

                    _hits.Flush();
                }, token).ConfigureAwait(true);

                FillListView(target);
                if (_hits.Count > 5000)
                    _status.Text = $"Done. {_hits.Count:N0} pointer(s); showing first 5 000. Narrow the range or use Memory View from a row.";
                else
                    _status.Text = $"Done. {_hits.Count:N0} pointer(s) found.";
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Pointer scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _status.Text = "Failed.";
            }
            finally
            {
                _btnScan.Enabled = true;
                _btnCancel.Enabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void FillListView(uint target)
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            const int max = 5000;
            int n = 0;
            foreach (var p in _hits.EnumerateActive())
            {
                if (n++ >= max) break;
                var it = new ListViewItem("0x" + p.ToString("X8")) { Tag = p };
                it.SubItems.Add("0x" + target.ToString("X8"));
                _lv.Items.Add(it);
            }
            _lv.EndUpdate();
        }
    }
}
