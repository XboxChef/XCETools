// =============================================================================
// Form1.Designer.cs - Cheat-Engine-styled host UI for XDCKIT
// =============================================================================
//   Layout (matches Cheat Engine 7.x):
//     [MenuStrip:  File | Edit | Table | D3D | Help]
//     [ToolStrip:  🔍 📁 💾  [process label]                      "XCE"]
//     [Label    :  Found: N]
//     [Split (V):  ListView (results) | scan controls]
//     [MidBar   :  Memory View       (separator)       Add Address Manually]
//     [Bottom   :  ListView (saved addresses)]
//     [Status   :  Advanced Options                              Table Extras]
// =============================================================================

namespace XCETools
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // -- root containers --------------------------------------------
            this.menuStrip1            = new System.Windows.Forms.MenuStrip();
            this.toolStrip1            = new System.Windows.Forms.ToolStrip();
            this.foundLabel            = new System.Windows.Forms.Label();
            this.splitMain             = new System.Windows.Forms.SplitContainer();
            this.midBar                = new System.Windows.Forms.Panel();
            this.savedListView         = new System.Windows.Forms.ListView();
            this.statusStrip1          = new System.Windows.Forms.StatusStrip();

            // -- menu items -------------------------------------------------
            this.menuFile              = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileOpenProcess   = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileDisconnect    = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileDiscover      = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFileSep1          = new System.Windows.Forms.ToolStripSeparator();
            this.menuFileExit          = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEdit              = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditMemoryView    = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditWalkMemory    = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditPointerScan   = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditBreakpoints   = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditController    = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditQuickFind     = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditMemoryWatch   = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditSettings      = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTable             = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTableAdd          = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTableClear        = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLive              = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLiveProfiles      = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLiveQuickSignIn   = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLiveFriends       = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLiveParty         = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLiveGuide         = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3D               = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DScreenshot     = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DLiveView       = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DNotify         = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DSep1           = new System.Windows.Forms.ToolStripSeparator();
            this.menuD3DRebootCold     = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DRebootWarm     = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DShutdown       = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DSep2           = new System.Windows.Forms.ToolStripSeparator();
            this.menuD3DStopGo         = new System.Windows.Forms.ToolStripMenuItem();
            this.menuD3DDump           = new System.Windows.Forms.ToolStripMenuItem();
            this.menuHelp              = new System.Windows.Forms.ToolStripMenuItem();
            this.menuHelpAbout         = new System.Windows.Forms.ToolStripMenuItem();

            // -- toolstrip items --------------------------------------------
            this.tbConnect             = new System.Windows.Forms.ToolStripButton();
            this.tbOpenTable           = new System.Windows.Forms.ToolStripButton();
            this.tbSaveTable           = new System.Windows.Forms.ToolStripButton();
            this.tbWalkMem             = new System.Windows.Forms.ToolStripButton();
            this.tbPointerScan         = new System.Windows.Forms.ToolStripButton();
            this.tbProcessBox          = new System.Windows.Forms.Panel();
            this.tbProcessLabel        = new System.Windows.Forms.Label();
            this.scanProgress          = new System.Windows.Forms.ProgressBar();
            this.tbProcessHost         = new System.Windows.Forms.ToolStripControlHost(this.tbProcessBox);
            this.tbSpring              = new System.Windows.Forms.ToolStripLabel();
            this.tbLogo                = new System.Windows.Forms.ToolStripLabel();

            // -- left side: results list ------------------------------------
            this.resultsListView       = new System.Windows.Forms.ListView();
            this.colAddress            = new System.Windows.Forms.ColumnHeader();
            this.colValue              = new System.Windows.Forms.ColumnHeader();
            this.colPrevious           = new System.Windows.Forms.ColumnHeader();

            // -- right side: scan controls ----------------------------------
            this.btnFirstScan          = new System.Windows.Forms.Button();
            this.btnNextScan           = new System.Windows.Forms.Button();
            this.btnUndoScan           = new System.Windows.Forms.Button();

            this.hexCheck              = new System.Windows.Forms.CheckBox();
            this.lblValue              = new System.Windows.Forms.Label();
            this.scanValueBox          = new System.Windows.Forms.TextBox();
            this.scanValueBox2         = new System.Windows.Forms.TextBox();
            this.lblBetween            = new System.Windows.Forms.Label();

            this.lblScanType           = new System.Windows.Forms.Label();
            this.scanTypeCombo         = new System.Windows.Forms.ComboBox();
            this.luaCheck              = new System.Windows.Forms.CheckBox();
            this.notCheck              = new System.Windows.Forms.CheckBox();
            this.lblValueType          = new System.Windows.Forms.Label();
            this.valueTypeCombo        = new System.Windows.Forms.ComboBox();

            this.grpMemOptions         = new System.Windows.Forms.GroupBox();
            this.grpMemoryScan        = new System.Windows.Forms.GroupBox();
            this.bottomScanExtras     = new System.Windows.Forms.FlowLayoutPanel();
            this.regionCombo           = new System.Windows.Forms.ComboBox();
            this.lblStart              = new System.Windows.Forms.Label();
            this.startBox              = new System.Windows.Forms.TextBox();
            this.lblStop               = new System.Windows.Forms.Label();
            this.stopBox               = new System.Windows.Forms.TextBox();
            this.cbWritable            = new System.Windows.Forms.CheckBox();
            this.cbExecutable          = new System.Windows.Forms.CheckBox();
            this.cbCopyOnWrite         = new System.Windows.Forms.CheckBox();
            this.cbActiveOnly          = new System.Windows.Forms.CheckBox();
            this.cbFastScan            = new System.Windows.Forms.CheckBox();
            this.cbReDump              = new System.Windows.Forms.CheckBox();
            this.alignmentBox          = new System.Windows.Forms.TextBox();
            this.rbAlignment           = new System.Windows.Forms.RadioButton();
            this.rbLastDigits          = new System.Windows.Forms.RadioButton();
            this.cbPauseScan           = new System.Windows.Forms.CheckBox();
            this.cbUnrandom            = new System.Windows.Forms.CheckBox();
            this.cbSpeedhack           = new System.Windows.Forms.CheckBox();

            // -- mid bar ----------------------------------------------------
            this.btnMemoryView         = new System.Windows.Forms.Button();
            this.btnAddAddress         = new System.Windows.Forms.Button();
            this.lblScanIcon           = new System.Windows.Forms.PictureBox();

            // -- bottom saved addresses ------------------------------------
            this.colActive             = new System.Windows.Forms.ColumnHeader();
            this.colDescription        = new System.Windows.Forms.ColumnHeader();
            this.colSavedAddress       = new System.Windows.Forms.ColumnHeader();
            this.colType               = new System.Windows.Forms.ColumnHeader();
            this.colSavedValue         = new System.Windows.Forms.ColumnHeader();

            // -- status strip -----------------------------------------------
            this.statusAdvanced        = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusSpring          = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusWire            = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusTable           = new System.Windows.Forms.ToolStripStatusLabel();

            this.menuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.midBar.SuspendLayout();
            this.grpMemOptions.SuspendLayout();
            this.grpMemoryScan.SuspendLayout();
            this.bottomScanExtras.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();

            // ----------------------------------------------------------------
            //  Color palette — AtlasTheme (Xbox green on charcoal)
            // ----------------------------------------------------------------
            System.Drawing.Color paneBg     = AtlasTheme.Surface;
            System.Drawing.Color deepBg     = AtlasTheme.Input;
            System.Drawing.Color text       = AtlasTheme.Text;
            System.Drawing.Color softText   = AtlasTheme.TextMuted;
            System.Drawing.Font  ui         = AtlasTheme.UiFont;

            // ----------------------------------------------------------------
            //  menuStrip
            // ----------------------------------------------------------------
            this.menuStrip1.BackColor  = paneBg;
            this.menuStrip1.ForeColor  = text;
            this.menuStrip1.Font       = ui;
            this.menuStrip1.Renderer   = AtlasTheme.CreateRenderer();
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuFile, this.menuEdit, this.menuTable, this.menuLive, this.menuD3D, this.menuHelp });
            this.menuStrip1.Dock = System.Windows.Forms.DockStyle.Top;
            this.menuStrip1.Name = "menuStrip1";

            this.menuFile.Text = "&File"; this.menuFile.ForeColor = text;
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuFileOpenProcess, this.menuFileDisconnect, this.menuFileDiscover,
                this.menuFileSep1, this.menuFileExit });

            this.menuFileOpenProcess.Text = "&Open Process (Connect)...";
            this.menuFileOpenProcess.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.K;
            this.menuFileOpenProcess.Click += new System.EventHandler(this.OnConnectClicked);

            this.menuFileDisconnect.Text = "&Disconnect";
            this.menuFileDisconnect.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D;
            this.menuFileDisconnect.Enabled = false;
            this.menuFileDisconnect.Click += new System.EventHandler(this.OnDisconnectClicked);

            this.menuFileDiscover.Text = "Discover on &LAN...";
            this.menuFileDiscover.Click += new System.EventHandler(this.OnDiscoverClicked);

            this.menuFileExit.Text = "E&xit";
            this.menuFileExit.Click += new System.EventHandler(this.OnExitClicked);

            this.menuEdit.Text = "&Edit"; this.menuEdit.ForeColor = text;
            this.menuEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuEditMemoryView, this.menuEditWalkMemory, this.menuEditPointerScan, this.menuEditBreakpoints,
                this.menuEditQuickFind, this.menuEditMemoryWatch,
                this.menuEditController, this.menuEditSettings });

            this.menuEditMemoryView.Text = "Memory &View...";
            this.menuEditMemoryView.Click += new System.EventHandler(this.OnOpenMemoryView);

            this.menuEditWalkMemory.Text = "Walk committed &memory...";
            this.menuEditWalkMemory.Enabled = false;
            this.menuEditWalkMemory.Click += new System.EventHandler(this.OnWalkMemoryClicked);

            this.menuEditPointerScan.Text = "&Pointer scan...";
            this.menuEditPointerScan.Enabled = false;
            this.menuEditPointerScan.Click += new System.EventHandler(this.OnPointerScanClicked);

            this.menuEditBreakpoints.Text = "&Breakpoints...";
            this.menuEditBreakpoints.Click += new System.EventHandler(this.OnOpenBreakpoints);

            this.menuEditQuickFind.Text = "Quick memory &find...";
            this.menuEditQuickFind.Enabled = false;
            this.menuEditQuickFind.Click += new System.EventHandler(this.OnOpenQuickFind);

            this.menuEditMemoryWatch.Text = "Memory &watch...";
            this.menuEditMemoryWatch.Enabled = false;
            this.menuEditMemoryWatch.Click += new System.EventHandler(this.OnOpenMemoryWatch);

            this.menuEditController.Text = "Virtual &controller...";
            this.menuEditController.Enabled = false;
            this.menuEditController.Click += new System.EventHandler(this.OnOpenController);

            this.menuEditSettings.Text = "&Settings...";
            this.menuEditSettings.Click += new System.EventHandler(this.OnOpenSettings);

            this.menuTable.Text = "&Table"; this.menuTable.ForeColor = text;
            this.menuTable.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuTableAdd, this.menuTableClear });
            this.menuTableAdd.Text = "&Add Address Manually...";
            this.menuTableAdd.Click += new System.EventHandler(this.OnAddAddressClicked);
            this.menuTableClear.Text = "&Clear Table";
            this.menuTableClear.Click += new System.EventHandler(this.OnClearTableClicked);

            this.menuLive.Text = "&Live"; this.menuLive.ForeColor = text;
            this.menuLive.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuLiveProfiles, this.menuLiveQuickSignIn,
                this.menuLiveFriends, this.menuLiveParty, this.menuLiveGuide });
            this.menuLiveProfiles.Text = "&Profiles / sign-in...";
            this.menuLiveProfiles.Enabled = false;
            this.menuLiveProfiles.Click += new System.EventHandler(this.OnLiveProfiles);
            this.menuLiveQuickSignIn.Text = "&Quick sign-in";
            this.menuLiveQuickSignIn.Enabled = false;
            this.menuLiveQuickSignIn.Click += new System.EventHandler(this.OnLiveQuickSignIn);
            this.menuLiveFriends.Text = "&Friends blade";
            this.menuLiveFriends.Enabled = false;
            this.menuLiveFriends.Click += new System.EventHandler(this.OnLiveFriends);
            this.menuLiveParty.Text = "&Party blade";
            this.menuLiveParty.Enabled = false;
            this.menuLiveParty.Click += new System.EventHandler(this.OnLiveParty);
            this.menuLiveGuide.Text = "&Guide";
            this.menuLiveGuide.Enabled = false;
            this.menuLiveGuide.Click += new System.EventHandler(this.OnLiveGuide);

            this.menuD3D.Text = "&D3D"; this.menuD3D.ForeColor = text;
            this.menuD3D.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuD3DScreenshot, this.menuD3DLiveView, this.menuD3DNotify, this.menuD3DSep1,
                this.menuD3DRebootCold, this.menuD3DRebootWarm, this.menuD3DShutdown,
                this.menuD3DSep2, this.menuD3DStopGo, this.menuD3DDump });

            this.menuD3DScreenshot.Text = "&Screenshot...";
            this.menuD3DScreenshot.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            this.menuD3DScreenshot.ShowShortcutKeys = true;
            this.menuD3DScreenshot.Enabled = false;
            this.menuD3DScreenshot.Click += new System.EventHandler(this.OnScreenshotClicked);

            this.menuD3DLiveView.Text = "Live &view...";
            this.menuD3DLiveView.Enabled = false;
            this.menuD3DLiveView.Click += new System.EventHandler(this.OnOpenLiveView);

            this.menuD3DNotify.Text = "&Notify...";
            this.menuD3DNotify.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N;
            this.menuD3DNotify.Enabled = false;
            this.menuD3DNotify.Click += new System.EventHandler(this.OnNotifyClicked);

            this.menuD3DRebootCold.Text = "Reboot (&Cold)";
            this.menuD3DRebootCold.Enabled = false;
            this.menuD3DRebootCold.Click += new System.EventHandler(this.OnRebootColdClicked);

            this.menuD3DRebootWarm.Text = "Reboot (&Warm)";
            this.menuD3DRebootWarm.Enabled = false;
            this.menuD3DRebootWarm.Click += new System.EventHandler(this.OnRebootWarmClicked);

            this.menuD3DShutdown.Text = "Shut&down";
            this.menuD3DShutdown.Enabled = false;
            this.menuD3DShutdown.Click += new System.EventHandler(this.OnShutdownClicked);

            this.menuD3DStopGo.Text = "&Stop / Go";
            this.menuD3DStopGo.Enabled = false;
            this.menuD3DStopGo.Click += new System.EventHandler(this.OnStopGoClicked);

            this.menuD3DDump.Text = "Du&mp Memory...";
            this.menuD3DDump.Enabled = false;
            this.menuD3DDump.Click += new System.EventHandler(this.OnDumpClicked);

            this.menuHelp.Text = "&Help"; this.menuHelp.ForeColor = text;
            this.menuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.menuHelpAbout });
            this.menuHelpAbout.Text = "&About XCE Atlas...";
            this.menuHelpAbout.Click += new System.EventHandler(this.OnAboutClicked);

            // ----------------------------------------------------------------
            //  toolStrip
            // ----------------------------------------------------------------
            this.toolStrip1.Dock          = System.Windows.Forms.DockStyle.Top;
            this.toolStrip1.BackColor     = paneBg;
            this.toolStrip1.ForeColor     = text;
            this.toolStrip1.GripStyle     = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Renderer      = AtlasTheme.CreateRenderer();
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(22, 22);
            this.toolStrip1.AutoSize      = false;
            this.toolStrip1.Height        = 52;
            this.toolStrip1.Padding       = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tbConnect, this.tbOpenTable, this.tbSaveTable, this.tbWalkMem, this.tbPointerScan,
                this.tbProcessHost, this.tbSpring, this.tbLogo });

            this.tbConnect.DisplayStyle  = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbConnect.ImageScaling  = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tbConnect.Image         = MakeOpenProcessToolbarIcon(22);
            this.tbConnect.ToolTipText   = "Open Process (Connect)";
            this.tbConnect.AutoSize      = false;
            this.tbConnect.Size          = new System.Drawing.Size(28, 28);
            this.tbConnect.Click        += new System.EventHandler(this.OnConnectClicked);

            this.tbOpenTable.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbOpenTable.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tbOpenTable.Image        = MakeFolderToolbarIcon(22);
            this.tbOpenTable.ToolTipText  = "Open Table";
            this.tbOpenTable.AutoSize     = false;
            this.tbOpenTable.Size         = new System.Drawing.Size(28, 28);
            this.tbOpenTable.Click       += new System.EventHandler(this.OnLoadTableClicked);

            this.tbSaveTable.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tbSaveTable.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tbSaveTable.Image        = MakeFloppyToolbarIcon(22);
            this.tbSaveTable.ToolTipText  = "Save Table";
            this.tbSaveTable.AutoSize     = false;
            this.tbSaveTable.Size         = new System.Drawing.Size(28, 28);
            this.tbSaveTable.Click       += new System.EventHandler(this.OnSaveTableClicked);

            this.tbWalkMem.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tbWalkMem.Text         = "Walk mem";
            this.tbWalkMem.ToolTipText  = "Walk committed memory (xbdm walkmem)";
            this.tbWalkMem.Enabled      = false;
            this.tbWalkMem.AutoSize     = true;
            this.tbWalkMem.ForeColor    = text;
            this.tbWalkMem.Click        += new System.EventHandler(this.OnWalkMemoryClicked);

            this.tbPointerScan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tbPointerScan.Text         = "Ptr scan";
            this.tbPointerScan.ToolTipText  = "Find pointers to an address (level 1, BE u32)";
            this.tbPointerScan.Enabled      = false;
            this.tbPointerScan.AutoSize     = true;
            this.tbPointerScan.ForeColor    = text;
            this.tbPointerScan.Click        += new System.EventHandler(this.OnPointerScanClicked);

            // -- stacked process label + progress bar (matches CE/XCE layout) --
            this.tbProcessBox.BackColor   = deepBg;
            this.tbProcessBox.Size        = new System.Drawing.Size(460, 44);
            this.tbProcessBox.Margin      = new System.Windows.Forms.Padding(0);
            this.tbProcessBox.Padding     = new System.Windows.Forms.Padding(0);

            this.tbProcessLabel.Text       = "  No process — connect to Xbox (Ctrl+K)";
            this.tbProcessLabel.ForeColor  = softText;
            this.tbProcessLabel.BackColor  = deepBg;
            this.tbProcessLabel.Font       = ui;
            this.tbProcessLabel.Dock       = System.Windows.Forms.DockStyle.Top;
            this.tbProcessLabel.Height     = 22;
            this.tbProcessLabel.TextAlign  = System.Drawing.ContentAlignment.MiddleLeft;

            this.scanProgress.Dock        = System.Windows.Forms.DockStyle.Fill;
            this.scanProgress.Style       = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.scanProgress.Minimum     = 0;
            this.scanProgress.Maximum     = 100;
            this.scanProgress.Value       = 0;
            this.scanProgress.Margin      = new System.Windows.Forms.Padding(2);
            this.scanProgress.Click      += new System.EventHandler(this.OnScanProgressClicked);

            this.tbProcessBox.Controls.Add(this.scanProgress);
            this.tbProcessBox.Controls.Add(this.tbProcessLabel);

            this.tbProcessHost.AutoSize = false;
            this.tbProcessHost.Size     = new System.Drawing.Size(460, 44);
            this.tbProcessHost.Margin   = new System.Windows.Forms.Padding(2, 1, 2, 1);

            this.tbSpring.Text  = string.Empty;
            this.tbSpring.AutoSize = false;
            this.tbSpring.Size  = new System.Drawing.Size(8, 48);

            this.tbLogo.Text        = "XCE Atlas";
            this.tbLogo.Image       = AtlasTheme.LoadAppIcon(24);
            this.tbLogo.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tbLogo.AutoSize    = false;
            this.tbLogo.Size        = new System.Drawing.Size(120, 48);
            this.tbLogo.Font        = AtlasTheme.UiFontSemibold;
            this.tbLogo.ForeColor   = AtlasTheme.TextAccent;
            this.tbLogo.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.tbLogo.Alignment   = System.Windows.Forms.ToolStripItemAlignment.Right;

            // ----------------------------------------------------------------
            //  foundLabel — lives inside the LEFT split panel, just above the
            //  results listview (matches Cheat Engine's "Found: N" position).
            // ----------------------------------------------------------------
            this.foundLabel.Dock      = System.Windows.Forms.DockStyle.Top;
            this.foundLabel.AutoSize  = false;
            this.foundLabel.Height    = 26;
            this.foundLabel.Padding   = new System.Windows.Forms.Padding(10, 5, 0, 0);
            this.foundLabel.BackColor = paneBg;
            this.foundLabel.ForeColor = AtlasTheme.TextAccent;
            this.foundLabel.Font      = AtlasTheme.UiFontSemibold;
            this.foundLabel.Text      = "Scan results · Found: 0";

            // ----------------------------------------------------------------
            //  splitMain — left = results, right = scan controls
            // ----------------------------------------------------------------
            this.splitMain.Dock           = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Orientation    = System.Windows.Forms.Orientation.Vertical;
            this.splitMain.BackColor      = paneBg;
            this.splitMain.SplitterWidth  = 4;
            this.splitMain.FixedPanel     = System.Windows.Forms.FixedPanel.Panel1;
            this.splitMain.Panel1MinSize  = 200;
            this.splitMain.Panel2MinSize  = 200;
            // Give the SplitContainer an explicit width BEFORE setting
            // SplitterDistance, otherwise EndInit() compares the requested
            // distance against the default (and very small) initial Width
            // and throws "SplitterDistance must be between Panel1MinSize
            // and Width - Panel2MinSize".  Width is reset by Dock=Fill at
            // first layout.
            this.splitMain.Size           = new System.Drawing.Size(800, 400);
            this.splitMain.SplitterDistance = 340;

            // Left: results
            this.resultsListView.Dock         = System.Windows.Forms.DockStyle.Fill;
            this.resultsListView.View         = System.Windows.Forms.View.Details;
            this.resultsListView.FullRowSelect= true;
            this.resultsListView.GridLines    = false;
            this.resultsListView.MultiSelect  = true;
            this.resultsListView.HideSelection= false;
            this.resultsListView.BackColor    = deepBg;
            this.resultsListView.ForeColor    = text;
            this.resultsListView.BorderStyle  = System.Windows.Forms.BorderStyle.None;
            this.resultsListView.Font         = AtlasTheme.CodeFont();
            this.colAddress.Text   = "Address";    this.colAddress.Width  = 88;
            this.colValue.Text     = "Value";      this.colValue.Width    = 88;
            this.colPrevious.Text  = "Previous";   this.colPrevious.Width = 88;
            this.resultsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colAddress, this.colValue, this.colPrevious });
            this.resultsListView.DoubleClick += new System.EventHandler(this.OnResultsDoubleClick);
            // Foundlabel must be added AFTER resultsListView so the Dock.Top
            // label sits above the Fill listview (last-added-with-Dock.Top
            // ends up closest to the top edge).
            this.splitMain.Panel1.Controls.Add(this.resultsListView);
            this.splitMain.Panel1.Controls.Add(this.foundLabel);
            this.splitMain.Panel1.BackColor   = deepBg;

            // Right: scan panel
            BuildScanPanel(this.splitMain.Panel2, paneBg, deepBg, text, softText, ui);

            // ----------------------------------------------------------------
            //  midBar — sits between the upper split (results+scan) and the
            //  saved-addresses ListView at the bottom.  Docking to Bottom and
            //  being added BEFORE savedListView puts it just above it.
            // ----------------------------------------------------------------
            this.midBar.Dock      = System.Windows.Forms.DockStyle.Bottom;
            this.midBar.Height    = 40;
            this.midBar.BackColor = paneBg;
            this.midBar.Paint    += new System.Windows.Forms.PaintEventHandler(this.OnMidBarPaint);

            this.btnMemoryView.Text       = "Memory View";
            this.btnMemoryView.FlatStyle  = System.Windows.Forms.FlatStyle.Flat;
            this.btnMemoryView.BackColor  = AtlasTheme.SurfaceRaised;
            this.btnMemoryView.ForeColor  = text;
            this.btnMemoryView.Font       = ui;
            this.btnMemoryView.Location   = new System.Drawing.Point(10, 8);
            this.btnMemoryView.Size       = new System.Drawing.Size(118, 26);
            this.btnMemoryView.FlatAppearance.BorderColor = AtlasTheme.Border;
            this.btnMemoryView.Cursor     = System.Windows.Forms.Cursors.Hand;
            this.btnMemoryView.Click     += new System.EventHandler(this.OnOpenMemoryView);

            this.lblScanIcon.BackColor   = System.Drawing.Color.Transparent;
            this.lblScanIcon.SizeMode    = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.lblScanIcon.Image       = MakeNoEntryGlyph(22);
            this.lblScanIcon.AutoSize    = false;
            this.lblScanIcon.Size        = new System.Drawing.Size(26, 26);
            this.lblScanIcon.Anchor      = System.Windows.Forms.AnchorStyles.Top;

            this.btnAddAddress.Text      = "Add address";
            this.btnAddAddress.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddAddress.BackColor = AtlasTheme.Accent;
            this.btnAddAddress.ForeColor = System.Drawing.Color.White;
            this.btnAddAddress.Font      = ui;
            this.btnAddAddress.Size      = new System.Drawing.Size(130, 26);
            this.btnAddAddress.FlatAppearance.BorderColor = AtlasTheme.AccentDim;
            this.btnAddAddress.Cursor    = System.Windows.Forms.Cursors.Hand;
            this.btnAddAddress.Anchor    = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnAddAddress.Click    += new System.EventHandler(this.OnAddAddressClicked);

            this.midBar.Controls.Add(this.btnMemoryView);
            this.midBar.Controls.Add(this.lblScanIcon);
            this.midBar.Controls.Add(this.btnAddAddress);
            this.midBar.Resize += new System.EventHandler(this.OnMidBarResize);

            // ----------------------------------------------------------------
            //  savedListView (bottom)
            // ----------------------------------------------------------------
            this.savedListView.Dock           = System.Windows.Forms.DockStyle.Bottom;
            this.savedListView.Height         = 180;
            this.savedListView.View           = System.Windows.Forms.View.Details;
            this.savedListView.FullRowSelect  = true;
            this.savedListView.CheckBoxes     = true;
            this.savedListView.GridLines      = false;
            this.savedListView.BackColor      = deepBg;
            this.savedListView.ForeColor      = text;
            this.savedListView.BorderStyle    = System.Windows.Forms.BorderStyle.None;
            this.savedListView.Font           = AtlasTheme.CodeFont();
            this.colActive.Text       = "Active";      this.colActive.Width      = 60;
            this.colDescription.Text  = "Description"; this.colDescription.Width = 220;
            this.colSavedAddress.Text = "Address";     this.colSavedAddress.Width= 110;
            this.colType.Text         = "Type";        this.colType.Width        = 90;
            this.colSavedValue.Text   = "Value";       this.colSavedValue.Width  = 220;
            this.savedListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colActive, this.colDescription, this.colSavedAddress, this.colType, this.colSavedValue });
            this.savedListView.DoubleClick += new System.EventHandler(this.OnSavedDoubleClick);
            this.savedListView.KeyDown     += new System.Windows.Forms.KeyEventHandler(this.OnSavedKeyDown);

            // ----------------------------------------------------------------
            //  statusStrip
            // ----------------------------------------------------------------
            this.statusStrip1.BackColor = paneBg;
            this.statusStrip1.ForeColor = text;
            this.statusStrip1.Font      = ui;
            this.statusStrip1.Renderer  = AtlasTheme.CreateRenderer();
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.statusAdvanced, this.statusSpring, this.statusWire, this.statusTable });

            this.statusAdvanced.Text   = "Advanced Options";
            this.statusAdvanced.Spring = false;
            this.statusAdvanced.ForeColor = softText;
            this.statusAdvanced.IsLink = true;
            this.statusAdvanced.LinkColor = softText;
            this.statusAdvanced.ActiveLinkColor = text;
            this.statusAdvanced.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.statusAdvanced.Click += new System.EventHandler(this.OnAdvancedOptionsClicked);

            this.statusSpring.Text   = string.Empty;
            this.statusSpring.Spring = true;

            this.statusWire.Text      = string.Empty;
            this.statusWire.ForeColor = softText;
            this.statusWire.AutoSize  = false;
            this.statusWire.Width     = 1;
            this.statusWire.Visible   = false;

            this.statusTable.Text      = "Table Extras";
            this.statusTable.ForeColor = softText;
            this.statusTable.IsLink    = true;
            this.statusTable.LinkColor = softText;
            this.statusTable.ActiveLinkColor = text;
            this.statusTable.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.statusTable.Click += new System.EventHandler(this.OnTableExtrasClicked);

            // ----------------------------------------------------------------
            //  Form
            // ----------------------------------------------------------------
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(960, 760);
            this.BackColor  = AtlasTheme.Window;
            this.ForeColor  = text;
            this.Font       = ui;
            this.MinimumSize = new System.Drawing.Size(720, 520);
            // Z-order matters for dock-style stacking. The FIRST control added
            // is the INNERMOST one (closest to the form's centre); each
            // subsequent control docks AGAINST whatever was added before it.
            // Layout target (top→bottom):
            //   menuStrip > toolStrip > splitMain (fill) > midBar > savedListView > statusStrip
            this.Controls.Add(this.splitMain);     // fill (innermost)
            this.Controls.Add(this.midBar);        // bottom: just above the saved list
            this.Controls.Add(this.savedListView); // bottom: below midBar
            this.Controls.Add(this.statusStrip1);  // bottom: at the very bottom
            this.Controls.Add(this.toolStrip1);    // top:    below the menu
            this.Controls.Add(this.menuStrip1);    // top:    outermost
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "XCE Atlas";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.midBar.ResumeLayout(false);
            this.grpMemOptions.ResumeLayout(false);
            this.grpMemOptions.PerformLayout();
            this.grpMemoryScan.ResumeLayout(false);
            this.bottomScanExtras.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        /// <summary>
        /// Layout the right-hand scan-options pane (First/Next/Undo Scan, value
        /// boxes, scan-type / value-type combos, Memory Scan Options group,
        /// Unrandomizer / Speedhack toggles).
        /// </summary>
        private void BuildScanPanel(System.Windows.Forms.SplitterPanel parent,
                                    System.Drawing.Color paneBg,
                                    System.Drawing.Color deepBg,
                                    System.Drawing.Color text,
                                    System.Drawing.Color softText,
                                    System.Drawing.Font  ui)
        {
            parent.BackColor = paneBg;
            parent.Padding   = new System.Windows.Forms.Padding(8);

            // --- Cheat Engine-style "Memory Scan" frame -------------------
            this.grpMemoryScan.Text      = "Memory Scan";
            this.grpMemoryScan.Font      = ui;
            this.grpMemoryScan.ForeColor = text;
            this.grpMemoryScan.BackColor = paneBg;
            this.grpMemoryScan.Dock     = System.Windows.Forms.DockStyle.Top;
            this.grpMemoryScan.Height   = 156;
            this.grpMemoryScan.Padding  = new System.Windows.Forms.Padding(8, 4, 8, 8);

            // --- top buttons: First Scan / Next Scan / Undo Scan ----------
            this.btnFirstScan.Text       = "First Scan";
            this.btnFirstScan.Location   = new System.Drawing.Point(8, 22);
            this.btnFirstScan.Size       = new System.Drawing.Size(100, 28);
            this.btnFirstScan.FlatStyle  = System.Windows.Forms.FlatStyle.Flat;
            this.btnFirstScan.BackColor  = AtlasTheme.Accent;
            this.btnFirstScan.ForeColor  = System.Drawing.Color.White;
            this.btnFirstScan.FlatAppearance.BorderColor = AtlasTheme.AccentDim;
            this.btnFirstScan.Cursor     = System.Windows.Forms.Cursors.Hand;
            this.btnFirstScan.Click     += new System.EventHandler(this.OnFirstScanClicked);

            this.btnNextScan.Text        = "Next Scan";
            this.btnNextScan.Location    = new System.Drawing.Point(116, 22);
            this.btnNextScan.Size        = new System.Drawing.Size(100, 28);
            this.btnNextScan.Enabled     = false;
            this.btnNextScan.FlatStyle   = System.Windows.Forms.FlatStyle.Flat;
            this.btnNextScan.BackColor   = AtlasTheme.SurfaceRaised;
            this.btnNextScan.ForeColor   = text;
            this.btnNextScan.FlatAppearance.BorderColor = AtlasTheme.Border;
            this.btnNextScan.Cursor      = System.Windows.Forms.Cursors.Hand;
            this.btnNextScan.Click      += new System.EventHandler(this.OnNextScanClicked);

            this.btnUndoScan.Text        = "Undo Scan";
            this.btnUndoScan.Location    = new System.Drawing.Point(224, 22);
            this.btnUndoScan.Size        = new System.Drawing.Size(100, 28);
            this.btnUndoScan.Enabled     = false;
            this.btnUndoScan.FlatStyle   = System.Windows.Forms.FlatStyle.Flat;
            this.btnUndoScan.BackColor   = AtlasTheme.SurfaceRaised;
            this.btnUndoScan.ForeColor   = text;
            this.btnUndoScan.FlatAppearance.BorderColor = AtlasTheme.Border;
            this.btnUndoScan.Cursor      = System.Windows.Forms.Cursors.Hand;
            this.btnUndoScan.Click      += new System.EventHandler(this.OnUndoScanClicked);

            // --- Value row ------------------------------------------------
            this.hexCheck.Text         = "Hex";
            this.hexCheck.Location     = new System.Drawing.Point(8, 58);
            this.hexCheck.Size         = new System.Drawing.Size(48, 22);
            this.hexCheck.ForeColor    = text;
            this.hexCheck.FlatStyle    = System.Windows.Forms.FlatStyle.Flat;

            this.lblValue.Text         = "Scan value:";
            this.lblValue.Location     = new System.Drawing.Point(56, 60);
            this.lblValue.AutoSize     = true;
            this.lblValue.ForeColor    = text;

            this.scanValueBox.Location = new System.Drawing.Point(112, 56);
            this.scanValueBox.Size     = new System.Drawing.Size(212, 22);
            this.scanValueBox.BackColor = deepBg;
            this.scanValueBox.ForeColor = text;
            this.scanValueBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.scanValueBox.Font     = new System.Drawing.Font("Consolas", 9F);

            this.lblBetween.Text       = "..";
            this.lblBetween.AutoSize   = true;
            this.lblBetween.ForeColor  = text;
            this.lblBetween.Visible    = false;

            this.scanValueBox2.Location = new System.Drawing.Point(0, 0);
            this.scanValueBox2.Size     = new System.Drawing.Size(110, 22);
            this.scanValueBox2.BackColor = deepBg;
            this.scanValueBox2.ForeColor = text;
            this.scanValueBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.scanValueBox2.Font     = new System.Drawing.Font("Consolas", 9F);
            this.scanValueBox2.Visible  = false;

            // --- Scan Type / Value Type rows -----------------------------
            this.lblScanType.Text     = "Scan Type:";
            this.lblScanType.Location = new System.Drawing.Point(8, 90);
            this.lblScanType.AutoSize = true;
            this.lblScanType.ForeColor = text;

            this.scanTypeCombo.Location = new System.Drawing.Point(96, 86);
            this.scanTypeCombo.Size     = new System.Drawing.Size(160, 24);
            this.scanTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.scanTypeCombo.BackColor = deepBg;
            this.scanTypeCombo.ForeColor = text;
            this.scanTypeCombo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.scanTypeCombo.Items.AddRange(new object[] {
                "Exact Value", "Bigger than...", "Smaller than...", "Value between...",
                "Unknown initial value" });
            this.scanTypeCombo.SelectedIndex = 0;
            this.scanTypeCombo.SelectedIndexChanged += new System.EventHandler(this.OnScanTypeChanged);

            this.luaCheck.Text      = "Lua formula";
            this.luaCheck.Location  = new System.Drawing.Point(260, 86);
            this.luaCheck.Size      = new System.Drawing.Size(96, 22);
            this.luaCheck.ForeColor = text;
            this.luaCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.notCheck.Text      = "Not";
            this.notCheck.Location  = new System.Drawing.Point(260, 106);
            this.notCheck.Size      = new System.Drawing.Size(60, 22);
            this.notCheck.ForeColor = text;
            this.notCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.lblValueType.Text     = "Value Type:";
            this.lblValueType.Location = new System.Drawing.Point(8, 118);
            this.lblValueType.AutoSize = true;
            this.lblValueType.ForeColor = text;

            this.valueTypeCombo.Location = new System.Drawing.Point(96, 114);
            this.valueTypeCombo.Size     = new System.Drawing.Size(160, 24);
            this.valueTypeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.valueTypeCombo.BackColor = deepBg;
            this.valueTypeCombo.ForeColor = text;
            this.valueTypeCombo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.valueTypeCombo.Items.AddRange(new object[] {
                "Byte", "2 Bytes", "4 Bytes", "8 Bytes",
                "Float", "Double", "String", "Array of byte" });
            this.valueTypeCombo.SelectedIndex = 2;

            this.grpMemoryScan.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnFirstScan, this.btnNextScan, this.btnUndoScan,
                this.hexCheck, this.lblValue, this.scanValueBox, this.lblBetween, this.scanValueBox2,
                this.lblScanType, this.scanTypeCombo, this.luaCheck, this.notCheck,
                this.lblValueType, this.valueTypeCombo });

            // --- Memory Scan Options group -------------------------------
            this.grpMemOptions.Text      = "Memory Scan Options";
            this.grpMemOptions.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.grpMemOptions.MinimumSize = new System.Drawing.Size(200, 220);
            this.grpMemOptions.ForeColor = text;
            this.grpMemOptions.BackColor = paneBg;
            this.grpMemOptions.Padding   = new System.Windows.Forms.Padding(8, 4, 8, 8);

            this.regionCombo.Location = new System.Drawing.Point(12, 24);
            this.regionCombo.Size     = new System.Drawing.Size(304, 24);
            this.regionCombo.Anchor   = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.regionCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.regionCombo.BackColor = deepBg;
            this.regionCombo.ForeColor = text;
            this.regionCombo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.regionCombo.Items.AddRange(new object[] {
                "All", "User Memory (0x80000000-0x9FFFFFFF)",
                "Title Heap (0xC0000000-0xCFFFFFFF)", "Kernel (0x80000000-0x80FFFFFF)",
                "Title Physical (0xC2000000-0xE0000000)", "Selected Module" });
            this.regionCombo.SelectedIndex = 2;
            this.regionCombo.SelectedIndexChanged += new System.EventHandler(this.OnRegionChanged);

            this.lblStart.Text     = "Start";
            this.lblStart.Location = new System.Drawing.Point(12, 56);
            this.lblStart.AutoSize = true;
            this.lblStart.ForeColor = text;

            this.startBox.Location = new System.Drawing.Point(80, 54);
            this.startBox.Size     = new System.Drawing.Size(236, 22);
            this.startBox.Anchor   = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.startBox.Text     = "C0000000";
            this.startBox.BackColor = deepBg;
            this.startBox.ForeColor = text;
            this.startBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.startBox.Font     = new System.Drawing.Font("Consolas", 9F);

            this.lblStop.Text      = "Stop";
            this.lblStop.Location  = new System.Drawing.Point(12, 80);
            this.lblStop.AutoSize  = true;
            this.lblStop.ForeColor = text;

            this.stopBox.Location  = new System.Drawing.Point(80, 78);
            this.stopBox.Size      = new System.Drawing.Size(236, 22);
            this.stopBox.Anchor   = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.stopBox.Text      = "CFFFFFFF";
            this.stopBox.BackColor = deepBg;
            this.stopBox.ForeColor = text;
            this.stopBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.stopBox.Font      = new System.Drawing.Font("Consolas", 9F);

            this.cbWritable.Text      = "Writable";
            this.cbWritable.Location  = new System.Drawing.Point(12, 106);
            this.cbWritable.AutoSize  = true;
            this.cbWritable.Checked   = true;
            this.cbWritable.ForeColor = text;
            this.cbWritable.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbExecutable.Text     = "Executable";
            this.cbExecutable.Location = new System.Drawing.Point(208, 106);
            this.cbExecutable.AutoSize = true;
            this.cbExecutable.Checked  = true;
            this.cbExecutable.ForeColor = text;
            this.cbExecutable.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbCopyOnWrite.Text     = "CopyOnWrite";
            this.cbCopyOnWrite.Location = new System.Drawing.Point(12, 128);
            this.cbCopyOnWrite.AutoSize = true;
            this.cbCopyOnWrite.ForeColor = text;
            this.cbCopyOnWrite.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbActiveOnly.Text     = "Active memory only";
            this.cbActiveOnly.Location = new System.Drawing.Point(12, 150);
            this.cbActiveOnly.AutoSize = true;
            this.cbActiveOnly.ForeColor = text;
            this.cbActiveOnly.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbFastScan.Text     = "Fast Scan";
            this.cbFastScan.Location = new System.Drawing.Point(12, 172);
            this.cbFastScan.AutoSize = true;
            this.cbFastScan.Checked  = true;
            this.cbFastScan.ForeColor = text;
            this.cbFastScan.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.alignmentBox.Location  = new System.Drawing.Point(94, 170);
            this.alignmentBox.Size      = new System.Drawing.Size(30, 22);
            this.alignmentBox.Text      = "4";
            this.alignmentBox.BackColor = deepBg;
            this.alignmentBox.ForeColor = text;
            this.alignmentBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            this.rbAlignment.Text      = "Alignment";
            this.rbAlignment.Location  = new System.Drawing.Point(140, 170);
            this.rbAlignment.AutoSize  = true;
            this.rbAlignment.Checked   = true;
            this.rbAlignment.ForeColor = text;
            this.rbAlignment.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.rbLastDigits.Text      = "Last Digits";
            this.rbLastDigits.Location  = new System.Drawing.Point(220, 170);
            this.rbLastDigits.AutoSize  = true;
            this.rbLastDigits.ForeColor = text;
            this.rbLastDigits.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbPauseScan.Text     = "Pause the game while scanning";
            this.cbPauseScan.Location = new System.Drawing.Point(12, 196);
            this.cbPauseScan.AutoSize = true;
            this.cbPauseScan.ForeColor = text;
            this.cbPauseScan.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbReDump.Text       = "Re-Dump on Next Scan";
            this.cbReDump.Location   = new System.Drawing.Point(12, 220);
            this.cbReDump.AutoSize   = true;
            this.cbReDump.Checked    = true;
            this.cbReDump.ForeColor  = text;
            this.cbReDump.FlatStyle  = System.Windows.Forms.FlatStyle.Flat;

            this.grpMemOptions.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.regionCombo, this.lblStart, this.startBox, this.lblStop, this.stopBox,
                this.cbWritable, this.cbExecutable, this.cbCopyOnWrite, this.cbActiveOnly,
                this.cbFastScan, this.alignmentBox, this.rbAlignment, this.rbLastDigits, this.cbPauseScan, this.cbReDump });

            // --- bottom strip (CE-style extras) ----------------------------
            this.bottomScanExtras.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomScanExtras.Height = 40;
            this.bottomScanExtras.AutoSize = false;
            this.bottomScanExtras.Padding = new System.Windows.Forms.Padding(4, 6, 4, 4);
            this.bottomScanExtras.Margin = new System.Windows.Forms.Padding(0);
            this.bottomScanExtras.BackColor = paneBg;
            this.bottomScanExtras.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.bottomScanExtras.WrapContents = false;

            this.cbUnrandom.Text      = "Unrandomizer";
            this.cbUnrandom.AutoSize  = true;
            this.cbUnrandom.Margin    = new System.Windows.Forms.Padding(4, 4, 16, 0);
            this.cbUnrandom.ForeColor = text;
            this.cbUnrandom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.cbSpeedhack.Text      = "Enable Speedhack";
            this.cbSpeedhack.AutoSize  = true;
            this.cbSpeedhack.Margin    = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.cbSpeedhack.ForeColor = text;
            this.cbSpeedhack.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            this.bottomScanExtras.Controls.Add(this.cbUnrandom);
            this.bottomScanExtras.Controls.Add(this.cbSpeedhack);

            parent.Controls.Add(this.grpMemOptions);
            parent.Controls.Add(this.grpMemoryScan);
            parent.Controls.Add(this.bottomScanExtras);
        }

        /// <summary>Monitor + magnifying glass (Cheat Engine "open process").</summary>
        private static System.Drawing.Image MakeOpenProcessToolbarIcon(int size)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float w = size * 0.58f, h = size * 0.42f, x = 2, y = size * 0.18f;
                using (var scr = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(70, 130, 210)))
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 210, 210), 1.2f))
                {
                    g.FillRectangle(scr, x + 1, y + 1, w - 2, h - 4);
                    g.DrawRectangle(pen, x, y, w, h);
                    g.FillRectangle(System.Drawing.Brushes.Gray, x + w * 0.38f, y + h - 1, w * 0.24f, size * 0.12f);
                }
                float cx = size * 0.64f, cy = size * 0.22f, rad = size * 0.16f;
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.8f))
                {
                    g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                    g.DrawLine(pen, cx + rad * 0.7f, cy + rad * 0.7f, size - 2, size - 2);
                }
            }
            return bmp;
        }

        /// <summary>Yellow folder icon.</summary>
        private static System.Drawing.Image MakeFolderToolbarIcon(int size)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float tab = size * 0.28f, h = size * 0.52f, y = size * 0.22f;
                using (var top = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(230, 200, 90)))
                using (var body = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(210, 175, 55)))
                using (var edge = new System.Drawing.Pen(System.Drawing.Color.FromArgb(120, 90, 30), 1f))
                {
                    g.FillRectangle(top, 3, y, tab + 2, tab * 0.55f);
                    g.FillRectangle(body, 3, y + tab * 0.45f, size - 6, h);
                    g.DrawRectangle(edge, 3, y + tab * 0.45f, size - 6, h);
                }
            }
            return bmp;
        }

        /// <summary>Floppy-disk icon.</summary>
        private static System.Drawing.Image MakeFloppyToolbarIcon(int size)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float x = size * 0.22f, y = size * 0.12f, w = size * 0.56f, h = size * 0.72f;
                using (var shell = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(95, 125, 175)))
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 55, 80), 1f))
                {
                    g.FillRectangle(shell, x, y, w, h);
                    g.DrawRectangle(pen, x, y, w, h);
                    g.FillRectangle(System.Drawing.Brushes.Black, x + w * 0.15f, y + 2, w * 0.7f, h * 0.22f);
                    g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(70, 110, 180)), x + w * 0.2f, y + h * 0.38f, w * 0.6f, h * 0.12f);
                }
            }
            return bmp;
        }

        /// <summary>Blue circular "E" mark inspired by Cheat Engine's branding.</summary>
        private static System.Drawing.Image MakeCheatEngineStyleLogo(int size)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float pad = 1, d = size - pad * 2;
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new System.Drawing.RectangleF(pad, pad, d, d),
                    System.Drawing.Color.FromArgb(40, 140, 230),
                    System.Drawing.Color.FromArgb(20, 70, 160),
                    45f))
                    g.FillEllipse(br, pad, pad, d, d);
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 220, 255), size * 0.08f))
                {
                    float cx = size * 0.5f, cy = size * 0.5f, ex = size * 0.22f;
                    g.DrawArc(pen, cx - ex, cy - ex, ex * 2, ex * 2, 20, 300);
                }
                using (var w = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                {
                    float lx = size * 0.32f, ty = size * 0.32f, bar = size * 0.1f, gap = size * 0.08f;
                    g.FillRectangle(w, lx, ty, size * 0.36f, bar * 0.55f);
                    g.FillRectangle(w, lx, ty + gap + bar * 0.55f, size * 0.28f, bar * 0.55f);
                    g.FillRectangle(w, lx, ty + (gap + bar * 0.55f) * 2, size * 0.32f, bar * 0.55f);
                    g.FillRectangle(w, lx, ty, bar * 0.55f, size * 0.36f);
                }
            }
            return bmp;
        }

        /// <summary>Red circle with diagonal bar (Cheat Engine divider glyph).</summary>
        private static System.Drawing.Image MakeNoEntryGlyph(int size)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float pad = size * 0.08f, d = size - pad * 2;
                using (var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 55, 55)))
                using (var edge = new System.Drawing.Pen(System.Drawing.Color.FromArgb(140, 20, 20), size * 0.06f))
                {
                    g.FillEllipse(fill, pad, pad, d, d);
                    g.DrawEllipse(edge, pad, pad, d, d);
                }
                using (var bar = new System.Drawing.Pen(System.Drawing.Color.White, size * 0.09f))
                {
                    bar.StartCap = bar.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    float m = size * 0.28f;
                    g.DrawLine(bar, m, m, size - m, size - m);
                }
            }
            return bmp;
        }

        #endregion

        // -- root containers --------------------------------------------------
        private System.Windows.Forms.MenuStrip            menuStrip1;
        private System.Windows.Forms.ToolStrip            toolStrip1;
        private System.Windows.Forms.Label                foundLabel;
        private System.Windows.Forms.SplitContainer       splitMain;
        private System.Windows.Forms.Panel                midBar;
        private System.Windows.Forms.ListView             savedListView;
        private System.Windows.Forms.StatusStrip          statusStrip1;

        // -- menu items -------------------------------------------------------
        private System.Windows.Forms.ToolStripMenuItem    menuFile;
        private System.Windows.Forms.ToolStripMenuItem    menuFileOpenProcess;
        private System.Windows.Forms.ToolStripMenuItem    menuFileDisconnect;
        private System.Windows.Forms.ToolStripMenuItem    menuFileDiscover;
        private System.Windows.Forms.ToolStripSeparator   menuFileSep1;
        private System.Windows.Forms.ToolStripMenuItem    menuFileExit;
        private System.Windows.Forms.ToolStripMenuItem    menuEdit;
        private System.Windows.Forms.ToolStripMenuItem    menuEditMemoryView;
        private System.Windows.Forms.ToolStripMenuItem    menuEditWalkMemory;
        private System.Windows.Forms.ToolStripMenuItem    menuEditPointerScan;
        private System.Windows.Forms.ToolStripMenuItem    menuEditBreakpoints;
        private System.Windows.Forms.ToolStripMenuItem    menuEditController;
        private System.Windows.Forms.ToolStripMenuItem    menuEditQuickFind;
        private System.Windows.Forms.ToolStripMenuItem    menuEditMemoryWatch;
        private System.Windows.Forms.ToolStripMenuItem    menuEditSettings;
        private System.Windows.Forms.ToolStripMenuItem    menuTable;
        private System.Windows.Forms.ToolStripMenuItem    menuTableAdd;
        private System.Windows.Forms.ToolStripMenuItem    menuTableClear;
        private System.Windows.Forms.ToolStripMenuItem    menuLive;
        private System.Windows.Forms.ToolStripMenuItem    menuLiveProfiles;
        private System.Windows.Forms.ToolStripMenuItem    menuLiveQuickSignIn;
        private System.Windows.Forms.ToolStripMenuItem    menuLiveFriends;
        private System.Windows.Forms.ToolStripMenuItem    menuLiveParty;
        private System.Windows.Forms.ToolStripMenuItem    menuLiveGuide;
        private System.Windows.Forms.ToolStripMenuItem    menuD3D;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DScreenshot;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DLiveView;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DNotify;
        private System.Windows.Forms.ToolStripSeparator   menuD3DSep1;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DRebootCold;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DRebootWarm;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DShutdown;
        private System.Windows.Forms.ToolStripSeparator   menuD3DSep2;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DStopGo;
        private System.Windows.Forms.ToolStripMenuItem    menuD3DDump;
        private System.Windows.Forms.ToolStripMenuItem    menuHelp;
        private System.Windows.Forms.ToolStripMenuItem    menuHelpAbout;

        // -- toolStrip items --------------------------------------------------
        private System.Windows.Forms.ToolStripButton      tbConnect;
        private System.Windows.Forms.ToolStripButton      tbOpenTable;
        private System.Windows.Forms.ToolStripButton      tbSaveTable;
        private System.Windows.Forms.ToolStripButton      tbWalkMem;
        private System.Windows.Forms.ToolStripButton      tbPointerScan;
        private System.Windows.Forms.Panel                tbProcessBox;
        private System.Windows.Forms.Label                tbProcessLabel;
        private System.Windows.Forms.ToolStripControlHost tbProcessHost;
        private System.Windows.Forms.ToolStripLabel       tbSpring;
        private System.Windows.Forms.ToolStripLabel       tbLogo;

        // -- left side: results ----------------------------------------------
        private System.Windows.Forms.ListView             resultsListView;
        private System.Windows.Forms.ColumnHeader         colAddress;
        private System.Windows.Forms.ColumnHeader         colValue;
        private System.Windows.Forms.ColumnHeader         colPrevious;

        // -- right side: scan controls ---------------------------------------
        private System.Windows.Forms.Button               btnFirstScan;
        private System.Windows.Forms.Button               btnNextScan;
        private System.Windows.Forms.Button               btnUndoScan;
        private System.Windows.Forms.CheckBox             hexCheck;
        private System.Windows.Forms.Label                lblValue;
        private System.Windows.Forms.TextBox              scanValueBox;
        private System.Windows.Forms.Label                lblBetween;
        private System.Windows.Forms.TextBox              scanValueBox2;
        private System.Windows.Forms.Label                lblScanType;
        private System.Windows.Forms.ComboBox             scanTypeCombo;
        private System.Windows.Forms.CheckBox             luaCheck;
        private System.Windows.Forms.CheckBox             notCheck;
        private System.Windows.Forms.Label                lblValueType;
        private System.Windows.Forms.ComboBox             valueTypeCombo;
        private System.Windows.Forms.GroupBox             grpMemoryScan;
        private System.Windows.Forms.GroupBox             grpMemOptions;
        private System.Windows.Forms.FlowLayoutPanel      bottomScanExtras;
        private System.Windows.Forms.ComboBox             regionCombo;
        private System.Windows.Forms.Label                lblStart;
        private System.Windows.Forms.TextBox              startBox;
        private System.Windows.Forms.Label                lblStop;
        private System.Windows.Forms.TextBox              stopBox;
        private System.Windows.Forms.CheckBox             cbWritable;
        private System.Windows.Forms.CheckBox             cbExecutable;
        private System.Windows.Forms.CheckBox             cbCopyOnWrite;
        private System.Windows.Forms.CheckBox             cbActiveOnly;
        private System.Windows.Forms.CheckBox             cbFastScan;
        private System.Windows.Forms.CheckBox             cbReDump;
        private System.Windows.Forms.TextBox              alignmentBox;
        private System.Windows.Forms.RadioButton          rbAlignment;
        private System.Windows.Forms.RadioButton          rbLastDigits;
        private System.Windows.Forms.CheckBox             cbPauseScan;
        private System.Windows.Forms.CheckBox             cbUnrandom;
        private System.Windows.Forms.CheckBox             cbSpeedhack;

        // -- midBar -----------------------------------------------------------
        private System.Windows.Forms.Button               btnMemoryView;
        private System.Windows.Forms.Button               btnAddAddress;
        private System.Windows.Forms.PictureBox           lblScanIcon;

        // -- saved addresses --------------------------------------------------
        private System.Windows.Forms.ColumnHeader         colActive;
        private System.Windows.Forms.ColumnHeader         colDescription;
        private System.Windows.Forms.ColumnHeader         colSavedAddress;
        private System.Windows.Forms.ColumnHeader         colType;
        private System.Windows.Forms.ColumnHeader         colSavedValue;

        // -- status strip -----------------------------------------------------
        private System.Windows.Forms.ToolStripStatusLabel statusAdvanced;
        private System.Windows.Forms.ToolStripStatusLabel statusSpring;
        private System.Windows.Forms.ToolStripStatusLabel statusWire;
        private System.Windows.Forms.ProgressBar          scanProgress;
        private System.Windows.Forms.ToolStripStatusLabel statusTable;
    }
}
