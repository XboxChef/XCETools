// =============================================================================
// XCE Atlas - DumpForm
// =============================================================================
// Three-preset memory dump picker patterned after the tiny "Dump" dialog from
// classic Xbox 360 cheat tooling.  Each preset slots into the same Start/Length
// boxes below it so users can still tweak before hitting Dump.
//
//   Physical RAM             0xC0000000  +0x1FFF0FFF
//   Base File / Image        0x82000000  +0x05000000
//   Allocated Data / Virtual 0x40000000  +0x05000000
//
// The actual transfer is handled by Form1.DumpRangeWithProgressAsync so the
// status-strip progress bar continues to work.
// =============================================================================

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class DumpForm : Form
    {
        // -- preset address ranges -------------------------------------------
        // These are the three canonical Xbox 360 dump windows; matches the
        // labelled buttons in the original dump-tool screenshot.

        public static readonly DumpPreset PhysicalRam =
            new DumpPreset("Physical RAM",             0xC0000000u, 0x1FFF0FFFu);
        public static readonly DumpPreset BaseFile =
            new DumpPreset("Base File / Image",        0x82000000u, 0x05000000u);
        public static readonly DumpPreset Virtual =
            new DumpPreset("Allocated Data / Virtual", 0x40000000u, 0x05000000u);

        // -- result ----------------------------------------------------------
        public uint StartAddress { get; private set; }
        public uint Length        { get; private set; }
        public string PresetName  { get; private set; }

        // -- controls --------------------------------------------------------
        private readonly Button   _btnPhysical = MakePresetButton(PhysicalRam.Name);
        private readonly Button   _btnBase     = MakePresetButton(BaseFile.Name);
        private readonly Button   _btnVirtual  = MakePresetButton(Virtual.Name);
        private readonly TextBox  _startBox    = MakeHexBox("0C000000");
        private readonly TextBox  _lengthBox   = MakeHexBox("01FFF0FFF");
        private readonly Button   _btnDump     = new Button
        {
            Text       = "Dump",
            Enabled    = false,
            FlatStyle  = FlatStyle.Flat,
            ForeColor  = Color.WhiteSmoke,
            BackColor  = Color.FromArgb(63, 63, 70),
        };

        public DumpForm()
        {
            Text             = "Dump";
            FormBorderStyle  = FormBorderStyle.FixedDialog;
            StartPosition    = FormStartPosition.CenterParent;
            ClientSize       = new Size(260, 230);
            MaximizeBox      = false;
            MinimizeBox      = false;
            BackColor        = Color.FromArgb(45, 45, 48);
            ForeColor        = Color.WhiteSmoke;
            Font             = new Font("Segoe UI", 9F);

            // -- preset buttons (stacked vertically across the top) ----------
            LayoutButton(_btnPhysical, 12,  10);
            LayoutButton(_btnBase,     12,  42);
            LayoutButton(_btnVirtual,  12,  74);

            _btnPhysical.Click += (_, __) => ApplyPreset(PhysicalRam);
            _btnBase.Click     += (_, __) => ApplyPreset(BaseFile);
            _btnVirtual.Click  += (_, __) => ApplyPreset(Virtual);

            // -- hex inputs --------------------------------------------------
            Controls.Add(new Label
            {
                Left = 12, Top = 116, AutoSize = true,
                Text = "Starting Offset 0x:", ForeColor = Color.WhiteSmoke,
            });
            _startBox.Left  = 130; _startBox.Top  = 113; _startBox.Width = 116;
            Controls.Add(_startBox);

            Controls.Add(new Label
            {
                Left = 12, Top = 144, AutoSize = true,
                Text = "Dump Length 0x:", ForeColor = Color.WhiteSmoke,
            });
            _lengthBox.Left = 130; _lengthBox.Top = 141; _lengthBox.Width = 116;
            Controls.Add(_lengthBox);

            // -- Dump button -------------------------------------------------
            _btnDump.Left   = 12;
            _btnDump.Top    = 180;
            _btnDump.Width  = 236;
            _btnDump.Height = 32;
            _btnDump.Click += (_, __) => OnDump();
            Controls.Add(_btnDump);

            _startBox.TextChanged  += (_, __) => Revalidate();
            _lengthBox.TextChanged += (_, __) => Revalidate();

            // Pick "Physical RAM" by default to match the screenshot's
            // highlighted state when the dialog first opens.
            ApplyPreset(PhysicalRam);
            AcceptButton = _btnDump;
        }

        // ------------------------------------------------------------------
        //  Behavior
        // ------------------------------------------------------------------

        private void ApplyPreset(DumpPreset preset)
        {
            PresetName     = preset.Name;
            _startBox.Text  = preset.Start.ToString("X", CultureInfo.InvariantCulture);
            _lengthBox.Text = preset.Length.ToString("X", CultureInfo.InvariantCulture);
            Revalidate();

            // Visually mark the active preset (subtle blue tint, matches the
            // selected button in the source screenshot).
            HighlightSelected(preset);
        }

        private void HighlightSelected(DumpPreset preset)
        {
            Color sel  = Color.FromArgb(0, 102, 204);
            Color norm = Color.FromArgb(63, 63, 70);
            _btnPhysical.BackColor = preset == PhysicalRam ? sel : norm;
            _btnBase.BackColor     = preset == BaseFile    ? sel : norm;
            _btnVirtual.BackColor  = preset == Virtual     ? sel : norm;
        }

        private void Revalidate()
        {
            bool ok = TryParseHex(_startBox.Text,  out uint _)
                   && TryParseHex(_lengthBox.Text, out uint len)
                   && len > 0;
            _btnDump.Enabled = ok;
        }

        private void OnDump()
        {
            if (!TryParseHex(_startBox.Text,  out uint s) ||
                !TryParseHex(_lengthBox.Text, out uint l) || l == 0)
            {
                MessageBox.Show(this, "Start and length must be valid hex.",
                                "Dump", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            StartAddress = s;
            Length       = l;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        private static Button MakePresetButton(string text) => new Button
        {
            Text      = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.WhiteSmoke,
            BackColor = Color.FromArgb(63, 63, 70),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        private static TextBox MakeHexBox(string defaultText) => new TextBox
        {
            Text        = defaultText,
            BackColor   = Color.FromArgb(30, 30, 30),
            ForeColor   = Color.WhiteSmoke,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 9.5F),
            CharacterCasing = CharacterCasing.Upper,
        };

        private static void LayoutButton(Button b, int left, int top)
        {
            b.Left   = left;
            b.Top    = top;
            b.Width  = 236;
            b.Height = 26;
        }

        public static bool TryParseHex(string s, out uint v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>Read-only descriptor for a dump preset.</summary>
        public sealed class DumpPreset
        {
            public string Name   { get; }
            public uint   Start  { get; }
            public uint   Length { get; }
            public DumpPreset(string name, uint start, uint length)
            { Name = name; Start = start; Length = length; }
        }
    }
}
