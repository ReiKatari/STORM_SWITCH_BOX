namespace StormSwitchBox.Services
{
    public class NintendoCatalogItem
    {
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "Экшен";
        public int Year { get; set; } = 1990;
        public string Developer { get; set; } = "Nintendo";
        public string Publisher { get; set; } = "Nintendo";
        public string Region { get; set; } = "USA";
        public string Desc { get; set; } = "";
        public string? TitleId { get; set; }
    }
}
