// =============================================================================
//  Form1.cs - XCE Atlas host (Cheat-Engine styled) wired to XDCKIT
// =============================================================================
//   Sections:
//     [1]  Construction + lifecycle      (XboxConsole instance, event wiring)
//     [2]  UI helpers                    (status, error, run-off-thread)
//     [3]  Connect / Disconnect / Discovery
//     [4]  Reboot / Shutdown / Stop / Go / Screenshot / Notify / About
//     [5]  Scan engine                   (First Scan, Next Scan, Undo Scan)
//     [6]  Saved-address table           (Add manually, Edit, Delete, Freeze)
//     [7]  Memory View dialog            (typed read/write)
//     [8]  Breakpoints dialog
//     [9]  Memory dump
//
//   All XDCKIT types live in the global namespace — no `using XDCKIT;` needed.
// =============================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    public partial class Form1 : Form
    {
        // -----------------------------------------------------------------
        // [1] Construction + lifecycle
        // -----------------------------------------------------------------

        private XboxConsole _console;
        private SynchronizationContext _ui;

        // Currently "attached" module (picked in the Process List dialog).
        // Shown in the toolbar's process label - mirrors Cheat Engine's
        // "00000C30-XCEAtlas.exe" display so the user can always see what
        // they're operating on.
        private string _selectedModuleName;
        private uint   _selectedModuleBase;
        private uint   _selectedModuleSize;

        // Scan state ------------------------------------------------------
        private DumpSnapshot _dumpInitial;
        private DumpSnapshot _dumpPrevious;
        private DumpSnapshot _dumpCurrent;
        private uint   _dumpBase;
        private uint   _dumpSize;
        private ScanHitPageStore _scanHits;
        /// <summary>False until the first scan pass has finished writing the hit list (RAM walk of dump vs paging reads).</summary>
        private bool _scanHitsOnDisk;
        /// <summary>Active dump's cancel source — Esc, Stop menu, or window close all flip this.</summary>
        private CancellationTokenSource _dumpCts;
        private readonly byte[] _chunkFlip = new byte[8];
        private int _incrementalUiPct = -1;
        private DateTime _incrementalUiAt = DateTime.MinValue;

        private const ulong ScanRangeHardCapBytes = 0x20000000ul;
        private const ulong ScanRangeConfirmBytes = 256ul * 1024 * 1024;
        private ulong _progressByteTotal;
        private int _progressLastStatusPct = -1;

        /// <summary>Cheat Engine-style context menu for the saved-address list.</summary>
        private ContextMenuStrip _ctxSavedAddresses;

        /// <summary>Polls the console and refreshes saved-address Value cells when memory (or hex display mode) changes.</summary>
        private readonly System.Windows.Forms.Timer _savedAddrValueTimer = new System.Windows.Forms.Timer { Interval = 200 };

        private ControllerForm _controllerForm;
        private ScreenshotStreamForm _liveViewForm;
        private MemoryWatchForm _memoryWatchForm;
        private QuickFindForm _quickFindForm;

        public Form1()
        {
            InitializeComponent();
            if (IsDesignTime)
                return;

            _console = new XboxConsole();
            _scanHits = new ScanHitPageStore();
            _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            try
            {
                _console.Client.CommandSent      += OnCommandSent;
                _console.Client.ResponseReceived += OnResponseReceived;
            }
            catch { /* events optional */ }

            UpdateConnectionUiState();
            OnRegionChanged(null, EventArgs.Empty);
            Shown += OnFormShownFitLayout;
            splitMain.SplitterMoved += OnSplitMainLayoutChanged;
            splitMain.Panel1.Resize += (_, __) => FitResultsColumns();
            WireSavedAddressTableUi();
            _savedAddrValueTimer.Tick += (_, __) => TickRefreshSavedAddressValues();
            hexCheck.CheckedChanged += (_, __) => TickRefreshSavedAddressValues();
            ApplyAtlasChrome();
        }

        private static bool IsDesignTime =>
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        private void OnFormShownFitLayout(object sender, EventArgs e)
        {
            FitResultsColumns();
            OnMidBarResize(null, EventArgs.Empty);
        }

        private void OnSplitMainLayoutChanged(object sender, EventArgs e)
        {
            FitResultsColumns();
            OnMidBarResize(null, EventArgs.Empty);
        }

        /// <summary>Second-pass styling after designer init (inputs, groups, hover states).</summary>
        private void ApplyAtlasChrome()
        {
            AtlasTheme.StyleInput(scanValueBox);
            AtlasTheme.StyleInput(scanValueBox2);
            AtlasTheme.StyleInput(startBox);
            AtlasTheme.StyleInput(stopBox);
            AtlasTheme.StyleInput(alignmentBox);
            AtlasTheme.StyleCombo(scanTypeCombo);
            AtlasTheme.StyleCombo(valueTypeCombo);
            AtlasTheme.StyleCombo(regionCombo);
            AtlasTheme.StyleCheck(hexCheck);
            AtlasTheme.StyleCheck(notCheck);
            AtlasTheme.StyleCheck(luaCheck);
            AtlasTheme.StyleCheck(cbWritable);
            AtlasTheme.StyleCheck(cbExecutable);
            AtlasTheme.StyleCheck(cbCopyOnWrite);
            AtlasTheme.StyleCheck(cbActiveOnly);
            AtlasTheme.StyleCheck(cbFastScan);
            AtlasTheme.StyleCheck(cbReDump);
            AtlasTheme.StyleCheck(cbPauseScan);
            AtlasTheme.StyleCheck(cbUnrandom);
            AtlasTheme.StyleCheck(cbSpeedhack);
            AtlasTheme.StyleGroup(grpMemoryScan);
            AtlasTheme.StyleGroup(grpMemOptions);
            AtlasTheme.StyleListView(resultsListView);
            AtlasTheme.StyleListView(savedListView);
            AtlasTheme.StyleFlatButton(btnMemoryView);
            AtlasTheme.StyleFlatButton(btnNextScan);
            AtlasTheme.StyleFlatButton(btnUndoScan);
            AtlasTheme.StyleFlatButton(btnFirstScan, primary: true);
            AtlasTheme.StyleFlatButton(btnAddAddress, primary: true);
        }

        private void OnMidBarPaint(object sender, PaintEventArgs e)
            => AtlasTheme.PaintAccentBar(e, midBar.ClientRectangle, 2);

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsDesignTime) return;
            try { _dumpCts?.Cancel();    } catch { /* ignore */ }
            try { _dumpCts?.Dispose();   } catch { /* ignore */ }
            DisposeDumpSnapshots();
            try { _console?.Disconnect(); } catch { /* ignore */ }
            try { _console?.Dispose();    } catch { /* ignore */ }
            try { _scanHits?.Dispose();   } catch { /* ignore */ }
            try { _ctxSavedAddresses?.Dispose(); } catch { /* ignore */ }
            try { _savedAddrValueTimer.Stop(); _savedAddrValueTimer.Dispose(); } catch { /* ignore */ }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _dumpCts != null && !_dumpCts.IsCancellationRequested)
            {
                CancelActiveDump("Cancelled (Esc).");
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnExitClicked(object sender, EventArgs e) => Close();

        // -----------------------------------------------------------------
        // [2] UI helpers - status / errors / off-thread runner
        // -----------------------------------------------------------------

        /// <summary>
        /// Refresh menu / toolbar enabled state and the process label.  No
        /// network round-trips happen here.
        /// </summary>
        private void UpdateConnectionUiState()
        {
            bool on = _console != null && _console.Connected;

            menuFileOpenProcess.Enabled = !on;
            menuFileDisconnect.Enabled  = on;
            menuLiveProfiles.Enabled    = on;
            menuLiveQuickSignIn.Enabled = on;
            menuLiveFriends.Enabled     = on;
            menuLiveParty.Enabled       = on;
            menuLiveGuide.Enabled       = on;
            menuD3DScreenshot.Enabled   = on;
            menuD3DLiveView.Enabled     = on;
            menuD3DNotify.Enabled       = on;
            menuD3DRebootCold.Enabled   = on;
            menuD3DRebootWarm.Enabled   = on;
            menuD3DShutdown.Enabled     = on;
            menuD3DStopGo.Enabled       = on;
            menuD3DDump.Enabled         = on;
            menuEditWalkMemory.Enabled  = on;
            menuEditPointerScan.Enabled = on;
            menuEditController.Enabled  = on;
            menuEditQuickFind.Enabled   = on;
            menuEditMemoryWatch.Enabled = on;
            tbWalkMem.Enabled           = on;
            tbPointerScan.Enabled       = on;

            if (on)
            {
                tbProcessLabel.Text = !string.IsNullOrEmpty(_selectedModuleName)
                    ? $"  ● {_selectedModuleBase:X8} — {_selectedModuleName}  ({_console.IPAddress})"
                    : $"  ● Connected — {_console.IPAddress}";
                AtlasTheme.StyleProcessChip(tbProcessLabel, connected: true);
            }
            else
            {
                tbProcessLabel.Text = "  ○ No process — connect to Xbox (Ctrl+K)";
                AtlasTheme.StyleProcessChip(tbProcessLabel, connected: false);
                _selectedModuleName = null;
                _selectedModuleBase = 0;
                _selectedModuleSize = 0;
            }

            SyncSavedAddressValuePollState();
        }

        // -------------------------------------------------------------------
        // Status-bar progress bar (Cheat-Engine style).
        // -------------------------------------------------------------------

        /// <summary>
        /// Show the bottom-right progress bar.  Use <paramref name="marquee"/>
        /// for operations of unknown duration (network round-trips with no
        /// reported chunk size).
        /// </summary>
        private void BeginProgress(bool marquee = false, bool cancellable = false, ulong byteTotal = 0)
        {
            _progressByteTotal = byteTotal;
            _progressLastStatusPct = -1;
            scanProgress.Style    = marquee ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            scanProgress.Value    = 0;
            scanProgress.Visible  = true;
            if (cancellable)
            {
                _dumpCts?.Dispose();
                _dumpCts = new CancellationTokenSource();
                scanProgress.Cursor = Cursors.Hand;
            }
            else
            {
                scanProgress.Cursor = Cursors.Default;
            }
            scanProgress.Refresh();
            SyncSavedAddressValuePollState();
        }

        /// <summary>Update the progress bar (0–100). Safe from dump worker threads.</summary>
        private void ReportProgress(int percent)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int>(ReportProgress), percent);
                return;
            }
            if (!scanProgress.Visible) return;
            if (scanProgress.Style == ProgressBarStyle.Marquee) return;
            int v = percent < 0 ? 0 : (percent > 100 ? 100 : percent);
            if (scanProgress.Value != v) scanProgress.Value = v;

            if (_progressByteTotal > 0 && v != _progressLastStatusPct && (v % 5 == 0 || v >= 100))
            {
                _progressLastStatusPct = v;
                double mibDone  = _progressByteTotal * (double)v / 100.0 / (1024 * 1024);
                double mibTotal = _progressByteTotal / (1024.0 * 1024.0);
                statusWire.Text = $"Dumping {v}% ({mibDone:N1} / {mibTotal:N1} MiB)…  Esc to cancel";
            }
        }

        private void SetStatusSafe(string text)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatusSafe), text);
                return;
            }
            statusWire.Text = text;
        }

        private bool RequireConnected(string operation)
        {
            if (_console != null && _console.ScanInProgress)
            {
                MessageBox.Show(this,
                    "A memory scan is in progress. Press Esc to cancel it, then try again.",
                    "XDCKIT",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }
            if (_console != null && _console.Connected) return true;
            ShowError(operation, new InvalidOperationException("Not connected."));
            return false;
        }

        /// <summary>Hide the progress bar and reset to 0.</summary>
        private void EndProgress()
        {
            scanProgress.Style   = ProgressBarStyle.Continuous;
            scanProgress.Value   = 0;
            scanProgress.Cursor  = Cursors.Default;
            _dumpCts?.Dispose();
            _dumpCts = null;
            _progressByteTotal = 0;
            SyncSavedAddressValuePollState();
        }

        private void OnScanProgressClicked(object sender, EventArgs e)
        {
            if (_dumpCts != null)
                CancelActiveDump("Cancelled (click).");
        }

        /// <summary>Cancel the in-flight scan/dump if any (Esc key or progress-bar click).</summary>
        private void CancelActiveDump(string reason = "scan cancelled")
        {
            var cts = _dumpCts;
            if (cts == null || cts.IsCancellationRequested) return;
            try { cts.Cancel(); } catch { /* ignore */ }
            statusWire.Text = reason;
        }

        /// <summary>Release all scan dump buffers (RAM or memory-mapped temp files).</summary>
        private void DisposeDumpSnapshots()
        {
            var seen = new HashSet<DumpSnapshot>();
            foreach (var s in new[] { _dumpInitial, _dumpPrevious, _dumpCurrent })
            {
                if (s == null || ReferenceEquals(s, DumpSnapshot.Empty)) continue;
                if (seen.Add(s)) s.Dispose();
            }
            _dumpInitial = _dumpPrevious = _dumpCurrent = null;
        }

        private void OnCommandSent(string cmd)
            => _ui?.Post(_ => statusWire.Text = "» " + Truncate(cmd, 80), null);

        private void OnResponseReceived(XbdmResponse resp)
            => _ui?.Post(_ => statusWire.Text = $"« {(int)resp.Status} {Truncate(resp.StatusMessage, 70)}", null);

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        /// <summary>
        /// Run <paramref name="action"/> on the threadpool so the UI message
        /// pump keeps draining (else WinForms throws ContextSwitchDeadlock).
        /// </summary>
        private async void SafeRun(string what, Action action)
        {
            if (_console != null && _console.ScanInProgress)
            {
                MessageBox.Show(this,
                    "A memory scan is in progress. Press Esc to cancel it, then try again.",
                    "XDCKIT",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            Exception thrown = null;
            await Task.Run(() => { try { action(); } catch (Exception ex) { thrown = ex; } }).ConfigureAwait(true);
            if (IsDisposed || Disposing) return;
            if (thrown != null) ShowError(what, thrown);
            else                statusWire.Text = "✓ " + what;
            UpdateConnectionUiState();
        }

        private void ShowError(string what, Exception ex)
        {
            statusWire.Text = "✗ " + what + ": " + ex.Message;
            MessageBox.Show(this, ex.Message, "XDCKIT — " + what,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // -----------------------------------------------------------------
        // [3] Connect / Disconnect / Discovery
        // -----------------------------------------------------------------

        /// <summary>
        /// Cheat-Engine-style "Open Process" entry point. Connects to a console
        /// if necessary, then shows <see cref="ProcessListForm"/> with every
        /// loaded module reported by xbdm's <c>modules</c> command.
        /// </summary>
        private async void OnConnectClicked(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                if (!await EnsureConnectedInteractiveAsync()) return;
            }

            using (var dlg = new ProcessListForm(_console))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedModuleName))
                {
                    _selectedModuleName = dlg.SelectedModuleName;
                    _selectedModuleBase = dlg.SelectedModuleBase;
                    _selectedModuleSize = dlg.SelectedModuleSize;
                    statusWire.Text = $"Selected module: {_selectedModuleBase:X8} - {_selectedModuleName}";
                    UpdateConnectionUiState();
                    PromptForScanRangePreset();
                }
            }
        }

        /// <summary>
        /// After picking a process, ask whether to scan the module's own
        /// base..base+size range or Title Heap (0xC0000000–0xCFFFFFFF).
        /// Cancel leaves the current Start/Stop boxes untouched.
        /// </summary>
        private void PromptForScanRangePreset()
        {
            bool haveModule = _selectedModuleBase != 0;
            uint modBase = _selectedModuleBase;
            uint modSize = _selectedModuleSize;
            uint modEnd  = haveModule ? unchecked(modBase + (modSize != 0 ? modSize : 0x100000u)) : 0;

            string moduleLine = haveModule
                ? $"Module:    0x{modBase:X8} - 0x{modEnd:X8} ({(modSize != 0 ? modSize : 0x100000):N0} bytes)"
                : "Module:    (unavailable - no size info)";

            string body =
                "Pick a Search Range preset:\n\n" +
                "  Yes      - " + moduleLine + "\n" +
                "  No       - Title Heap: 0xC0000000 - 0xCFFFFFFF\n" +
                "  Cancel  - Keep current Start/Stop values\n";

            var btn = haveModule ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.OKCancel;
            var ans = MessageBox.Show(this, body, "Scan Range", btn,
                                      MessageBoxIcon.Question,
                                      haveModule ? MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button1);

            if (ans == DialogResult.Cancel) return;

            if (haveModule && ans == DialogResult.Yes)
            {
                regionCombo.SelectedIndex = 5;   // "Selected Module"
                    return;
            }

            // No / OK
            regionCombo.SelectedIndex = 2;       // "Title Heap (0xC0000000-0xCFFFFFFF)"
        }

        /// <summary>
        /// Prompts the user for a console IP (blank = LAN auto-discover) and
        /// returns once a successful xbdm session is established. Returns
        /// false if the user cancels or connection fails.
        /// </summary>
        private async Task<bool> EnsureConnectedInteractiveAsync()
        {
            string ip = Prompt("Open Process (Connect)", "Console IP (blank = LAN auto-discover):", "");
            if (ip == null) return false;
            ip = ip.Trim();
            statusWire.Text = ip.Length == 0 ? "Discovering on LAN..." : "Connecting to " + ip + "...";

            bool ok = await Task.Run(() =>
            {
                try
                {
                    if (ip.Length == 0 || ip.Equals("auto", StringComparison.OrdinalIgnoreCase))
                        return _console.Connect();

                    int colon = ip.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(ip.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
                        return _console.Connect(ip.Substring(0, colon), port);

                    return _console.Connect(ip);
                }
                catch { return false; }
            });

            UpdateConnectionUiState();
            if (!ok)
            {
                statusWire.Text = "Connection failed.";
                MessageBox.Show(this,
                    "Could not connect to a console.\nCheck the IP / network and that xbdm is running.",
                    "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            statusWire.Text = "Connected to " + _console.IPAddress;
            return true;
        }

        private void OnDisconnectClicked(object sender, EventArgs e)
            => SafeRun("disconnect", () => _console.Disconnect());

        /// <summary>
        /// LAN discovery. Spawns the unified UDP+TCP scan from
        /// <see cref="XboxClient.DiscoverConsolesAsync"/> and streams hits into
        /// the picker so the user sees consoles as they answer (no need to wait
        /// for the full sweep before picking the first one).
        /// </summary>
        private async void OnDiscoverClicked(object sender, EventArgs e)
        {
            menuFileDiscover.Enabled = false;
            statusWire.Text = "Scanning LAN…";
            BeginProgress(marquee: true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                using var picker = new DiscoveryPickerForm(_ui);
                int counter = 0;
                var options = new XboxClient.DiscoveryOptions
                {
                    PerHostTimeoutMs    = 250,
                    NameServiceTimeoutMs = 1500,
                    Progress             = hit =>
                    {
                        Interlocked.Increment(ref counter);
                        picker.AddHit(hit);
                        _ui.Post(_ => statusWire.Text = $"Discovered {counter} console(s)…", null);
                    },
                };

                var sweep = Task.Run(() => XboxClient.DiscoverConsolesAsync(options, cts.Token), cts.Token);
                picker.WireCancel(cts);

                // The picker is modal: WinForms pumps messages while it's
                // open, so Progress callbacks (marshalled via _ui.Post) keep
                // appending hits into the listview while the user waits.
                picker.ShowDialog(this);

                cts.Cancel();
                try { await sweep.ConfigureAwait(true); } catch { /* cancelled */ }

                if (picker.PickedIP == null)
                {
                    statusWire.Text = counter == 0 ? "No xbdm consoles answered." : "Discovery cancelled.";
                    if (counter == 0)
                        MessageBox.Show(this, "No xbdm consoles answered on the LAN.",
                                        "Discovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                statusWire.Text = "Connecting to " + picker.PickedIP + "…";
                bool ok = await Task.Run(() =>
                {
                    try { return _console.Connect(picker.PickedIP); }
                    catch { return false; }
                }).ConfigureAwait(true);

                UpdateConnectionUiState();
                statusWire.Text = ok ? "Connected to " + _console.IPAddress : "Connection failed.";
            }
            catch (OperationCanceledException) { statusWire.Text = "Discovery cancelled."; }
            catch (Exception ex) { ShowError("discover", ex); }
            finally { menuFileDiscover.Enabled = true; EndProgress(); }
        }

        // -----------------------------------------------------------------
        // [4] Reboot / Shutdown / Stop / Go / Screenshot / Notify / About
        // -----------------------------------------------------------------

        private void OnRebootColdClicked(object sender, EventArgs e)
            => SafeRun("reboot cold", () => _console.Reboot(XboxReboot.Cold));

        private void OnRebootWarmClicked(object sender, EventArgs e)
            => SafeRun("reboot warm", () => _console.Reboot(XboxReboot.Warm));

        private void OnShutdownClicked(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Shut the console down?", "XDCKIT",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SafeRun("shutdown", () => _console.ShutDown());
        }

        private bool _frozen;
        private void OnStopGoClicked(object sender, EventArgs e)
        {
            if (_frozen) { SafeRun("go",   _console.Go);   _frozen = false; menuD3DStopGo.Text = "&Stop / Go"; }
            else         { SafeRun("stop", _console.Stop); _frozen = true;  menuD3DStopGo.Text = "Go (resume)"; }
        }

        /// <summary>
        /// Capture the framebuffer once and (a) copy the decoded image to the
        /// Windows clipboard so the user can paste it anywhere, (b) save the
        /// PNG to disk.  When the surface can't be decoded the raw bytes are
        /// still written to a sibling <c>.bin</c> so nothing is lost.
        /// </summary>
        private async void OnScreenshotClicked(object sender, EventArgs e)
        {
            if (!_console.Connected) { ShowError("screenshot", new InvalidOperationException("Not connected.")); return; }

            string path;
            using (var sfd = new SaveFileDialog
            {
                Filter   = "PNG image (*.png)|*.png|Raw frame buffer (*.bin)|*.bin",
                FileName = $"xbox-{DateTime.Now:yyyyMMdd-HHmmss}.png"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                path = sfd.FileName;
            }

            menuD3DScreenshot.Enabled = false;
            statusWire.Text = "Capturing framebuffer...";
            BeginProgress(marquee: true);
            try
            {
                ScreenshotInfo info = default;
                byte[] frame = null;
                await Task.Run(() => info = _console.Screenshot(out frame)).ConfigureAwait(true);

                bool wroteImage = false;
                bool copiedToClipboard = false;
                string writtenPath = null;
                bool wantRaw = string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase);

                if (!wantRaw && XboxScreenshot.TryDecodeBitmap(info, frame, out var bmp))
                {
                    using (bmp)
                    {
                        try { Clipboard.SetImage(bmp); copiedToClipboard = true; }
                        catch (Exception cex) { statusWire.Text = "Clipboard copy failed: " + cex.Message; }

                        try
                        {
                            string dir = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);
                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            wroteImage = true;
                            writtenPath = path;
                        }
                        catch (Exception sex) { ShowError("screenshot save", sex); }
                    }
                }

                if (!wroteImage)
                {
                    string raw = wantRaw ? path : Path.ChangeExtension(path, ".bin");
                    if (frame != null && frame.Length > 0)
                    {
                        File.WriteAllBytes(raw, frame);
                        writtenPath = raw;
                    }
                }

                string parts =
                    (wroteImage
                        ? $"Screenshot {info.Width}×{info.Height} → {Path.GetFileName(writtenPath)}"
                        : writtenPath != null
                            ? $"Couldn't decode geometry → raw {Path.GetFileName(writtenPath)}"
                            : "Screenshot failed (empty frame).")
                    + (copiedToClipboard ? "   (also copied to clipboard)" : string.Empty);

                statusWire.Text = parts;
            }
            catch (Exception ex) { ShowError("screenshot", ex); }
            finally { menuD3DScreenshot.Enabled = _console.Connected; EndProgress(); }
        }

        private void OnNotifyClicked(object sender, EventArgs e)
        {
            string text = Prompt("Notify", "Notification text:", "XDCKIT online");
            if (text == null) return;
            SafeRun("notify", () => _console.Notify(text, XNotiyLogo.FLASHING_XBOX_LOGO));
        }

        private void OnAboutClicked(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "XCE Atlas\nPowered by XDCKIT — the Xbox Direct Connect Kit.\n\n" +
                "Library types live in the global namespace.\n" +
                "See docs/USE_CASES.md for code examples.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnOpenSettings(object sender, EventArgs e)
            => MessageBox.Show(this, "Settings are stored next to the executable.\n(Coming soon.)",
                               "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void OnAdvancedOptionsClicked(object sender, EventArgs e)
            => MessageBox.Show(this, "Advanced options are not yet implemented.",
                               "Advanced Options", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void OnTableExtrasClicked(object sender, EventArgs e)
            => MessageBox.Show(this, "Table extras are not yet implemented.",
                               "Table Extras", MessageBoxButtons.OK, MessageBoxIcon.Information);

        /// <summary>
        /// Size the three result columns so <c>Address | Value | Previous</c>
        /// always fits the left panel — fixed pixel widths clip the last column
        /// when the splitter is narrow (looks like CE is missing a column).
        /// </summary>
        private void FitResultsColumns()
        {
            if (resultsListView == null || resultsListView.IsDisposed || !resultsListView.IsHandleCreated) return;
            int w = resultsListView.ClientSize.Width - 4;
            if (w < 180) return;
            bool vScroll = resultsListView.Items.Count * 18 > resultsListView.ClientSize.Height;
            if (vScroll) w -= SystemInformation.VerticalScrollBarWidth;
            int c = Math.Max(72, w / 3);
            colAddress.Width = c;
            colValue.Width = c;
            colPrevious.Width = Math.Max(72, w - c - c);
        }

        // Mid-bar layout: center the scan-paused icon between the two
        // buttons after every resize.
        private void OnMidBarResize(object sender, EventArgs e)
        {
            int v = (midBar.Height - lblScanIcon.Height) / 2;
            lblScanIcon.Left = (midBar.Width - lblScanIcon.Width) / 2;
            lblScanIcon.Top  = Math.Max(0, v);
            btnAddAddress.Left = midBar.Width - btnAddAddress.Width - 8;
            btnAddAddress.Top  = (midBar.Height - btnAddAddress.Height) / 2;
            btnMemoryView.Top  = (midBar.Height - btnMemoryView.Height) / 2;
        }

        // -----------------------------------------------------------------
        // [5] Scan engine — First Scan / Next Scan / Undo Scan
        // -----------------------------------------------------------------

        private ScanFilterSettings CaptureScanFilterSettings()
        {
            var (kind, isF, isD) = ResolveValueType();
            int width = (int)kind;
            return new ScanFilterSettings(
                scanTypeCombo.SelectedIndex,
                kind, isF, isD,
                notCheck.Checked,
                hexCheck.Checked,
                ComputeAlignmentStep(width),
                scanValueBox.Text,
                scanValueBox2.Text);
        }

        /// <summary>Validate filter inputs on the UI thread before starting background work.</summary>
        private bool TryPrepareScanFilter(out ScanFilterSettings settings, out string error)
        {
            settings = CaptureScanFilterSettings();
            error = null;

            switch (settings.ScanTypeIndex)
            {
                case 0:
                    if (!TryParseTypedValue(settings.ValueText, settings.Kind, settings.IsF, settings.IsD, settings.HexValues, out _, out error))
                        return false;
                    break;
                case 1:
                case 2:
                    if (!TryParseTypedValue(settings.ValueText, settings.Kind, settings.IsF, settings.IsD, settings.HexValues, out _, out error))
                        return false;
                    break;
                case 3:
                    if (!TryParseTypedValue(settings.ValueText, settings.Kind, settings.IsF, settings.IsD, settings.HexValues, out _, out error))
                        return false;
                    if (!TryParseTypedValue(settings.Value2Text, settings.Kind, settings.IsF, settings.IsD, settings.HexValues, out _, out error))
                        return false;
                    break;
            }
            return true;
        }

        private (ScanKind kind, bool isF, bool isD) ResolveValueType()
        {
            switch (valueTypeCombo.SelectedIndex)
            {
                case 0: return (ScanKind.Byte,   false, false);
                case 1: return (ScanKind.Width2, false, false);
                case 2: return (ScanKind.Width4, false, false);
                case 3: return (ScanKind.Width8, false, false);
                case 4: return (ScanKind.Width4, true,  false);
                case 5: return (ScanKind.Width8, false, true);
                default:return (ScanKind.Width4, false, false);
            }
        }

        private bool TryParseHexU64(string s, out ulong v)
        {
            v = 0; if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim(); if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>
        /// "First Scan" / "New Scan" — pull a fresh dump of the entire user
        /// specified [start, stop) range from the console and apply the chosen
        /// scan-type filter.  Confirms first when the range is very large
        /// (&gt;= 256 MiB) so we don't accidentally start a multi-gig pull.
        /// </summary>
        private async void OnFirstScanClicked(object sender, EventArgs e)
        {
            if (!RequireConnected("first scan")) return;
            if (!TryParseScanRange(out uint start, out uint length, out string rangeError))
            {
                MessageBox.Show(this, rangeError, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ConfirmLargeScanRange(length)) return;

            if (!TryPrepareScanFilter(out ScanFilterSettings filter, out string filterErr))
            {
                MessageBox.Show(this, filterErr, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnFirstScan.Enabled = false;
            SetStatusSafe($"Scanning 0x{length:X} bytes from 0x{start:X8}…  (Esc to cancel)");
            BeginProgress(cancellable: true, byteTotal: length);
            _incrementalUiPct = -1;
            _incrementalUiAt = DateTime.MinValue;
            try
            {
                DisposeDumpSnapshots();
                _dumpBase = start;
                _dumpSize = length;
                _scanHits.StartNewSession();
                _scanHitsOnDisk = false;

                List<(uint Start, uint Length)> committed = null;
                if (cbActiveOnly.Checked)
                    committed = XboxMemoryScanHelpers.BuildMergedCommittedIntervals(_console, start, length);

                DumpSnapshot snap;
                using (var session = _console.BeginScanSession(start, length))
                using (var pipeline = new ScanChunkPipeline(p =>
                {
                    FilterFirstPassChunk(filter, p.Buffer, p.BufferDumpOffset, p.BufferLength);
                    MaybeRefreshIncrementalScanUi(p.Percent);
                }, _dumpCts.Token))
                {
                    snap = await DumpSnapshot.CaptureAsync(
                            _console, start, length, ReportProgress, _dumpCts.Token,
                            activeMemoryOnly: cbActiveOnly.Checked,
                            onChunkScanned: p => pipeline.Enqueue(p),
                            session: session,
                            committedSegments: committed)
                        .ConfigureAwait(true);
                    pipeline.Complete();
                }

                _dumpInitial  = snap;
                _dumpPrevious = snap;
                _dumpCurrent  = snap;
                _scanHits.Flush();
                _scanHitsOnDisk = true;

                IncrementalScanUiUpdate(100);
                btnNextScan.Enabled  = true;
                btnUndoScan.Enabled  = false;
                string mode = snap.HasLinearBuffer(out _) ? " (RAM)" : snap.IsMemoryMapped ? " (mapped)" : string.Empty;
                SetStatusSafe($"New scan: {_scanHits.Count:N0} matches in {length:N0} bytes{mode}.");
            }
            catch (OperationCanceledException) { SetStatusSafe("First scan cancelled."); }
            catch (Exception ex) { ShowError("first scan", ex); }
            finally { btnFirstScan.Enabled = true; EndProgress(); }
        }

        private int ComputeAlignmentStep(int defaultStep)
        {
            if (!cbFastScan.Checked || rbLastDigits.Checked) return 1;
            int n;
            if (int.TryParse(alignmentBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
                return n;
            return defaultStep;
        }

        private int AlignmentStep(int defaultStep) => ComputeAlignmentStep(defaultStep);

        private bool TryParseScanRange(out uint start, out uint length, out string errorMessage)
        {
            start = 0;
            length = 0;
            errorMessage = null;

            if (!TryParseHexU64(startBox.Text, out ulong start64) ||
                !TryParseHexU64(stopBox.Text, out ulong stop64) ||
                stop64 < start64)
            {
                errorMessage = "Range invalid. Enter hex addresses with stop >= start.";
                return false;
            }

            start = (uint)Math.Min(0xFFFFFFFFul, start64);
            uint stopU = (uint)Math.Min(0xFFFFFFFFul, stop64);
            // Stop is the last byte included (matches region presets like C0000000–CFFFFFFF).
            ulong length64 = (ulong)stopU - start + 1;
            if (length64 == 0)
            {
                errorMessage = "Range is empty.";
                return false;
            }

            if (length64 > ScanRangeHardCapBytes)
            {
                errorMessage =
                    $"Range is {length64 / (1024.0 * 1024.0):N0} MiB, larger than the {ScanRangeHardCapBytes / (1024 * 1024)} MiB hard cap. " +
                    "Narrow Start/Stop or use the region presets and try again.";
                return false;
            }

            length = (uint)length64;
            return true;
        }

        private bool ConfirmLargeScanRange(ulong lengthBytes)
        {
            if (lengthBytes < ScanRangeConfirmBytes) return true;
            return MessageBox.Show(this,
                $"Pull {lengthBytes / (1024.0 * 1024.0):N0} MiB from the console for a new scan?\n\n" +
                "Use region presets or “Active memory only” to shrink large ranges. Esc cancels while dumping.",
                "Large memory dump",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void RunScanFilter(bool firstPass)
        {
            if (!TryPrepareScanFilter(out ScanFilterSettings filter, out string err))
            {
                MessageBox.Show(this, err, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string filterError = RunScanFilterCore(filter, firstPass);
            if (filterError != null)
                MessageBox.Show(this, filterError, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshResultsListView();
        }

        /// <summary>Apply filter using a UI snapshot (no controls — safe on a worker thread).</summary>
        private string RunScanFilterCore(ScanFilterSettings s, bool firstPass)
        {
            switch (s.ScanTypeIndex)
            {
                case 0: return FilterExact(s);
                case 1: return FilterRelative(s, +1, firstPass);
                case 2: return FilterRelative(s, -1, firstPass);
                case 3: return FilterBetween(s);
                case 4:
                    return firstPass
                        ? PopulateUnknownInitialHits(s)
                        : FilterChangedFromPrev(s, changed: true);
                default:
                    return null;
            }
        }

        /// <summary>
        /// "Next Scan" — when <c>Re-Dump</c> is checked, pulls the same window
        /// from the console again and applies the chosen filter; when
        /// unchecked, skips the network round-trip and just re-filters the
        /// cached dump.  Matches the classic XCE Tools 2.0 "Re-Dump" toggle.
        /// </summary>
        private async void OnNextScanClicked(object sender, EventArgs e)
        {
            if (_dumpInitial == null) { OnFirstScanClicked(sender, e); return; }

            bool reDump = cbReDump.Checked;
            if (reDump && !RequireConnected("next scan")) return;

            if (!TryPrepareScanFilter(out ScanFilterSettings filter, out string filterErr))
            {
                MessageBox.Show(this, filterErr, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool useGrouped = reDump && _scanHitsOnDisk && _scanHits.Count > 0
                && NextScanGrouped.ShouldUseGroupedRedump(_scanHits.Count, _dumpSize);

            btnNextScan.Enabled = false;
            SetStatusSafe(reDump
                ? (useGrouped ? "Refreshing hits…  (Esc to cancel)" : "Re-dumping…  (Esc to cancel)")
                : "Re-filtering cached dump…");
            BeginProgress(cancellable: reDump, byteTotal: useGrouped ? 0 : (reDump ? _dumpSize : 0));
            try
            {
                if (reDump && useGrouped)
                {
                    var oldPrevSlot = _dumpPrevious;
                    _dumpPrevious = _dumpCurrent ?? _dumpInitial;

                    using (var session = _console.BeginScanSession(_dumpBase, _dumpSize))
                    {
                        int lastPct = -1;
                        string filterError = await Task.Run(
                            () => NextScanGrouped.FilterHits(
                                session,
                                _scanHits,
                                _dumpBase,
                                _dumpPrevious ?? _dumpInitial,
                                _dumpCurrent ?? _dumpInitial,
                                filter,
                                _scanHits.EnumerateActive(),
                                GroupedHitKeeps,
                                _dumpCts.Token,
                                (pct, hits) =>
                                {
                                    if (pct == lastPct) return;
                                    lastPct = pct;
                                    try
                                    {
                                        BeginInvoke(new Action(() =>
                                        {
                                            if (IsDisposed) return;
                                            ReportProgress(pct);
                                            statusWire.Text =
                                                $"Next scan {pct}% · {hits:N0} matches · Esc to cancel";
                                        }));
                                    }
                                    catch { /* form closing */ }
                                }),
                            _dumpCts.Token).ConfigureAwait(true);

                        if (filterError != null)
                            MessageBox.Show(this, filterError, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    if (oldPrevSlot != null && !ReferenceEquals(oldPrevSlot, DumpSnapshot.Empty) &&
                        !ReferenceEquals(oldPrevSlot, _dumpInitial) &&
                        !ReferenceEquals(oldPrevSlot, _dumpCurrent))
                        oldPrevSlot.Dispose();

                    RefreshResultsListView();
                }
                else if (reDump)
                {
                    List<(uint Start, uint Length)> committed = null;
                    if (cbActiveOnly.Checked)
                        committed = XboxMemoryScanHelpers.BuildMergedCommittedIntervals(_console, _dumpBase, _dumpSize);

                    DumpSnapshot newSnap;
                    using (var session = _console.BeginScanSession(_dumpBase, _dumpSize))
                    {
                        newSnap = await DumpSnapshot.CaptureAsync(
                                _console, _dumpBase, _dumpSize, ReportProgress, _dumpCts.Token,
                                activeMemoryOnly: cbActiveOnly.Checked,
                                session: session,
                                committedSegments: committed)
                            .ConfigureAwait(true);
                    }

                    var oldPrevSlot = _dumpPrevious;
                    _dumpPrevious = _dumpCurrent ?? _dumpInitial;
                    _dumpCurrent = newSnap;
                    if (oldPrevSlot != null && !ReferenceEquals(oldPrevSlot, DumpSnapshot.Empty) &&
                        !ReferenceEquals(oldPrevSlot, _dumpInitial))
                        oldPrevSlot.Dispose();

                    SetStatusSafe("Filtering…");
                    string filterError = await Task.Run(
                        () => RunScanFilterCore(filter, firstPass: false),
                        _dumpCts.Token).ConfigureAwait(true);
                    if (filterError != null)
                        MessageBox.Show(this, filterError, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RefreshResultsListView();
                }
                else
                {
                    string filterError = RunScanFilterCore(filter, firstPass: false);
                    if (filterError != null)
                        MessageBox.Show(this, filterError, "XDCKIT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RefreshResultsListView();
                }

                btnUndoScan.Enabled = _scanHits.HasUndo;
                SetStatusSafe($"Next scan: {_scanHits.Count:N0} matches.");
            }
            catch (OperationCanceledException) { SetStatusSafe("Next scan cancelled."); }
            catch (Exception ex) { ShowError("next scan", ex); }
            finally { btnNextScan.Enabled = true; EndProgress(); }
        }

        private void OnUndoScanClicked(object sender, EventArgs e)
        {
            if (!_scanHits.HasUndo) return;
            _scanHits.TryUndo(out _);
            RefreshResultsListView();
            btnUndoScan.Enabled = _scanHits.HasUndo;
            statusWire.Text = $"Undo: {_scanHits.Count:N0} matches.";
        }

        private void OnScanTypeChanged(object sender, EventArgs e)
        {
            int i = scanTypeCombo.SelectedIndex;
            bool between = i == 3;
            bool unknown = i == 4;

            scanValueBox.Visible  = !unknown;
            lblValue.Visible      = !unknown;
            hexCheck.Visible      = !unknown;

            lblBetween.Visible    = between;
            scanValueBox2.Visible = between;

            if (between)
            {
                scanValueBox.Size  = new Size(110, 22);
                lblBetween.Location = new Point(scanValueBox.Right + 4, scanValueBox.Top + 2);
                scanValueBox2.Location = new Point(scanValueBox.Right + 24, scanValueBox.Top);
            }
            else
            {
                scanValueBox.Size = new Size(228, 22);
            }
        }

        private void OnRegionChanged(object sender, EventArgs e)
        {
            switch (regionCombo.SelectedIndex)
            {
                case 0: startBox.Text = "80000000"; stopBox.Text = "FFFFFFFF"; break;
                case 1: startBox.Text = "80000000"; stopBox.Text = "9FFFFFFF"; break;
                case 2: startBox.Text = "C0000000"; stopBox.Text = "CFFFFFFF"; break;
                case 3: startBox.Text = "80000000"; stopBox.Text = "80FFFFFF"; break;
                case 4: startBox.Text = "C2000000"; stopBox.Text = "E0000000"; break;
                case 5:
                    if (_selectedModuleBase != 0)
                    {
                        uint size = _selectedModuleSize != 0 ? _selectedModuleSize : 0x100000;
                        uint end  = unchecked(_selectedModuleBase + size);
                        startBox.Text = $"{_selectedModuleBase:X8}";
                        stopBox.Text  = $"{end:X8}";
                    }
                    break;
            }
        }

        // ---- Filters ---------------------------------------------------

        /// <summary>Scan one captured chunk during first scan (dump + filter interleaved).</summary>
        private void FilterFirstPassChunk(ScanFilterSettings s, byte[] chunk, int dumpOff, int chunkLen)
        {
            if (chunk == null || chunkLen <= 0) return;
            int width = s.ValueWidth;
            int maxOff = chunkLen - width;
            if (maxOff < 0) return;
            uint baseAddr = _dumpBase + (uint)dumpOff;

            switch (s.ScanTypeIndex)
            {
                case 0:
                    if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] target, out _))
                        return;
                    ScanFirstPass.RunExactOnBuffer(chunk, target, s.AlignmentStep, baseAddr, s.NotCheck, _scanHits, maxOff, 0);
                    return;
                case 1:
                    FilterRelativeChunk(s, chunk, chunkLen, maxOff, baseAddr, sign: +1);
                    return;
                case 2:
                    FilterRelativeChunk(s, chunk, chunkLen, maxOff, baseAddr, sign: -1);
                    return;
                case 3:
                    FilterBetweenChunk(s, chunk, chunkLen, maxOff, baseAddr);
                    return;
                case 4:
                    for (int off = 0; off <= maxOff; off += s.AlignmentStep)
                        _scanHits.Append(baseAddr + (uint)off);
                    return;
            }
        }

        private void FilterRelativeChunk(ScanFilterSettings s, byte[] chunk, int chunkLen, int maxOff, uint baseAddr, int sign)
        {
            if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] target, out _))
                return;
            double t = ReadTyped(target, 0, s.Kind, s.IsF, s.IsD);
            for (int off = 0; off <= maxOff; off += s.AlignmentStep)
            {
                double v = DumpSnapshot.ReadTypedInBuffer(chunk, off, s.Kind, s.IsF, s.IsD, chunkLen, _chunkFlip);
                bool keep = sign > 0 ? v > t : v < t;
                if (s.NotCheck) keep = !keep;
                if (keep) _scanHits.Append(baseAddr + (uint)off);
            }
        }

        private void FilterBetweenChunk(ScanFilterSettings s, byte[] chunk, int chunkLen, int maxOff, uint baseAddr)
        {
            if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] lo, out _) ||
                !TryParseTypedValue(s.Value2Text, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] hi, out _))
                return;
            double a = ReadTyped(lo, 0, s.Kind, s.IsF, s.IsD);
            double b = ReadTyped(hi, 0, s.Kind, s.IsF, s.IsD);
            if (a > b) { double t = a; a = b; b = t; }
            for (int off = 0; off <= maxOff; off += s.AlignmentStep)
            {
                double v = DumpSnapshot.ReadTypedInBuffer(chunk, off, s.Kind, s.IsF, s.IsD, chunkLen, _chunkFlip);
                bool inRange = v >= a && v <= b;
                bool keep = s.NotCheck ? !inRange : inRange;
                if (keep) _scanHits.Append(baseAddr + (uint)off);
            }
        }

        private void MaybeRefreshIncrementalScanUi(int percent)
        {
            if (percent >= 100)
            {
                if (InvokeRequired)
                    BeginInvoke(new Action(() => IncrementalScanUiUpdate(100)));
                else
                    IncrementalScanUiUpdate(100);
                return;
            }

            var now = DateTime.UtcNow;
            if (percent == _incrementalUiPct && (now - _incrementalUiAt).TotalMilliseconds < 200)
                return;

            _incrementalUiPct = percent;
            _incrementalUiAt = now;
            if (InvokeRequired)
                BeginInvoke(new Action(() => IncrementalScanUiUpdate(percent)));
            else
                IncrementalScanUiUpdate(percent);
        }

        private void IncrementalScanUiUpdate(int percent)
        {
            RefreshResultsListView();
            ReportProgress(percent);
            double mibDone  = _dumpSize * (double)percent / 100.0 / (1024 * 1024);
            double mibTotal = _dumpSize / (1024.0 * 1024.0);
            statusWire.Text = $"Scanning {percent}% ({mibDone:N1}/{mibTotal:N1} MiB) · {_scanHits.Count:N0} found · Esc to cancel";
        }

        private string FilterExact(ScanFilterSettings s)
        {
            if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] target, out string err))
                return err;

            return FilterMatches(s, (snap, off) =>
            {
                bool match = snap.RegionMatches(off, target);
                return s.NotCheck ? !match : match;
            });
        }

        private string FilterBetween(ScanFilterSettings s)
        {
            if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] lo, out string e1))
                return e1;
            if (!TryParseTypedValue(s.Value2Text, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] hi, out string e2))
                return e2;
            double a = ReadTyped(lo, 0, s.Kind, s.IsF, s.IsD);
            double b = ReadTyped(hi, 0, s.Kind, s.IsF, s.IsD);
            if (a > b) { double t = a; a = b; b = t; }

            return FilterMatches(s, (cur, off) =>
            {
                double v = ReadTyped(cur, off, s.Kind, s.IsF, s.IsD);
                bool inRange = v >= a && v <= b;
                return s.NotCheck ? !inRange : inRange;
            });
        }

        private string FilterRelative(ScanFilterSettings s, int sign, bool firstPass)
        {
            if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] target, out string err))
                return err;
            double t = ReadTyped(target, 0, s.Kind, s.IsF, s.IsD);

            return FilterMatches(s, (cur, off) =>
            {
                double v = ReadTyped(cur, off, s.Kind, s.IsF, s.IsD);
                bool keep = sign > 0 ? v > t : v < t;
                return s.NotCheck ? !keep : keep;
            });
        }

        private string FilterChangedFromPrev(ScanFilterSettings s, bool changed)
        {
            DumpSnapshot prev = _dumpPrevious ?? _dumpInitial;
            return FilterMatches(s, (cur, off) =>
            {
                double a = ReadTyped(prev, off, s.Kind, s.IsF, s.IsD);
                double b = ReadTyped(cur, off, s.Kind, s.IsF, s.IsD);
                bool diff = a != b;
                bool keep = changed ? diff : !diff;
                return s.NotCheck ? !keep : keep;
            });
        }

        /// <summary>Predicate for grouped next-scan (live chunk vs previous dump).</summary>
        private bool GroupedHitKeeps(
            ScanFilterSettings s,
            DumpSnapshot previous,
            uint dumpBase,
            uint chunkBase,
            byte[] chunk,
            uint address,
            byte[] flipScratch)
        {
            int off = (int)(address - dumpBase);
            int rel = (int)(address - chunkBase);
            int width = s.ValueWidth;
            if (off < 0 || rel < 0 || rel + width > chunk.Length) return false;
            if (previous == null || off + width > previous.Length) return false;

            switch (s.ScanTypeIndex)
            {
                case 0:
                    if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] target, out _))
                        return false;
                    bool match = ChunkRegionMatches(chunk, rel, target);
                    return s.NotCheck ? !match : match;
                case 1:
                case 2:
                    if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] relTarget, out _))
                        return false;
                    double t = ReadTyped(relTarget, 0, s.Kind, s.IsF, s.IsD);
                    double v = DumpSnapshot.ReadTypedInBuffer(chunk, rel, s.Kind, s.IsF, s.IsD, chunk.Length, flipScratch);
                    bool relKeep = s.ScanTypeIndex == 1 ? v > t : v < t;
                    return s.NotCheck ? !relKeep : relKeep;
                case 3:
                    if (!TryParseTypedValue(s.ValueText, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] lo, out _) ||
                        !TryParseTypedValue(s.Value2Text, s.Kind, s.IsF, s.IsD, s.HexValues, out byte[] hi, out _))
                        return false;
                    double a = ReadTyped(lo, 0, s.Kind, s.IsF, s.IsD);
                    double b = ReadTyped(hi, 0, s.Kind, s.IsF, s.IsD);
                    if (a > b) { double swap = a; a = b; b = swap; }
                    double cv = DumpSnapshot.ReadTypedInBuffer(chunk, rel, s.Kind, s.IsF, s.IsD, chunk.Length, flipScratch);
                    bool inRange = cv >= a && cv <= b;
                    return s.NotCheck ? !inRange : inRange;
                case 4:
                    double prevVal = ReadTyped(previous, off, s.Kind, s.IsF, s.IsD);
                    double curVal = DumpSnapshot.ReadTypedInBuffer(chunk, rel, s.Kind, s.IsF, s.IsD, chunk.Length, flipScratch);
                    bool diff = prevVal != curVal;
                    bool changedKeep = diff;
                    return s.NotCheck ? !changedKeep : changedKeep;
                default:
                    return false;
            }
        }

        private static bool ChunkRegionMatches(byte[] chunk, int offset, byte[] pattern)
        {
            if (pattern == null || offset < 0 || offset + pattern.Length > chunk.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (chunk[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        private string FilterMatches(ScanFilterSettings s, Func<DumpSnapshot, int, bool> keep)
        {
            var cur = _dumpCurrent ?? _dumpInitial;
            if (cur == null || cur.Length == 0)
                return "No memory dump loaded — run First Scan.";

            int width = s.ValueWidth;

            if (!_scanHitsOnDisk)
            {
                int maxOff = cur.Length - width;
                if (maxOff >= 0)
                {
                    for (int off = 0; off <= maxOff; off += s.AlignmentStep)
                    {
                        if (keep(cur, off))
                            _scanHits.Append(_dumpBase + (uint)off);
                    }
                }
                _scanHits.Flush();
                _scanHitsOnDisk = true;
                return null;
            }

            if (!_scanHits.TryFilterToNewGeneration(addr =>
                {
                    int off = (int)(addr - _dumpBase);
                    if (off < 0 || off + width > cur.Length) return false;
                    return keep(cur, off);
                }, out string err))
                return err ?? "Filter failed.";

            return null;
        }

        private string PopulateUnknownInitialHits(ScanFilterSettings s)
        {
            var cur = _dumpInitial;
            int width = s.ValueWidth;
            int maxOff = cur.Length - width;
            if (maxOff < 0)
            {
                _scanHits.Flush();
                _scanHitsOnDisk = true;
                return null;
            }
            for (int off = 0; off <= maxOff; off += s.AlignmentStep)
                _scanHits.Append(_dumpBase + (uint)off);
            _scanHits.Flush();
            _scanHitsOnDisk = true;
            return null;
        }

        private void RefreshResultsListView()
        {
            const int maxShown = 1000;
            var cur  = _dumpCurrent ?? _dumpInitial ?? DumpSnapshot.Empty;
            var prev = _dumpPrevious ?? _dumpInitial ?? cur;
            var (kind, isF, isD) = ResolveValueType();

            resultsListView.BeginUpdate();
            resultsListView.Items.Clear();
            int shown = 0;
            foreach (var addr in _scanHits.EnumerateActive())
            {
                if (shown++ >= maxShown) break;
                int off = (int)(addr - _dumpBase);
                if (off < 0 || off + (int)kind > cur.Length) continue;
                var item = new ListViewItem("0x" + addr.ToString("X8"));
                item.SubItems.Add(FormatTyped(ReadTyped(cur,  off, kind, isF, isD), kind, isF, isD, hexCheck.Checked));
                item.SubItems.Add(FormatTyped(ReadTyped(prev, off, kind, isF, isD), kind, isF, isD, hexCheck.Checked));
                item.Tag = addr;
                resultsListView.Items.Add(item);
            }
            resultsListView.EndUpdate();

            foundLabel.Text = $"Scan results · Found: {_scanHits.Count:N0}" +
                (_scanHits.Count > maxShown ? $"  (first {maxShown})" : string.Empty);
            FitResultsColumns();
        }

        private void OnResultsDoubleClick(object sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count == 0) return;
            uint addr = (uint)resultsListView.SelectedItems[0].Tag;
            string desc = Prompt("Add to table", "Description:", $"Address 0x{addr:X8}");
            if (desc == null) return;
            AddSavedRow(desc, addr, valueTypeCombo.Text);
        }

        private bool TryParseTypedValue(string text, ScanKind kind, bool isF, bool isD, out byte[] beBytes, out string error)
            => TryParseTypedValue(text, kind, isF, isD, hexCheck.Checked, out beBytes, out error);

        private static bool TryParseTypedValue(
            string text, ScanKind kind, bool isF, bool isD, bool hexDisplay,
            out byte[] beBytes, out string error)
        {
            beBytes = null; error = null;
            text = (text ?? string.Empty).Trim();
            try
            {
                bool hex = hexDisplay && !isF && !isD;
                NumberStyles ns = hex ? NumberStyles.HexNumber : NumberStyles.Integer;
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { text = text.Substring(2); ns = NumberStyles.HexNumber; }

                switch (kind)
                {
                    case ScanKind.Byte:
                        beBytes = new[] { byte.Parse(text, ns, CultureInfo.InvariantCulture) }; return true;
                    case ScanKind.Width2:
                        beBytes = BitConverter.GetBytes(ushort.Parse(text, ns, CultureInfo.InvariantCulture)); Array.Reverse(beBytes); return true;
                    case ScanKind.Width4 when isF:
                        beBytes = BitConverter.GetBytes(float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)); Array.Reverse(beBytes); return true;
                    case ScanKind.Width4:
                        beBytes = BitConverter.GetBytes(uint.Parse(text, ns, CultureInfo.InvariantCulture)); Array.Reverse(beBytes); return true;
                    case ScanKind.Width8 when isD:
                        beBytes = BitConverter.GetBytes(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)); Array.Reverse(beBytes); return true;
                    case ScanKind.Width8:
                        beBytes = BitConverter.GetBytes(ulong.Parse(text, ns, CultureInfo.InvariantCulture)); Array.Reverse(beBytes); return true;
                    default:
                        error = "Unknown width."; return false;
                }
            }
            catch (Exception ex) { error = "Could not parse value: " + ex.Message; return false; }
        }

        private static double ReadTyped(byte[] buf, int off, ScanKind kind, bool isF, bool isD)
        {
            if (off < 0 || off >= buf.Length) return 0;
            switch (kind)
            {
                case ScanKind.Byte:   return buf[off];
                case ScanKind.Width2: return (ushort)((buf[off] << 8) | buf[off + 1]);
                case ScanKind.Width4 when isF:
                {
                    var b = new[] { buf[off + 3], buf[off + 2], buf[off + 1], buf[off] };
                    return BitConverter.ToSingle(b, 0);
                }
                case ScanKind.Width4:
                    return ((uint)buf[off] << 24) | ((uint)buf[off + 1] << 16) | ((uint)buf[off + 2] << 8) | buf[off + 3];
                case ScanKind.Width8 when isD:
                {
                    var b = new[] { buf[off + 7], buf[off + 6], buf[off + 5], buf[off + 4],
                                    buf[off + 3], buf[off + 2], buf[off + 1], buf[off] };
                    return BitConverter.ToDouble(b, 0);
                }
                case ScanKind.Width8:
                {
                    ulong v = 0;
                    for (int i = 0; i < 8; i++) v = (v << 8) | buf[off + i];
                    return v;
                }
            }
            return 0;
        }

        private static double ReadTyped(DumpSnapshot snap, int off, ScanKind kind, bool isF, bool isD)
        {
            if (snap == null || snap.Length == 0) return 0;
            return snap.ReadTyped(off, kind, isF, isD);
        }

        private static string FormatTyped(double v, ScanKind kind, bool isF, bool isD, bool hex)
        {
            if (isF || isD) return v.ToString("R", CultureInfo.InvariantCulture);
            int width = (int)kind;
            if (hex) return "0x" + ((ulong)v).ToString("X" + (width * 2));
            return ((long)v).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Format the value at <paramref name="addr"/> for the saved table (same conventions as scan results / <see cref="WriteTypedValue"/>).</summary>
        private string ReadCurrentValueDisplay(uint addr, string type)
        {
            if (!_console.Connected) return string.Empty;
            bool hex = hexCheck.Checked;
            type = string.IsNullOrEmpty(type) ? "4 Bytes" : type;
            try
            {
                switch (type)
                {
                    case "Byte":
                        return FormatTyped(_console.ReadByte(addr), ScanKind.Byte, false, false, hex);
                    case "2 Bytes":
                        return FormatTyped(_console.ReadUInt16(addr), ScanKind.Width2, false, false, hex);
                    case "4 Bytes":
                        return FormatTyped(_console.ReadUInt32(addr), ScanKind.Width4, false, false, hex);
                    case "8 Bytes":
                        return FormatTyped(_console.ReadUInt64(addr), ScanKind.Width8, false, false, hex);
                    case "Float":
                        return FormatTyped(_console.ReadFloat(addr), ScanKind.Width4, true, false, hex);
                    case "Double":
                        return FormatTyped(_console.ReadDouble(addr), ScanKind.Width8, false, true, hex);
                    case "String":
                        {
                            string s = _console.ReadString(addr, 0x100);
                            if (string.IsNullOrEmpty(s)) return string.Empty;
                            return s.Replace("\r", " ").Replace("\n", " ");
                        }
                    default:
                        {
                            byte[] b = _console.GetMemory(addr, 16);
                            return BitConverter.ToString(b).Replace("-", string.Empty);
                        }
                }
            }
            catch
            {
                return "?";
            }
        }

        private void RefreshSavedRowLiveValue(ListViewItem row)
        {
            if (!_console.Connected) return;
            string typ = row.SubItems.Count > 3 ? row.SubItems[3].Text : "4 Bytes";
            string s = ReadCurrentValueDisplay(ParseAddrFromRow(row), typ);
            if (row.SubItems.Count > 4) row.SubItems[4].Text = s;
            else row.SubItems.Add(s);
        }

        private void SyncSavedAddressValuePollState()
        {
            if (IsDisposed || Disposing) return;
            _savedAddrValueTimer.Enabled = !IsDesignTime
                && _console != null
                && _console.Connected
                && savedListView != null
                && savedListView.Items.Count > 0
                && _dumpCts == null
                && !_console.ScanInProgress
                && Visible;
        }

        private void TickRefreshSavedAddressValues()
        {
            if (!_console.Connected || savedListView.Items.Count == 0) return;
            try
            {
                savedListView.BeginUpdate();
                foreach (ListViewItem it in savedListView.Items)
                {
                    uint addr;
                    try { addr = ParseAddrFromRow(it); }
                    catch (FormatException) { continue; }
                    string typ = it.SubItems.Count > 3 ? it.SubItems[3].Text : "4 Bytes";
                    string next = ReadCurrentValueDisplay(addr, typ);
                    string cur = it.SubItems.Count > 4 ? it.SubItems[4].Text : string.Empty;
                    if (next == cur) continue;
                    if (it.SubItems.Count > 4) it.SubItems[4].Text = next;
                    else it.SubItems.Add(next);
                }
            }
            catch
            {
                // Ignore transient read errors (disconnect race, bad address).
            }
            finally
            {
                savedListView.EndUpdate();
            }
        }

        // -----------------------------------------------------------------
        // [6] Saved-address table
        // -----------------------------------------------------------------

        private void OnAddAddressClicked(object sender, EventArgs e)
        {
            string addrText = Prompt("Add Address Manually", "Address (hex):", "");
            if (string.IsNullOrWhiteSpace(addrText)) return;
            if (!TryParseHexU64(addrText, out ulong a))
            { MessageBox.Show(this, "Address must be hex.", "XDCKIT"); return; }
            string desc = Prompt("Add Address Manually", "Description:", "");
            if (desc == null) return;
            AddSavedRow(desc, (uint)a, valueTypeCombo.Text);
        }

        private void OnClearTableClicked(object sender, EventArgs e)
        {
            if (savedListView.Items.Count == 0) return;
            if (MessageBox.Show(this, "Clear all saved addresses?", "XDCKIT",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            savedListView.Items.Clear();
            SyncSavedAddressValuePollState();
        }

        private void OnLoadTableClicked(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "Cheat table (*.xcetbl;*.ct)|*.xcetbl;*.CT|All files (*.*)|*.*"
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    savedListView.Items.Clear();
                    foreach (var row in CheatTableXml.LoadFromFile(ofd.FileName))
                        savedListView.Items.Add(ListViewItemFromCheatRow(row));
                    statusWire.Text = "Loaded " + Path.GetFileName(ofd.FileName);
                    SyncSavedAddressValuePollState();
                }
                catch (Exception ex) { ShowError("load table", ex); }
            }
        }

        private void OnSaveTableClicked(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "Cheat table (*.xcetbl)|*.xcetbl|All files (*.*)|*.*",
                FileName = "table.xcetbl"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    CheatTableXml.SaveToFile(sfd.FileName, EnumerateCheatRowsFromSavedList());
                    statusWire.Text = "Saved " + Path.GetFileName(sfd.FileName);
                }
                catch (Exception ex) { ShowError("save table", ex); }
            }
        }

        private IEnumerable<CheatTableRow> EnumerateCheatRowsFromSavedList()
        {
            int index = 0;
            foreach (ListViewItem it in savedListView.Items)
            {
                yield return CheatRowFromListViewItem(it, index);
                index++;
            }
        }

        private static CheatTableRow CheatRowFromListViewItem(ListViewItem it, int index)
        {
            uint addr = it.Tag is uint u ? u : 0;
            if (addr == 0 && it.SubItems.Count > 2)
                CheatTableXml.TryParseAddress(it.SubItems[2].Text, out addr);

            return new CheatTableRow
            {
                Id = index,
                Active = it.Checked,
                Description = it.SubItems.Count > 1 ? it.SubItems[1].Text : "No description",
                Address = addr,
                VariableType = it.SubItems.Count > 3 ? it.SubItems[3].Text : "4 Bytes",
                Value = it.SubItems.Count > 4 ? it.SubItems[4].Text : string.Empty
            };
        }

        private ListViewItem ListViewItemFromCheatRow(CheatTableRow row)
        {
            string value = _console.Connected
                ? ReadCurrentValueDisplay(row.Address, row.VariableType)
                : row.Value;
            if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(row.Value))
                value = row.Value;

            var item = new ListViewItem(string.Empty) { Checked = row.Active };
            item.SubItems.Add(row.Description);
            item.SubItems.Add("0x" + row.Address.ToString("X8"));
            item.SubItems.Add(row.VariableType);
            item.SubItems.Add(value);
            item.Tag = row.Address;
            return item;
        }

        private void AddSavedRow(string desc, uint addr, string type)
        {
            var item = new ListViewItem(string.Empty) { Checked = false };
            item.SubItems.Add(desc);
            item.SubItems.Add("0x" + addr.ToString("X8"));
            item.SubItems.Add(type);
            item.SubItems.Add(ReadCurrentValueDisplay(addr, type));
            item.Tag = addr;
            savedListView.Items.Add(item);
            SyncSavedAddressValuePollState();
        }

        private void OnSavedDoubleClick(object sender, EventArgs e)
        {
            if (savedListView.SelectedItems.Count == 0) return;
            EditSavedValue(savedListView.SelectedItems[0]);
        }

        private void OnSavedKeyDown(object sender, KeyEventArgs e)
        {
            if (RouteSavedListHotkey(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>Cheat Engine-style shortcuts for the saved-address list.</summary>
        private bool RouteSavedListHotkey(Keys kd)
        {
            if (kd == (Keys.Control | Keys.V))
            {
                TryPasteSavedRowsFromClipboard();
                return true;
            }

            if (savedListView.SelectedItems.Count == 0)
                return false;

            var row = savedListView.SelectedItems[0];

            if (kd == Keys.Delete)
            {
                DeleteSelectedSavedRows();
                return true;
            }

            if (kd == Keys.Space)
            {
                ToggleSelectedSavedChecked();
                return true;
            }

            if (kd == (Keys.Control | Keys.B))
            {
                BrowseMemoryForSavedRow(row);
                return true;
            }

            if (kd == (Keys.Control | Keys.D))
            {
                BrowseMemoryForSavedRow(row);
                return true;
            }

            if (kd == (Keys.Control | Keys.C))
            {
                CopySelectedSavedRowsToClipboard();
                return true;
            }

            if (kd == (Keys.Control | Keys.X))
            {
                CutSelectedSavedRows();
                return true;
            }

            if (kd == Keys.F5)
            {
                ShowSavedFeatureNotImplemented("Find out what accesses this address");
                return true;
            }

            if (kd == Keys.F6)
            {
                ShowSavedFeatureNotImplemented("Find out what writes to this address");
                return true;
            }

            if (kd == (Keys.Shift | Keys.Control | Keys.Alt | Keys.Enter))
            {
                SmartEditSavedRow(row);
                return true;
            }

            if (kd == (Keys.Control | Keys.Alt | Keys.Enter))
            {
                EditSavedAddress(row);
                return true;
            }

            if (kd == (Keys.Control | Keys.Enter))
            {
                EditSavedDescription(row);
                return true;
            }

            if (kd == (Keys.Alt | Keys.Enter))
            {
                EditSavedType(row);
                return true;
            }

            if (kd == Keys.Enter)
            {
                EditSavedValue(row);
                return true;
            }

            return false;
        }

        private static uint ParseAddrFromRow(ListViewItem row)
        {
            if (row.Tag is uint t) return t;
            return ParseAddrCellStrict(row.SubItems.Count > 2 ? row.SubItems[2].Text : "0");
        }

        private static uint ParseAddrCellStrict(string cell)
        {
            if (!TryParseAddrCell(cell, out uint a))
                throw new FormatException("Address must be hex (e.g. 82000000 or 0x82000000).");
            return a;
        }

        private static bool TryParseAddrCell(string cell, out uint addr)
        {
            addr = 0;
            if (string.IsNullOrWhiteSpace(cell)) return false;
            cell = cell.Trim();
            if (cell.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cell = cell.Substring(2);
            return uint.TryParse(cell, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out addr);
        }

        private void WriteTypedValue(uint addr, string type, string text)
        {
            text = text.Trim();
            NumberStyles ns = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            switch (type)
            {
                case "Byte":    _console.WriteByte  (addr, byte.Parse  (text, ns, CultureInfo.InvariantCulture)); break;
                case "2 Bytes": _console.WriteUInt16(addr, ushort.Parse(text, ns, CultureInfo.InvariantCulture)); break;
                case "4 Bytes": _console.WriteUInt32(addr, uint.Parse  (text, ns, CultureInfo.InvariantCulture)); break;
                case "8 Bytes": _console.WriteUInt64(addr, ulong.Parse (text, ns, CultureInfo.InvariantCulture)); break;
                case "Float":   _console.WriteFloat (addr, float.Parse (text, NumberStyles.Float, CultureInfo.InvariantCulture)); break;
                case "Double":  _console.WriteDouble(addr, double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)); break;
                case "String":  _console.WriteString(addr, text); break;
                default:        _console.SetMemory  (addr, HexToBytes(text)); break;
            }
        }

        private void WireSavedAddressTableUi()
        {
            savedListView.PreviewKeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                    ev.IsInputKey = true;
            };

            _ctxSavedAddresses = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.WhiteSmoke,
                ShowImageMargin = false,
                Renderer = new ToolStripProfessionalRenderer(new DarkColorTable())
            };
            _ctxSavedAddresses.Opening += OnSavedContextOpening;

            ToolStripMenuItem Mi(string text, string shortcutDisplay, EventHandler click)
            {
                var m = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcutDisplay };
                m.Click += click;
                return m;
            }

            var miDelete = Mi("Delete this record", "Del", (_, __) => DeleteSelectedSavedRows());
            var miChange = new ToolStripMenuItem("Change record");
            miChange.DropDownItems.Add(Mi("Description…", "Ctrl+Enter", (_, __) => { if (TryGetPrimarySavedRow(out var r)) EditSavedDescription(r); }));
            miChange.DropDownItems.Add(Mi("Address…", "Ctrl+Alt+Enter", (_, __) => { if (TryGetPrimarySavedRow(out var r)) EditSavedAddress(r); }));
            miChange.DropDownItems.Add(Mi("Type…", "Alt+Enter", (_, __) => { if (TryGetPrimarySavedRow(out var r)) EditSavedType(r); }));
            miChange.DropDownItems.Add(Mi("Value…", "Enter", (_, __) => { if (TryGetPrimarySavedRow(out var r)) EditSavedValue(r); }));
            miChange.DropDownItems.Add(Mi("Smart edit address(es)…", "Shift+Ctrl+Alt+Enter", (_, __) => { if (TryGetPrimarySavedRow(out var r)) SmartEditSavedRow(r); }));

            var miBrowse = Mi("Browse this memory region", "Ctrl+B", (_, __) => { if (TryGetPrimarySavedRow(out var r)) BrowseMemoryForSavedRow(r); });
            var miDisasm = Mi("Disassemble this memory region", "Ctrl+D", (_, __) => { if (TryGetPrimarySavedRow(out var r)) BrowseMemoryForSavedRow(r); });
            var miPtr = Mi("Pointer scan for this address", null, (_, __) => { if (TryGetPrimarySavedRow(out var r)) OpenPointerScanForTarget(ParseAddrFromRow(r)); });
            var miAcc = Mi("Find out what accesses this address", "F5", (_, __) => ShowSavedFeatureNotImplemented("Find out what accesses this address"));
            var miWrt = Mi("Find out what writes to this address", "F6", (_, __) => ShowSavedFeatureNotImplemented("Find out what writes to this address"));
            var miCut = Mi("Cut", "Ctrl+X", (_, __) => CutSelectedSavedRows());
            var miCopy = Mi("Copy", "Ctrl+C", (_, __) => CopySelectedSavedRowsToClipboard());
            var miPaste = Mi("Paste", "Ctrl+V", (_, __) => TryPasteSavedRowsFromClipboard());
            var miToggle = Mi("Toggle selected records", "Space", (_, __) => ToggleSelectedSavedChecked());

            _ctxSavedAddresses.Items.Add(miDelete);
            _ctxSavedAddresses.Items.Add(miChange);
            _ctxSavedAddresses.Items.Add(new ToolStripSeparator());
            _ctxSavedAddresses.Items.Add(miBrowse);
            _ctxSavedAddresses.Items.Add(miDisasm);
            _ctxSavedAddresses.Items.Add(new ToolStripSeparator());
            _ctxSavedAddresses.Items.Add(miPtr);
            _ctxSavedAddresses.Items.Add(miAcc);
            _ctxSavedAddresses.Items.Add(miWrt);
            _ctxSavedAddresses.Items.Add(new ToolStripSeparator());
            _ctxSavedAddresses.Items.Add(miCut);
            _ctxSavedAddresses.Items.Add(miCopy);
            _ctxSavedAddresses.Items.Add(miPaste);
            _ctxSavedAddresses.Items.Add(new ToolStripSeparator());
            _ctxSavedAddresses.Items.Add(miToggle);

            savedListView.ContextMenuStrip = _ctxSavedAddresses;
        }

        private void OnSavedContextOpening(object sender, CancelEventArgs e)
        {
            if (_ctxSavedAddresses == null) return;
            bool row = savedListView.SelectedItems.Count > 0;
            foreach (ToolStripItem it in _ctxSavedAddresses.Items)
            {
                if (it is ToolStripSeparator) continue;
                var text = it.Text ?? string.Empty;
                it.Enabled = row || text.StartsWith("Paste", StringComparison.Ordinal);
                if (it is ToolStripMenuItem mm && mm.HasDropDownItems)
                {
                    foreach (ToolStripItem sub in mm.DropDownItems)
                        sub.Enabled = row;
                }
            }
        }

        private bool TryGetPrimarySavedRow(out ListViewItem row)
        {
            row = null;
            if (savedListView.SelectedItems.Count == 0) return false;
            row = savedListView.SelectedItems[0];
            return true;
        }

        private void DeleteSelectedSavedRows()
        {
            var copy = new List<ListViewItem>();
            foreach (ListViewItem it in savedListView.SelectedItems) copy.Add(it);
            foreach (var it in copy) savedListView.Items.Remove(it);
            SyncSavedAddressValuePollState();
        }

        private void BrowseMemoryForSavedRow(ListViewItem row)
        {
            if (_console.ScanInProgress)
            {
                MessageBox.Show(this, "Cancel the active memory scan (Esc) before opening Memory View.", "Memory View",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!_console.Connected) { MessageBox.Show(this, "Connect to a console first.", "Memory View", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            uint a = ParseAddrFromRow(row);
            using (var dlg = new MemoryViewForm(_console, a)) dlg.ShowDialog(this);
        }

        private void EditSavedValue(ListViewItem row)
        {
            if (!_console.Connected) { MessageBox.Show(this, "Connect to a console first.", "Edit value", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string cur = row.SubItems.Count > 4 ? row.SubItems[4].Text : string.Empty;
            string newVal = Prompt("Edit value", row.SubItems[1].Text + " (" + row.SubItems[3].Text + ")", cur);
            if (newVal == null) return;
            try
            {
                uint a = ParseAddrFromRow(row);
                WriteTypedValue(a, row.SubItems[3].Text, newVal);
                if (row.SubItems.Count > 4) row.SubItems[4].Text = newVal;
                else row.SubItems.Add(newVal);
            }
            catch (Exception ex) { ShowError("edit value", ex); }
        }

        private void EditSavedDescription(ListViewItem row)
        {
            string t = Prompt("Description", "Description:", row.SubItems[1].Text);
            if (t != null) row.SubItems[1].Text = t;
        }

        private void EditSavedAddress(ListViewItem row)
        {
            string cur = row.SubItems.Count > 2 ? row.SubItems[2].Text : string.Empty;
            string t = Prompt("Address", "Address (hex):", cur);
            if (t == null) return;
            if (!TryParseHexU64(t.Trim(), out ulong a64))
            { MessageBox.Show(this, "Address must be hex.", "XDCKIT"); return; }
            uint a = (uint)Math.Min(0xFFFFFFFFul, a64);
            row.SubItems[2].Text = "0x" + a.ToString("X8");
            row.Tag = a;
            RefreshSavedRowLiveValue(row);
        }

        private void EditSavedType(ListViewItem row)
        {
            var pick = PickFromList("Value type", new List<string>
            {
                "Byte", "2 Bytes", "4 Bytes", "8 Bytes", "Float", "Double", "String", "Array of byte"
            });
            if (pick == null) return;
            row.SubItems[3].Text = pick;
            RefreshSavedRowLiveValue(row);
        }

        private void SmartEditSavedRow(ListViewItem row)
        {
            using (var dlg = new Form
            {
                Text = "Smart edit address",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(400, 220),
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.WhiteSmoke
            })
            {
                int y = 12;
                var ld = new Label { Left = 12, Top = y, Width = 90, Text = "Description:", ForeColor = Color.WhiteSmoke };
                var td = new TextBox { Left = 100, Top = y - 2, Width = 280, Text = row.SubItems[1].Text, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
                y += 32;
                var la = new Label { Left = 12, Top = y, Width = 90, Text = "Address:", ForeColor = Color.WhiteSmoke };
                var ta = new TextBox { Left = 100, Top = y - 2, Width = 280, Text = row.SubItems[2].Text, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
                y += 32;
                var lt = new Label { Left = 12, Top = y, Width = 90, Text = "Type:", ForeColor = Color.WhiteSmoke };
                var tt = new ComboBox { Left = 100, Top = y - 2, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke };
                tt.Items.AddRange(new object[] { "Byte", "2 Bytes", "4 Bytes", "8 Bytes", "Float", "Double", "String", "Array of byte" });
                tt.SelectedItem = row.SubItems[3].Text;
                if (tt.SelectedIndex < 0) tt.SelectedIndex = 2;
                y += 32;
                var lv = new Label { Left = 12, Top = y, Width = 90, Text = "Value:", ForeColor = Color.WhiteSmoke };
                var tv = new TextBox { Left = 100, Top = y - 2, Width = 280, Text = row.SubItems.Count > 4 ? row.SubItems[4].Text : string.Empty, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
                y += 44;
                var ok = new Button { Text = "OK", Left = 220, Top = y, Width = 72, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, ForeColor = Color.WhiteSmoke, BackColor = Color.FromArgb(63, 63, 70) };
                var cancel = new Button { Text = "Cancel", Left = 304, Top = y, Width = 72, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, ForeColor = Color.WhiteSmoke, BackColor = Color.FromArgb(63, 63, 70) };
                dlg.Controls.AddRange(new Control[] { ld, td, la, ta, lt, tt, lv, tv, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                row.SubItems[1].Text = td.Text;
                if (!TryParseHexU64(ta.Text.Trim(), out ulong a64))
                { MessageBox.Show(this, "Address must be hex.", "XDCKIT"); return; }
                uint a = (uint)Math.Min(0xFFFFFFFFul, a64);
                row.SubItems[2].Text = "0x" + a.ToString("X8");
                row.Tag = a;
                row.SubItems[3].Text = tt.SelectedItem?.ToString() ?? "4 Bytes";
                if (!_console.Connected) { MessageBox.Show(this, "Connect to write value.", "XDCKIT"); return; }
                try
                {
                    WriteTypedValue(a, row.SubItems[3].Text, tv.Text);
                    if (row.SubItems.Count > 4) row.SubItems[4].Text = tv.Text;
                    else row.SubItems.Add(tv.Text);
                }
                catch (Exception ex) { ShowError("smart edit", ex); }
            }
        }

        private void CopySelectedSavedRowsToClipboard()
        {
            var sb = new StringBuilder();
            foreach (ListViewItem it in savedListView.SelectedItems)
            {
                string v = it.SubItems.Count > 4 ? it.SubItems[4].Text : string.Empty;
                sb.Append(it.Checked ? "1" : "0").Append('\t')
                  .Append(it.SubItems[1].Text).Append('\t')
                  .Append(it.SubItems[2].Text).Append('\t')
                  .Append(it.SubItems[3].Text).Append('\t')
                  .Append(v)
                  .AppendLine();
            }
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString().TrimEnd());
        }

        private void CutSelectedSavedRows()
        {
            CopySelectedSavedRowsToClipboard();
            DeleteSelectedSavedRows();
        }

        private void TryPasteSavedRowsFromClipboard()
        {
            if (!Clipboard.ContainsText()) return;
            string blob = Clipboard.GetText();
            foreach (var raw in blob.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] p = line.IndexOf('\t') >= 0 ? line.Split('\t') : line.Split('|');
                if (p.Length < 4) continue;
                bool active = p[0] == "1" || p[0].Equals("true", StringComparison.OrdinalIgnoreCase);
                string desc = p[1].Trim();
                if (!TryParseAddrCell(p[2], out uint addr)) continue;
                string typ = p[3].Trim();
                string val = p.Length > 4 ? p[4] : string.Empty;
                var item = new ListViewItem(string.Empty) { Checked = active };
                item.SubItems.Add(desc);
                item.SubItems.Add("0x" + addr.ToString("X8"));
                item.SubItems.Add(typ);
                item.SubItems.Add(_console.Connected ? ReadCurrentValueDisplay(addr, typ) : val);
                item.Tag = addr;
                savedListView.Items.Add(item);
            }
            SyncSavedAddressValuePollState();
        }

        private void ToggleSelectedSavedChecked()
        {
            foreach (ListViewItem it in savedListView.SelectedItems)
                it.Checked = !it.Checked;
        }

        private void ShowSavedFeatureNotImplemented(string feature)
        {
            MessageBox.Show(
                this,
                $"{feature} is not implemented in XCE Atlas yet.",
                "XDCKIT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // -----------------------------------------------------------------
        // [7] Memory View dialog
        // -----------------------------------------------------------------

        private void OnOpenMemoryView(object sender, EventArgs e)
        {
            if (_console.ScanInProgress)
            {
                MessageBox.Show(this, "Cancel the active memory scan (Esc) before opening Memory View.", "Memory View",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new MemoryViewForm(_console)) dlg.ShowDialog(this);
        }

        private void OnWalkMemoryClicked(object sender, EventArgs e)
        {
            if (!_console.Connected) { ShowError("walk memory", new InvalidOperationException("Not connected.")); return; }
            using (var dlg = new WalkMemoryForm(_console)) dlg.ShowDialog(this);
        }

        private void OnPointerScanClicked(object sender, EventArgs e)
            => OpenPointerScanForTarget(null);

        /// <summary>Opens pointer scan; <paramref name="targetOverride"/> is the address to find pointers to (CE: target).</summary>
        private void OpenPointerScanForTarget(uint? targetOverride)
        {
            if (!_console.Connected) { ShowError("pointer scan", new InvalidOperationException("Not connected.")); return; }
            uint? target = targetOverride;
            if (target == null && resultsListView.SelectedItems.Count > 0 && resultsListView.SelectedItems[0].Tag is uint a)
                target = a;
            string sh = startBox.Text?.Trim();
            string eh = stopBox.Text?.Trim();
            using (var dlg = new PointerScanForm(_console, target, sh, eh))
                dlg.ShowDialog(this);
        }

        // -----------------------------------------------------------------
        // [8] Breakpoints dialog
        // -----------------------------------------------------------------

        private void OnOpenBreakpoints(object sender, EventArgs e)
        {
            using (var dlg = new BreakpointsForm(_console)) dlg.ShowDialog(this);
        }

        private void OnOpenController(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first (File → Open Process).", "Virtual Controller",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_controllerForm == null || _controllerForm.IsDisposed)
            {
                _controllerForm = new ControllerForm(_console);
                _controllerForm.FormClosed += (_, __) => _controllerForm = null;
            }
            _controllerForm.Show(this);
            _controllerForm.BringToFront();
        }

        private void OnOpenQuickFind(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first (File → Open Process).", "Quick Find",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_quickFindForm == null || _quickFindForm.IsDisposed)
            {
                _quickFindForm = new QuickFindForm(_console, startBox.Text, stopBox.Text);
                _quickFindForm.FormClosed += (_, __) => _quickFindForm = null;
            }
            _quickFindForm.Show(this);
            _quickFindForm.BringToFront();
        }

        private void OnOpenMemoryWatch(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first (File → Open Process).", "Memory Watch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_memoryWatchForm == null || _memoryWatchForm.IsDisposed)
            {
                _memoryWatchForm = new MemoryWatchForm(_console);
                _memoryWatchForm.FormClosed += (_, __) => _memoryWatchForm = null;
            }
            _memoryWatchForm.Show(this);
            _memoryWatchForm.BringToFront();
        }

        private void OnLiveProfiles(object sender, EventArgs e)
        {
            if (_console == null || !_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first.", "Live",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var f = new LiveProfilesForm(_console))
                f.ShowDialog(this);
        }

        private void OnLiveQuickSignIn(object sender, EventArgs e) =>
            SafeRun("quick sign-in", () => XboxLiveUi.QuickSignIn(_console));

        private void OnLiveFriends(object sender, EventArgs e) =>
            SafeRun("friends", () => XboxLiveUi.OpenFriends(_console));

        private void OnLiveParty(object sender, EventArgs e) =>
            SafeRun("party", () => XboxLiveUi.OpenParty(_console));

        private void OnLiveGuide(object sender, EventArgs e) =>
            SafeRun("guide", () => XboxLiveUi.OpenGuide(_console));

        private void OnOpenLiveView(object sender, EventArgs e)
        {
            if (!_console.Connected)
            {
                MessageBox.Show(this, "Connect to the console first (File → Open Process).", "Live View",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_liveViewForm == null || _liveViewForm.IsDisposed)
            {
                _liveViewForm = new ScreenshotStreamForm(_console);
                _liveViewForm.FormClosed += (_, __) => _liveViewForm = null;
            }
            _liveViewForm.Show(this);
            _liveViewForm.BringToFront();
        }

        // -----------------------------------------------------------------
        // [9] Memory dump
        // -----------------------------------------------------------------

        private async void OnDumpClicked(object sender, EventArgs e)
        {
            if (!_console.Connected) { ShowError("dump", new InvalidOperationException("Not connected.")); return; }

            uint start, len;
            string presetName;
            using (var dlg = new DumpForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                start       = dlg.StartAddress;
                len         = dlg.Length;
                presetName  = dlg.PresetName;
            }

            using (var sfd = new SaveFileDialog
            {
                Filter   = "Binary dump (*.bin)|*.bin|All files (*.*)|*.*",
                FileName = BuildDumpFileName(presetName, start, len)
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                statusWire.Text = $"Dumping 0x{len:X} bytes from 0x{start:X8} ({presetName})...";
                BeginProgress();
                try
                {
                    await DumpSnapshot.WriteDumpToFileAsync(_console, start, len, sfd.FileName, ReportProgress, CancellationToken.None)
                        .ConfigureAwait(true);
                    statusWire.Text = $"Dumped → {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex) { ShowError("dump", ex); }
                finally { EndProgress(); }
            }
        }

        /// <summary>Build a friendly filename like "physram-C0000000-DFFF0FFF.bin".</summary>
        private static string BuildDumpFileName(string presetName, uint start, uint len)
        {
            string slug;
            if (string.Equals(presetName, DumpForm.PhysicalRam.Name, StringComparison.Ordinal))  slug = "physram";
            else if (string.Equals(presetName, DumpForm.BaseFile.Name,    StringComparison.Ordinal)) slug = "image";
            else if (string.Equals(presetName, DumpForm.Virtual.Name,     StringComparison.Ordinal)) slug = "virtual";
            else slug = "dump";
            return $"{slug}-{start:X8}-{(start + len):X8}.bin";
        }

        // -----------------------------------------------------------------
        // Shared helpers
        // -----------------------------------------------------------------

        private static byte[] HexToBytes(string s)
        {
            s = (s ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
            if ((s.Length & 1) != 0) throw new FormatException("Hex string must have even length.");
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return b;
        }

        private string Prompt(string title, string label, string defaultText)
        {
            using (var dlg = new Form
            {
                Text            = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(380, 130),
                MinimizeBox     = false,
                MaximizeBox     = false,
                BackColor       = Color.FromArgb(45, 45, 48),
                ForeColor       = Color.WhiteSmoke
            })
            {
                var lbl = new Label { Left = 12, Top = 14, AutoSize = true, Text = label, ForeColor = Color.WhiteSmoke };
                var tb  = new TextBox
                {
                    Left = 12, Top = 38, Width = 356, Text = defaultText,
                    BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var ok  = new Button { Text = "OK",     Left = 212, Top = 80, Width = 72, DialogResult = DialogResult.OK,     FlatStyle = FlatStyle.Flat, ForeColor = Color.WhiteSmoke, BackColor = Color.FromArgb(63, 63, 70) };
                var ko  = new Button { Text = "Cancel", Left = 296, Top = 80, Width = 72, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, ForeColor = Color.WhiteSmoke, BackColor = Color.FromArgb(63, 63, 70) };
                dlg.Controls.AddRange(new Control[] { lbl, tb, ok, ko });
                dlg.AcceptButton = ok; dlg.CancelButton = ko;
                return dlg.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        private string PickFromList(string title, List<string> items)
        {
            using (var dlg = new Form
            {
                Text            = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(320, 320),
                MinimizeBox     = false,
                MaximizeBox     = false,
                BackColor       = Color.FromArgb(45, 45, 48),
                ForeColor       = Color.WhiteSmoke
            })
            {
                var list = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.None
                };
                foreach (var v in items) list.Items.Add(v);
                if (list.Items.Count > 0) list.SelectedIndex = 0;
                var ok = new Button
                {
                    Text = "Use selected", Dock = DockStyle.Bottom, Height = 32,
                    FlatStyle = FlatStyle.Flat, ForeColor = Color.WhiteSmoke,
                    BackColor = Color.FromArgb(63, 63, 70), DialogResult = DialogResult.OK
                };
                dlg.Controls.Add(list); dlg.Controls.Add(ok);
                return dlg.ShowDialog(this) == DialogResult.OK ? list.SelectedItem?.ToString() : null;
            }
        }
    }
}
