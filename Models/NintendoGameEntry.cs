using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using System.Text.Json.Serialization;

namespace StormSwitchBox.Models
{
    public partial class NintendoGameEntry : ObservableObject
    {
        [ObservableProperty] private string _id = "";
        [ObservableProperty] private string _title = "";
        [ObservableProperty] private string _system = "";
        [ObservableProperty] private string _systemShort = "";
        [ObservableProperty] private string _genre = "Неизвестно";
        [ObservableProperty] private string _releaseDate = "N/A";
        [ObservableProperty] private string _developer = "Nintendo";
        [ObservableProperty] private string _publisher = "Nintendo";
        [ObservableProperty] private string _version = "v1.0";
        [ObservableProperty] private string _edition = "Standard Edition";
        [ObservableProperty] private string _region = "WW";
        [ObservableProperty] private string _coverUrl = "";
        [ObservableProperty] private string _description = "Описание отсутствует.";
        [ObservableProperty] private string _players = "1 игрок";
        [ObservableProperty] private string _rating = "Все возрасты";

        [ObservableProperty]
        [JsonIgnore]
        private BitmapImage? _coverBitmap;

        [JsonIgnore]
        public SolidColorBrush SystemBadgeBrush => new SolidColorBrush(GetSystemColor(SystemShort));

        private static Color GetSystemColor(string sysShort) => sysShort switch
        {
            "NES" => Color.FromArgb(255, 229, 57, 53),
            "FDS" => Color.FromArgb(255, 216, 27, 96),
            "SNES" => Color.FromArgb(255, 142, 36, 170),
            "BS-X" => Color.FromArgb(255, 94, 53, 177),
            "N64" => Color.FromArgb(255, 57, 73, 171),
            "GC" => Color.FromArgb(255, 30, 136, 229),
            "Wii" => Color.FromArgb(255, 0, 172, 193),
            "WiiU" => Color.FromArgb(255, 0, 137, 123),
            "Switch" => Color.FromArgb(255, 229, 57, 53),
            "Switch 2" => Color.FromArgb(255, 230, 0, 18),
            "GB" => Color.FromArgb(255, 124, 179, 66),
            "GBC" => Color.FromArgb(255, 253, 216, 53),
            "GBA" => Color.FromArgb(255, 251, 140, 0),
            "NDS" => Color.FromArgb(255, 109, 76, 65),
            "3DS" => Color.FromArgb(255, 211, 47, 47),
            "VB" => Color.FromArgb(255, 194, 24, 91),
            "GW" => Color.FromArgb(255, 84, 110, 122),
            "PM" => Color.FromArgb(255, 0, 137, 123),
            "TVG" => Color.FromArgb(255, 244, 81, 30),
            _ => Color.FromArgb(255, 0, 229, 255)
        };
    }
}
