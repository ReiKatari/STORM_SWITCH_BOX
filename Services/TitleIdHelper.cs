using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StormSwitchBox.Services
{
    public static class TitleIdHelper
    {
        private static readonly Regex Hex16Regex = new(@"(?<![0-9A-Fa-f])(0[14]00[0-9A-Fa-f]{12}|0004[0-9A-Fa-f]{12})(?![0-9A-Fa-f])", RegexOptions.Compiled);

        public static string? ExtractHex16(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Hex16Regex.Match(text);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }

        public static bool TryParseHex16(string? tidHex, out ulong tid)
        {
            tid = 0;
            if (string.IsNullOrWhiteSpace(tidHex)) return false;
            string clean = tidHex.Trim();
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(2);
            return clean.Length == 16 && ulong.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out tid);
        }

        public static ulong NormalizeBaseTitleId(ulong tid)
        {
            ulong topByte = tid >> 56;
            if (topByte == 0x01 || topByte == 0x04)
            {
                ulong low12 = tid & 0xFFFUL;
                if (low12 == 0x800UL)
                    return tid & ~0xFFFUL;
                if (low12 != 0UL)
                    return (tid - 0x1000UL) & ~0xFFFUL;
                return tid & ~0xFFFUL;
            }
            if ((tid >> 48) == 0x0004UL)
                return (0x00040000UL << 32) | (tid & 0xFFFFFFFFUL);
            return tid;
        }

        public static string NormalizeBaseTitleIdHex(string? tidHex)
        {
            if (!TryParseHex16(tidHex, out ulong tid))
                return tidHex?.Trim().ToUpperInvariant() ?? string.Empty;
            return NormalizeBaseTitleId(tid).ToString("X16");
        }

        public static ulong ComputePatchTitleId(ulong baseTid) => (NormalizeBaseTitleId(baseTid) & ~0xFFFUL) | 0x800UL;

        public static ulong ComputeDlcBaseId(ulong baseTid) => (NormalizeBaseTitleId(baseTid) & ~0xFFFUL) + 0x1000UL;

        public static ulong ComputeDlcTitleId(ulong baseTid, int oneBasedIndex)
        {
            ulong offset = (ulong)Math.Max(1, oneBasedIndex);
            return ComputeDlcBaseId(baseTid) + offset;
        }
    }
}
