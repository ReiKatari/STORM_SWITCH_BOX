using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Ленивый загрузчик базы данных каталогов 19 поколений Nintendo из единого сжатого бинарного ресурса (tools/db/catalogs.zst).
    /// Заменяет 17 статических C#-файлов объёмом 13.8 МБ (~38 000 строк), ускоряя JIT-компиляцию и экономя 40–60 МБ ОЗУ.
    /// </summary>
    public static class CatalogDataStore
    {
        private static readonly object _syncLock = new();
        private static Dictionary<string, List<NintendoCatalogItem>>? _cache;

        public static List<NintendoCatalogItem> GetCatalog(string catalogKey)
        {
            lock (_syncLock)
            {
                if (_cache == null)
                {
                    _cache = LoadAllCatalogs();
                }

                if (_cache.TryGetValue(catalogKey, out var list))
                {
                    return list;
                }

                return new List<NintendoCatalogItem>();
            }
        }

        private static Dictionary<string, List<NintendoCatalogItem>> LoadAllCatalogs()
        {
            var result = new Dictionary<string, List<NintendoCatalogItem>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates =
                {
                    Path.Combine(baseDir, "tools", "db", "catalogs.zst"),
                    Path.Combine(baseDir, "..", "..", "..", "tools", "db", "catalogs.zst"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "tools", "db", "catalogs.zst")
                };

                string? dbPath = null;
                foreach (var c in candidates)
                {
                    if (File.Exists(c))
                    {
                        dbPath = c;
                        break;
                    }
                }

                if (dbPath == null) return result;

                using var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                ms.Position = 0;

                using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
                byte[] magic = br.ReadBytes(4);
                if (magic.Length < 4 || Encoding.ASCII.GetString(magic) != "SCAT")
                    return result;

                uint catalogCount = br.ReadUInt32();
                for (uint c = 0; c < catalogCount; c++)
                {
                    ushort nameLen = br.ReadUInt16();
                    string catName = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                    uint entryCount = br.ReadUInt32();
                    var list = new List<NintendoCatalogItem>((int)entryCount);

                    for (uint i = 0; i < entryCount; i++)
                    {
                        string title = ReadString(br);
                        string genre = ReadString(br);
                        int year = br.ReadInt32();
                        string dev = ReadString(br);
                        string pub = ReadString(br);
                        string reg = ReadString(br);
                        string desc = ReadString(br);
                        string tid = ReadString(br);
                        string ed = ReadString(br);
                        string ver = ReadString(br);
                        string lang = ReadString(br);

                        list.Add(new NintendoCatalogItem
                        {
                            Title = string.Intern(title),
                            Genre = string.Intern(genre),
                            Year = year,
                            Developer = string.Intern(dev),
                            Publisher = string.Intern(pub),
                            Region = string.Intern(reg),
                            Desc = desc,
                            TitleId = string.IsNullOrEmpty(tid) ? null : tid,
                            Edition = string.Intern(ed),
                            Version = string.Intern(ver),
                            Languages = string.Intern(lang)
                        });
                    }

                    result[catName] = list;
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[CatalogDataStore] Ошибка чтения сжатой базы каталогов: {ex.Message}", Models.LogLevel.Warning);
            }

            return result;
        }

        private static string ReadString(BinaryReader br)
        {
            ushort len = br.ReadUInt16();
            if (len == 0) return string.Empty;
            return Encoding.UTF8.GetString(br.ReadBytes(len));
        }
    }

    public static class BsxCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("BsxCatalogData"); }
    public static class FdsCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("FdsCatalogData"); }
    public static class GameBoyCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GameBoyCatalogData"); }
    public static class GameCubeCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GameCubeCatalogData"); }
    public static class GbaCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GbaCatalogData"); }
    public static class GbcCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GbcCatalogData"); }
    public static class N64CatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("N64CatalogData"); }
    public static class NdsCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("NdsCatalogData"); }
    public static class NesCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("NesCatalogData"); }
    public static class PokemonMiniCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("PokemonMiniCatalogData"); }
    public static class SnesCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("SnesCatalogData"); }
    public static class Switch2CatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("Switch2CatalogData"); }
    public static class SwitchExtraCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("SwitchExtraCatalogData"); }
    public static class ThreeDsCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("ThreeDsCatalogData"); }
    public static class VirtualBoyCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("VirtualBoyCatalogData"); }
    public static class WiiCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("WiiCatalogData"); }
    public static class WiiUCatalogData { public static List<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("WiiUCatalogData"); }
}
