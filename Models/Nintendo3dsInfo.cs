namespace StormSwitchBox.Models
{
    public class Nintendo3dsInfo
    {
        public string TitleId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ContentType { get; set; } = "Application"; // Application, Patch, AddOnContent
        public long SizeBytes { get; set; }
        public byte[]? IconBytes { get; set; }
        public string? GameName { get; set; }
        public string? Publisher { get; set; }
        public string? ProductCode { get; set; }
        public string FileFormat { get; set; } = "3DS"; // 3DS, CCI, CIA, CXI, CFA
    }
}
