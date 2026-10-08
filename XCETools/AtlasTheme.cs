using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XCETools
{
    /// <summary>Shared visual language for XCE Atlas — dark shell, Xbox green accents, CE-style density.</summary>
    internal static class AtlasTheme
    {
        // Surfaces
        public static readonly Color Window       = Color.FromArgb(24, 24, 27);
        public static readonly Color Surface      = Color.FromArgb(32, 32, 36);
        public static readonly Color SurfaceRaised = Color.FromArgb(42, 42, 48);
        public static readonly Color Input        = Color.FromArgb(18, 18, 20);
        public static readonly Color Border       = Color.FromArgb(58, 58, 66);
        public static readonly Color BorderFocus  = Color.FromArgb(16, 124, 16);

        // Text
        public static readonly Color Text         = Color.FromArgb(232, 232, 236);
        public static readonly Color TextMuted    = Color.FromArgb(158, 158, 168);
        public static readonly Color TextAccent   = Color.FromArgb(120, 220, 120);

        // Xbox green family
        public static readonly Color Accent       = Color.FromArgb(16, 124, 16);
        public static readonly Color AccentHover  = Color.FromArgb(22, 150, 22);
        public static readonly Color AccentDim    = Color.FromArgb(12, 72, 12);
        public static readonly Color AccentGlow   = Color.FromArgb(40, 180, 70);

        // Semantic
        public static readonly Color Danger       = Color.FromArgb(200, 58, 58);
        public static readonly Color Warning      = Color.FromArgb(210, 145, 45);
        public static readonly Color Success      = AccentGlow;

        private static Font _uiFont;
        private static Font _uiFontSemibold;
        private static Font _codeFont;

        /// <summary>Segoe UI — lazy so the WinForms designer does not block on font linking.</summary>
        public static Font UiFont =>
            _uiFont ?? (_uiFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point));

        /// <summary>Bold Segoe UI (avoids a separate “Segoe UI Semibold” family lookup in the designer).</summary>
        public static Font UiFontSemibold =>
            _uiFontSemibold ?? (_uiFontSemibold = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point));

        /// <summary>Monospace for hex/addresses; Consolas in the designer, Cascadia Mono at runtime when installed.</summary>
        public static Font CodeFont()
        {
            if (_codeFont != null) return _codeFont;
            if (IsDesignTime)
                return _codeFont = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

            try
            {
                using (var fam = new FontFamily("Cascadia Mono"))
                    return _codeFont = new Font(fam, 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return _codeFont = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private static bool IsDesignTime =>
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        public static void StyleForm(Form f)
        {
            f.BackColor = Window;
            f.ForeColor = Text;
            f.Font = UiFont;
        }

        public static void StyleMenu(ToolStrip menu)
        {
            menu.BackColor = Surface;
            menu.ForeColor = Text;
            menu.Font = UiFont;
            menu.Renderer = new ToolStripProfessionalRenderer(new AtlasColorTable());
        }

        public static void StyleToolStrip(ToolStrip ts)
        {
            ts.BackColor = Surface;
            ts.ForeColor = Text;
            ts.Font = UiFont;
            ts.Renderer = new ToolStripProfessionalRenderer(new AtlasColorTable());
        }

        public static void StyleStatusStrip(StatusStrip strip)
        {
            strip.BackColor = Surface;
            strip.ForeColor = TextMuted;
            strip.Font = UiFont;
            strip.Renderer = new ToolStripProfessionalRenderer(new AtlasColorTable());
        }

        public static void StyleFlatButton(Button btn, bool primary = false)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = UiFont;
            btn.Cursor = Cursors.Hand;
            if (primary)
            {
                btn.BackColor = Accent;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = AccentDim;
                btn.FlatAppearance.MouseOverBackColor = AccentHover;
                btn.FlatAppearance.MouseDownBackColor = AccentDim;
            }
            else
            {
                btn.BackColor = SurfaceRaised;
                btn.ForeColor = Text;
                btn.FlatAppearance.BorderColor = Border;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 58);
                btn.FlatAppearance.MouseDownBackColor = Surface;
            }
        }

        public static void StyleInput(TextBox tb)
        {
            tb.BackColor = Input;
            tb.ForeColor = Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = CodeFont();
        }

        public static void StyleCombo(ComboBox cb)
        {
            cb.BackColor = Input;
            cb.ForeColor = Text;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = UiFont;
        }

        public static void StyleCheck(CheckBox cb)
        {
            cb.ForeColor = Text;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = UiFont;
        }

        public static void StyleGroup(GroupBox gb)
        {
            gb.ForeColor = TextAccent;
            gb.BackColor = Surface;
            gb.Font = UiFontSemibold;
        }

        public static void StyleListView(ListView lv)
        {
            lv.BackColor = Input;
            lv.ForeColor = Text;
            lv.BorderStyle = BorderStyle.None;
            lv.Font = CodeFont();
        }

        public static void StyleHeaderLabel(Label lbl, string prefix = null)
        {
            lbl.BackColor = Surface;
            lbl.ForeColor = TextMuted;
            lbl.Font = UiFontSemibold;
            if (!string.IsNullOrEmpty(prefix) && !lbl.Text.StartsWith(prefix, System.StringComparison.Ordinal))
                lbl.Text = prefix + lbl.Text;
        }

        public static void StyleFoundBadge(Label lbl)
        {
            lbl.BackColor = Surface;
            lbl.ForeColor = TextAccent;
            lbl.Font = UiFontSemibold;
            lbl.Padding = new Padding(10, 4, 0, 0);
        }

        public static void StyleProcessChip(Label lbl, bool connected)
        {
            lbl.BackColor = Input;
            lbl.ForeColor = connected ? TextAccent : TextMuted;
            lbl.Font = UiFont;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Padding = new Padding(8, 0, 8, 0);
        }

        public static void PaintAccentBar(PaintEventArgs e, Rectangle bounds, int thickness = 3)
        {
            using var brush = new LinearGradientBrush(
                bounds,
                AccentGlow,
                Accent,
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
        }

        public static Image LoadAppIcon(int size)
        {
            if (IsDesignTime)
                return MakeFallbackLogo(size);

            try
            {
                string path = System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "xbox-cheat-engine-icon.png");
                if (System.IO.File.Exists(path))
                {
                    using var src = Image.FromFile(path);
                    return new Bitmap(src, new Size(size, size));
                }
            }
            catch { /* fallback below */ }

            return MakeFallbackLogo(size);
        }

        private static Image MakeFallbackLogo(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float pad = size * 0.06f;
                using (var br = new LinearGradientBrush(
                    new RectangleF(pad, pad, size - pad * 2, size - pad * 2),
                    AccentGlow, Accent, 45f))
                    g.FillEllipse(br, pad, pad, size - pad * 2, size - pad * 2);
                using (var pen = new Pen(Color.White, size * 0.07f))
                {
                    float cx = size * 0.5f, cy = size * 0.52f, r = size * 0.22f;
                    g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 200, 220);
                }
            }
            return bmp;
        }

        public static ToolStripRenderer CreateRenderer()
            => new ToolStripProfessionalRenderer(new AtlasColorTable());

        private sealed class AtlasColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Border;
            public override Color MenuItemSelected => SurfaceRaised;
            public override Color MenuItemSelectedGradientBegin => SurfaceRaised;
            public override Color MenuItemSelectedGradientEnd => SurfaceRaised;
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color MenuStripGradientBegin => Surface;
            public override Color MenuStripGradientEnd => Surface;
            public override Color ToolStripGradientBegin => Surface;
            public override Color ToolStripGradientEnd => Surface;
            public override Color ToolStripBorder => Border;
            public override Color ButtonSelectedHighlight => SurfaceRaised;
            public override Color ButtonPressedHighlight => Color.FromArgb(58, 58, 66);
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }
    }
}
