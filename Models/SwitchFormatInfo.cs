namespace StormSwitchBox.Models
{
    public class SwitchFormatInfo
    {
        public string TitleId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public byte[]? IconBytes { get; set; }
        public string? GameName { get; set; }
        public string? Publisher { get; set; }

        public uint EmbeddedPatchVersion { get; set; }
        public bool IsMonolithicHardPatch { get; set; }
        public bool HasPatchCnmt { get; set; }
        public bool HasBaseCnmt { get; set; }
        public bool HasProgramNca { get; set; }
        public bool HasPatchProgramNca { get; set; }
        public string BaseTitleId { get; set; } = string.Empty;
        public string PatchTitleId { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> ContainedTitleIds { get; set; } = new();
    }
}
