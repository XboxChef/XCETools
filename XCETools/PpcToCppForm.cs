// =============================================================================
// PpcToCppForm.cs - View / export PPC disassembly as C++ pseudocode
// =============================================================================
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace XCETools
{
    internal sealed class PpcToCppForm : Form
    {
        private readonly XboxConsole _console;
        private readonly uint _address;
        private readonly uint _byteCount;
        private readonly TextBox _source = new TextBox();
        private readonly TextBox _output = new TextBox();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly string _defaultFuncName;
        private readonly TextBox _nameBox;

        public PpcToCppForm(XboxConsole console, uint address, uint byteCount, string ppcListing, string cppText, string title, string funcName)
        {
            _console = console;
            _address = address;
            _byteCount = byteCount;
            _defaultFuncName = funcName ?? "ppc_snippet";
            Text = title ?? "PPC to C++";
            ClientSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Consolas", 9.5F);

            var top = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = BackColor };
            var lbl = new Label { Text = "Function:", Left = 8, Top = 10, AutoSize = true, ForeColor = Color.WhiteSmoke };
            _nameBox = new TextBox
            {
                Left = 72, Top = 6, Width = 200,
                Text = _defaultFuncName,
                BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.WhiteSmoke,
            };
            var btnRefresh = Btn("Regenerate", 280, 4);
            btnRefresh.Click += (_, __) => Regenerate();
            top.Controls.AddRange(new Control[] { lbl, _nameBox, btnRefresh });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = BackColor };
            var btnCopy = Btn("Copy C++", 8, 6);
            btnCopy.Click += (_, __) =>
            {
                if (!string.IsNullOrEmpty(_output.Text))
                    Clipboard.SetText(_output.Text);
            };
            var btnSave = Btn("Save .cpp…", 100, 6);
            btnSave.Click += (_, __) => SaveCpp();
            var btnCopyHdr = Btn("Copy stub header", 200, 6);
            btnCopyHdr.Click += (_, __) => Clipboard.SetText(PpcToCppStub.Header);
            bottom.Controls.AddRange(new Control[] { btnCopy, btnSave, btnCopyHdr });

            _source.Multiline = true;
            _source.ReadOnly = true;
            _source.ScrollBars = ScrollBars.Both;
            _source.Dock = DockStyle.Fill;
            _source.BackColor = Color.FromArgb(30, 30, 30);
            _source.ForeColor = Color.FromArgb(200, 200, 200);
            _source.Font = Font;
            _source.Text = ppcListing ?? string.Empty;
            _source.WordWrap = false;

            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.ScrollBars = ScrollBars.Both;
            _output.Dock = DockStyle.Fill;
            _output.BackColor = Color.FromArgb(25, 35, 30);
            _output.ForeColor = Color.FromArgb(180, 220, 180);
            _output.Font = Font;
            _output.Text = cppText ?? string.Empty;
            _output.WordWrap = false;

            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.BackColor = BackColor;
            _split.Panel1.BackColor = BackColor;
            _split.Panel2.BackColor = BackColor;
            _split.SplitterDistance = 220;
            _split.Panel1.Controls.Add(_source);
            _split.Panel2.Controls.Add(_output);

            var hdr = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Text = "  Disassembly (input)",
                ForeColor = Color.FromArgb(140, 140, 140),
                BackColor = Color.FromArgb(50, 50, 55),
            };
            var hdr2 = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Text = "  C++ pseudocode (output)",
                ForeColor = Color.FromArgb(140, 140, 140),
                BackColor = Color.FromArgb(50, 50, 55),
            };
            _split.Panel1.Controls.Add(hdr);
            _split.Panel2.Controls.Add(hdr2);

            Controls.Add(_split);
            Controls.Add(bottom);
            Controls.Add(top);
        }

        private void Regenerate()
        {
            if (_console == null || !_console.Connected)
            {
                MessageBox.Show(this, "Console disconnected.", Text);
                return;
            }
            try
            {
                string fn = _nameBox.Text?.Trim();
                if (string.IsNullOrEmpty(fn)) fn = _defaultFuncName;
                byte[] bytes = _console.GetMemory(_address, _byteCount);
                _source.Text = PpcToCppExport.BuildListing(bytes, _address);
                _output.Text = PpcToCpp.TranslateBytes(bytes, _address, fn);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SaveCpp()
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "C++ source (*.cpp)|*.cpp|Header (*.h)|*.h|All files (*.*)|*.*",
                FileName = _defaultFuncName + ".cpp",
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(sfd.FileName, _output.Text, Encoding.UTF8);
            }
        }

        private static Button Btn(string text, int left, int top) => new Button
        {
            Text = text, Left = left, Top = top, Width = 120, Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(63, 63, 70),
            ForeColor = Color.WhiteSmoke,
        };
    }

    internal static class PpcToCppStub
    {
        public const string Header = @"// Minimal stubs for XDCKIT PpcToCpp output (wire to your runtime).
#pragma once
#include <cstdint>

struct PpcContext {
    uint32_t r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15;
    uint32_t r16, r17, r18, r19, r20, r21, r22, r23, r24, r25, r26, r27, r28, r29, r30, r31;
    uint32_t cr, lr, ctr, xer, msr;
    uint32_t insn;
    // float f0..f31; vector vr0..vr127 — add as needed
};

inline uint32_t PpcLoad32(uint8_t* base, uint32_t addr) {
    return *reinterpret_cast<uint32_t*>(base + addr);
}
inline void PpcStore32(uint8_t* base, uint32_t addr, uint32_t v) {
    *reinterpret_cast<uint32_t*>(base + addr) = v;
}
// ... implement PpcLoad8/16/64, FP, VMX, branch helpers as needed
";
    }
}
