using System;
using System.Collections.Generic;

namespace StormSwitchBox.Models
{
    public class GameMetadataEditModel
    {
        public string TitleId { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string TitleNameRussian { get; set; } = string.Empty;
        public string TitleNameEnglish { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public string? CustomIconPath { get; set; }
        public byte[]? OriginalIconBytes { get; set; }
        public byte[]? CustomIconBytes { get; set; }
        public byte[]? RawNacpBytes { get; set; }
        public string TempExtractDir { get; set; } = string.Empty;
        public string ModNameRomFs { get; set; } = string.Empty;
        public string ModNameExeFs { get; set; } = string.Empty;
        public bool HasRomFs { get; set; }
        public bool HasExeFs { get; set; }

        // 3D Banner & Icon Badge Studio (5.1.1)
        public bool Is3ds { get; set; }
        public bool BadgeRus { get; set; }
        public bool Badge60Fps { get; set; }
        public bool BadgeMod { get; set; }
        public bool BadgeAllDlc { get; set; }
        public string CustomBadgeText { get; set; } = string.Empty;
        public string BadgePosition { get; set; } = "BottomRight";
        public string CustomAudioWavPath { get; set; } = string.Empty;

        public bool HasBadges =>
            BadgeRus || Badge60Fps || BadgeMod || BadgeAllDlc || !string.IsNullOrWhiteSpace(CustomBadgeText);

        public bool HasModifications =>
            CustomIconBytes != null ||
            HasBadges ||
            !string.IsNullOrWhiteSpace(CustomAudioWavPath) ||
            !string.IsNullOrWhiteSpace(TitleNameRussian) ||
            !string.IsNullOrWhiteSpace(TitleNameEnglish) ||
            !string.IsNullOrWhiteSpace(Publisher);
    }
}
