// =============================================================================
// XCE Atlas - MemoryViewForm - Cheat-Engine-styled Memory Viewer (PowerPC build)
// =============================================================================
//
// Two-pane layout:
//
//   [ File ] [ Edit ] [ Search ] [ View ] [ Debug ] [ Kernel tools ]
//   Resizable split: disassembly (PPC) above, hex/ASCII below.
//   Status bar shows selection range + raw bytes; Ctrl+[ / Ctrl+] navigate history.
//   [ Module+offset                                       ] [ Go ]
//   +------------------------------------------------------+
//   | Address              | Bytes        | Opcode    | Comment |
//   |  xboxkrnl.exe+1010   | 38 60 00 01  | li r3, 1  | 1       |
//   |  xboxkrnl.exe+1014   | 4E 80 00 20  | blr       |         |
//   |  ...                                                       |
//   +------------------------------------------------------+
//   | Protect:Read/Execute  Base=82001000 Size=1000 Module=...   |
//   +------------------------------------------------------+
//   | <HexBox - 16-byte rows, address + hex + ASCII>            |
//   +------------------------------------------------------+
//
// All memory I/O is dispatched through Task.Run so the UI stays responsive
// while reading from the console.  The hex view uses Be.Windows.Forms.HexBox
// with a custom <see cref="XboxByteProvider"/> that reads on demand.
// =============================================================================

