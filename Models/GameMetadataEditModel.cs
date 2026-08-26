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
    }
}
