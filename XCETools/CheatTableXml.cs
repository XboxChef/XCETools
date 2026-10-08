using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace XCETools
{
    /// <summary>One row in the saved-address table (Cheat Engine <c>CheatEntry</c>).</summary>
    internal sealed class CheatTableRow
    {
        public int Id;
        public string Description = "No description";
        public string VariableType = "4 Bytes";
        public uint Address;
        /// <summary>Checkbox / freeze-active flag in the UI.</summary>
        public bool Active;
        /// <summary>Last displayed value (optional; refreshed when connected).</summary>
        public string Value = string.Empty;
    }

    /// <summary>
    /// Cheat Engine–compatible XML tables (<c>.xcetbl</c> / <c>.CT</c>).
    /// Legacy pipe-separated lines are still accepted on load.
    /// </summary>
    internal static class CheatTableXml
    {
        /// <summary>Written on save; CE 7.5+ tables commonly use 46+.</summary>
        public const string CheatEngineTableVersion = "46";

        public static bool IsXmlPayload(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("<CheatTable", StringComparison.OrdinalIgnoreCase);
        }

        public static IList<CheatTableRow> LoadFromFile(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            return IsXmlPayload(text)
                ? ParseXml(text).ToList()
                : ParseLegacyLines(text).ToList();
        }

        public static void SaveToFile(string path, IEnumerable<CheatTableRow> rows)
        {
            var list = rows?.ToList() ?? new List<CheatTableRow>();
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("CheatTable",
                    new XAttribute("CheatEngineTableVersion", CheatEngineTableVersion),
                    new XElement("CheatEntries", list.Select(RowToElement)),
                    new XElement("UserdefinedSymbols")));

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(path, settings))
                doc.Save(writer);
        }

        private static IEnumerable<CheatTableRow> ParseXml(string text)
        {
            XDocument doc;
            try { doc = XDocument.Parse(text, LoadOptions.None); }
            catch (XmlException ex)
            {
                throw new InvalidDataException("Table is not valid XML: " + ex.Message, ex);
            }

            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "CheatTable", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Root element must be <CheatTable>.");

            var rows = new List<CheatTableRow>();
            int nextId = 0;
            foreach (var entry in root.Descendants("CheatEntry"))
            {
                var addrEl = entry.Element("Address");
                if (addrEl == null) continue;

                if (!TryParseAddress(addrEl.Value, out uint addr)) continue;

                var row = new CheatTableRow
                {
                    Id = ParseId(entry.Element("ID"), nextId),
                    Description = NormalizeDescription(entry.Element("Description")?.Value),
                    VariableType = NormalizeVariableType(entry.Element("VariableType")?.Value),
                    Address = addr,
                    Active = ParseBool(entry.Element("Active")?.Value)
                        || ParseBool(entry.Element("Frozen")?.Value),
                    Value = entry.Element("Value")?.Value?.Trim() ?? string.Empty
                };
                rows.Add(row);
                nextId = Math.Max(nextId, row.Id + 1);
            }

            return rows;
        }

        private static IEnumerable<CheatTableRow> ParseLegacyLines(string text)
        {
            var rows = new List<CheatTableRow>();
            int id = 0;
            foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Split('|');
                if (parts.Length < 4) continue;
                if (!TryParseAddress(parts[2], out uint addr)) continue;

                rows.Add(new CheatTableRow
                {
                    Id = id++,
                    Active = parts[0] == "1",
                    Description = string.IsNullOrWhiteSpace(parts[1]) ? "No description" : parts[1].Trim(),
                    Address = addr,
                    VariableType = NormalizeVariableType(parts[3]),
                    Value = parts.Length > 4 ? parts[4] : string.Empty
                });
            }
            return rows;
        }

        private static XElement RowToElement(CheatTableRow row, int index)
        {
            int id = row.Id >= 0 ? row.Id : index;
            var el = new XElement("CheatEntry",
                new XElement("ID", id.ToString(CultureInfo.InvariantCulture)),
                new XElement("Description", row.Description ?? "No description"),
                new XElement("VariableType", row.VariableType ?? "4 Bytes"),
                new XElement("Address", row.Address.ToString("X8", CultureInfo.InvariantCulture)));

            if (row.Active)
                el.Add(new XElement("Active", "1"));

            if (!string.IsNullOrEmpty(row.Value))
                el.Add(new XElement("Value", row.Value));

            return el;
        }

        private static int ParseId(XElement idEl, int fallback)
        {
            if (idEl == null) return fallback;
            return int.TryParse(idEl.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                ? id
                : fallback;
        }

        private static bool ParseBool(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s == "1"
                || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseAddress(string cell, out uint addr)
        {
            addr = 0;
            if (string.IsNullOrWhiteSpace(cell)) return false;
            cell = cell.Trim();
            if (cell.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cell = cell.Substring(2);
            cell = cell.Replace(" ", string.Empty);
            return uint.TryParse(cell, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out addr);
        }

        private static string NormalizeDescription(string s)
        {
            s = (s ?? string.Empty).Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2).Trim();
            return string.IsNullOrEmpty(s) ? "No description" : s;
        }

        private static string NormalizeVariableType(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "4 Bytes";
            s = s.Trim();
            if (string.Equals(s, "Array of byte", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Array Of Byte", StringComparison.OrdinalIgnoreCase))
                return "Array of byte";
            return s;
        }
    }
}