using Be.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class MemoryViewForm : Form
    {
        // ---- theme constants ------------------------------------------------
        private static readonly Color BgDark    = Color.FromArgb(45, 45, 48);
        private static readonly Color BgPanel   = Color.FromArgb(30, 30, 30);
        private static readonly Color BgHeader  = Color.FromArgb(63, 63, 70);
        private static readonly Color FgText    = Color.WhiteSmoke;
        private static readonly Color FgDim     = Color.FromArgb(180, 180, 180);
        private static readonly Color AccentBlue = Color.FromArgb(0, 102, 204);
        private static readonly Color AsmOpcode  = Color.FromArgb(255, 92, 92);   // mnemonics
        private static readonly Color AsmBranch  = Color.FromArgb(255, 165, 0);   // branch labels
        private static readonly Color AsmBytes   = Color.FromArgb(160, 160, 160); // byte column
        private static readonly Color AsmComment = Color.FromArgb(120, 200, 120); // comment
        /// <summary>Cheat Engine-style: hex + ASCII body default (bright green).</summary>
        private static readonly Color HexDataGreen   = Color.FromArgb(0, 255, 80);
        /// <summary>Bytes that differ from the snapshot when the view was loaded / jumped.</summary>
        private static readonly Color HexEditedRed   = Color.FromArgb(255, 70, 70);

        // ---- state ----------------------------------------------------------
        private readonly XboxConsole _console;
        private readonly List<ModuleInfo> _modules = new List<ModuleInfo>();
        private readonly XboxByteProvider _hexProvider;

        private uint   _topAddress = 0x82000000;
        private uint   _selectedAddress = 0x82000000;
        private const int InstructionsPerPage = 32;

        // ---- controls -------------------------------------------------------
        private readonly MenuStrip   _menu       = new MenuStrip();
        private readonly ToolStrip   _addrBar    = new ToolStrip();
        private readonly ToolStripTextBox _addrBox = new ToolStripTextBox();
        private readonly ToolStripButton  _goBtn   = new ToolStripButton("Go");
        private readonly SplitContainer _split     = new SplitContainer();
        private readonly ListView    _asm        = new ListView();
        private readonly Label       _regionInfo = new Label();
        private readonly HexBox      _hex        = new HexBox();
        private readonly StatusStrip _status    = new StatusStrip();
        private readonly ToolStripStatusLabel _statusMain = new ToolStripStatusLabel();

        private readonly List<uint> _navHistory = new List<uint>();
        private int _navPos = -1;
        private ToolStripMenuItem _mBytes8, _mBytes16, _mBytes32, _mGroup4;

        // =====================================================================
        //  Construction
        // =====================================================================

        public MemoryViewForm(XboxConsole console, uint? navigateTo = null)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _hexProvider = new XboxByteProvider(_console);

            if (navigateTo.HasValue)
            {
                _topAddress = navigateTo.Value & ~3u;
                _selectedAddress = _topAddress;
            }

            Text = "Memory Viewer";
            ClientSize = new Size(900, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BgDark;
            ForeColor = FgText;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            BuildMenu();
            BuildAddressBar();
            BuildDisassemblyGrid();
            BuildRegionInfo();
            BuildHexPanel();
            BuildSplitAndStatus();

            // Dock order (bottom-up): fill consumes remaining space after tops.
            Controls.Add(_split);
            Controls.Add(_status);
            Controls.Add(_addrBar);
            Controls.Add(_menu);

            SetBytesPerLine(16);
            ApplyWordGroup(_mGroup4.Checked);

            Shown += async (_, __) => await InitialLoadAsync();
        }

        private void BuildSplitAndStatus()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.BackColor = BgDark;
            _split.Panel1.BackColor = BgDark;
            _split.Panel2.BackColor = BgDark;
            _split.SplitterWidth = 6;
            _split.SplitterDistance = 320;
            _split.Panel1MinSize = 100;
            _split.Panel2MinSize = 120;

            _asm.Dock = DockStyle.Top;
            _asm.Height = 280;
            _regionInfo.Dock = DockStyle.Top;

            _split.Panel1.Controls.Add(_asm);
            _split.Panel1.Controls.Add(_regionInfo);

            _hex.Dock = DockStyle.Fill;
            _split.Panel2.Controls.Add(_hex);

            _status.Dock = DockStyle.Bottom;
            _status.SizingGrip = true;
            _status.BackColor = BgHeader;
            _status.ForeColor = FgText;
            _status.Padding = new Padding(4, 2, 4, 2);
            _statusMain.Spring = true;
            _statusMain.TextAlign = ContentAlignment.MiddleLeft;
            _statusMain.Text = "Ready";
            _status.Items.Add(_statusMain);
        }

        // =====================================================================
        //  Menu + toolbar
        // =====================================================================

        private void BuildMenu()
        {
            _menu.Dock = DockStyle.Top;
            _menu.BackColor = BgHeader;
            _menu.ForeColor = FgText;
            _menu.Renderer  = new DarkMenuRenderer();

            ToolStripMenuItem File_ = TopMenu("File");
            File_.DropDownItems.Add(MenuItem("Close",  Keys.Alt | Keys.F4, (_, __) => Close()));

            ToolStripMenuItem Edit_ = TopMenu("Edit");
            Edit_.DropDownItems.Add(MenuItem("Copy hex bytes",       Keys.Control | Keys.C, (_, __) => CopyHexSelection()));
            Edit_.DropDownItems.Add(MenuItem("Copy address (sel.)", Keys.Control | Keys.Shift | Keys.C, (_, __) => CopyAddressFromSelection()));

            ToolStripMenuItem Search_ = TopMenu("Search");
            Search_.DropDownItems.Add(MenuItem("Goto Address...", Keys.Control | Keys.G, (_, __) => FocusGoto()));

            ToolStripMenuItem View_ = TopMenu("View");
            View_.DropDownItems.Add(MenuItem("Refresh", Keys.F5, async (_, __) => await ReloadAsync()));
            View_.DropDownItems.Add(new ToolStripSeparator());
            _mBytes8  = MenuCheck("8 bytes per line",  false, (_, __) => SetBytesPerLine(8));
            _mBytes16 = MenuCheck("16 bytes per line", true,  (_, __) => SetBytesPerLine(16));
            _mBytes32 = MenuCheck("32 bytes per line", false, (_, __) => SetBytesPerLine(32));
            View_.DropDownItems.Add(_mBytes8);
            View_.DropDownItems.Add(_mBytes16);
            View_.DropDownItems.Add(_mBytes32);
            View_.DropDownItems.Add(new ToolStripSeparator());
            _mGroup4 = MenuCheck("Group as words (4 bytes)", true, (_, __) => ApplyWordGroup(_mGroup4.Checked));
            View_.DropDownItems.Add(_mGroup4);
            View_.DropDownItems.Add(new ToolStripSeparator());
            View_.DropDownItems.Add(MenuItem("Back",    Keys.Control | Keys.Oem4,  async (_, __) => await NavBackAsync()));
            View_.DropDownItems.Add(MenuItem("Forward", Keys.Control | Keys.Oem6, async (_, __) => await NavForwardAsync()));
            View_.DropDownItems.Add(new ToolStripSeparator());
            View_.DropDownItems.Add(MenuItem("PPC to C++...", Keys.Control | Keys.Shift | Keys.D, (_, __) => ShowPpcToCpp()));

            ToolStripMenuItem Debug_ = TopMenu("Debug");
            Debug_.DropDownItems.Add(MenuItem("Set Breakpoint Here", Keys.F9,    (_, __) => SafeRun(() => _console.SetBreakpoint(_selectedAddress))));
            Debug_.DropDownItems.Add(MenuItem("Clear Breakpoint",    Keys.F10,   (_, __) => SafeRun(() => _console.ClearBreakpoint(_selectedAddress))));
            Debug_.DropDownItems.Add(MenuItem("Clear All",           Keys.None,  (_, __) => SafeRun(() => _console.ClearAllBreakpoints())));
            Debug_.DropDownItems.Add(new ToolStripSeparator());
            Debug_.DropDownItems.Add(MenuItem("Stop",  Keys.Control | Keys.F2, (_, __) => SafeRun(() => _console.Stop())));
            Debug_.DropDownItems.Add(MenuItem("Go",    Keys.F2,                 (_, __) => SafeRun(() => _console.Go())));

            ToolStripMenuItem Kernel_ = TopMenu("Kernel tools");
            Kernel_.DropDownItems.Add(MenuItem("Modules...",       Keys.None, (_, __) => ShowModulesList()));
            Kernel_.DropDownItems.Add(MenuItem("Threads...",       Keys.None, async (_, __) => await ShowThreadsListAsync()));
            Kernel_.DropDownItems.Add(MenuItem("Memory Regions...",Keys.None, async (_, __) => await ShowRegionsListAsync()));

            _menu.Items.Add(File_);
            _menu.Items.Add(Edit_);
            _menu.Items.Add(Search_);
            _menu.Items.Add(View_);
            _menu.Items.Add(Debug_);
            _menu.Items.Add(Kernel_);
        }

        private void BuildAddressBar()
        {
            _addrBar.Dock = DockStyle.Top;
            _addrBar.BackColor = BgDark;
            _addrBar.ForeColor = FgText;
            _addrBar.GripStyle = ToolStripGripStyle.Hidden;
            _addrBar.RenderMode = ToolStripRenderMode.System;
            _addrBar.Padding = new Padding(4, 2, 4, 2);

            _addrBox.Font = new Font("Consolas", 10F);
            _addrBox.BackColor = BgPanel;
            _addrBox.ForeColor = FgText;
            _addrBox.AutoSize = false;
            _addrBox.Width = 320;
            _addrBox.Text = FormatAddress(_topAddress);
            _addrBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    OnGoTo();
                }
            };

            _goBtn.ForeColor = FgText;
            _goBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _goBtn.Click += (_, __) => OnGoTo();

            _addrBar.Items.Add(new ToolStripLabel("Address: ") { ForeColor = FgDim });
            _addrBar.Items.Add(_addrBox);
            _addrBar.Items.Add(_goBtn);
        }

        // =====================================================================
        //  Disassembly grid
        // =====================================================================

        private void BuildDisassemblyGrid()
        {
            _asm.View = View.Details;
            _asm.Dock = DockStyle.Top;
            _asm.Height = 280;
            _asm.BackColor = BgPanel;
            _asm.ForeColor = FgText;
            _asm.Font = new Font("Consolas", 9.5F);
            _asm.FullRowSelect = true;
            _asm.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _asm.GridLines = false;
            _asm.OwnerDraw = true;
            _asm.MultiSelect = false;
            _asm.HideSelection = false;
            _asm.BorderStyle = BorderStyle.None;

            _asm.Columns.Add("Address", 220);
            _asm.Columns.Add("Bytes",   120);
            _asm.Columns.Add("Opcode",  260);
            _asm.Columns.Add("Comment", 200);

            _asm.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(BgHeader)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var fb = new SolidBrush(FgText))
                using (var pen = new Pen(Color.FromArgb(80, 80, 80)))
                {
                    e.Graphics.DrawString(e.Header.Text, _asm.Font, fb,
                                          new RectangleF(e.Bounds.X + 6, e.Bounds.Y + 3, e.Bounds.Width, e.Bounds.Height));
                    e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                }
            };

            _asm.DrawItem += (s, e) => { /* row painted in DrawSubItem */ };

            _asm.DrawSubItem += (s, e) =>
            {
                bool selected = e.Item.Selected;
                Color rowBg = selected ? AccentBlue : BgPanel;
                using (var b = new SolidBrush(rowBg)) e.Graphics.FillRectangle(b, e.Bounds);

                string text = e.SubItem.Text ?? string.Empty;
                Color fg;
                if (selected)
                {
                    fg = Color.White;
                }
                else
                {
                    switch (e.ColumnIndex)
                    {
                        case 0:  fg = FgText;     break;  // address (module+offset)
                        case 1:  fg = AsmBytes;   break;  // hex bytes
                        case 2:  fg = IsBranchRow(e.Item) ? AsmBranch : AsmOpcode; break;
                        case 3:  fg = AsmComment; break;
                        default: fg = FgText;     break;
                    }
                }
                TextRenderer.DrawText(e.Graphics, text, _asm.Font, e.Bounds, fg,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };

            _asm.SelectedIndexChanged += (s, e) =>
            {
                if (_asm.SelectedItems.Count == 0) return;
                if (_asm.SelectedItems[0].Tag is uint a)
                {
                    _selectedAddress = a;
                    HighlightHexAddress(a);
                }
            };

            _asm.MouseWheel += async (s, e) => await OnAsmScroll(e.Delta);
            _asm.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.PageDown) { e.Handled = true; await OnAsmScroll(-InstructionsPerPage * 120); }
                else if (e.KeyCode == Keys.PageUp) { e.Handled = true; await OnAsmScroll(InstructionsPerPage * 120); }
                else if (e.KeyCode == Keys.F5)    { e.Handled = true; await ReloadAsync(); }
                else if (e.KeyCode == Keys.Down && _asm.SelectedItems.Count > 0)
                {
                    int i = _asm.SelectedItems[0].Index;
                    if (i + 1 < _asm.Items.Count)
                    {
                        _asm.Items[i + 1].Selected = true;
                        _asm.Items[i + 1].Focused = true;
                        _asm.EnsureVisible(i + 1);
                        e.Handled = true;
                    }
                }
                else if (e.KeyCode == Keys.Up && _asm.SelectedItems.Count > 0)
                {
                    int i = _asm.SelectedItems[0].Index;
                    if (i > 0)
                    {
                        _asm.Items[i - 1].Selected = true;
                        _asm.Items[i - 1].Focused = true;
                        _asm.EnsureVisible(i - 1);
                        e.Handled = true;
                    }
                }
            };

            _asm.MouseDoubleClick += async (s, e) =>
            {
                var hit = _asm.HitTest(e.Location);
                if (hit.Item?.Tag is uint a && IsBranchRow(hit.Item))
                {
                    // Follow branch target if we have one tagged on the item.
                    if (hit.Item.SubItems[2].Tag is uint target)
                    {
                        _selectedAddress = target;
                        await JumpToAsync(target);
                    }
                }
            };
        }

        private static bool IsBranchRow(ListViewItem it)
        {
            string op = it?.SubItems[2]?.Text ?? string.Empty;
            return op.StartsWith("b ", StringComparison.Ordinal)
                || op.StartsWith("bl ", StringComparison.Ordinal)
                || op.StartsWith("bne", StringComparison.Ordinal)
                || op.StartsWith("beq", StringComparison.Ordinal)
                || op.StartsWith("blt", StringComparison.Ordinal)
                || op.StartsWith("bgt", StringComparison.Ordinal)
                || op.StartsWith("ble", StringComparison.Ordinal)
                || op.StartsWith("bge", StringComparison.Ordinal)
                || op.StartsWith("bdnz", StringComparison.Ordinal)
                || op.StartsWith("bdz", StringComparison.Ordinal)
                || op == "blr"
                || op == "bctr"
                || op == "bctrl";
        }

        // =====================================================================
        //  Region info + hex panel
        // =====================================================================

        private void BuildRegionInfo()
        {
            _regionInfo.Dock = DockStyle.Top;
            _regionInfo.Height = 22;
            _regionInfo.BackColor = BgHeader;
            _regionInfo.ForeColor = FgText;
            _regionInfo.Font = new Font("Consolas", 9F);
            _regionInfo.TextAlign = ContentAlignment.MiddleLeft;
            _regionInfo.Padding = new Padding(6, 0, 0, 0);
            _regionInfo.Text = "Protect:?  AllocationBase=?  Base=?  Size=?  Module=?";
        }

        private void BuildHexPanel()
        {
            _hex.Dock = DockStyle.Fill;
            _hex.BackColor = BgPanel;
            _hex.ForeColor = HexDataGreen;
            _hex.InfoForeColor = FgDim;
            _hex.SelectionBackColor = AccentBlue;
            _hex.SelectionForeColor = Color.White;
            _hex.ShadowSelectionColor = Color.FromArgb(120, AccentBlue);
            _hex.Font = new Font("Consolas", 10F);
            _hex.BorderStyle = BorderStyle.None;
            _hex.LineInfoVisible = true;
            _hex.ColumnInfoVisible = true;
            _hex.StringViewVisible = true;
            _hex.UseFixedBytesPerLine = true;
            _hex.BytesPerLine = 16;
            _hex.VScrollBarVisible = true;
            _hex.GroupSize = 1;
            _hex.GroupSeparatorVisible = false;
            _hex.ReadOnly = false;
            _hex.ByteProvider = _hexProvider;
            _hex.SelectionStartChanged += (_, __) => UpdateHexStatus();
            _hex.SelectionLengthChanged += (_, __) => UpdateHexStatus();
            _hex.GotFocus += (_, __) => UpdateHexStatus();
            _hex.Paint += OnHexPostPaint;
        }

        /// <summary>
        /// Be.HexBox 1.6.1 paints all body text with <see cref="Control.ForeColor"/> only.
        /// After the control paints, redraw edited bytes in red (Cheat Engine convention).
        /// </summary>
        private void OnHexPostPaint(object sender, PaintEventArgs e)
        {
            if (sender != _hex || _hexProvider == null) return;
            try
            {
                HexPostPaintSupport.PaintEditedBytes(_hex, e.Graphics, _hexProvider, HexEditedRed, BgPanel);
            }
            catch
            {
                /* HexBox internals changed — skip overlay rather than break the viewer */
            }
        }

        /// <summary>Reflection bridge to private HexBox layout helpers (version-locked to Be.HexBox 1.6.1).</summary>
        private static class HexPostPaintSupport
        {
            private const BindingFlags Bf = BindingFlags.Instance | BindingFlags.NonPublic;
            private static readonly Type TH = typeof(HexBox);
            private static readonly FieldInfo FStartByte = TH.GetField("_startByte", Bf);
            private static readonly FieldInfo FEndByte = TH.GetField("_endByte", Bf);
            private static readonly FieldInfo FIHexMaxHBytes = TH.GetField("_iHexMaxHBytes", Bf);
            private static readonly FieldInfo FCharSize = TH.GetField("_charSize", Bf);
            private static readonly FieldInfo FStringFormat = TH.GetField("_stringFormat", Bf);
            private static readonly MethodInfo MGetGridBytePoint = TH.GetMethod("GetGridBytePoint", Bf, null, new[] { typeof(int) }, null);
            private static readonly MethodInfo MGetBytePointF = TH.GetMethods(Bf).Single(m => m.Name == "GetBytePointF" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Point));
            private static readonly MethodInfo MGetByteStringPointF = TH.GetMethod("GetByteStringPointF", Bf, null, new[] { typeof(Point) }, null);

            private static bool Ready =>
                FStartByte != null && FEndByte != null && FIHexMaxHBytes != null && FCharSize != null &&
                FStringFormat != null && MGetGridBytePoint != null && MGetBytePointF != null && MGetByteStringPointF != null;

            public static void PaintEditedBytes(HexBox hex, Graphics g, XboxByteProvider prov, Color editedFore, Color eraseBack)
            {
                if (!Ready || hex.ByteProvider == null || !prov.HasEditedBytes()) return;

                long startByte = (long)FStartByte.GetValue(hex);
                long endByte = (long)FEndByte.GetValue(hex);
                int iHexMaxHBytes = (int)FIHexMaxHBytes.GetValue(hex);
                var charSize = (SizeF)FCharSize.GetValue(hex);
                var fmt = (StringFormat)FStringFormat.GetValue(hex);
                long len = hex.ByteProvider.Length;
                if (len <= 0) return;

                long internEnd = Math.Min(len - 1, endByte + iHexMaxHBytes);
                long selStart = hex.SelectionStart;
                long selLen = hex.SelectionLength;
                bool hasSel = selLen != 0;

                using (var erase = new SolidBrush(eraseBack))
                using (var pen = new SolidBrush(editedFore))
                {
                    var sm = g.SmoothingMode;
                    var th = g.TextRenderingHint;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    int counter = -1;
                    for (long i = startByte; i < internEnd + 1; i++)
                    {
                        counter++;
                        if (!prov.IsByteEdited(i)) continue;
                        if (hasSel && i >= selStart && i <= selStart + selLen - 1) continue;

                        var grid = (Point)MGetGridBytePoint.Invoke(hex, new object[] { counter });
                        byte b = hex.ByteProvider.ReadByte(i);
                        string hx = b.ToString(hex.HexCasing == HexCasing.Upper ? "X2" : "x2", CultureInfo.InvariantCulture);
                        string asc = new string(hex.ByteCharConverter.ToChar(b), 1);

                        PointF hexPt = (PointF)MGetBytePointF.Invoke(hex, new object[] { grid });
                        float w = charSize.Width, h = charSize.Height;
                        g.FillRectangle(erase, hexPt.X, hexPt.Y, w * 2, h);
                        g.DrawString(hx.Substring(0, 1), hex.Font, pen, hexPt, fmt);
                        hexPt = new PointF(hexPt.X + w, hexPt.Y);
                        g.DrawString(hx.Substring(1, 1), hex.Font, pen, hexPt, fmt);

                        PointF strPt = (PointF)MGetByteStringPointF.Invoke(hex, new object[] { grid });
                        g.FillRectangle(erase, strPt.X, strPt.Y, w, h);
                        g.DrawString(asc, hex.Font, pen, strPt, fmt);
                    }

                    g.SmoothingMode = sm;
                    g.TextRenderingHint = th;
                }
            }
        }

        // =====================================================================
        //  Memory I/O
        // =====================================================================

        private async Task InitialLoadAsync()
        {
            await Task.Run(() =>
            {
                try { _modules.AddRange(_console.GetModules() ?? Enumerable.Empty<ModuleInfo>()); } catch { /* offline */ }
            });
            await JumpToAsync(_topAddress);
        }

        private async Task ReloadAsync() => await JumpToAsync(_topAddress);

        private void OnGoTo()
        {
            if (!TryResolveAddress(_addrBox.Text, out uint a))
            {
                MessageBox.Show(this, "Address must be hex (e.g. 0x82000000) or 'module.exe+offset'.",
                                "Memory Viewer", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            _ = JumpToAsync(a);
        }

        private async Task JumpToAsync(uint addr, bool recordHistory = true)
        {
            _topAddress = addr & ~3u; // align to instruction boundary
            _selectedAddress = _topAddress;
            _addrBox.Text = FormatAddress(_topAddress);

            byte[] bytes = Array.Empty<byte>();
            try
            {
                bytes = await Task.Run(() => _console.GetMemory(_topAddress, (uint)(InstructionsPerPage * 4)));
            }
            catch (Exception ex)
            {
                _regionInfo.Text = "Read failed: " + ex.Message;
                _asm.Items.Clear();
                return;
            }

            if (recordHistory)
            {
                while (_navHistory.Count - 1 > _navPos)
                    _navHistory.RemoveAt(_navHistory.Count - 1);
                if (_navPos < 0 || _navHistory[_navPos] != _topAddress)
                {
                    _navHistory.Add(_topAddress);
                    _navPos = _navHistory.Count - 1;
                }
            }

            RenderDisassembly(_topAddress, bytes);
            UpdateRegionInfo(_topAddress);

            _hexProvider.SetWindow(_topAddress, 0x10000);
            _hex.LineInfoOffset = (long)_hexProvider.WindowStart;
            HighlightHexAddress(_topAddress);
        }

        private async Task OnAsmScroll(int delta)
        {
            int rowsBy4 = delta > 0 ? -4 : 4;        // wheel up = earlier addresses
            uint next = unchecked((uint)((int)_topAddress + rowsBy4 * 4));
            await JumpToAsync(next);
        }

        // =====================================================================
        //  Disassembly rendering
        // =====================================================================

        private void RenderDisassembly(uint start, byte[] bytes)
        {
            _asm.BeginUpdate();
            _asm.Items.Clear();

            int rows = Math.Min(InstructionsPerPage, bytes.Length / 4);
            for (int i = 0; i < rows; i++)
            {
                uint addr = start + (uint)(i * 4);
                uint word = (uint)((bytes[i*4]     << 24)
                                 | (bytes[i*4 + 1] << 16)
                                 | (bytes[i*4 + 2] <<  8)
                                 |  bytes[i*4 + 3]);

                var d = PpcDisassembler.Decode(word, addr);

                var row = new ListViewItem(FormatAddress(addr)) { Tag = addr };
                row.UseItemStyleForSubItems = false;

                // Bytes
                row.SubItems.Add($"{bytes[i*4]:X2} {bytes[i*4+1]:X2} {bytes[i*4+2]:X2} {bytes[i*4+3]:X2}");

                // Opcode (mnemonic + operands)
                var opSub = new ListViewItem.ListViewSubItem(row, d.FullText);
                if (d.BranchTarget.HasValue) opSub.Tag = d.BranchTarget.Value;
                row.SubItems.Add(opSub);

                // Comment column
                string comment = BuildComment(d);
                row.SubItems.Add(comment);

                _asm.Items.Add(row);
            }

            if (_asm.Items.Count > 0)
            {
                _asm.Items[0].Selected = true;
                _asm.EnsureVisible(0);
            }
            _asm.EndUpdate();
        }

        private string BuildComment(PpcInstruction d)
        {
            // CE shows the decimal interpretation of immediates / displacements.
            if (d.BranchTarget.HasValue)
                return FormatAddress(d.BranchTarget.Value);
            if (d.Immediate.HasValue)
                return d.Immediate.Value.ToString(CultureInfo.InvariantCulture);
            return string.Empty;
        }

        // =====================================================================
        //  Module + region resolution
        // =====================================================================

        private bool TryResolveAddress(string text, out uint addr)
        {
            addr = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();

            int plus = text.IndexOf('+');
            if (plus > 0)
            {
                string name = text.Substring(0, plus).Trim();
                string off  = text.Substring(plus + 1).Trim();
                var m = _modules.FirstOrDefault(x => string.Equals(x?.Name, name, StringComparison.OrdinalIgnoreCase));
                if (m == null) return false;
                if (!TryParseHex(off, out uint o)) return false;
                addr = m.BaseAddress + o;
                return true;
            }
            return TryParseHex(text, out addr);
        }

        private static bool TryParseHex(string s, out uint v)
        {
            v = 0; if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim(); if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }

        private string FormatAddress(uint addr)
        {
            var m = _modules.FirstOrDefault(x => x != null && addr >= x.BaseAddress && addr < x.BaseAddress + x.Size);
            if (m != null) return $"{m.Name}+{(addr - m.BaseAddress):X}";
            return addr.ToString("X8");
        }

        private void UpdateRegionInfo(uint addr)
        {
            var m = _modules.FirstOrDefault(x => x != null && addr >= x.BaseAddress && addr < x.BaseAddress + x.Size);
            if (m == null)
            {
                _regionInfo.Text = $"Protect:?  AllocationBase=?  Base={addr:X8}  Size=?  Module=?";
                return;
            }

            // Look up the containing section for accurate protect flags.
            string protect = "?";
            uint sectionBase = m.BaseAddress;
            uint sectionSize = m.Size;
            try
            {
                var sections = _console.GetModuleSections(m.Name);
                if (sections != null)
                {
                    foreach (var s in sections)
                    {
                        if (addr >= s.Base && addr < s.Base + s.Size)
                        {
                            sectionBase = s.Base;
                            sectionSize = s.Size;
                            protect = FormatSectionFlags(s.Flags);
                            break;
                        }
                    }
                }
            }
            catch { /* offline / not supported */ }

            _regionInfo.Text = $"Protect:{protect}  AllocationBase={m.BaseAddress:X8}  Base={sectionBase:X8}  " +
                               $"Size={sectionSize:X}  Module={m.Name}";
        }

        private static string FormatSectionFlags(uint flags)
        {
            // Section flags from xbdm 'modsections' use a small bitmap:
            //   bit 0  loaded
            //   bit 1  readable
            //   bit 2  writable
            //   bit 3  executable
            //   bit 4  uninitialised
            //   bit 5  paged
            // Match the CE convention "Read/Write" / "Read/Execute" / "RWX" etc.
            var parts = new List<string>(3);
            if ((flags & 0x2) != 0) parts.Add("Read");
            if ((flags & 0x4) != 0) parts.Add("Write");
            if ((flags & 0x8) != 0) parts.Add("Execute");
            return parts.Count == 0 ? "?" : string.Join("/", parts);
        }

        private void HighlightHexAddress(uint addr)
        {
            long offset = unchecked((long)(addr - _hexProvider.WindowStart));
            if (offset < 0 || offset >= _hexProvider.Length) return;
            _hex.Select(offset, 4);
            _hex.ScrollByteIntoView();
            UpdateHexStatus();
        }

        // =====================================================================
        //  Kernel-tools dialogs (minimal modals)
        // =====================================================================

        private void ShowModulesList()
        {
            using (var dlg = new SimpleListPicker("Modules",
                _modules.Select(m => $"{m.BaseAddress:X8}  {m.Size,8:X}  {m.Name}").ToArray()))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.Selected))
                {
                    // Address column is first 8 hex chars.
                    if (uint.TryParse(dlg.Selected.Substring(0, 8), NumberStyles.HexNumber,
                                      CultureInfo.InvariantCulture, out uint a))
                        _ = JumpToAsync(a);
                }
            }
        }

        private async Task ShowThreadsListAsync()
        {
            string[] rows = Array.Empty<string>();
            try
            {
                rows = await Task.Run(() =>
                {
                    // Prefer the rich "threadex" listing; fall back to bare thread ids
                    // if the console returns nothing (older xbdm builds).
                    var ex = _console.GetThreadListingEx();
                    if (ex != null && ex.Length > 0) return ex;
                    var ids = _console.GetThreadIds();
                    return ids?.Select(id => $"thread=0x{id:X}").ToArray() ?? Array.Empty<string>();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Threads", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            using (var dlg = new SimpleListPicker("Threads", rows)) dlg.ShowDialog(this);
        }

        private async Task ShowRegionsListAsync()
        {
            var rows = new List<string>();
            try
            {
                rows = await Task.Run(() =>
                {
                    var list = new List<string>();
                    foreach (var m in _modules)
                    {
                        try
                        {
                            var secs = _console.GetModuleSections(m.Name);
                            if (secs == null) continue;
                            foreach (var s in secs)
                                list.Add($"{s.Base:X8}  {s.Size,8:X}  {FormatSectionFlags(s.Flags),-12}  {m.Name}!{s.Name}");
                        }
                        catch { /* not supported for this module */ }
                    }
                    return list;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Memory Regions", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            using (var dlg = new SimpleListPicker("Memory Regions", rows.ToArray()))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.Selected) &&
                    uint.TryParse(dlg.Selected.Substring(0, 8), NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture, out uint a))
                    _ = JumpToAsync(a);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static ToolStripMenuItem TopMenu(string text)
            => new ToolStripMenuItem(text) { ForeColor = FgText };

        private static ToolStripMenuItem MenuItem(string text, Keys shortcut, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text) { ForeColor = FgText };
            if (shortcut != Keys.None) it.ShortcutKeys = shortcut;
            it.Click += onClick;
            return it;
        }

        private static ToolStripMenuItem MenuCheck(string text, bool startChecked, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text)
            {
                ForeColor = FgText,
                CheckOnClick = true,
                Checked = startChecked
            };
            it.Click += onClick;
            return it;
        }

        private void FocusGoto()
        {
            _addrBar.Focus();
            _addrBox.Focus();
            _addrBox.SelectAll();
        }

        private void SetBytesPerLine(int n)
        {
            _hex.BytesPerLine = n;
            _hex.UseFixedBytesPerLine = true;
            if (_mBytes8 != null) _mBytes8.Checked = n == 8;
            if (_mBytes16 != null) _mBytes16.Checked = n == 16;
            if (_mBytes32 != null) _mBytes32.Checked = n == 32;
        }

        private void ApplyWordGroup(bool on)
        {
            _hex.GroupSeparatorVisible = on;
            _hex.GroupSize = on ? 4 : 1;
        }

        private async Task NavBackAsync()
        {
            if (_navPos <= 0) return;
            _navPos--;
            await JumpToAsync(_navHistory[_navPos], recordHistory: false);
        }

        private async Task NavForwardAsync()
        {
            if (_navPos < 0 || _navPos >= _navHistory.Count - 1) return;
            _navPos++;
            await JumpToAsync(_navHistory[_navPos], recordHistory: false);
        }

        private uint? GetHexCaretAddress()
        {
            long i = _hex.SelectionStart;
            if (i < 0 || i >= _hexProvider.Length) return null;
            return unchecked(_hexProvider.WindowStart + (uint)i);
        }

        private void UpdateHexStatus()
        {
            if (_statusMain == null) return;
            uint? abs = GetHexCaretAddress();
            if (!abs.HasValue)
            {
                _statusMain.Text = "Hex: (out of window)";
                return;
            }

            int len = (int)Math.Max(1, _hex.SelectionLength);
            len = (int)Math.Min(len, 32);
            var sb = new StringBuilder();
            sb.Append("Sel: ").Append(FormatAddress(abs.Value));
            if (_hex.SelectionLength > 1)
                sb.Append("–").Append(FormatAddress(unchecked(abs.Value + (uint)_hex.SelectionLength - 1)));

            sb.Append("   ");
            for (int k = 0; k < len; k++)
            {
                long idx = _hex.SelectionStart + k;
                if (idx < 0 || idx >= _hexProvider.Length) break;
                if (k > 0) sb.Append(' ');
                sb.Append(_hexProvider.ReadByte(idx).ToString("X2"));
            }
            if (_hex.SelectionLength > len) sb.Append(" …");
            _statusMain.Text = sb.ToString();
        }

        private void CopyHexSelection()
        {
            if (_asm.Focused && _asm.SelectedItems.Count > 0)
            {
                Clipboard.SetText(_asm.SelectedItems[0].SubItems[1].Text ?? string.Empty);
                _statusMain.Text = "Copied instruction bytes";
                return;
            }

            int len = (int)Math.Max(1, _hex.SelectionLength);
            len = (int)Math.Min(len, 4096);
            long start = _hex.SelectionStart;
            if (start < 0 || start >= _hexProvider.Length) return;

            var sb = new StringBuilder(len * 3);
            for (int k = 0; k < len; k++)
            {
                long idx = start + k;
                if (idx >= _hexProvider.Length) break;
                if (k > 0) sb.Append(' ');
                sb.Append(_hexProvider.ReadByte(idx).ToString("X2"));
            }
            Clipboard.SetText(sb.ToString());
            _statusMain.Text = $"Copied {len} byte(s) as hex";
        }

        private void CopyAddressFromSelection()
        {
            uint a = GetHexCaretAddress() ?? _selectedAddress;
            Clipboard.SetText("0x" + a.ToString("X8"));
            _statusMain.Text = "Copied " + FormatAddress(a);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Oem4))  { _ = NavBackAsync(); return true; }
            if (keyData == (Keys.Control | Keys.Oem6)) { _ = NavForwardAsync(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowPpcToCpp()
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                uint addr = _topAddress & ~3u;
                uint len = (uint)(InstructionsPerPage * 4);
                byte[] bytes = _console.GetMemory(addr, len);
                string listing = PpcToCppExport.BuildListing(bytes, addr);
                string fn = "sub_" + addr.ToString("X8", CultureInfo.InvariantCulture);
                string cpp = PpcToCpp.TranslateBytes(bytes, addr, fn);
                using (var dlg = new PpcToCppForm(_console, addr, len, listing, cpp, "PPC to C++ — " + FormatAddress(addr), fn))
                    dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SafeRun(Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Memory Viewer", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }
    }

    // =========================================================================
    //  XboxByteProvider - on-demand byte feed for the HexBox
    // =========================================================================

    internal sealed class XboxByteProvider : IByteProvider
    {
        private readonly XboxConsole _console;
        private byte[] _data = Array.Empty<byte>();
        private byte[] _original = Array.Empty<byte>();
        private bool[] _dirty = Array.Empty<bool>();
        private int _dirtyCount;
        public uint WindowStart { get; private set; }

        public XboxByteProvider(XboxConsole console) { _console = console; }

        public void SetWindow(uint start, uint size)
        {
            WindowStart = start;
            try { _data = _console.GetMemory(start, size); }
            catch { _data = Array.Empty<byte>(); }
            _original = _data.Length == 0 ? Array.Empty<byte>() : (byte[])_data.Clone();
            _dirty = _data.Length == 0 ? Array.Empty<bool>() : new bool[_data.Length];
            _dirtyCount = 0;
            LengthChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public byte ReadByte(long index) => index >= 0 && index < _data.Length ? _data[index] : (byte)0;

        public void WriteByte(long index, byte value)
        {
            if (index < 0 || index >= _data.Length) return;
            _data[index] = value;
            RefreshDirtyBit((int)index);
            uint addr = (uint)(WindowStart + index);
            try { _console.WriteByte(addr, value); }
            catch { /* connection lost; UI keeps the change locally */ }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshDirtyBit(int i)
        {
            if (_original.Length == 0 || i < 0 || i >= _original.Length || i >= _dirty.Length) return;
            bool now = _data[i] != _original[i];
            if (_dirty[i] == now) return;
            _dirty[i] = now;
            if (now) _dirtyCount++;
            else _dirtyCount--;
        }

        public bool IsByteEdited(long index)
            => index >= 0 && index < _dirty.Length && _dirty[index];

        public bool HasEditedBytes() => _dirtyCount > 0;

        public void InsertBytes(long index, byte[] bs) { /* fixed-window: no-op */ }
        public void DeleteBytes(long index, long length) { /* fixed-window: no-op */ }

        public long Length => _data.Length;

        public event EventHandler LengthChanged;
        public event EventHandler Changed;

        public bool HasChanges() => HasEditedBytes();
        public void ApplyChanges() { /* writes are pushed eagerly in WriteByte */ }
        public bool SupportsInsertBytes() => false;
        public bool SupportsDeleteBytes() => false;
        public bool SupportsWriteByte()   => true;
    }

    // =========================================================================
    //  Custom renderers
    // =========================================================================

    /// <summary>Flat dark renderer for <see cref="MenuStrip"/> + dropdowns.</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { ToolStripManager.VisualStylesEnabled = false; }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || ((e.Item as ToolStripMenuItem)?.DropDown?.Visible ?? false))
            {
                using (var b = new SolidBrush(Color.FromArgb(0, 102, 204)))
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(63, 63, 70)))
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.WhiteSmoke;
            base.OnRenderItemText(e);
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected           => Color.FromArgb(0, 102, 204);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0, 102, 204);
        public override Color MenuItemSelectedGradientEnd   => Color.FromArgb(0, 102, 204);
        public override Color MenuStripGradientBegin     => Color.FromArgb(63, 63, 70);
        public override Color MenuStripGradientEnd       => Color.FromArgb(63, 63, 70);
        public override Color ToolStripDropDownBackground=> Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientBegin   => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientMiddle  => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientEnd     => Color.FromArgb(45, 45, 48);
        public override Color SeparatorDark              => Color.FromArgb(80, 80, 80);
        public override Color MenuBorder                 => Color.FromArgb(80, 80, 80);
        public override Color MenuItemBorder             => Color.FromArgb(0, 102, 204);
    }

    // =========================================================================
    //  Minimal modal "pick a row" dialog used by Kernel-tools menu items.
    // =========================================================================

    internal sealed class SimpleListPicker : Form
    {
        private readonly ListBox _list = new ListBox();
        public string Selected { get; private set; }

        public SimpleListPicker(string title, string[] rows)
        {
            Text = title;
            ClientSize = new Size(620, 380);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Consolas", 9F);

            _list.Dock = DockStyle.Fill;
            _list.BackColor = Color.FromArgb(30, 30, 30);
            _list.ForeColor = Color.WhiteSmoke;
            _list.BorderStyle = BorderStyle.None;
            _list.Items.AddRange(rows ?? Array.Empty<string>());
            _list.DoubleClick += (_, __) =>
            {
                if (_list.SelectedItem != null) { Selected = _list.SelectedItem.ToString(); DialogResult = DialogResult.OK; Close(); }
            };

            var ok = new Button { Text = "OK", Dock = DockStyle.Right, Width = 100,
                                   BackColor = Color.FromArgb(63, 63, 70), ForeColor = Color.WhiteSmoke,
                                   FlatStyle = FlatStyle.Flat };
            ok.Click += (_, __) =>
            {
                if (_list.SelectedItem != null) Selected = _list.SelectedItem.ToString();
                DialogResult = DialogResult.OK; Close();
            };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(45, 45, 48) };
            bottom.Controls.Add(ok);
            Controls.Add(_list);
            Controls.Add(bottom);
        }
    }
}
