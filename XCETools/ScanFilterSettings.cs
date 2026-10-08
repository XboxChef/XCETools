namespace XCETools
{
    /// <summary>UI snapshot for filter work on a background thread (no control access).</summary>
    internal readonly struct ScanFilterSettings
    {
        public readonly int ScanTypeIndex;
        public readonly ScanKind Kind;
        public readonly bool IsF;
        public readonly bool IsD;
        public readonly bool NotCheck;
        public readonly bool HexValues;
        public readonly int AlignmentStep;
        public readonly string ValueText;
        public readonly string Value2Text;

        public ScanFilterSettings(
            int scanTypeIndex, ScanKind kind, bool isF, bool isD,
            bool notCheck, bool hexValues, int alignmentStep,
            string valueText, string value2Text)
        {
            ScanTypeIndex = scanTypeIndex;
            Kind = kind;
            IsF = isF;
            IsD = isD;
            NotCheck = notCheck;
            HexValues = hexValues;
            AlignmentStep = alignmentStep;
            ValueText = valueText ?? string.Empty;
            Value2Text = value2Text ?? string.Empty;
        }

        public int ValueWidth => (int)Kind;
    }
}
