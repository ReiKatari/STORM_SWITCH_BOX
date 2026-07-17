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
    }
}
