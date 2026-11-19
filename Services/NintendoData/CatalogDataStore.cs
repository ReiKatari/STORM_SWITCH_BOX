using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Ленивый загрузчик базы данных каталогов 19 поколений Nintendo из единого сжатого бинарного ресурса (tools/db/catalogs.zst).
    /// Заменяет 17 статических C#-файлов объёмом 13.8 МБ (~38 000 строк), ускоряя JIT-компиляцию и экономя 40-60 МБ ОЗУ.
    /// </summary>
    public static class CatalogDataStore
    {
        private static readonly object _syncLock = new();
        private static Dictionary<string, IReadOnlyList<NintendoCatalogItem>>? _cache;

        public static IReadOnlyList<NintendoCatalogItem> GetCatalog(string catalogKey)
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

                return Array.Empty<NintendoCatalogItem>();
            }
        }

        private static Dictionary<string, IReadOnlyList<NintendoCatalogItem>> LoadAllCatalogs()
        {
            var result = new Dictionary<string, IReadOnlyList<NintendoCatalogItem>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string? dbPath = StormToolResolver.ResolveCatalogsZst();
                if (dbPath == null)
                {
                    string[] candidates =
                    {
                        Path.Combine(baseDir, "tools", "db", "catalogs.zst"),
                        Path.Combine(baseDir, "..", "..", "..", "tools", "db", "catalogs.zst"),
                        Path.Combine(baseDir, "..", "..", "..", "..", "tools", "db", "catalogs.zst")
                    };

                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            dbPath = c;
                            break;
                        }
                    }
                }

                if (dbPath == null) return result;

                using var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Span<byte> hdr = stackalloc byte[4];
                int hdrRead = fs.Read(hdr);
                fs.Position = 0;

                using var ms = new MemoryStream();
                if (hdrRead >= 2 && hdr[0] == 0x1F && hdr[1] == 0x8B)
                {
                    using var gz = new GZipStream(fs, CompressionMode.Decompress);
                    gz.CopyTo(ms);
                }
                else if (hdrRead >= 4 && hdr[0] == 0x28 && hdr[1] == 0xB5 && hdr[2] == 0x2F && hdr[3] == 0xFD)
                {
                    using var zst = new ZstdSharp.DecompressionStream(fs);
                    zst.CopyTo(ms);
                }
                else
                {
                    return result;
                }

                ms.Position = 0;

                using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
                Span<byte> magic = stackalloc byte[4];
                ms.ReadExactly(magic);
                if (Encoding.ASCII.GetString(magic) != "SCAT")
                    return result;

                // Локальный пул повторяющихся справочных значений (не засоряет глобальный CLR string.Intern пул)
                var sharedStrings = new Dictionary<string, string>(StringComparer.Ordinal);
                string Deduplicate(string val)
                {
                    if (string.IsNullOrEmpty(val)) return string.Empty;
                    if (sharedStrings.TryGetValue(val, out var existing)) return existing;
                    sharedStrings[val] = val;
                    return val;
                }

                uint catalogCount = br.ReadUInt32();
                for (uint c = 0; c < catalogCount; c++)
                {
                    ushort nameLen = br.ReadUInt16();
                    byte[] nameBuf = new byte[nameLen];
                    if (nameLen > 0) ms.ReadExactly(nameBuf);
                    string catName = Encoding.UTF8.GetString(nameBuf);

                    uint entryCount = br.ReadUInt32();
                    var list = new List<NintendoCatalogItem>((int)entryCount);

                    for (uint i = 0; i < entryCount; i++)
                    {
                        string title = ReadString(br, ms);
                        string genre = Deduplicate(ReadString(br, ms));
                        int year = br.ReadInt32();
                        string dev = Deduplicate(ReadString(br, ms));
                        string pub = Deduplicate(ReadString(br, ms));
                        string reg = Deduplicate(ReadString(br, ms));
                        string desc = ReadString(br, ms);
                        string tid = ReadString(br, ms);
                        string ed = Deduplicate(ReadString(br, ms));
                        string ver = Deduplicate(ReadString(br, ms));
                        string lang = Deduplicate(ReadString(br, ms));

                        list.Add(new NintendoCatalogItem
                        {
                            Title = title,
                            Genre = genre,
                            Year = year,
                            Developer = dev,
                            Publisher = pub,
                            Region = reg,
                            Desc = desc,
                            TitleId = string.IsNullOrEmpty(tid) ? null : tid,
                            Edition = ed,
                            Version = ver,
                            Languages = lang
                        });
                    }

                    result[catName] = list.AsReadOnly();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[CatalogDataStore] Ошибка чтения сжатой базы каталогов: {ex.Message}", Models.LogLevel.Warning);
            }

            return result;
        }

        private static string ReadString(BinaryReader br, Stream stream)
        {
            ushort len = br.ReadUInt16();
            if (len == 0) return string.Empty;
            byte[] buf = new byte[len];
            stream.ReadExactly(buf);
            return Encoding.UTF8.GetString(buf);
        }
    }

    public static class BsxCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("BsxCatalogData"); }
    public static class FdsCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("FdsCatalogData"); }
    public static class GameBoyCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GameBoyCatalogData"); }
    public static class GameCubeCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GameCubeCatalogData"); }
    public static class GbaCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GbaCatalogData"); }
    public static class GbcCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("GbcCatalogData"); }
    public static class N64CatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("N64CatalogData"); }
    public static class NdsCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("NdsCatalogData"); }
    public static class NesCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("NesCatalogData"); }
    public static class PokemonMiniCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("PokemonMiniCatalogData"); }
    public static class SnesCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("SnesCatalogData"); }
    public static class Switch2CatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("Switch2CatalogData"); }
    public static class SwitchExtraCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("SwitchExtraCatalogData"); }
    public static class ThreeDsCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("ThreeDsCatalogData"); }
    public static class VirtualBoyCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("VirtualBoyCatalogData"); }
    public static class WiiCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("WiiCatalogData"); }
    public static class WiiUCatalogData { public static IReadOnlyList<NintendoCatalogItem> GetCatalog() => CatalogDataStore.GetCatalog("WiiUCatalogData"); }
}
