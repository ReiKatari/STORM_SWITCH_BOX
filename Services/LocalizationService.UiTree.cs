using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI.UI.Controls;

namespace StormSwitchBox.Services
{
    public partial class LocalizationService
    {
        private readonly ConditionalWeakTable<object, Dictionary<string, string>> _originalTexts = new();

        private Dictionary<string, string>? _ruToKey;
        private Dictionary<string, string> RuToKey
        {
            get
            {
                if (_ruToKey == null)
                {
                    _ruToKey = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (_strings.TryGetValue("ru", out var ruDict))
                    {
                        foreach (var kvp in ruDict)
                        {
                            if (!string.IsNullOrEmpty(kvp.Value) && !_ruToKey.ContainsKey(kvp.Value))
                            {
                                _ruToKey[kvp.Value] = kvp.Key;
                            }
                        }
                    }
                }
                return _ruToKey;
            }
        }

        public string TranslateText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";

            if (_currentLanguage == "ru")
            {
                return text;
            }

            if (_directTranslations.TryGetValue(text, out var entry))
            {
                return GetLocalizedFromEntry(entry, _currentLanguage);
            }

            string trimmed = text.Trim();
            if (trimmed.Length != text.Length && _directTranslations.TryGetValue(trimmed, out var entryTrimmed))
            {
                string trans = GetLocalizedFromEntry(entryTrimmed, _currentLanguage);
                int leadWs = 0;
                while (leadWs < text.Length && char.IsWhiteSpace(text[leadWs])) leadWs++;
                int trailWs = 0;
                while (trailWs < text.Length && char.IsWhiteSpace(text[text.Length - 1 - trailWs])) trailWs++;
                return new string(' ', leadWs) + trans + new string(' ', trailWs);
            }

            if (RuToKey.TryGetValue(text, out var key) || RuToKey.TryGetValue(trimmed, out key))
            {
                if (_strings.TryGetValue(_currentLanguage, out var langDict) && langDict.TryGetValue(key, out var val))
                {
                    return val;
                }
            }

            return text;
        }

        private static string GetLocalizedFromEntry((string En, string De, string Fr, string Zh, string Ja) entry, string lang) => lang switch
        {
            "en" => entry.En,
            "de" => entry.De,
            "fr" => entry.Fr,
            "zh" => entry.Zh,
            "ja" => entry.Ja,
            _ => entry.En
        };

        public void TranslateRoot(DependencyObject? root)
        {
            if (root == null) return;
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            TraverseElement(root, visited);
        }

        private void TraverseElement(object? node, HashSet<object> visited)
        {
            if (node == null || !visited.Add(node)) return;

            if (node is UIElement uie)
            {
                var toolTipObj = ToolTipService.GetToolTip(uie);
                if (toolTipObj is string strTip)
                {
                    TranslateProp(uie, "ToolTip", strTip, val => ToolTipService.SetToolTip(uie, val));
                }
                else if (toolTipObj is ToolTip tt && tt.Content is string ttStr)
                {
                    TranslateProp(tt, "Content", ttStr, val => tt.Content = val);
                }

                if (uie.ContextFlyout != null)
                {
                    TraverseFlyout(uie.ContextFlyout, visited);
                }
            }

            switch (node)
            {
                case ContentDialog cd:
                    if (cd.Title is string titleStr)
                    {
                        TranslateProp(cd, "Title", titleStr, val => cd.Title = val);
                    }
                    if (!string.IsNullOrEmpty(cd.PrimaryButtonText))
                    {
                        TranslateProp(cd, "PrimaryButtonText", cd.PrimaryButtonText, val => cd.PrimaryButtonText = val);
                    }
                    if (!string.IsNullOrEmpty(cd.SecondaryButtonText))
                    {
                        TranslateProp(cd, "SecondaryButtonText", cd.SecondaryButtonText, val => cd.SecondaryButtonText = val);
                    }
                    if (!string.IsNullOrEmpty(cd.CloseButtonText))
                    {
                        TranslateProp(cd, "CloseButtonText", cd.CloseButtonText, val => cd.CloseButtonText = val);
                    }
                    if (cd.Content is DependencyObject cdContent)
                    {
                        TraverseElement(cdContent, visited);
                    }
                    break;

                case TabViewItem tvi:
                    if (tvi.Header is string tviHeader)
                    {
                        TranslateProp(tvi, "Header", tviHeader, val => tvi.Header = val);
                    }
                    if (tvi.Content is string tviContentStr)
                    {
                        TranslateProp(tvi, "Content", tviContentStr, val => tvi.Content = val);
                    }
                    else if (tvi.Content is DependencyObject tviContentObj)
                    {
                        TraverseElement(tviContentObj, visited);
                    }
                    break;

                case AppBarButton abb:
                    if (!string.IsNullOrEmpty(abb.Label))
                    {
                        TranslateProp(abb, "Label", abb.Label, val => abb.Label = val);
                    }
                    break;

                case Expander exp:
                    if (exp.Header is string expH)
                    {
                        TranslateProp(exp, "Header", expH, val => exp.Header = val);
                    }
                    else if (exp.Header is DependencyObject hObj)
                    {
                        TraverseElement(hObj, visited);
                    }
                    if (exp.Content is DependencyObject cObj)
                    {
                        TraverseElement(cObj, visited);
                    }
                    break;

                case TextBlock tb:
                    if (tb.Inlines != null && tb.Inlines.Count > 1)
                    {
                        foreach (var inline in tb.Inlines)
                        {
                            if (inline is Run run && !string.IsNullOrEmpty(run.Text))
                            {
                                TranslateProp(run, "Text", run.Text, val => run.Text = val);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(tb.Text))
                    {
                        TranslateProp(tb, "Text", tb.Text, val => tb.Text = val);
                    }
                    break;

                case TextBox tbox:
                    if (tbox.Header is string tbh)
                    {
                        TranslateProp(tbox, "Header", tbh, val => tbox.Header = val);
                    }
                    if (!string.IsNullOrEmpty(tbox.PlaceholderText))
                    {
                        TranslateProp(tbox, "PlaceholderText", tbox.PlaceholderText, val => tbox.PlaceholderText = val);
                    }
                    break;

                case PasswordBox pbox:
                    if (pbox.Header is string pbh)
                    {
                        TranslateProp(pbox, "Header", pbh, val => pbox.Header = val);
                    }
                    if (!string.IsNullOrEmpty(pbox.PlaceholderText))
                    {
                        TranslateProp(pbox, "PlaceholderText", pbox.PlaceholderText, val => pbox.PlaceholderText = val);
                    }
                    break;

                case AutoSuggestBox asb:
                    if (asb.Header is string asbh)
                    {
                        TranslateProp(asb, "Header", asbh, val => asb.Header = val);
                    }
                    if (!string.IsNullOrEmpty(asb.PlaceholderText))
                    {
                        TranslateProp(asb, "PlaceholderText", asb.PlaceholderText, val => asb.PlaceholderText = val);
                    }
                    break;

                case ComboBox cb:
                    if (cb.Header is string cbh)
                    {
                        TranslateProp(cb, "Header", cbh, val => cb.Header = val);
                    }
                    if (!string.IsNullOrEmpty(cb.PlaceholderText))
                    {
                        TranslateProp(cb, "PlaceholderText", cb.PlaceholderText, val => cb.PlaceholderText = val);
                    }
                    foreach (var item in cb.Items)
                    {
                        if (item is DependencyObject itemDobj)
                        {
                            TraverseElement(itemDobj, visited);
                        }
                    }
                    break;

                case NumberBox nb:
                    if (nb.Header is string nbh)
                    {
                        TranslateProp(nb, "Header", nbh, val => nb.Header = val);
                    }
                    if (!string.IsNullOrEmpty(nb.PlaceholderText))
                    {
                        TranslateProp(nb, "PlaceholderText", nb.PlaceholderText, val => nb.PlaceholderText = val);
                    }
                    break;

                case ToggleSwitch ts:
                    if (ts.Header is string tsh)
                    {
                        TranslateProp(ts, "Header", tsh, val => ts.Header = val);
                    }
                    if (ts.OnContent is string onC)
                    {
                        TranslateProp(ts, "OnContent", onC, val => ts.OnContent = val);
                    }
                    if (ts.OffContent is string offC)
                    {
                        TranslateProp(ts, "OffContent", offC, val => ts.OffContent = val);
                    }
                    break;

                case DataGrid dg:
                    foreach (var col in dg.Columns)
                    {
                        if (col.Header is string colH)
                        {
                            TranslateProp(col, "Header", colH, val => col.Header = val);
                        }
                    }
                    break;

                case ContentControl cc:
                    if (cc.Content is string contentStr)
                    {
                        TranslateProp(cc, "Content", contentStr, val => cc.Content = val);
                    }
                    else if (cc.Content is DependencyObject contentDobj)
                    {
                        TraverseElement(contentDobj, visited);
                    }
                    break;
            }

            if (node is Button btn && btn.Flyout != null)
            {
                TraverseFlyout(btn.Flyout, visited);
            }

            switch (node)
            {
                case Panel p:
                    foreach (var child in p.Children)
                    {
                        TraverseElement(child, visited);
                    }
                    break;

                case Border b when b.Child != null:
                    TraverseElement(b.Child, visited);
                    break;

                case ScrollViewer sv when sv.Content is DependencyObject svContent:
                    TraverseElement(svContent, visited);
                    break;

                case Viewbox vb when vb.Child != null:
                    TraverseElement(vb.Child, visited);
                    break;

                case UserControl uc when uc.Content is DependencyObject ucContent:
                    TraverseElement(ucContent, visited);
                    break;
            }

            if (node is DependencyObject dobj)
            {
                int count = VisualTreeHelper.GetChildrenCount(dobj);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(dobj, i);
                    TraverseElement(child, visited);
                }
            }
        }

        private void TraverseFlyout(FlyoutBase? flyout, HashSet<object> visited)
        {
            if (flyout == null || !visited.Add(flyout)) return;

            if (flyout is MenuFlyout mf)
            {
                TraverseMenuItems(mf.Items, visited);
            }
            else if (flyout is Flyout fl && fl.Content is DependencyObject flRoot)
            {
                TraverseElement(flRoot, visited);
            }
        }

        private void TraverseMenuItems(IList<MenuFlyoutItemBase>? items, HashSet<object> visited)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null || !visited.Add(item)) continue;

                switch (item)
                {
                    case ToggleMenuFlyoutItem tmfi:
                        if (!string.IsNullOrEmpty(tmfi.Text))
                        {
                            TranslateProp(tmfi, "Text", tmfi.Text, val => tmfi.Text = val);
                        }
                        break;

                    case MenuFlyoutItem mfi:
                        if (!string.IsNullOrEmpty(mfi.Text))
                        {
                            TranslateProp(mfi, "Text", mfi.Text, val => mfi.Text = val);
                        }
                        break;

                    case MenuFlyoutSubItem mfsi:
                        if (!string.IsNullOrEmpty(mfsi.Text))
                        {
                            TranslateProp(mfsi, "Text", mfsi.Text, val => mfsi.Text = val);
                        }
                        TraverseMenuItems(mfsi.Items, visited);
                        break;
                }
            }
        }

        private void TranslateProp(object target, string propertyName, string? currentVal, Action<string> setter)
        {
            if (string.IsNullOrEmpty(currentVal)) return;

            if (!_originalTexts.TryGetValue(target, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                _originalTexts.Add(target, map);
            }

            if (map.TryGetValue(propertyName, out var origText))
            {
                var trans = TranslateText(origText);
                if (trans != currentVal)
                {
                    setter(trans);
                }
            }
            else
            {
                if (ContainsCyrillic(currentVal) || _directTranslations.ContainsKey(currentVal) || _directTranslations.ContainsKey(currentVal.Trim()))
                {
                    map[propertyName] = currentVal;
                    var trans = TranslateText(currentVal);
                    if (trans != currentVal)
                    {
                        setter(trans);
                    }
                }
            }
        }

        private static bool ContainsCyrillic(string? s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if ((c >= '\u0400' && c <= '\u04FF') || c == '\u0501') return true;
            }
            return false;
        }

        private static readonly Dictionary<string, (string En, string De, string Fr, string Zh, string Ja)> _directTranslations = new(StringComparer.Ordinal)
        {
            ["0 Б"] = ("0 B", "0 B", "0 o", "0 B", "0 B"),
            ["ОБНОВЛЕНИЕ (РУС)"] = ("UPDATE (RUS)", "UPDATE (RUS)", "MISE À JOUR (RUS)", "更新补丁 (俄语)", "アップデート (RUS)"),
            ["ИНФО"] = ("INFO", "INFO", "INFO", "说明文件", "情報"),
            ["МОД / РУСИФИКАТОР"] = ("MOD / TRANSLATION", "MOD / ÜBERSETZUNG", "MOD / TRADUCTION", "模组 / 汉化补丁", "MOD / 翻訳パッチ"),
            ["ИГРА"] = ("GAME", "SPIEL", "JEU", "游戏本体", "ゲーム本編"),
            ["ОБНОВЛЕНИЕ"] = ("UPDATE", "UPDATE", "MISE À JOUR", "更新补丁", "アップデート"),
            ["ФАЙЛ"] = ("FILE", "DATEI", "FICHIER", "文件", "ファイル"),
            ["0 тем"] = ("0 themes", "0 Designs", "0 thèmes", "0 个主题", "0 テーマ"),
            ["1 МБ (Рекомендуется)"] = ("1 MB (Recommended)", "1 MB (Empfohlen)", "1 Mo (Recommandé)", "1 MB (推荐)", "1 MB (推奨)"),
            ["1 игра"] = ("1 game", "1 Spiel", "1 jeu", "1 款游戏", "1 ゲーム"),
            ["2 МБ"] = ("2 MB", "2 MB", "2 Mo", "2 MB", "2 MB"),
            ["2 потока (Пониженная нагрузка)"] = ("2 threads (Low load)", "2 Threads (Geringe Auslastung)", "2 threads (Charge faible)", "2 线程 (低负载)", "2 スレッド (低負荷)"),
            ["23.0.0 (Рекомендуется)"] = ("23.0.0 (Recommended)", "23.0.0 (Empfohlen)", "23.0.0 (Recommandé)", "23.0.0 (推荐)", "23.0.0 (推奨)"),
            ["256 КБ"] = ("256 KB", "256 KB", "256 Ko", "256 KB", "256 KB"),
            ["3DS (CCI - для эмуляторов)"] = ("3DS (CCI - for emulators)", "3DS (CCI - für Emulatoren)", "3DS (CCI - pour émulateurs)", "3DS (CCI - 模拟器格式)", "3DS (CCI - エミュレータ用)"),
            ["4 МБ"] = ("4 MB", "4 MB", "4 Mo", "4 MB", "4 MB"),
            ["4 потока (Рекомендуется)"] = ("4 threads (Recommended)", "4 Threads (Empfohlen)", "4 threads (Recommandé)", "4 线程 (推荐)", "4 スレッド (推奨)"),
            ["4GB на части для совместимости со старыми FAT32 флешками.\"/>"] = ("4GB parts for compatibility with older FAT32 flash drives.\"/>", "4GB Teile zur Kompatibilität mit älteren FAT32-Flash-Laufwerken.\"/>", "Parties de 4 Go pour la compatibilité avec les anciennes clés FAT32.\"/>", "4GB 分卷以兼容旧版 FAT32 闪存盘。\"/>", "4GB 分割（従来の FAT32 USBメモリ互換用）。\"/>"),
            ["512 КБ"] = ("512 KB", "512 KB", "512 Ko", "512 KB", "512 KB"),
            ["8 потоков"] = ("8 threads", "8 Threads", "8 threads", "8 线程", "8 スレッド"),
            ["CIA (Установочный пакет)"] = ("CIA (Installation package)", "CIA (Installationspaket)", "CIA (Package d'installation)", "CIA (安装包格式)", "CIA (インストールパッケージ)"),
            ["CXI (Исполняемый NCCH)"] = ("CXI (Executable NCCH)", "CXI (Ausführbares NCCH)", "CXI (NCCH exécutable)", "CXI (可执行 NCCH)", "CXI (実行可能 NCCH)"),
            ["ExeFS (Код / Иконка)"] = ("ExeFS (Code / Icon)", "ExeFS (Code / Symbol)", "ExeFS (Code / Icône)", "ExeFS (代码 / 图标)", "ExeFS (コード / アイコン)"),
            ["FW 1.0 (65796 - Отключить)"] = ("FW 1.0 (65796 - Disable)", "FW 1.0 (65796 - Deaktivieren)", "FW 1.0 (65796 - Désactiver)", "FW 1.0 (65796 - 禁用)", "FW 1.0 (65796 - 無効化)"),
            ["HTTP / SOCKS5 Прокси (необязательно)"] = ("HTTP / SOCKS5 Proxy (optional)", "HTTP / SOCKS5 Proxy (optional)", "Proxy HTTP / SOCKS5 (optionnel)", "HTTP / SOCKS5 代理 (可选)", "HTTP / SOCKS5 プロキシ (オプション)"),
            ["HardPatch физически распаковывает дельту и запекает чистый RomFS в единый Program NCA для гарантированной работы на любых эмуляторах (включая старые версии) и консолях"] = ("HardPatch physically unpacks delta and bakes clean RomFS into a single Program NCA for guaranteed compatibility on all emulators (including older builds) and consoles", "HardPatch entpackt das Delta physisch und backt ein sauberes RomFS in ein einziges Program NCA für garantierte Kompatibilität auf allen Emulatoren und Konsolen", "HardPatch décompresse physiquement le delta et intègre un RomFS propre dans un Program NCA unique pour une compatibilité garantie sur tous les émulateurs et consoles", "HardPatch 物理拆包增量补丁，并将干净的 RomFS 烘焙到单个 Program NCA 中，以确保在所有模拟器（包括旧版本）和真机上的兼容运行", "HardPatch は差分パッチを物理的に展開し、クリーンな RomFS を単一の Program NCA にベイクすることで、すべてのエミュレータ（旧バージョン含む）および実機での確実な動作を保証します"),
            ["IP-адрес консоли"] = ("Console IP address", "Konsolen-IP-Adresse", "Adresse IP de la console", "主机 IP 地址", "本体 IP アドレス"),
            ["Live-превью иконки"] = ("Live icon preview", "Live-Symbolvorschau", "Aperçu de l'icône en direct", "图标实时预览", "アイコンのライブプレビュー"),
            ["NSP (Чистый контейнер PFS0)"] = ("NSP (Clean PFS0 container)", "NSP (Sauberer PFS0-Container)", "NSP (Conteneur PFS0 propre)", "NSP (纯净 PFS0 容器)", "NSP (クリーン PFS0 コンテナ)"),
            ["NSZ (Блочный NCZBLOCK, рекомендуется)"] = ("NSZ (Block NCZBLOCK, recommended)", "NSZ (Block-NCZBLOCK, empfohlen)", "NSZ (Blocs NCZBLOCK, recommandé)", "NSZ (块式 NCZBLOCK，推荐)", "NSZ (ブロック NCZBLOCK、推奨)"),
            ["NUP — Монолитный контейнер Switch 2 (NUP v1.0, Zstandard + Дедупликация)"] = ("NUP — Switch 2 Monolithic container (NUP v1.0, Zstandard + Deduplication)", "NUP — Monolithischer Switch 2 Container (NUP v1.0, Zstandard + Deduplizierung)", "NUP — Conteneur monolithique Switch 2 (NUP v1.0, Zstandard + Déduplication)", "NUP — Switch 2 单体式集成容器 (NUP v1.0, Zstandard + 重复数据删除)", "NUP — Switch 2 モノリシックコンテナ (NUP v1.0, Zstandard + 重複排除)"),
            ["RomFS (Ресурсы)"] = ("RomFS (Assets)", "RomFS (Ressourcen)", "RomFS (Ressources)", "RomFS (资源文件)", "RomFS (アセット)"),
            ["RuTracker и Облачный загрузчик игр / модов..."] = ("RuTracker and Cloud game/mod downloader...", "RuTracker & Cloud-Downloader für Spiele/Mods...", "RuTracker et téléchargeur cloud de jeux/mods...", "RuTracker 与云端游戏/模组下载器...", "RuTracker およびクラウド ゲーム/MOD ダウンローダー..."),
            ["SmartShrink для Switch 2 (удаление 0xFF/0x00 паддингов и выравнивание секторов)"] = ("SmartShrink for Switch 2 (removes 0xFF/0x00 padding and aligns sectors)", "SmartShrink für Switch 2 (entfernt 0xFF/0x00-Padding und richtet Sektoren aus)", "SmartShrink pour Switch 2 (supprime le rembourrage 0xFF/0x00 et aligne les secteurs)", "适用于 Switch 2 的 SmartShrink (去除 0xFF/0x00 填充并对齐扇区)", "Switch 2 用 SmartShrink (0xFF/0x00 パディング除去およびセクタアライメント)"),
            ["Switch (Все)"] = ("Switch (All)", "Switch (Alle)", "Switch (Tous)", "Switch (全部)", "Switch (すべて)"),
            ["Switch (Игры)"] = ("Switch (Games)", "Switch (Spiele)", "Switch (Jeux)", "Switch (游戏)", "Switch (ゲーム)"),
            ["Switch (Моды/RUS)"] = ("Switch (Mods/RUS)", "Switch (Mods/RUS)", "Switch (Mods/RUS)", "Switch (模组/汉化)", "Switch (MOD/日本語)"),
            ["WW | 1-2 игрока"] = ("WW | 1-2 Players", "WW | 1-2 Spieler", "WW | 1-2 joueurs", "全球版 | 1-2 名玩家", "WW | 1-2 プレイヤー"),
            ["XCI (Картридж Switch 2)"] = ("XCI (Switch 2 Cartridge)", "XCI (Switch 2 Cartridge)", "XCI (Cartouche Switch 2)", "XCI (Switch 2 卡带)", "XCI (Switch 2 カートリッジ)"),
            ["XCZ (Сжатый Multi-Cart)"] = ("XCZ (Compressed Multi-Cart)", "XCZ (Komprimierte Multi-Cart)", "XCZ (Multi-cartouche compressée)", "XCZ (压缩多合一卡带)", "XCZ (圧縮マルチカートリッジ)"),
            ["XCZ (Сжатый картридж)"] = ("XCZ (Compressed Cartridge)", "XCZ (Komprimierte Cartridge)", "XCZ (Cartouche compressée)", "XCZ (压缩卡带)", "XCZ (圧縮カートリッジ)"),
            ["~0 Б"] = ("~0 B", "~0 B", "~0 o", "~0 B", "~0 B"),
            ["«Умная» папка Nintendo 3DS"] = ("Nintendo 3DS Smart Watch Folder", "Intelligenter Überwachungsordner (3DS)", "Dossier intelligent Nintendo 3DS", "Nintendo 3DS 智能监控文件夹", "Nintendo 3DS スマート監視フォルダ"),
            ["«Умная» папка Nintendo Switch"] = ("Nintendo Switch Smart Watch Folder", "Intelligenter Überwachungsordner (Switch)", "Dossier intelligent Nintendo Switch", "Nintendo Switch 智能监控文件夹", "Nintendo Switch スマート監視フォルダ"),
            ["Авто (Оптимально)"] = ("Auto (Optimal)", "Auto (Optimal)", "Auto (Optimal)", "自动 (最佳)", "自動 (最適)"),
            ["Авто-вшивание модов, русификаторов и читов (base/romfs, exefs)"] = ("Auto-inject mods, localizations and cheats (base/romfs, exefs)", "Mods, Übersetzungen und Cheats automatisch integrieren (base/romfs, exefs)", "Injection auto des mods, traductions et codes de triche (base/romfs, exefs)", "自动注入模组、本地化和金手指 (base/romfs, exefs)", "MOD、翻訳パッチ、チートの自動埋め込み (base/romfs, exefs)"),
            ["Авто-инъекция патчей 60 FPS и твиков графики"] = ("Auto-injection of 60 FPS patches and graphics tweaks", "Automatische Injektion von 60-FPS-Patches und Grafik-Tweaks", "Injection auto des patchs 60 FPS et réglages graphiques", "自动注入 60 FPS 补丁与图形优化补丁", "60 FPS パッチおよびグラフィック調整の自動注入"),
            ["Авто-разблокировка дополнений (Switch 2 DLC Unlocker)"] = ("Auto-unlock add-ons (Switch 2 DLC Unlocker)", "DLCs automatisch freischalten (Switch 2 DLC Unlocker)", "Déverrouillage auto des DLC (Switch 2 DLC Unlocker)", "自动解锁追加内容 (Switch 2 DLC 解锁器)", "DLC 自動ロック解除 (Switch 2 DLC アンロッカー)"),
            ["Авто-сшивка Мульти-контента (База + Обновления + DLC в единый монолитный файл)"] = ("Auto-merge Multi-Content (Base + Updates + DLC into a single monolithic file)", "Multi-Content automatisch zusammenführen (Basis + Updates + DLC in eine monolithische Datei)", "Fusion auto multi-contenu (Base + Mises à jour + DLC en un seul fichier monolithique)", "多重内容自动合并 (本体 + 更新补丁 + DLC 合并为单体文件)", "マルチコンテンツ自動結合 (ベース + アップデート + DLC を単一ファイルに統合)"),
            ["Автоматическая интеграция модификаций, переводов и графических патчей из указанной папки при сборке контейнера"] = ("Automatically integrates mods, translations, and graphics patches from the specified folder during container build", "Integriert automatisch Modifikationen, Übersetzungen und Grafik-Patches aus dem angegebenen Ordner beim Container-Build", "Intègre automatiquement les modifications, traductions et correctifs graphiques du dossier spécifié lors de la création du conteneur", "构建容器时自动整合指定文件夹中的模组、汉化翻译及图形画质补丁", "コンテナビルド時に指定フォルダからMOD、翻訳、グラフィックパッチを自動統合します"),
            ["Автоматическая очистка мусора из модов и сжатие текстур PNG без потери качества."] = ("Automatically cleans mod junk and compresses PNG textures losslessly.", "Bereinigt Mod-Mülldateien automatisch und komprimiert PNG-Texturen verlustfrei.", "Nettoie automatiquement les résidus de mods et compresse les textures PNG sans perte.", "自动清理模组垃圾文件并无损压缩 PNG 纹理贴图。", "MODの不要ファイルを自動クリーンアップし、画質劣化なしでPNGテクスチャを圧縮します。"),
            ["Автоматическая проверка целостности (SHA-256 / NSO0 / NPDM / PFS0) после сборки"] = ("Automatic integrity verification (SHA-256 / NSO0 / NPDM / PFS0) after build", "Automatische Integritätsprüfung (SHA-256 / NSO0 / NPDM / PFS0) nach dem Build", "Vérification automatique de l'intégrité (SHA-256 / NSO0 / NPDM / PFS0) après la création", "构建完成后自动执行完整性校验 (SHA-256 / NSO0 / NPDM / PFS0)", "ビルド後に完全性チェックを自動実行 (SHA-256 / NSO0 / NPDM / PFS0)"),
            ["Автоматическая распаковка и обратная перепаковка сжатых архивов .zs внутри RomFS при сшивке и внедрении локализаций"] = ("Automatically unpacks and repacks compressed .zs archives inside RomFS during merging and localization injection", "Automatisches Entpacken und erneutes Packen komprimierter .zs-Archive im RomFS beim Zusammenführen und Einbinden von Lokalisierungen", "Décompression et réempaquetage automatiques des archives compressées .zs dans RomFS lors de la fusion et de l'injection des localisations", "合并与注入本地化补丁时，自动解包并重新打包 RomFS 内部压缩的 .zs 归档", "統合およびローカライズ適用時に RomFS 内の圧縮 .zs アーカイブを自動展開・再パックします"),
            ["Автоматически внедряет актуальные IPS патчи 60 кадров/сек и исправления графики при пересборке"] = ("Automatically injects current 60 FPS IPS patches and graphics fixes during rebuilding", "Bettet aktuelle 60-FPS-IPS-Patches und Grafikkorrekturen beim Neuerstellen automatisch ein", "Injecte automatiquement les patchs IPS 60 FPS actuels et les correctifs graphiques lors de la reconstruction", "重构时自动注入最新的 60 FPS IPS 补丁和图形画质修正", "再構築時に最新の 60 FPS IPS パッチおよびグラフィック修正を自動適用します"),
            ["Автоматически обнаруживает файлы 3DS (.3ds, .cci, .cia, .cxi, архивы .zip, .rar, .7z) и отправляет в задачи"] = ("Automatically detects 3DS files (.3ds, .cci, .cia, .cxi, .zip, .rar, .7z) and queues tasks", "Erkennt 3DS-Dateien (.3ds, .cci, .cia, .cxi, .zip, .rar, .7z) automatisch", "Détecte automatiquement les fichiers 3DS (.3ds, .cci, .cia, .cxi, .zip, .rar, .7z)", "自动检测 3DS 游戏文件 (.3ds, .cci, .cia, .cxi, .zip, .rar, .7z) 并加入任务", "3DSゲームファイル（.3ds, .cci, .cia, .cxi, .zip, .rar, .7z）を自動検出してタスク追加"),
            ["Автоматически обнаруживает файлы Switch (.nsp, .nsz, .xci, .xcz, архивы .zip, .rar, .7z) и отправляет в задачи"] = ("Automatically detects Switch files (.nsp, .nsz, .xci, .xcz, .zip, .rar, .7z) and queues tasks", "Erkennt Switch-Dateien (.nsp, .nsz, .xci, .xcz, .zip, .rar, .7z) automatisch", "Détecte automatiquement les fichiers Switch (.nsp, .nsz, .xci, .xcz, .zip, .rar, .7z)", "自动检测 Switch 游戏文件 (.nsp, .nsz, .xci, .xcz, .zip, .rar, .7z) 并加入任务", "Switchゲームファイル（.nsp, .nsz, .xci, .xcz, .zip, .rar, .7z）を自動検出してタスク追加"),
            ["Автоматически объединять базовую игру, обновления и все DLC в единый монолитный файл формата 1G+1U+ND"] = ("Automatically merge base game, updates, and all DLC into a single monolithic 1G+1U+ND format file", "Basisspiel, Updates und alle DLCs automatisch in einer monolithischen 1G+1U+ND-Datei zusammenführen", "Fusionner automatiquement le jeu de base, les mises à jour et tous les DLC en un seul fichier monolithique 1G+1U+ND", "自动将游戏本体、更新补丁及全部 DLC 合并为 1G+1U+ND 格式的单体文件", "ベースゲーム、更新データ、全DLCを 1G+1U+ND 形式の単一ファイルに自動統合します"),
            ["Автоматически разбивает готовые файлы > 4 ГБ на чанки (.xc0/.xc1 или .00/.01) для FAT32 SD-карт"] = ("Automatically splits files > 4 GB into chunks (.xc0/.xc1 or .00/.01) for FAT32 SD cards", "Teilt Dateien > 4 GB automatisch für FAT32-SD-Karten auf", "Divise automatiquement les fichiers > 4 Go pour les cartes SD FAT32", "自动将大于4GB的文件拆分为分卷，兼容FAT32格式内存卡", "4GBを超えるファイルをFAT32 SDカード用に自動分割"),
            ["Автоматически раскладывает готовые файлы по папкам и добавляет [TitleID] [vVersion] в имя файла."] = ("Automatically organizes output files into folders and appends [TitleID] [vVersion] to the filename.", "Sortiert fertige Dateien automatisch in Ordner ein und hängt [TitleID] [vVersion] an den Dateinamen an.", "Organise automatiquement les fichiers terminés dans des dossiers et ajoute [TitleID] [vVersion] au nom du fichier.", "自动将生成的文件整理到对应文件夹，并在文件名中追加 [TitleID] [vVersion] 标签。", "完成したファイルを自動的にフォルダへ仕分けし、ファイル名に [TitleID] [vVersion] を付与します。"),
            ["Автоматически распознать части/игры из файлов раздачи и названия темы"] = ("Automatically recognize parts/games from torrent files and topic title", "Teile/Spiele anhand von Torrent-Dateien und Thread-Titel automatisch erkennen", "Reconnaître automatiquement les parties/jeux à partir des fichiers du torrent et du titre du sujet", "自动从种子文件和帖子标题中识别各分卷/游戏", "配布ファイルおよびトピック名からパート／ゲームを自動認識します"),
            ["Автоматически удаляет оригинальный входной файл после успешной конвертации или сборки"] = ("Automatically deletes input files after 100% error-free completion", "Löscht Eingabedateien nach 100% fehlerfreier Verarbeitung automatisch", "Supprime automatiquement les fichiers d'entrée après une exécution 100 % sans erreur", "在转换或构建100%无误完成后自动删除原始输入文件", "タスクが正常に完了した後、元の入力ファイルを自動的に削除"),
            ["Автоматический обход Cloudflare..."] = ("Automatic Cloudflare bypass...", "Automatischer Cloudflare-Bypass...", "Contournement automatique de Cloudflare...", "正在自动绕过 Cloudflare 防护...", "Cloudflare を自動回避中..."),
            ["Автоматический поиск ключей Switch 2 в системных папках"] = ("Automatically scan for Switch 2 keys in system folders", "Automatische Suche nach Switch 2-Schlüsseln in Systemordnern", "Recherche automatique des clés Switch 2 dans les dossiers système", "自动在系统文件夹中扫描 Switch 2 密钥", "システムフォルダ内の Switch 2 キーを自動検索"),
            ["Автоматическое освобождение дискового пространства: удаление входных архивов и дампов строго при 100% успехе"] = ("Automatic disk space cleanup: deletes source archives and dumps only upon 100% success", "Automatische Festplattenbereinigung: Löscht Quellarchive und Dumps nur bei 100 % Erfolg", "Libération automatique d'espace disque : suppression des archives et dumps sources uniquement en cas de succès à 100 %", "自动释放磁盘空间：仅在构建 100% 成功时删除源压缩包及镜像转储", "ディスク領域の自動解放：ビルドが100%成功した場合に限りソースアーカイブとダンプを削除します"),
            ["Автоматическое скачивание и установка свежих prod.keys и title.keys из облачного репозитория STORM SWITCH"] = ("Automatically download and install latest prod.keys and title.keys from the STORM SWITCH cloud repository", "Neueste prod.keys und title.keys automatisch aus dem STORM SWITCH Cloud-Repository herunterladen und installieren", "Téléchargement et installation automatiques des derniers fichiers prod.keys et title.keys depuis le cloud STORM SWITCH", "自动从 STORM SWITCH 云端存储库下载并安装最新的 prod.keys 和 title.keys", "STORM SWITCH クラウド リポジトリから最新の prod.keys と title.keys を自動ダウンロード・適用"),
            ["Автоматическое удаление пустых областей 0xFF/0x00 и нормализация смещений секторов PFS0 для экономии места"] = ("Automatically removes 0xFF/0x00 padding and normalizes PFS0 sector offsets to save space", "Entfernt 0xFF/0x00-Leerbereiche automatisch und normalisiert PFS0-Sektor-Offsets, um Speicherplatz zu sparen", "Suppression automatique du rembourrage 0xFF/0x00 et normalisation des décalages de secteurs PFS0 pour économiser l'espace", "自动剔除 0xFF/0x00 空白填充并标准化 PFS0 扇区偏移以节省磁盘空间", "空領域 0xFF/0x00 を自動削除し、PFS0 セクタオフセットを正規化して容量を削減します"),
            ["Активный файл: Не указан (нажмите «Авто-поиск» или «Выбрать файл»)"] = ("Active file: Not specified (click \"Auto-Detect\" or \"Select File\")", "Aktive Datei: Nicht angegeben (klicken Sie auf „Autosuche“ oder „Datei wählen“)", "Fichier actif : non spécifié (cliquez sur « Recherche auto » ou « Choisir un fichier »)", "当前文件：未指定 (请点击“自动查找”或“选择文件”)", "アクティブなファイル：未指定（「自動検出」または「ファイルを選択」をクリック）"),
            ["Алгоритмы сборки и очистки Switch"] = ("Switch Build & Cleanup Algorithms", "Switch Erstellungs- & Bereinigungsalgorithmen", "Algorithmes de compilation et nettoyage Switch", "Switch 构建与清理算法", "Switch ビルド＆クリーンアップアルゴリズム"),
            ["Алгоритмы сборки и форматы Nintendo 3DS"] = ("Nintendo 3DS Build & Format Algorithms", "Nintendo 3DS Erstellungs- & Formatalgorithmen", "Algorithmes et formats Nintendo 3DS", "Nintendo 3DS 构建与格式算法", "Nintendo 3DS ビルド＆形式アルゴリズム"),
            ["Алый Красный"] = ("Scarlet Red", "Scharlachrot", "Rouge écarlate", "猩红色", "スカーレットレッド"),
            ["Анализирует собранные дополнения и выводит список недостающих DLC из базы TitleDB."] = ("Analyzes collected add-ons and lists missing DLC from TitleDB.", "Analysiert vorhandene DLCs und listet fehlende DLCs anhand von TitleDB auf.", "Analyse les extensions collectées et liste les DLC manquants depuis TitleDB.", "分析已整合的追加内容，并根据 TitleDB 数据库列出缺失的 DLC。", "収集した追加コンテンツを分析し、TitleDB データベースに基づき未所持の DLC を一覧表示します。"),
            ["Аудитор и дедупликатор коллекции игр..."] = ("Game collection auditor and deduplicator...", "Spielesammlung-Auditor und Deduplizierer...", "Audit et déduplication de la collection de jeux...", "游戏库审计与重复数据清理器...", "ゲームコレクション監査および重複排除ツール..."),
            ["Баланс (уровень 10)"] = ("Balanced (level 10)", "Ausgewogen (Stufe 10)", "Équilibré (niveau 10)", "均衡 (级别 10)", "バランス (レベル 10)"),
            ["Без изменений"] = ("No changes", "Unverändert", "Inchangé", "保持原样", "変更なし"),
            ["Безопасность, совместимость и верификация Switch 2"] = ("Switch 2 Security, Compatibility and Verification", "Switch 2 Sicherheit, Kompatibilität und Verifizierung", "Sécurité, compatibilité et vérification Switch 2", "Switch 2 安全性、兼容性与完整性验证", "Switch 2 セキュリティ、互換性および検証"),
            ["Библиотека игр"] = ("Game Library", "Spielebibliothek", "Bibliothèque de jeux", "任天堂游戏库", "ゲームライブラリ"),
            ["Блочное сжатие Zstandard для Switch 2 (NSZ / XCZ)"] = ("Zstandard block compression for Switch 2 (NSZ / XCZ)", "Zstandard-Blockkomprimierung für Switch 2 (NSZ / XCZ)", "Compression par blocs Zstandard pour Switch 2 (NSZ / XCZ)", "适用于 Switch 2 的 Zstandard 块压缩 (NSZ / XCZ)", "Switch 2 用 Zstandard ブロック圧縮 (NSZ / XCZ)"),
            ["Быстрое (уровень 3)"] = ("Fast (level 3)", "Schnell (Stufe 3)", "Rapide (niveau 3)", "快速 (级别 3)", "高速 (レベル 3)"),
            ["Быстрое сшивание базового NCA и обновления без пересборки RomFS"] = ("Fast merging of base NCA and update without rebuilding RomFS", "Schnelles Zusammenführen von Basis-NCA und Update ohne RomFS-Neubau", "Fusion rapide du NCA de base et de la mise à jour sans reconstruire RomFS", "快速合并基础 NCA 与更新补丁（无需重建 RomFS）", "RomFS を再構築することなくベース NCA と更新データを高速結合"),
            ["В конец"] = ("To bottom", "Ans Ende", "À la fin", "跳至末尾", "最後へ"),
            ["В начало"] = ("To top", "An den Anfang", "Au début", "跳至开头", "最初へ"),
            ["В трей"] = ("To tray", "In den Infobereich", "Vers la barre des tâches", "最小化到托盘", "トレイへ最小化"),
            ["Введение"] = ("Introduction", "Einführung", "Introduction", "简介", "はじめに"),
            ["Введите английское название игры (напр. Aggelos)"] = ("Enter English game title (e.g. Aggelos)", "Geben Sie den englischen Spieltitel ein (z. B. Aggelos)", "Entrez le titre du jeu en anglais (ex. Aggelos)", "输入游戏英文名称 (例如 Aggelos)", "ゲームの英語タイトルを入力（例：Aggelos）"),
            ["Введите имя издателя или автора (напр. PQube / Wonderboy Bobi)"] = ("Enter publisher or author name (e.g. PQube / Wonderboy Bobi)", "Geben Sie den Publisher- oder Autorennamen ein (z. B. PQube / Wonderboy Bobi)", "Entrez le nom de l'éditeur ou de l'auteur (ex. PQube / Wonderboy Bobi)", "输入发行商或作者名称 (例如 PQube / Wonderboy Bobi)", "パブリッシャーまたは開発者名を入力（例：PQube / Wonderboy Bobi）"),
            ["Введите название игры (напр. Aggelos, Zelda), ссылку на тему RuTracker, Magnet или ID темы..."] = ("Enter game title (e.g. Aggelos, Zelda), RuTracker link, Magnet link, or topic ID...", "Geben Sie einen Spieltitel (z. B. Aggelos, Zelda), RuTracker-Link, Magnet-Link oder die Themen-ID ein...", "Entrez le titre du jeu (ex. Aggelos, Zelda), un lien RuTracker, un lien Magnet ou l'ID du sujet...", "输入游戏名称 (如 Aggelos, Zelda)、RuTracker 帖子链接、磁力链接或主题 ID...", "ゲーム名（例：Aggelos、Zelda）、RuTracker リンク、Magnet リンク、またはトピック ID を入力..."),
            ["Введите номер страницы и нажмите Enter"] = ("Enter page number and press Enter", "Seitennummer eingeben und Enter drücken", "Entrez le numéro de page et appuyez sur Entrée", "输入页码并按 Enter 确认", "ページ番号を入力して Enter キーを押してください"),
            ["Введите пароль..."] = ("Enter password...", "Passwort eingeben...", "Entrez le mot de passe...", "输入密码...", "パスワードを入力..."),
            ["Введите русское название игры (напр. Аггелос)"] = ("Enter Russian game title (e.g. Aggelos)", "Geben Sie den russischen Spieltitel ein (z. B. Aggelos)", "Entrez le titre du jeu en russe (ex. Aggelos)", "输入游戏俄语/本地化名称 (例如 Aggelos)", "ゲームのロシア語タイトルを入力（例：Aggelos）"),
            ["Вверху слева"] = ("Top left", "Oben links", "En haut à gauche", "左上角", "左上"),
            ["Вверху справа"] = ("Top right", "Oben rechts", "En haut à droite", "右上角", "右上"),
            ["Версия"] = ("Version", "Version", "Version", "版本", "バージョン"),
            ["Версия ключей прошивки (6 ячеек)"] = ("Firmware keys version (6 slots)", "Firmware-Schlüsselversion (6 Slots)", "Version des clés du micrologiciel (6 emplacements)", "固件密钥版本 (6 槽位)", "ファームウェアキーバージョン (6スロット)"),
            ["Версия:"] = ("Version:", "Version:", "Version :", "版本：", "バージョン:"),
            ["Видеозахват:"] = ("Video capture:", "Videoaufnahme:", "Capture vidéo :", "视频捕捉：", "動画キャプチャ:"),
            ["Визуальный инспектор RomFS / ExeFS..."] = ("Visual RomFS / ExeFS Inspector...", "Visueller RomFS / ExeFS Inspektor...", "Inspecteur visuel RomFS / ExeFS...", "RomFS / ExeFS 可视化检查器...", "RomFS / ExeFS ビジュアルインスペクター..."),
            ["Внизу слева"] = ("Bottom left", "Unten links", "En bas à gauche", "左下角", "左下"),
            ["Внизу справа"] = ("Bottom right", "Unten rechts", "En bas à droite", "右下角", "右下"),
            ["Внутр. имя (English, напр. Aggelos)"] = ("Internal title (English, e.g. Aggelos)", "Interner Name (Englisch, z. B. Aggelos)", "Nom interne (Anglais, ex. Aggelos)", "内部名称 (英文，例如 Aggelos)", "内部タイトル (英語、例：Aggelos)"),
            ["Внутр. имя (Русское, напр. Аггелос)"] = ("Internal title (Russian, e.g. Aggelos)", "Interner Name (Russisch, z. B. Aggelos)", "Nom interne (Russe, ex. Aggelos)", "内部名称 (俄文，例如 Aggelos)", "内部タイトル (ロシア語、例：Aggelos)"),
            ["Внутреннее английское название игры (вшивается в NACP / SMDH контейнера)"] = ("Internal English game title (embedded in container NACP / SMDH)", "Interner englischer Spieltitel (im NACP / SMDH des Containers eingebettet)", "Titre interne du jeu en anglais (intégré dans le NACP / SMDH du conteneur)", "游戏内部英文名称 (嵌入容器 NACP / SMDH 中)", "内部英語ゲームタイトル (コンテナの NACP / SMDH に埋め込み)"),
            ["Внутреннее русское название игры (вшивается в NACP / SMDH контейнера)"] = ("Internal Russian game title (embedded in container NACP / SMDH)", "Interner russischer Spieltitel (im NACP / SMDH des Containers eingebettet)", "Titre interne du jeu en russe (intégré dans le NACP / SMDH du conteneur)", "游戏内部俄文名称 (嵌入容器 NACP / SMDH 中)", "内部ロシア語ゲームタイトル (コンテナの NACP / SMDH に埋め込み)"),
            ["Возобновить выполнение задачи"] = ("Resume task execution", "Aufgabenausführung fortsetzen", "Reprendre l'exécution de la tâche", "恢复执行任务", "タスクの実行を再開"),
            ["Возобновить задачу"] = ("Resume task", "Aufgabe fortsetzen", "Reprendre la tâche", "恢复任务", "タスクを再開"),
            ["Возрастной рейтинг:"] = ("Age rating:", "Altersfreigabe:", "Classification par âge :", "年龄评级：", "対象年齢:"),
            ["Воспроизводить звуковой сигнал по окончании выполнения очереди задач"] = ("Play audio cue upon task queue completion", "Signalton nach Abschluss der Aufgabenwarteschlange abspielen", "Jouer un signal sonore à la fin de la file d'attente", "任务队列处理完成时播放提示音", "タスクキュー完了時にサウンド通知を再生"),
            ["Восстановить исходный исполняемый файл эмулятора из резервной копии .bak"] = ("Restore original emulator executable from .bak backup", "Originale Emulator-Programmdatei aus .bak-Sicherung wiederherstellen", "Restaurer l'exécutable d'origine de l'émulateur depuis la sauvegarde .bak", "从 .bak 备份中恢复模拟器原始可执行文件", ".bak バックアップからエミュレータの元の実行ファイルを復元"),
            ["Время"] = ("Time", "Zeit", "Temps", "时间", "時間"),
            ["Все"] = ("All", "Alle", "Tous", "全部", "すべて"),
            ["Все жанры"] = ("All Genres", "Alle Genres", "Tous les genres", "所有类型", "すべてのジャンル"),
            ["Все игры"] = ("All Games", "Alle Spiele", "Tous les jeux", "全部游戏", "すべてのゲーム"),
            ["Все издатели"] = ("All Publishers", "Alle Publisher", "Tous les éditeurs", "所有发行商", "すべてのメーカー"),
            ["Все обработки"] = ("All processing types", "Alle Verarbeitungen", "Tous les traitements", "所有处理类型", "すべての処理"),
            ["Все разделы"] = ("All categories", "Alle Kategorien", "Toutes les catégories", "所有分类", "すべてのカテゴリ"),
            ["Все разработчики"] = ("All Developers", "Alle Entwickler", "Tous les développeurs", "所有开发商", "すべての開発元"),
            ["Все статусы"] = ("All statuses", "Alle Status", "Tous les statuts", "所有状态", "すべてのステータス"),
            ["Все языки"] = ("All languages", "Alle Sprachen", "Toutes les langues", "全部语言", "すべての言語"),
            ["Вставьте значение cookie bb_session из браузера..."] = ("Paste bb_session cookie value from browser...", "bb_session Cookie-Wert aus dem Browser einfügen...", "Collez la valeur du cookie bb_session du navigateur...", "从浏览器中粘贴 bb_session Cookie 值...", "ブラウザから bb_session クッキーの値を貼り付け..."),
            ["Входная папка:"] = ("Input folder:", "Eingabeordner:", "Dossier source :", "输入文件夹：", "入力フォルダ:"),
            ["Входные файлы:"] = ("Input files:", "Eingabedateien:", "Fichiers sources :", "输入文件：", "入力ファイル:"),
            ["Вшивает проверенные сообществом твики графики и 60 FPS прямо в модуль ExeFS main."] = ("Injects community-tested graphics tweaks and 60 FPS patches directly into the ExeFS main module.", "Bettet von der Community getestete Grafik-Tweaks und 60-FPS-Patches direkt in das ExeFS-Main-Modul ein.", "Injecte les réglages graphiques et correctifs 60 FPS validés par la communauté directement dans le module principal ExeFS.", "将社区经过验证的画质优化与 60 FPS 补丁直接植入 ExeFS main 模块中。", "コミュニティ検証済みのグラフィック調整および 60 FPS パッチを ExeFS main モジュールへ直接適用します。"),
            ["Вшить DLC UNLOCKER (автоизвлечение и разблокировка всех дополнений в RomFS — авто-режим)"] = ("Inject DLC UNLOCKER (auto-extract and unlock all add-ons in RomFS — auto mode)", "DLC UNLOCKER einbinden (automatisches Entpacken und Freischalten aller DLCs im RomFS — Auto-Modus)", "Injecter DLC UNLOCKER (extraction auto et déverrouillage de tous les DLC dans RomFS — mode auto)", "注入 DLC UNLOCKER (自动提取并在 RomFS 中解锁所有追加内容 — 自动模式)", "DLC UNLOCKER 適用 (RomFS 内の全DLC自動抽出およびロック解除 — 自動モード)"),
            ["Выберите версию ключей прошивки HOS"] = ("Select HOS firmware keys version", "Wählen Sie die HOS-Firmware-Schlüsselversion", "Sélectionnez la version des clés de firmware HOS", "选择 HOS 固件密钥版本", "HOS ファームウェアキーのバージョンを選択"),
            ["Выберите задачу в таблице для просмотра параметров и лога"] = ("Select a task in the table to view parameters and log", "Wählen Sie eine Aufgabe in der Tabelle, um Parameter und Protokoll anzuzeigen", "Sélectionnez une tâche dans le tableau pour afficher les paramètres et le journal", "在表格中选择一项任务以查看参数和日志", "パラメータとログを確認するには、一覧からタスクを選択してください"),
            ["Выберите итоговое расширение контейнера для этой игры (NSP, NSZ, XCI, XCZ)"] = ("Select output container format for this game (NSP, NSZ, XCI, XCZ)", "Wählen Sie das Ausgabe-Containerformat für dieses Spiel (NSP, NSZ, XCI, XCZ)", "Sélectionnez l'extension du conteneur de sortie pour ce jeu (NSP, NSZ, XCI, XCZ)", "选择此游戏的最终输出容器格式 (NSP, NSZ, XCI, XCZ)", "このゲームの出力コンテナ形式を選択（NSP, NSZ, XCI, XCZ）"),
            ["Выберите любой файл слева для мгновенного предпросмотра первых 4 КБ без распаковки образа."] = ("Select any file on the left for instant 4 KB preview without unpacking the image.", "Wählen Sie links eine Datei für eine sofortige 4-KB-Vorschau ohne Entpacken des Images.", "Sélectionnez un fichier à gauche pour un aperçu instantané des 4 premiers Ko sans décompresser l'image.", "在左侧选择任意文件即可即时预览前 4 KB 内容，无需解压镜像。", "左側からファイルを選択すると、イメージを展開することなく先頭 4 KB を即座にプレビューできます。"),
            ["Выберите основной язык приложения (применяется мгновенно)"] = ("Choose the main application language (applied immediately)", "Wählen Sie die Hauptsprache der Anwendung (wird sofort angewendet)", "Choisissez la langue principale de l'application (appliquée immédiatement)", "选择应用程序主要显示语言（即时生效）", "アプリケーションの表示言語を選択します（即座に適用）"),
            ["Выберите папку с играми Switch / 3DS и нажмите «Сканировать коллекцию»."] = ("Select folder with Switch / 3DS games and click \"Scan Collection\".", "Wählen Sie den Ordner mit Switch / 3DS-Spielen und klicken Sie auf „Sammlung scannen“.", "Sélectionnez le dossier contenant les jeux Switch / 3DS et cliquez sur « Analyser la collection ».", "选择存放 Switch / 3DS 游戏的文件夹，然后点击“扫描游戏库”。", "Switch / 3DS ゲームのあるフォルダを選択し、「コレクションをスキャン」をクリックしてください。"),
            ["Выберите языки, которые нужно ОСТАВИТЬ при вырезании:"] = ("Select languages to KEEP when trimming:", "Wählen Sie Sprachen, die BEIBEHALTEN werden sollen:", "Sélectionnez les langues à CONSERVER :", "请勾选精简时需要保留的语言：", "削除時に保持する言語を選択してください："),
            ["Выбор главного акцентного цвета элементов приложения"] = ("Choose primary accent color for UI elements", "Hauptakzentfarbe für Elemente wählen", "Choisissez la couleur d'accentuation principale des éléments", "选择界面主要交互元素的高亮强调颜色", "UI要素のメインアクセントカラーを選択"),
            ["Выбор общей стилистической темы оформления приложения"] = ("Select visual style and color scheme for the application", "Wählen Sie das visuelle Gesamtdesign der Anwendung", "Sélectionnez le style visuel et la palette de couleurs de l'application", "选择应用程序的整体界面配色方案", "アプリケーションのビジュアルテーマを選択"),
            ["Выбор уровня сжатия алгоритма Zstd (от Быстрого 3 до Ультра 22)"] = ("Select Zstd compression level (from Fast 3 to Ultra 22)", "Zstd-Komprimierungsstufe wählen (von Schnell 3 bis Ultra 22)", "Sélectionnez le niveau de compression Zstd (de Rapide 3 à Ultra 22)", "选择Zstd压缩算法级别（从 快速3 到 极致22）", "Zstd圧縮レベルを選択（高速3からウルトラ22まで）"),
            ["Выбрать .exe эмулятора для применения обратимого патча Direct Boot"] = ("Select emulator executable (.exe) to apply reversible Direct Boot patch", "Emulator-.exe auswählen, um reversiblen Direct-Boot-Patch anzuwenden", "Sélectionner le fichier .exe de l'émulateur pour appliquer le patch réversible Direct Boot", "选择模拟器 .exe 可执行文件以应用可逆的 Direct Boot 补丁", "リバーシブルな Direct Boot パッチを適用するエミュレータの .exe を選択"),
            ["Выбрать все"] = ("Select all", "Alle auswählen", "Tout sélectionner", "全选", "すべて選択"),
            ["Выбрать папку"] = ("Select folder", "Ordner auswählen", "Sélectionner un dossier", "选择文件夹", "フォルダを選択"),
            ["Выбрать папку..."] = ("Choose Folder...", "Ordner wählen...", "Choisir un dossier...", "选择文件夹...", "フォルダを選択..."),
            ["Выбрать свою картинку PNG/JPG для иконки этой игры (256x256)"] = ("Select custom PNG/JPG image for this game's icon (256x256)", "Eigenes PNG/JPG-Bild für das Symbol dieses Spiels wählen (256x256)", "Sélectionner une image PNG/JPG personnalisée pour l'icône de ce jeu (256x256)", "为此游戏选择自定义 PNG/JPG 图标 (256x256)", "このゲーム用のカスタム PNG/JPG アイコン画像を選択（256x256）"),
            ["Выполнение"] = ("Executing", "In Bearbeitung", "En cours", "执行中", "実行中"),
            ["Выполняемая задача и формат:"] = ("Task and format:", "Aktuelle Aufgabe und Format:", "Tâche et format :", "当前任务与格式：", "実行タスクおよびフォーマット:"),
            ["Выходная папка"] = ("Output folder", "Ausgabeordner", "Dossier de sortie", "输出文件夹", "出力フォルダ"),
            ["Выходная папка 3DS по умолчанию"] = ("Default 3DS Output Folder", "Standardmäßiger 3DS-Ausgabeordner", "Dossier de sortie 3DS par défaut", "3DS 默认输出文件夹", "3DS デフォルト出力フォルダ"),
            ["Выходная папка Nintendo Switch 2 по умолчанию"] = ("Default Nintendo Switch 2 output folder", "Standard-Ausgabeordner für Nintendo Switch 2", "Dossier de sortie Nintendo Switch 2 par défaut", "默认 Nintendo Switch 2 输出文件夹", "デフォルトの Nintendo Switch 2 出力フォルダ"),
            ["Выходная папка Switch по умолчанию"] = ("Default Switch Output Folder", "Standardmäßiger Switch-Ausgabeordner", "Dossier de sortie Switch par défaut", "Switch 默认输出文件夹", "Switch デフォルト出力フォルダ"),
            ["Выходная папка:"] = ("Output folder:", "Ausgabeordner:", "Dossier de sortie :", "输出文件夹：", "出力フォルダ:"),
            ["Выходной формат 3DS по умолчанию"] = ("Default 3DS Output Format", "Standardmäßiges 3DS-Ausgabeformat", "Format de sortie 3DS par défaut", "3DS 默认输出格式", "3DS デフォルト出力形式"),
            ["Выходной формат для 3DS"] = ("Output format for 3DS", "Ausgabeformat für 3DS", "Format de sortie pour 3DS", "3DS 输出格式", "3DS 出力フォーマット"),
            ["Выходной формат для Switch / Switch 2"] = ("Output format for Switch / Switch 2", "Ausgabeformat für Switch / Switch 2", "Format de sortie pour Switch / Switch 2", "Switch / Switch 2 输出格式", "Switch / Switch 2 出力フォーマット"),
            ["ГОТОВ К РАБОТЕ"] = ("READY TO WORK", "BEREIT", "PRÊT À L'EMPLOI", "准备就绪", "準備完了"),
            ["Генерация универсальных билетов и снятие консольных ограничений для мгновенного доступа ко всем установленным DLC"] = ("Generate universal tickets and remove console restrictions for instant access to all installed DLC", "Universelle Tickets generieren und Konsolenbeschränkungen aufheben für sofortigen Zugriff auf alle installierten DLCs", "Génération de tickets universels et suppression des restrictions console pour un accès instantané à tous les DLC installés", "生成通用票证并解除主机签名限制，以便即刻访问已安装的全部 DLC", "ユニバーサルチケットを生成し本体制限を解除して、インストール済み全DLCへの即時アクセスを可能にします"),
            ["Готов к виртуальному сканированию контейнера."] = ("Ready for virtual container scanning.", "Bereit für den virtuellen Containerscan.", "Prêt pour l'analyse virtuelle du conteneur.", "准备执行虚拟容器扫描。", "仮想コンテナのスキャン準備が完了しました。"),
            ["Готов к передаче. HTTP-сервер раздачи запущен автоматически."] = ("Ready for transfer. Distribution HTTP server started automatically.", "Bereit zur Übertragung. Lokaler HTTP-Server wurde automatisch gestartet.", "Prêt pour le transfert. Le serveur de distribution HTTP a démarré automatiquement.", "准备传输。分发 HTTP 服务器已自动启动。", "転送準備完了。配信 HTTP サーバーが自動起動しました。"),
            ["Готов к поиску и загрузке игр или облачных модификаций."] = ("Ready to search and download games or cloud mods.", "Bereit zum Suchen und Herunterladen von Spielen oder Cloud-Modifikationen.", "Prêt pour la recherche et le téléchargement de jeux ou de mods cloud.", "准备搜索并下载游戏或云端模组。", "ゲームまたはクラウド MOD の検索およびダウンロード準備完了。"),
            ["Готово"] = ("Ready", "Fertig", "Prêt", "完成", "完了"),
            ["Да"] = ("Yes", "Ja", "Oui", "是", "はい"),
            ["Дата выхода:"] = ("Release Date:", "Erscheinungsdatum:", "Date de sortie :", "发售日期：", "発売日："),
            ["Детальная информация:"] = ("Details:", "Detaillierte Informationen:", "Détails :", "详细信息：", "詳細情報:"),
            ["Дешифровано"] = ("Decrypted", "Entschlüsselt", "Déchiffré", "已解密", "復号済み"),
            ["Для всех | 0100000000010000"] = ("For all | 0100000000010000", "Für alle | 0100000000010000", "Pour tous | 0100000000010000", "通用 | 0100000000010000", "共通 | 0100000000010000"),
            ["Добавить"] = ("Add", "Hinzufügen", "Ajouter", "添加", "追加"),
            ["Добавить ещё одну часть / игру (до 10+ отдельных задач из одной раздачи)"] = ("Add another part/game (up to 10+ separate tasks from a single release)", "Weiteren Teil/Spiel hinzufügen (bis zu 10+ separate Aufgaben aus einem Release)", "Ajouter une autre partie/jeu (jusqu'à 10+ tâches distinctes d'un même partage)", "添加另一个分卷/游戏 (单个发布可拆分为 10+ 个独立任务)", "別のパート/ゲームを追加（単一の配布から10個以上の個別タスクを作成可能）"),
            ["Добавить папку..."] = ("Add folder...", "Ordner hinzufügen...", "Ajouter un dossier...", "添加文件夹...", "フォルダを追加..."),
            ["Добавить файлы"] = ("Add Files", "Dateien hinzufügen", "Ajouter des fichiers", "添加文件", "ファイルを追加"),
            ["Добавить файлы..."] = ("Add files...", "Dateien hinzufügen...", "Ajouter des fichiers...", "添加文件...", "ファイルを追加..."),
            ["Добавлять технические теги [TitleID] [vXXX] (1G+1U) к итоговому имени файла"] = ("Append technical tags [TitleID] [vXXX] (1G+1U) to output filename", "Technische Tags [TitleID] [vXXX] (1G+1U) an den Ausgabedateinamen anhängen", "Ajouter les balises techniques [TitleID] [vXXX] (1G+1U) au nom du fichier de sortie", "在输出文件名末尾追加技术标签 [TitleID] [vXXX] (1G+1U)", "出力ファイル名に技術タグ [TitleID] [vXXX] (1G+1U) を追加する"),
            ["Добавьте 2 или более игр Switch (.NSP, .NSZ, .XCI, .XCZ) для упаковки в единый мульти-картридж для MIG Switch, эмуляторов и коллекций сборников."] = ("Add 2 or more Switch games (.NSP, .NSZ, .XCI, .XCZ) to pack into a single multi-cartridge for MIG Switch, emulators, and compilations.", "Fügen Sie 2 oder mehr Switch-Spiele (.NSP, .NSZ, .XCI, .XCZ) hinzu, um sie in ein Multi-Cartridge für MIG Switch, Emulatoren und Sammlungen zu packen.", "Ajoutez 2 jeux Switch ou plus (.NSP, .NSZ, .XCI, .XCZ) pour les regrouper dans une multi-cartouche pour MIG Switch, les émulateurs et les compilations.", "添加 2 款或更多 Switch 游戏 (.NSP, .NSZ, .XCI, .XCZ) 以打包为用于 MIG Switch、模拟器和合集收藏的单体多合一卡带。", "MIG Switch、エミュレータ、コレクション用に、2本以上の Switch ゲーム（.NSP, .NSZ, .XCI, .XCZ）を追加して単一のマルチカートリッジにまとめます。"),
            ["Доп. ссылки только для этой части (русификатор / обновление / DLC — через пробел или запятую)..."] = ("Additional links for this part only (localization / update / DLC — separated by space or comma)...", "Zusätzliche Links nur für diesen Teil (Übersetzung / Update / DLC — getrennt durch Leerzeichen oder Komma)...", "Liens supplémentaires pour cette partie uniquement (traduction / mise à jour / DLC — séparés par un espace ou une virgule)...", "仅针对此部分的附加链接 (汉化 / 更新补丁 / DLC — 用空格或逗号分隔)...", "このパート専用の追加リンク（翻訳 / アップデート / DLC — スペースまたはカンマ区切り）..."),
            ["Если выключено — сохраняется ровно то имя выходного файла, которое указано в поле «Итоговое имя игры / выходного файла»"] = ("If disabled — keeps the exact output filename specified in the \"Output game / filename\" field", "Wenn deaktiviert, wird genau der Dateiname beibehalten, der im Feld „Ausgabedateiname“ angegeben ist", "Si désactivé, conserve exactement le nom de fichier spécifié dans le champ « Nom du jeu / fichier de sortie »", "若关闭 — 则严格保留“最终输出游戏/文件名”字段中指定的名称", "無効の場合 — 「出力ファイル名」フィールドで指定されたファイル名がそのまま使用されます"),
            ["Жанр:"] = ("Genre:", "Genre:", "Genre :", "游戏类型：", "ジャンル："),
            ["Журнал сборки:"] = ("Build log:", "Build-Protokoll:", "Journal de compilation :", "构建日志：", "ビルドログ:"),
            ["Загружать ключи для расшифровки. Можно перетащить файл прямо сюда"] = ("Load keys for decryption. You can drag and drop file directly here", "Schlüssel zur Entschlüsselung laden. Datei hierher ziehen", "Charger les clés de déchiffrement. Glissez et déposez le fichier ici", "用于解密ROM。可以直接将密钥文件拖放到此处", "復号に必要なキーを読み込みます。ここにファイルをドラッグ可能"),
            ["Загружено ключей: 0 шт."] = ("Keys loaded: 0", "Geladene Schlüssel: 0", "Clés chargées : 0", "已加载密钥：0 个", "ロード済みキー: 0 個"),
            ["Загрузка и анализ файлов..."] = ("Loading and analyzing files...", "Dateien werden geladen und analysiert...", "Chargement et analyse des fichiers...", "正在加载并分析文件...", "ファイルの読み込みと分析中..."),
            ["Загрузка из TitleDB / Tinfoil"] = ("Download from TitleDB / Tinfoil", "Download von TitleDB / Tinfoil", "Téléchargement depuis TitleDB / Tinfoil", "从 TitleDB / Tinfoil 下载", "TitleDB / Tinfoil からダウンロード"),
            ["Задача"] = ("Task", "Aufgabe", "Tâche", "任务", "タスク"),
            ["Закат Оранжевый"] = ("Sunset Orange", "Sonnenuntergangs-Orange", "Orange couchant", "日落橙", "サンセットオレンジ"),
            ["Закрыть"] = ("Close", "Schließen", "Fermer", "关闭", "閉じる"),
            ["Закрыть детальную информацию у всех задач"] = ("Collapse details for all tasks", "Details für alle Aufgaben ausblenden", "Masquer les détails de toutes les tâches", "收起所有任务的详细信息", "全タスクの詳細を閉じる"),
            ["Запустить все"] = ("Start All", "Alle starten", "Tout démarrer", "全部开始", "すべて開始"),
            ["Звуковые уведомления"] = ("Sound Notifications", "Tonbenachrichtigungen", "Notifications sonores", "声音通知", "サウンド通知"),
            ["Зеркало RuTracker"] = ("RuTracker Mirror", "RuTracker-Mirror", "Miroir RuTracker", "RuTracker 镜像站点", "RuTracker ミラー"),
            ["ИГРА #1"] = ("GAME #1", "SPIEL #1", "JEU #1", "游戏 #1", "ゲーム #1"),
            ["Игр: 0"] = ("Games: 0", "Spiele: 0", "Jeux : 0", "游戏数：0", "ゲーム数: 0"),
            ["Игра #1"] = ("Game #1", "Spiel #1", "Jeu #1", "游戏 #1", "ゲーム #1"),
            ["Игра #10"] = ("Game #10", "Spiel #10", "Jeu #10", "游戏 #10", "ゲーム #10"),
            ["Игра #2"] = ("Game #2", "Spiel #2", "Jeu #2", "游戏 #2", "ゲーム #2"),
            ["Игра #3"] = ("Game #3", "Spiel #3", "Jeu #3", "游戏 #3", "ゲーム #3"),
            ["Игра #4"] = ("Game #4", "Spiel #4", "Jeu #4", "游戏 #4", "ゲーム #4"),
            ["Игра #5"] = ("Game #5", "Spiel #5", "Jeu #5", "游戏 #5", "ゲーム #5"),
            ["Игра #6"] = ("Game #6", "Spiel #6", "Jeu #6", "游戏 #6", "ゲーム #6"),
            ["Игра #7"] = ("Game #7", "Spiel #7", "Jeu #7", "游戏 #7", "ゲーム #7"),
            ["Игра #8"] = ("Game #8", "Spiel #8", "Jeu #8", "游戏 #8", "ゲーム #8"),
            ["Игра #9"] = ("Game #9", "Spiel #9", "Jeu #9", "游戏 #9", "ゲーム #9"),
            ["Игры установятся на любом CFW без привязки к тикетам. Работает через NSC_Builder (--C_clean_ND)."] = ("Games will install on any CFW without ticket binding. Powered by NSC_Builder (--C_clean_ND).", "Spiele lassen sich auf jeder CFW ohne Ticket-Bindung installieren. Basiert auf NSC_Builder (--C_clean_ND).", "Les jeux s'installeront sur n'importe quel CFW sans dépendance aux tickets. Utilise NSC_Builder (--C_clean_ND).", "游戏可安装于任何免票证签名的自定义固件 (CFW)。基于 NSC_Builder (--C_clean_ND) 运作。", "チケット不要で任意の CFW にインストール可能。NSC_Builder (--C_clean_ND) を使用。"),
            ["Издание / Версия:"] = ("Edition / Version:", "Edition / Version:", "Édition / Version :", "版本 / 发行版：", "エディション / バージョン:"),
            ["Издатель / Разработчик"] = ("Publisher / Developer", "Publisher / Entwickler", "Éditeur / Développeur", "发行商 / 开发商", "パブリッシャー / 開発元"),
            ["Издатель / Разработчик (напр. PQube)"] = ("Publisher / Developer (e.g. PQube)", "Publisher / Entwickler (z. B. PQube)", "Éditeur / Développeur (ex. PQube)", "发行商 / 开发商 (如 PQube)", "パブリッシャー / 開発元 (例: PQube)"),
            ["Издатель / Разработчик игры (вшивается в NACP / SMDH контейнера)"] = ("Game Publisher / Developer (embedded into container NACP / SMDH)", "Publisher / Entwickler des Spiels (wird in Container NACP / SMDH eingebettet)", "Éditeur / Développeur du jeu (intégré dans le NACP / SMDH du conteneur)", "游戏发行商 / 开发商 (写入容器 NACP / SMDH 元数据)", "ゲームのパブリッシャー / 開発元（コンテナの NACP / SMDH に埋め込み）"),
            ["Издатель:"] = ("Publisher:", "Herausgeber:", "Éditeur :", "发行商：", "メーカー："),
            ["Изумрудный Зеленый"] = ("Emerald Green", "Smaragdgrün", "Vert émeraude", "翡翠绿", "エメラルドグリーン"),
            ["Иконка"] = ("Icon", "Symbol", "Icône", "图标", "アイコン"),
            ["Иконка / задача"] = ("Icon / task", "Symbol / Aufgabe", "Icône / tâche", "图标 / 任务", "アイコン / タスク"),
            ["Или Cookie bb_session (для входа без пароля)"] = ("Or Cookie bb_session (for passwordless login)", "Oder bb_session-Cookie (für passwortlose Anmeldung)", "Ou Cookie bb_session (pour connexion sans mot de passe)", "或 bb_session Cookie (用于免密登录)", "または bb_session クッキー（パスワードなしログイン用）"),
            ["Имя мода:"] = ("Mod name:", "Mod-Name:", "Nom du mod :", "模组名称：", "MOD名:"),
            ["Имя русификатора / мода"] = ("Localization / mod name", "Name der Übersetzung / Mod", "Nom de la traduction / du mod", "本地化 / 模组名称", "翻訳パッチ / MOD名"),
            ["Имя сборника (мульти-картриджа)"] = ("Collection name (multi-cartridge)", "Sammlungsname (Multi-Cartridge)", "Nom de la collection (multi-cartouche)", "合集名称 (多合一卡带)", "コレクション名（マルチカートリッジ）"),
            ["Имя файла:"] = ("Filename:", "Dateiname:", "Nom du fichier :", "文件名：", "ファイル名:"),
            ["Инспектор полноты дополнений (DLC Completeness)"] = ("DLC Completeness Inspector", "DLC-Vollständigkeits-Inspektor", "Inspecteur d'exhaustivité des DLC", "DLC 完整性检查器", "DLC 完全性インスペクター"),
            ["Инструкция"] = ("Guide", "Anleitung", "Guide", "使用说明", "ガイド"),
            ["Интеграция с эмуляторами (Путь к папке эмулятора)"] = ("Emulator Integration (Path to emulator directory)", "Emulator-Integration (Pfad zum Emulator-Ordner)", "Intégration d'émulateur (Chemin vers le dossier de l'émulateur)", "模拟器集成 (模拟器文件夹路径)", "エミュレータ統合 (エミュレータのフォルダパス)"),
            ["Интерактивный предпросмотр и симуляция"] = ("Interactive preview and simulation", "Interaktive Vorschau und Simulation", "Aperçu interactif et simulation", "交互式预览与模拟", "インタラクティブプレビューとシミュレーション"),
            ["Информация"] = ("Catalog", "Katalog", "Catalogue", "游戏库", "カタログ"),
            ["Использовать исходный джингл игры (до 3 сек IMA-ADPCM)"] = ("Use original game jingle (up to 3 sec IMA-ADPCM)", "Originalen Spiel-Jingle verwenden (bis zu 3 Sek. IMA-ADPCM)", "Utiliser le jingle d'origine du jeu (jusqu'à 3 s IMA-ADPCM)", "使用游戏原始片头音频 (最长 3 秒 IMA-ADPCM)", "ゲーム本来のジングルを使用（最大3秒 IMA-ADPCM）"),
            ["Использовать исходный джингл игры (до 3 сек. WAV PCM или BCWAV)"] = ("Use original game jingle (up to 3 sec. WAV PCM or BCWAV)", "Originalen Spiel-Jingle verwenden (bis zu 3 Sek. WAV PCM oder BCWAV)", "Utiliser le jingle d'origine du jeu (jusqu'à 3 s WAV PCM ou BCWAV)", "使用游戏原始片头音频 (最长 3 秒 WAV PCM 或 BCWAV)", "ゲーム本来のジングルを使用（最大3秒 WAV PCM または BCWAV）"),
            ["Использовать официальный компрессор nsz.exe с независимыми блоками по 1 МБ (NCZBLOCK) вместо встроенного движка"] = ("Use official nsz.exe compressor with independent 1 MB blocks (NCZBLOCK) instead of built-in engine", "Offiziellen nsz.exe-Kompressor mit unabhängigen 1-MB-Blöcken (NCZBLOCK) anstelle der integrierten Engine verwenden", "Utiliser le compresseur officiel nsz.exe avec des blocs indépendants de 1 Mo (NCZBLOCK) au lieu du moteur intégré", "使用官方 nsz.exe 压缩器及独立的 1 MB 数据块 (NCZBLOCK)，替代内置引擎", "内蔵エンジンの代わりに、独立 1 MB ブロック（NCZBLOCK）を採用した公式 nsz.exe コンプレッサを使用する"),
            ["Используется для дешифровки NCCH, CIA и RomFS при сборке и конвертации. Можно перетащить файл сюда"] = ("Used for NCCH, CIA, and RomFS decryption during builds and conversions. Drag file here", "Wird zur NCCH-, CIA- und RomFS-Entschlüsselung verwendet. Datei hierher ziehen", "Utilisé pour le déchiffrement NCCH, CIA et RomFS", "在构建和转换时用于解密NCCH、CIA和RomFS。可直接拖放文件", "ビルドおよび変換時のNCCH、CIA、RomFSの復号に使用。ファイルをドラッグ可能"),
            ["История"] = ("History", "Verlauf", "Historique", "历史记录", "履歴"),
            ["Итоговое имя выходного файла (напр. Aggelos (2018) [WW] [RUS]...)"] = ("Final output filename (e.g. Aggelos (2018) [WW] [RUS]...)", "Endgültiger Ausgabedateiname (z. B. Aggelos (2018) [WW] [RUS]...)", "Nom final du fichier de sortie (ex. Aggelos (2018) [WW] [RUS]...)", "最终输出文件名 (例如 Aggelos (2018) [WW] [RUS]...)", "最終出力ファイル名（例：Aggelos (2018) [WW] [RUS]...）"),
            ["Итоговое имя выходного файла на диске (.nsp / .nsz / .xci / .xcz)"] = ("Final output filename on disk (.nsp / .nsz / .xci / .xcz)", "Endgültiger Ausgabedateiname auf Datenträger (.nsp / .nsz / .xci / .xcz)", "Nom final du fichier de sortie sur disque (.nsp / .nsz / .xci / .xcz)", "磁盘上的最终输出文件名 (.nsp / .nsz / .xci / .xcz)", "ディスク上の最終出力ファイル名（.nsp / .nsz / .xci / .xcz）"),
            ["Итоговый формат (расширение) выходного файла для этой игры (NSP, NSZ, XCI, XCZ)"] = ("Output file format (extension) for this game (NSP, NSZ, XCI, XCZ)", "Ausgabeformat (Erweiterung) für dieses Spiel (NSP, NSZ, XCI, XCZ)", "Format (extension) du fichier de sortie pour ce jeu (NSP, NSZ, XCI, XCZ)", "此游戏的最终输出文件格式/扩展名 (NSP, NSZ, XCI, XCZ)", "このゲームの最終出力形式・拡張子（NSP, NSZ, XCI, XCZ）"),
            ["К какой игре/части относится этот файл раздачи"] = ("Which game/part this release file belongs to", "Zu welchem Spiel/Teil diese Release-Datei gehört", "À quel jeu/partie appartient ce fichier de release", "该分发文件所属的游戏/分卷", "この配布ファイルが属するゲーム/パート"),
            ["К какой игре/части применять этот русификатор / патч / DLC"] = ("Which game/part to apply this localization / patch / DLC to", "Auf welches Spiel/welchen Teil dieser Patch / diese Übersetzung / dieser DLC angewendet werden soll", "À quel jeu/partie appliquer cette traduction / ce patch / ce DLC", "该本地化/补丁/DLC 应用到哪个游戏/分卷", "この翻訳 / パッチ / DLC を適用する対象ゲーム/パート"),
            ["Каждая отмеченная часть создаст отдельную задачу со своим итоговым именем, форматом (NSP/NSZ/XCI/XCZ), набором файлов, русификатором и ссылками"] = ("Each checked part will create a separate task with its own output name, format (NSP/NSZ/XCI/XCZ), file set, mod, and links", "Jeder markierte Teil erstellt eine separate Aufgabe mit eigenem Ausgabenamen, Format (NSP/NSZ/XCI/XCZ), Dateisatz, Übersetzung und Links", "Chaque partie cochée créera une tâche distincte avec son propre nom de sortie, format (NSP/NSZ/XCI/XCZ), ensemble de fichiers, traduction et liens", "每个勾选的分卷都将创建为独立任务，拥有各自的输出名称、格式 (NSP/NSZ/XCI/XCZ)、文件集、汉化包及链接", "チェックしたパートごとに、独自の出力名、形式（NSP/NSZ/XCI/XCZ）、ファイルセット、翻訳、リンクを持つ個別タスクが作成されます"),
            ["Каталог для архивов SDMC"] = ("SDMC Archive Directory", "SDMC-Archivverzeichnis", "Répertoire des archives SDMC", "SDMC 归档目录", "SDMC アーカイブディレクトリ"),
            ["Каталог распакованных дампов Switch 2 (exefs/romfs) или архивов. По умолчанию: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS"] = ("Directory of extracted Switch 2 dumps (exefs/romfs) or archives. Default: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS", "Verzeichnis entpackter Switch 2 Dumps (exefs/romfs) oder Archive. Standard: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS", "Répertoire des dumps Switch 2 extraits (exefs/romfs) ou archives. Par défaut : Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS", "解压后的 Switch 2 转储 (exefs/romfs) 或压缩包目录。默认：Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS", "展開された Switch 2 ダンプ（exefs/romfs）またはアーカイブのディレクトリ。デフォルト: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS"),
            ["Ключи подключены"] = ("Keys connected", "Schlüssel geladen", "Clés connectées", "密钥已连接", "キー接続済み"),
            ["Коды ошибок (2xxx-xxxx)"] = ("Error codes (2xxx-xxxx)", "Fehlercodes (2xxx-xxxx)", "Codes d'erreur (2xxx-xxxx)", "错误代码 (2xxx-xxxx)", "エラーコード (2xxx-xxxx)"),
            ["Количество дополнений:"] = ("Number of add-ons:", "Anzahl der Add-ons:", "Nombre d'extensions :", "追加内容数量：", "追加コンテンツ数:"),
            ["Количество задач, выполняемых одновременно в фоновой очереди (от 1 до 5)"] = ("Number of tasks processed in parallel (1 to 5)", "Anzahl parallel ausgeführter Aufgaben (1 bis 5)", "Nombre de tâches traitées en parallèle (1 à 5)", "后台并发处理的任务数量（1 到 5）", "バックグラウンドで並行処理するタスク数（1〜5）"),
            ["Количество параллельных потоков ЦП для многопоточного сжатия Zstandard без зависаний системы"] = ("Number of parallel CPU threads for multithreaded Zstandard compression without system freezing", "Anzahl paralleler CPU-Threads für Multithread-Zstandard-Kompression ohne Systemhänger", "Nombre de threads CPU parallèles pour la compression multithread Zstandard sans blocage du système", "用于多线程 Zstandard 压缩的 CPU 并行线程数（防止系统卡顿）", "システムのフリーズを防ぎつつ Zstandard マルチスレッド圧縮を行う並列 CPU スレッド数"),
            ["Комплексная верификация хэшей всех блоков NCA, заголовков NPDM и структуры PFS0 сразу по завершении компиляции"] = ("Comprehensive verification of all NCA block hashes, NPDM headers, and PFS0 structure upon compilation completion", "Umfassende Überprüfung aller NCA-Block-Hashes, NPDM-Header und der PFS0-Struktur unmittelbar nach Fertigstellung", "Vérification complète des hachages de blocs NCA, des en-têtes NPDM et de la structure PFS0 dès la fin de la compilation", "编译完成后，立即全面验证所有 NCA 数据块哈希、NPDM 标头以及 PFS0 结构", "コンパイル完了直後に、全 NCA ブロックハッシュ、NPDM ヘッダー、および PFS0 構造を包括的に検証します"),
            ["Комплексное выходное имя файла по папкам"] = ("Organize output into structured subfolders", "Ausgabe in strukturierte Unterordner sortieren", "Organiser les sorties dans des sous-dossiers structurés", "按文件夹整理输出文件", "フォルダ別に整理して出力"),
            ["Кон. размер"] = ("Final size", "Endgröße", "Taille finale", "最终大小", "最終サイズ"),
            ["Кон. формат"] = ("Final format", "Endformat", "Format final", "最终格式", "最終フォーマット"),
            ["Кон. формат:"] = ("Final format:", "Endformat:", "Format final :", "最终格式：", "最終フォーマット:"),
            ["Конвертация"] = ("Convert", "Konvertieren", "Convertir", "格式转换", "フォーマット変換"),
            ["Конвертирует ticket-зависимые NSP в стандартный крипто-формат без тикетов (--C_clean_ND)"] = ("Converts ticket-dependent NSPs to standard ticketless format (--C_clean_ND)", "Konvertiert ticketabhängige NSPs in ticketloses Standardformat (--C_clean_ND)", "Convertit les NSP dépendants d'un ticket en format standard sans ticket (--C_clean_ND)", "将依赖票据的NSP转换为通用的无票据加密格式 (--C_clean_ND)", "チケット依存のNSPを標準的なチケットレス形式に変換 (--C_clean_ND)"),
            ["Конструктор мульти-картриджей XCI / XCZ..."] = ("Multi-Cartridge XCI / XCZ Builder...", "Multi-Cartridge XCI / XCZ-Ersteller...", "Créateur de multi-cartouches XCI / XCZ...", "多合一卡带 XCI / XCZ 制作器...", "マルチカートリッジ XCI / XCZ ビルダー..."),
            ["Контейнер / NCA"] = ("Container / NCA", "Container / NCA", "Conteneur / NCA", "容器 / NCA", "コンテナ / NCA"),
            ["Копировать"] = ("Copy", "Kopieren", "Copier", "复制", "コピー"),
            ["Копировать Title ID"] = ("Copy Title ID", "Title-ID kopieren", "Copier le Title ID", "复制 Title ID", "Title ID をコピー"),
            ["Копировать актуальную версию"] = ("Copy latest version", "Aktuelle Version kopieren", "Copier la dernière version", "复制最新版本", "最新バージョンをコピー"),
            ["Копировать версию"] = ("Copy version", "Version kopieren", "Copier la version", "复制版本", "バージョンをコピー"),
            ["Копировать дату выхода"] = ("Copy release date", "Veröffentlichungsdatum kopieren", "Copier la date de sortie", "复制发售日期", "発売日をコピー"),
            ["Копировать журнал (лог)"] = ("Copy log", "Protokoll kopieren", "Copier le journal", "复制日志", "ログをコピー"),
            ["Копировать изображение"] = ("Copy image", "Bild kopieren", "Copier l'image", "复制图像", "画像をコピー"),
            ["Копировать код версии"] = ("Copy version code", "Versionscode kopieren", "Copier le code de version", "复制版本代码", "バージョンコードをコピー"),
            ["Копировать размер"] = ("Copy size", "Größe kopieren", "Copier la taille", "复制大小", "サイズをコピー"),
            ["Коррекция полей RequiredSystemVersion в метаданных CNMT для предотвращения ложных ошибок устаревшей прошивки"] = ("Correct RequiredSystemVersion fields in CNMT metadata to prevent false outdated firmware errors", "RequiredSystemVersion-Felder in CNMT-Metadaten korrigieren, um falsche Firmware-Fehler zu verhindern", "Correction des champs RequiredSystemVersion dans les métadonnées CNMT pour éviter les fausses erreurs de micrologiciel obsolète", "修正 CNMT 元数据中的 RequiredSystemVersion 字段，防止提示系统固件版本过低的误报错误", "CNMT メタデータの RequiredSystemVersion フィールドを補正し、古いファームウェアによる誤エラーを防止します"),
            ["Криптографические ключи Nintendo 3DS"] = ("Nintendo 3DS Cryptographic Keys", "Nintendo 3DS Kryptoschlüssel", "Clés cryptographiques Nintendo 3DS", "Nintendo 3DS 密钥", "Nintendo 3DS 暗号化キー"),
            ["Криптографические ключи Nintendo Switch"] = ("Nintendo Switch Cryptographic Keys", "Nintendo Switch Kryptoschlüssel", "Clés cryptographiques Nintendo Switch", "Nintendo Switch 密钥", "Nintendo Switch 暗号化キー"),
            ["Криптографические ключи Nintendo Switch 2 (Ounce / NVN2)"] = ("Nintendo Switch 2 cryptographic keys (Ounce / NVN2)", "Nintendo Switch 2 Kryptografieschlüssel (Ounce / NVN2)", "Clés cryptographiques Nintendo Switch 2 (Ounce / NVN2)", "Nintendo Switch 2 加密密钥 (Ounce / NVN2)", "Nintendo Switch 2 暗号化キー (Ounce / NVN2)"),
            ["ЛОГ ВЫПОЛНЕНИЯ"] = ("EXECUTION LOG", "AUSFÜHRUNGSPROTOKOLL", "JOURNAL D'EXÉCUTION", "执行日志", "実行ログ"),
            ["Лента внизу (Ribbon)"] = ("Bottom ribbon", "Leiste unten (Ribbon)", "Bandeau inférieur (Ribbon)", "底部功能区 (Ribbon)", "下部リボン"),
            ["Логин (необязательно)"] = ("Login (optional)", "Benutzername (optional)", "Identifiant (optionnel)", "用户名 (可选)", "ログイン名 (オプション)"),
            ["Логин RuTracker"] = ("RuTracker Username", "RuTracker-Benutzername", "Identifiant RuTracker", "RuTracker 用户名", "RuTracker ユーザー名"),
            ["Максимальное количество ядер ЦП, выделяемых для ZstdSharp-сжатия"] = ("Max CPU cores allocated for Zstd compression", "Maximale CPU-Kerne für Zstd-Kompression", "Nombre maximal de cœurs CPU alloués à la compression Zstd", "分配给Zstd压缩算法的最大CPU核心数", "Zstd圧縮に割り当てる最大CPUコア数"),
            ["Максимум"] = ("Maximum", "Maximum", "Maximum", "最大", "最大"),
            ["Максимум одновременных задач"] = ("Maximum Concurrent Tasks", "Maximale gleichzeitige Aufgaben", "Tâches simultanées maximales", "最大同时任务数", "最大同時実行タスク数"),
            ["Маскирует требования к прошивке (0-30), позволяя запускать новые игры на старых FW"] = ("Masks firmware requirements (0-30) to run new games on older FW", "Maskiert Firmware-Anforderungen (0-30) für ältere FW", "Masque les exigences de firmware (0-30) pour les anciens FW", "伪装固件要求（0-30），允许在低版本系统上运行新游戏", "FW要件を偽装し(0-30)、古いFWで新しいゲームを起動可能にします"),
            ["Мастер-ключи Horizon OS и спецификация Ounce для расшифровки и сборки дампов Switch 2. Поддерживается перетаскивание (Drag и Drop)"] = ("Horizon OS master keys and Ounce specification for decrypting and building Switch 2 dumps. Supports Drag & Drop", "Horizon OS Master-Keys und Ounce-Spezifikation zum Entschlüsseln und Erstellen von Switch 2 Dumps. Unterstützt Drag & Drop", "Clés maîtresses Horizon OS et spécification Ounce pour déchiffrer et créer des dumps Switch 2. Glisser-déposer pris en charge", "用于解密和构建 Switch 2 转储镜像的 Horizon OS 主密钥与 Ounce 规范。支持拖放操作 (Drag & Drop)", "Switch 2 ダンプの復号および構築用 Horizon OS マスターキーと Ounce 仕様。ドラッグ＆ドロップ対応"),
            ["Метаданные NACP:"] = ("NACP Metadata:", "NACP-Metadaten:", "Métadonnées NACP :", "NACP 元数据：", "NACP メタデータ:"),
            ["Многопоточность (Используемые ядра)"] = ("Multithreading (CPU Cores)", "Multithreading (CPU-Kerne)", "Multithreading (cœurs CPU)", "多线程设置（CPU核心数）", "マルチスレッド（使用CPUコア数）"),
            ["Множественный выбор жанров"] = ("Multiple genre selection", "Mehrfachauswahl Genres", "Sélection multiple de genres", "多选流派/类型", "ジャンルの複数選択"),
            ["Множественный выбор издателей"] = ("Multiple publisher selection", "Mehrfachauswahl Publisher", "Sélection multiple d'éditeurs", "多选发行商", "パブリッシャーの複数選択"),
            ["Множественный выбор разработчиков"] = ("Multiple developer selection", "Mehrfachauswahl Entwickler", "Sélection multiple de développeurs", "多选开发商", "開発元の複数選択"),
            ["Множественный выбор языков"] = ("Multiple language selection", "Mehrfachauswahl Sprachen", "Sélection multiple de langues", "多选语言", "言語の複数選択"),
            ["Мульти-контент"] = ("Multi-content", "Multi-Inhalt", "Multi-contenu", "多内容合集", "マルチコンテンツ"),
            ["Мульти-поиск (напр. Mario, Zelda)..."] = ("Multi-search (e.g. Mario, Zelda)...", "Multi-Suche (z. B. Mario, Zelda)...", "Recherche multiple (ex. Mario, Zelda)...", "复合搜索 (如 Mario, Zelda)...", "マルチ検索 (例: Mario, Zelda)..."),
            ["На странице:"] = ("Per page:", "Pro Seite:", "Par page :", "每页显示：", "表示件数："),
            ["Нажмите «Найти консоль» или введите IP ниже..."] = ("Click \"Find Console\" or enter IP below...", "Klicken Sie auf „Konsole suchen“ oder IP unten eingeben...", "Cliquez sur « Trouver la console » ou entrez l'IP ci-dessous...", "点击“查找主机”或在下方输入 IP...", "「本体を検索」をクリックするか、下記にIPを入力してください..."),
            ["Нажмите или перетащите PNG/JPG сюда, чтобы заменить иконку игры (256x256)"] = ("Click or drag PNG/JPG here to replace game icon (256x256)", "Klicken oder ziehen Sie PNG/JPG hierher, um das Spielsymbol zu ersetzen (256x256)", "Cliquez ou glissez une image PNG/JPG ici pour remplacer l'icône du jeu (256x256)", "点击或拖放 PNG/JPG 图像至此处以替换游戏图标 (256x256)", "ここをクリックまたは PNG/JPG をドラッグしてゲームアイコンを変更（256x256）"),
            ["Нажмите правой кнопкой мыши для сохранения обложки"] = ("Right-click to save cover", "Rechtsklick zum Speichern des Covers", "Faites un clic droit pour enregistrer la jaquette", "右键单击以保存封面图", "右クリックでカバー画像を保存"),
            ["Название ExeFS / IPS модификации (папки)"] = ("ExeFS / IPS mod name (folder)", "Name der ExeFS / IPS-Modifikation (Ordner)", "Nom de la modification ExeFS / IPS (dossier)", "ExeFS / IPS 模组名称 (文件夹名)", "ExeFS / IPS MOD名（フォルダ名）"),
            ["Название RomFS модификации (папки)"] = ("RomFS mod name (folder)", "Name der RomFS-Modifikation (Ordner)", "Nom de la modification RomFS (dossier)", "RomFS 模组名称 (文件夹名)", "RomFS MOD名（フォルダ名）"),
            ["Название игры (English / основное)"] = ("Game title (English / Main)", "Spieltitel (Englisch / Hauptname)", "Titre du jeu (Anglais / Principal)", "游戏标题 (英文 / 主标题)", "ゲームタイトル (英語 / メイン)"),
            ["Название игры (Русское)"] = ("Game title (Russian)", "Spieltitel (Russisch)", "Titre du jeu (Russe)", "游戏标题 (俄文)", "ゲームタイトル (ロシア語)"),
            ["Найдено оптимизаций"] = ("Optimizations found", "Optimierungen gefunden", "Optimisations trouvées", "发现优化项", "最適化項目が見つかりました"),
            ["Найти информацию в сети"] = ("Search info online", "Online nach Informationen suchen", "Rechercher des informations en ligne", "在线查询游戏信息", "オンラインで情報を検索"),
            ["Напр. socks5://127.0.0.1:1080 или http://127.0.0.1:8080"] = ("e.g. socks5://127.0.0.1:1080 or http://127.0.0.1:8080", "z. B. socks5://127.0.0.1:1080 oder http://127.0.0.1:8080", "ex. socks5://127.0.0.1:1080 ou http://127.0.0.1:8080", "例如 socks5://127.0.0.1:1080 或 http://127.0.0.1:8080", "例: socks5://127.0.0.1:1080 または http://127.0.0.1:8080"),
            ["Например: Nintendo"] = ("For example: Nintendo", "Beispiel: Nintendo", "Exemple : Nintendo", "例如：Nintendo", "例: 任天堂"),
            ["Например: The Legend of Zelda"] = ("For example: The Legend of Zelda", "Beispiel: The Legend of Zelda", "Exemple : The Legend of Zelda", "例如：The Legend of Zelda", "例: ゼルダの伝説"),
            ["Например: The Legend of Zelda [RUS]"] = ("For example: The Legend of Zelda [RUS]", "Beispiel: The Legend of Zelda [RUS]", "Exemple : The Legend of Zelda [RUS]", "例如：The Legend of Zelda [RUS]", "例: ゼルダの伝説 [RUS]"),
            ["Настройки зеркала RuTracker, обхода блокировок (DoH / Прокси), авторизации и папки загрузки"] = ("RuTracker mirror, censorship bypass (DoH / Proxy), authentication, and download folder settings", "Einstellungen für RuTracker-Mirror, Zensurumgehung (DoH / Proxy), Authentifizierung und Download-Ordner", "Paramètres du miroir RuTracker, du contournement (DoH / Proxy), de l'authentification et du dossier de téléchargement", "RuTracker 镜像站点、网络绕行 (DoH / 代理)、身份验证及下载文件夹设置", "RuTracker ミラー、制限回避 (DoH / プロキシ)、認証、およびダウンロードフォルダの設定"),
            ["Нач. размер"] = ("Original size", "Ursprungsgröße", "Taille initiale", "初始大小", "初期サイズ"),
            ["Нач. формат"] = ("Original format", "Ursprungsformat", "Format initial", "初始格式", "初期フォーマット"),
            ["Не указан (автономные ZIP-архивы не создаются)"] = ("Not set (standalone ZIP archives disabled)", "Nicht festgelegt (eigenständige ZIP-Archive deaktiviert)", "Non défini (archives ZIP autonomes désactivées)", "未指定（不创建独立 ZIP 归档）", "未指定（スタンドアロン ZIP アーカイブは作成されません）"),
            ["Не указана (по умолчанию Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)"] = ("Not specified (default Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)", "Nicht angegeben (Standard: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)", "Non spécifié (par défaut Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)", "未指定 (默认为 Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)", "未指定 (デフォルト: Q:\\CONSOLES\\Nintendo Switch 2\\DOWNLOADS)"),
            ["Не указана (по умолчанию Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)"] = ("Not specified (default Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)", "Nicht angegeben (Standard: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)", "Non spécifié (par défaut Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)", "未指定 (默认为 Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)", "未指定 (デフォルト: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES)"),
            ["Не указана (сохранять в папку с исходным файлом)"] = ("Not set (save to source file folder)", "Nicht festgelegt (im Quellordner speichern)", "Non défini (enregistrer dans le dossier source)", "未指定（默认保存至源文件所在目录）", "未設定（元のファイルと同じフォルダに保存）"),
            ["Неоновый Фиолетовый"] = ("Neon Purple", "Neon-Violett", "Violet néon", "霓虹紫", "ネオンパープル"),
            ["Нет описания"] = ("No description", "Keine Beschreibung", "Aucune description", "无描述", "説明なし"),
            ["О программе"] = ("About", "Über das Programm", "À propos du programme", "关于软件", "バージョン情報"),
            ["Оба компрессора (встроенный и внешний) всегда сжимают методом Block с блоками 1 МБ для прямого запуска в эмуляторах без распаковки на диск."] = ("Both compressors (built-in and external) always compress using Block mode with 1 MB chunks for direct execution in emulators without extracting to disk.", "Beide Kompressoren (integriert und extern) komprimieren stets im Block-Modus mit 1-MB-Blöcken für den direkten Start in Emulatoren ohne Entpacken auf die Festplatte.", "Les deux compresseurs (intégré et externe) compressent toujours en mode Bloc avec des blocs de 1 Mo pour une exécution directe dans les émulateurs sans décompression sur disque.", "两种压缩器（内置与外部）均统一采用 1 MB 数据块的 Block 模式压缩，支持在模拟器中免解压直接加载运行。", "内蔵および外部の両コンプレッサは、ディスク展開不要でエミュレータから直接起動できるよう、常に 1 MB ブロック方式で圧縮します。"),
            ["Обзор приложения"] = ("App overview", "App-Übersicht", "Aperçu de l'application", "应用程序概览", "アプリの概要"),
            ["Обзор..."] = ("Browse...", "Durchsuchen...", "Parcourir...", "浏览...", "参照..."),
            ["Обновить базу"] = ("Update database", "Datenbank aktualisieren", "Mettre à jour la base", "更新数据库", "データベースを更新"),
            ["Обновить базу данных TitleDB"] = ("Update TitleDB database", "TitleDB-Datenbank aktualisieren", "Mettre à jour la base de données TitleDB", "更新 TitleDB 数据库", "TitleDB データベースを更新"),
            ["Обновить и синхронизировать базы данных игр"] = ("Update and synchronize game databases", "Spieledatenbanken aktualisieren und synchronisieren", "Mettre à jour et synchroniser les bases de données de jeux", "更新并同步游戏数据库", "ゲームデータベースを更新・同期"),
            ["Обновление"] = ("Update", "Aktualisierung", "Mise à jour", "更新", "アップデート"),
            ["Обновление данных TitleDB"] = ("Update TitleDB Database", "TitleDB-Datenbank aktualisieren", "Mettre à jour TitleDB", "更新 TitleDB 数据库", "TitleDB データベースを更新"),
            ["Обработка"] = ("Processing", "Verarbeitung", "Traitement", "正在处理", "処理中"),
            ["Обработка..."] = ("Processing...", "In Bearbeitung...", "Traitement...", "处理中...", "処理中..."),
            ["Обратимая модификация исполняемого файла эмулятора"] = ("Reversible modification of emulator executable", "Reversible Modifikation der Emulator-Programmdatei", "Modification réversible de l'exécutable de l'émulateur", "模拟器可执行程序的可逆修改", "エミュレータ実行ファイルのリバーシブルな改変"),
            ["Обратимая модификация исполняемого файла эмулятора (Eden sx2a, PokeDinner, Eglantine, Selevet) для прямого запуска упакованных .nup и Plaintext .nsp без внешнего main.npdm (с авто-резервной копией .bak)"] = ("Reversible modification of emulator executable (Eden sx2a, PokeDinner, Eglantine, Selevet) for direct boot of packed .nup and Plaintext .nsp without external main.npdm (with auto .bak backup)", "Reversible Modifikation der Emulator-Programmdatei (Eden sx2a, PokeDinner, Eglantine, Selevet) für direkten Start gepackter .nup und Plaintext .nsp ohne externes main.npdm (mit automatischer .bak-Sicherung)", "Modification réversible de l'exécutable de l'émulateur (Eden sx2a, PokeDinner, Eglantine, Selevet) pour le lancement direct des .nup et .nsp Plaintext sans main.npdm externe (avec sauvegarde auto .bak)", "可逆修改模拟器可执行文件 (Eden sx2a, PokeDinner, Eglantine, Selevet)，使其无需外部 main.npdm 即可直接启动打包好的 .nup 及明文 .nsp（附带自动 .bak 备份）", "外部 main.npdm なしでパック済み .nup および平文 .nsp を直接起動するためのエミュレータ実行ファイル（Eden sx2a, PokeDinner, Eglantine, Selevet）の可逆パッチ（自動 .bak バックアップ付き）"),
            ["Общее название мода по умолчанию (напр. Русификатор текста)"] = ("Default mod title (e.g. Text Localization)", "Standard-Mod-Titel (z. B. Textübersetzung)", "Titre par défaut du mod (ex. Traduction du texte)", "默认模组通用标题 (例如 文本汉化包)", "デフォルトの共通MOD名（例：テキスト日本語化）"),
            ["Общие"] = ("General", "Allgemein", "Général", "常规", "一般"),
            ["Общий размер коллекции"] = ("Total collection size", "Gesamtgröße der Sammlung", "Taille totale de la collection", "游戏库总容量", "コレクション合計サイズ"),
            ["Общий формат выходного файла (NSP, NSZ, XCI, XCZ) для всех игр в раздаче"] = ("Global output format (NSP, NSZ, XCI, XCZ) for all games in release", "Globales Ausgabeformat (NSP, NSZ, XCI, XCZ) für alle Spiele im Release", "Format global de sortie (NSP, NSZ, XCI, XCZ) pour tous les jeux du partage", "该发布中所有游戏的统一输出格式 (NSP, NSZ, XCI, XCZ)", "配布内の全ゲームに対する共通出力形式（NSP, NSZ, XCI, XCZ）"),
            ["Ограничивает проверку версии системного ПО консоли в метаданных CNMT"] = ("Caps required system software version checks in CNMT metadata", "Begrenzt erforderliche Systemversionsprüfungen in CNMT", "Limite la vérification de version système dans les métadonnées CNMT", "限制CNMT元数据中声明的最低系统版本要求", "CNMTメタデータ内の必須システムバージョンチェックを制限"),
            ["Ожидание"] = ("Pending", "Wartend", "En attente", "等待中", "待機中"),
            ["Описание игры..."] = ("Game description...", "Spielbeschreibung...", "Description du jeu...", "游戏描述...", "ゲーム概要..."),
            ["Описание:"] = ("Description:", "Beschreibung:", "Description :", "描述：", "説明:"),
            ["Оптимизация ассетов модов (Oxipng и очистка мусора)"] = ("Mod Asset Optimization (Oxipng and Junk Cleaning)", "Mod-Asset-Optimierung (Oxipng und Bereinigung)", "Optimisation des ressources de mods (Oxipng et nettoyage)", "MOD 资源优化（Oxipng 与垃圾清理）", "MODアセット最適化 (Oxipngと不要ファイル削除)"),
            ["Опционально: загружается автоматически, если файл лежит рядом с prod.keys"] = ("Optional: loaded automatically if placed next to prod.keys", "Optional: Wird automatisch geladen, wenn sich die Datei neben prod.keys befindet", "Optionnel : chargé automatiquement si le fichier est placé à côté de prod.keys", "可选：若与 prod.keys 置于同一目录下将自动加载", "オプション: prod.keys と同じフォルダにある場合は自動的に読み込まれます"),
            ["Остановить все"] = ("Stop All", "Alle stoppen", "Tout arrêter", "全部停止", "すべて停止"),
            ["Открыть выходную папку"] = ("Open output folder", "Ausgabeordner öffnen", "Ouvrir le dossier de sortie", "打开输出文件夹", "出力フォルダを開く"),
            ["Открыть локальный .torrent файл с диска"] = ("Open local .torrent file from disk", "Lokale .torrent-Datei von Festplatte öffnen", "Ouvrir le fichier .torrent local depuis le disque", "从本地磁盘打开 .torrent 种子文件", "ローカルの .torrent ファイルをディスクから開く"),
            ["Открыть полную Студию метаданных, иконок и бейджей (NACP / SMDH) для этой игры — как в списке задач"] = ("Open full Metadata, Icon & Badge Studio (NACP / SMDH) for this game — as in task queue", "Vollständiges Metadaten-, Icon- & Badge-Studio (NACP / SMDH) für dieses Spiel öffnen", "Ouvrir le studio complet de métadonnées, icônes et badges (NACP / SMDH) pour ce jeu", "为此游戏打开完整的元数据、图标与徽章工作室 (NACP / SMDH)", "このゲームのメタデータ・アイコン・バッジスタジオ（NACP / SMDH）を開く"),
            ["Отмена"] = ("Cancel", "Abbrechen", "Annuler", "取消", "キャンセル"),
            ["Отменен"] = ("Cancelled", "Abgebrochen", "Annulé", "已取消", "キャンセル済み"),
            ["Отменено"] = ("Cancelled", "Abgebrochen", "Annulé", "已取消", "キャンセル済み"),
            ["Отменить выполнение задачи"] = ("Cancel task execution", "Aufgabenausführung abbrechen", "Annuler l'exécution de la tâche", "取消执行任务", "タスクの実行を中止"),
            ["Отменить задачу"] = ("Cancel task", "Aufgabe abbrechen", "Annuler la tâche", "取消任务", "タスクをキャンセル"),
            ["Отметьте нужные ссылки"] = ("Check required links", "Erforderliche Links markieren", "Cochez les liens requis", "勾选所需链接", "必要なリンクを選択"),
            ["Отображаемая версия:"] = ("Display Version:", "Angezeigte Version:", "Version affichée :", "显示版本：", "表示バージョン:"),
            ["Отображение столбцов"] = ("Column Display", "Spaltenanzeige", "Affichage des colonnes", "列显示", "カラム表示"),
            ["Отправить на консоль (Wi-Fi / FTP / FBI QR)..."] = ("Send to Console (Wi-Fi / FTP / FBI QR)...", "An Konsole senden (WLAN / FTP / FBI-QR)...", "Envoyer à la console (Wi-Fi / FTP / QR FBI)...", "发送到主机 (Wi-Fi / FTP / FBI 二维码)...", "本体へ送信（Wi-Fi / FTP / FBI QRコード）..."),
            ["Отпустите файлы для добавления"] = ("Drop files to add", "Dateien zum Hinzufügen ablegen", "Déposez les fichiers à ajouter", "释放文件以添加", "ファイルをドロップして追加"),
            ["Оформление и звуковые уведомления"] = ("Appearance & Sound Notifications", "Erscheinungsbild & Tonbenachrichtigungen", "Apparence et notifications sonores", "外观与声音通知", "外観とサウンド通知"),
            ["Очистить все"] = ("Clear All", "Alles leeren", "Tout effacer", "全部清除", "すべてクリア"),
            ["Очистить выбранный звук"] = ("Clear selected sound", "Ausgewählten Ton löschen", "Effacer le son sélectionné", "清除所选声音", "選択したサウンドをクリア"),
            ["Очистить выходную папку 3DS"] = ("Clear 3DS output folder", "3DS-Ausgabeordner leeren", "Vider le dossier de sortie 3DS", "清空 3DS 输出文件夹", "3DS出力フォルダをクリア"),
            ["Очистить выходную папку Switch"] = ("Clear Switch output folder", "Switch-Ausgabeordner leeren", "Vider le dossier de sortie Switch", "清空 Switch 输出文件夹", "Switch出力フォルダをクリア"),
            ["Очистить выходную папку Switch 2"] = ("Clear Switch 2 output folder", "Switch 2-Ausgabeordner leeren", "Vider le dossier de sortie Switch 2", "清空 Switch 2 输出文件夹", "Switch 2出力フォルダをクリア"),
            ["Очистить журнал"] = ("Clear Log", "Protokoll leeren", "Effacer le journal", "清空日志", "ログをクリア"),
            ["Очистить историю"] = ("Clear History", "Verlauf leeren", "Effacer l'historique", "清除历史记录", "履歴をクリア"),
            ["Очистить каталог для архивов SDMC"] = ("Clear SDMC archive directory", "SDMC-Archivverzeichnis leeren", "Vider le répertoire d'archives SDMC", "清空 SDMC 归档目录", "SDMCアーカイブディレクトリをクリア"),
            ["Очистить папку загрузок"] = ("Clear downloads folder", "Download-Ordner leeren", "Vider le dossier des téléchargements", "清空下载文件夹", "ダウンロードフォルダをクリア"),
            ["Очистить список"] = ("Clear List", "Liste leeren", "Vider la liste", "清空列表", "リストをクリア"),
            ["Очистка персональных билетов и подписей аккаунта (Strip Personalized Tickets)"] = ("Strip Personalized Tickets and Account Signatures", "Personalisierte Tickets und Kontosignaturen entfernen", "Suppression des tickets personnalisés et des signatures de compte", "清除个人票据与账号签名 (Strip Personalized Tickets)", "個人チケットおよびアカウント署名の削除（Strip Personalized Tickets）"),
            ["Ошибка"] = ("Error", "Fehler", "Erreur", "错误", "エラー"),
            ["ПАРАМЕТРЫ ЗАДАЧИ"] = ("TASK PARAMETERS", "AUFGABENPARAMETER", "PARAMÈTRES DE LA TÂCHE", "任务参数", "タスク設定"),
            ["ПОЛНЫЙ ЖУРНАЛ СОБЫТИЙ"] = ("FULL EVENT LOG", "VOLLSTÄNDIGES EREIGNISPROTOKOLL", "JOURNAL COMPLET DES ÉVÉNEMENTS", "完整事件日志", "完全なイベントログ"),
            ["ПРИМЕНИТЬ ВЕРСИЮ"] = ("APPLY VERSION", "VERSION ANWENDEN", "APPLIQUER LA VERSION", "应用版本", "バージョンを適用"),
            ["Папка для сохранения обработанных файлов 3DS (.3ds / .cia / .cxi). Можно перетащить папку сюда"] = ("Folder for processed 3DS files (.3ds / .cia / .cxi). Drag folder here", "Ordner für verarbeitete 3DS-Dateien (.3ds / .cia / .cxi). Ordner hierher ziehen", "Dossier pour les fichiers 3DS traités (.3ds / .cia / .cxi)", "保存处理后的3DS文件（.3ds / .cia / .cxi）。支持拖放文件夹", "処理済み3DSファイル（.3ds / .cia / .cxi）の保存先。フォルダをドラッグ可能"),
            ["Папка для сохранения обработанных файлов Nintendo Switch 2 (.nsp / .nsz / .xci / .xcz). По умолчанию: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES"] = ("Folder for saving processed Nintendo Switch 2 files (.nsp / .nsz / .xci / .xcz). Default: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES", "Ordner zum Speichern verarbeiteter Nintendo Switch 2-Dateien (.nsp / .nsz / .xci / .xcz). Standard: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES", "Dossier d'enregistrement des fichiers Nintendo Switch 2 traités (.nsp / .nsz / .xci / .xcz). Par défaut : Q:\\CONSOLES\\Nintendo Switch 2\\GAMES", "保存已处理 Nintendo Switch 2 文件的文件夹 (.nsp / .nsz / .xci / .xcz)。默认路径: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES", "処理済みNintendo Switch 2ファイル（.nsp / .nsz / .xci / .xcz）の保存フォルダ。デフォルト: Q:\\CONSOLES\\Nintendo Switch 2\\GAMES"),
            ["Папка для сохранения обработанных файлов Switch (.nsp / .nsz / .xci / .xcz). Можно перетащить папку сюда"] = ("Folder for processed Switch files (.nsp / .nsz / .xci / .xcz). Drag folder here", "Ordner für verarbeitete Switch-Dateien (.nsp / .nsz / .xci / .xcz). Ordner hierher ziehen", "Dossier pour les fichiers Switch traités (.nsp / .nsz / .xci / .xcz)", "保存处理后的Switch文件（.nsp / .nsz / .xci / .xcz）。支持拖放文件夹", "処理済みSwitchファイル（.nsp / .nsz / .xci / .xcz）の保存先。フォルダをドラッグ可能"),
            ["Папка для упаковки готовых SDMC-пакетов в автономные ZIP-архивы (.zip) для реальной консоли Switch. Если путь не указан — архивы не создаются, а выходная папка с играми остается идеально чистой"] = ("Directory for packaging compiled SDMC data into standalone ZIP archives (.zip) for physical Switch console. If empty — archives are not created, keeping the main game folder completely clean", "Verzeichnis zum Packen von SDMC-Daten in eigenständige ZIP-Archive (.zip) für physische Switch-Konsolen. Wenn leer, werden keine Archive erstellt und der Spielordner bleibt sauber", "Répertoire pour empaqueter les données SDMC en archives ZIP autonomes (.zip) pour la console Switch", "用于将 SDMC 数据打包为独立 ZIP 压缩包 (.zip) 的目录，方便物理 Switch 主机使用。若未指定，则不生成压缩包，游戏输出目录保持完全纯净", "実機 Switch 用に SDMC データをスタンドアロン ZIP アーカイブ (.zip) に圧縮保存するフォルダ。未設定の場合、アーカイブは作成されず、ゲーム出力先は常にクリーンな状態に保たれます"),
            ["Папка загрузок и распаковки дампов Switch 2"] = ("Switch 2 dump download and unpack folder", "Download- und Entpackordner für Switch 2-Dumps", "Dossier de téléchargement et d'extraction des dumps Switch 2", "Switch 2 Dump 下载与解包文件夹", "Switch 2 ダンプのダウンロードおよび展開フォルダ"),
            ["Папка модов и русификаторов Switch 2 (не указана)"] = ("Switch 2 mods and localizations folder (not specified)", "Ordner für Switch 2-Mods und Übersetzungen (nicht festgelegt)", "Dossier des mods et traductions Switch 2 (non spécifié)", "Switch 2 模组与汉化补丁文件夹（未指定）", "Switch 2 MOD・翻訳パッチフォルダ（未指定）"),
            ["Папка на SD"] = ("SD Folder", "Ordner auf SD", "Dossier sur la SD", "SD 卡文件夹", "SDカードフォルダ"),
            ["Папка не выбрана (можно перетащить)"] = ("No folder selected (drag & drop supported)", "Kein Ordner ausgewählt (kann hierher gezogen werden)", "Aucun dossier sélectionné (glisser-déposer supporté)", "未选择文件夹（支持拖拽）", "フォルダが選択されていません（ドラッグ＆ドロップ可能）"),
            ["Папка сохранения и библиотека Nintendo Switch 2"] = ("Nintendo Switch 2 save folder and library", "Nintendo Switch 2-Speicherordner und Bibliothek", "Dossier de sauvegarde et bibliothèque Nintendo Switch 2", "Nintendo Switch 2 保存文件夹与游戏库", "Nintendo Switch 2 保存フォルダおよびライブラリ"),
            ["Папка сохранения игр и модов..."] = ("Games & mods output folder...", "Ordner für Spiele & Mods...", "Dossier d'enregistrement des jeux et mods...", "游戏与模组保存文件夹...", "ゲームとMODの保存フォルダ..."),
            ["Папки для сканирования"] = ("Folders for Scanning", "Ordner zum Scannen", "Dossiers à analyser", "扫描目录列表", "スキャン対象フォルダ"),
            ["Папки сканирования"] = ("Scan Folders", "Suchordner", "Dossiers d'analyse", "扫描文件夹", "スキャンフォルダ"),
            ["Параметры"] = ("Settings", "Einstellungen", "Paramètres", "设置", "設定"),
            ["Пароль"] = ("Password", "Passwort", "Mot de passe", "密码", "パスワード"),
            ["Патч минимальной требуемой версии прошивки (MasterKey / Min OS Version)"] = ("Patch minimum required firmware version (MasterKey / Min OS Version)", "Mindest-Firmwareversion patchen (MasterKey / Min OS Version)", "Patcher la version minimale de firmware requise (MasterKey / Min OS Version)", "修补最低系统固件版本要求 (MasterKey / Min OS Version)", "最低要求ファームウェアバージョンのパッチ（MasterKey / Min OS Version）"),
            ["Патчер эмуляторов Switch 2 и поддержка NUP / Plaintext NSP (NSP-Box Direct Boot)"] = ("Switch 2 Emulator Patcher & NUP / Plaintext NSP Support (NSP-Box Direct Boot)", "Switch 2-Emulator-Patcher & NUP / Plaintext-NSP-Unterstützung (NSP-Box Direct Boot)", "Patcheur d'émulateurs Switch 2 et support NUP / NSP texte brut (NSP-Box Direct Boot)", "Switch 2 模拟器补丁工具与 NUP / 明文 NSP 支持 (NSP-Box Direct Boot)", "Switch 2エミュレータパッチャーおよびNUP / Plaintext NSP対応（NSP-Box Direct Boot）"),
            ["Перейти на страницу"] = ("Go to page", "Zur Seite gehen", "Aller à la page", "跳转到页面", "ページへ移動"),
            ["Переключить платформу (Switch / Switch 2 / 3DS)"] = ("Switch platform (Switch / Switch 2 / 3DS)", "Plattform wechseln (Switch / Switch 2 / 3DS)", "Changer de plateforme (Switch / Switch 2 / 3DS)", "切换平台 (Switch / Switch 2 / 3DS)", "プラットフォーム切り替え（Switch / Switch 2 / 3DS）"),
            ["Перетащите любое изображение (PNG/JPEG) для установки иконки игры"] = ("Drag and drop any image (PNG/JPEG) to set the game icon", "Beliebiges Bild (PNG/JPEG) hierher ziehen, um das Spielsymbol festzulegen", "Glissez n'importe quelle image (PNG/JPEG) pour définir l'icône du jeu", "拖放任意图片 (PNG/JPEG) 以设置游戏图标", "ゲームアイコンを設定するには画像（PNG/JPEG）をドラッグ＆ドロップしてください"),
            ["По дате выхода"] = ("By Release Date", "Nach Datum", "Par date de sortie", "按发售日", "発売日順"),
            ["По названию"] = ("By Title", "Nach Titel", "Par titre", "按名称", "タイトル順"),
            ["По системе"] = ("By System", "Nach System", "Par système", "按系统", "システム順"),
            ["По умолчанию отключено для экономии места на диске (полный кэш обложек 19 консолей может занимать до 12–14 ГБ в AppData\\Local\\StormSwitchBox\\covers)."] = ("Disabled by default to save disk space (full 19-console cover cache can take up to 12–14 GB in AppData\\Local\\StormSwitchBox\\covers).", "Standardmäßig deaktiviert, um Speicherplatz zu sparen (voller Cover-Cache für 19 Konsolen kann 12–14 GB in AppData\\Local\\StormSwitchBox\\covers belegen).", "Désactivé par défaut pour économiser l'espace disque (le cache complet des 19 consoles peut occuper 12 à 14 Go dans AppData\\Local\\StormSwitchBox\\covers).", "默认禁用以节省磁盘空间（19个主机的完整封面缓存可能在 AppData\\Local\\StormSwitchBox\\covers 占用多达 12–14 GB）。", "ディスク容量節約のためデフォルトで無効です（全19機種のカバーキャッシュ全体で AppData\\Local\\StormSwitchBox\\covers 内に最大12〜14GB消費する可能性があります）。"),
            ["Поддерживаемые языки:"] = ("Supported Languages:", "Unterstützte Sprachen:", "Langues supportées :", "支持语言：", "対応言語:"),
            ["Поиск жанра..."] = ("Search genre...", "Genre suchen...", "Rechercher un genre...", "搜索类型...", "ジャンルを検索..."),
            ["Поиск издателя..."] = ("Search publisher...", "Herausgeber suchen...", "Rechercher un éditeur...", "搜索发行商...", "パブリッシャーを検索..."),
            ["Поиск по названию или Title ID..."] = ("Search by title or Title ID...", "Nach Titel oder Title-ID suchen...", "Rechercher par titre ou Title ID...", "按游戏名称或 Title ID 搜索...", "タイトルまたはTitle IDで検索..."),
            ["Поиск студии..."] = ("Search studio...", "Studio suchen...", "Rechercher un studio...", "搜索工作室...", "開発スタジオを検索..."),
            ["Поиск тем..."] = ("Search topics...", "Themen suchen...", "Rechercher des sujets...", "搜索主题...", "トピックを検索..."),
            ["Поиск языка..."] = ("Search language...", "Sprache suchen...", "Rechercher une langue...", "搜索语言...", "言語を検索..."),
            ["Показать состав пакета Homebrew"] = ("Show Homebrew package contents", "Homebrew-Paketinhalte anzeigen", "Afficher le contenu du package Homebrew", "显示自制软件 (Homebrew) 软件包内容", "Homebrewパッケージの構成を表示"),
            ["Показать список файлов"] = ("Show file list", "Dateiliste anzeigen", "Afficher la liste des fichiers", "显示文件列表", "ファイル一覧を表示"),
            ["Понижение версии ключей (KeyGen)"] = ("Key Generation Downgrade (KeyGen)", "Schlüsselversions-Downgrade (KeyGen)", "Rétrogradation de version des clés (KeyGen)", "降低密钥生成版本 (KeyGen)", "キー世代ダウングレード (KeyGen)"),
            ["Понижение системных требований (RSV Cap Downgrade)"] = ("RSV Cap Downgrade", "Systemanforderungs-Downgrade (RSV Cap)", "Rétrogradation RSV Cap", "降低系统版本要求 (RSV Cap Downgrade)", "システム要件ダウングレード (RSV Cap)"),
            ["Порт FTP"] = ("FTP Port", "FTP-Port", "Port FTP", "FTP 端口", "FTPポート"),
            ["Потенциальная экономия"] = ("Potential Savings", "Mögliche Ersparnis", "Économie potentielle", "潜在节省空间", "削減可能な容量"),
            ["Потоки процессора при сборке и сжатии"] = ("CPU threads for building and compression", "CPU-Threads für Erstellung und Komprimierung", "Threads processeur pour la création et compression", "构建与压缩时使用的 CPU 线程数", "ビルド・圧縮時のCPUスレッド数"),
            ["Потяните вверх или вниз, чтобы изменить размер журнала"] = ("Drag up or down to resize log", "Nach oben oder unten ziehen, um Protokollgröße anzupassen", "Glissez vers le haut ou le bas pour redimensionner le journal", "上下拖动以调整日志面板大小", "上下にドラッグしてログサイズを変更"),
            ["Предпросмотр файла (HEX / UTF-8 / Изображение)"] = ("File Preview (HEX / UTF-8 / Image)", "Dateivorschau (HEX / UTF-8 / Bild)", "Aperçu du fichier (HEX / UTF-8 / Image)", "文件预览 (HEX / UTF-8 / 图像)", "ファイルプレビュー（HEX / UTF-8 / 画像）"),
            ["Предыдущая страница"] = ("Previous page", "Vorherige Seite", "Page précédente", "上一页", "前のページ"),
            ["Применить сеть"] = ("Apply network", "Netzwerk anwenden", "Appliquer le réseau", "应用网络设置", "ネットワーク設定を適用"),
            ["Принудительный HardPatch (Монолитный RomFS)"] = ("Force HardPatch (Monolithic RomFS)", "HardPatch erzwingen (Monolithisches RomFS)", "Forcer le HardPatch (RomFS monolithique)", "强制 HardPatch（单体 RomFS）", "HardPatchを強制（モノリシックRomFS）"),
            ["Принять обновление (убирает ложную плашку)"] = ("Accept update (dismisses false notification)", "Update akzeptieren (entfernt falschen Hinweis)", "Accepter la mise à jour (masque la fausse alerte)", "接受更新（消除误报提示）", "アップデートを承認（誤検出バナーを非表示）"),
            ["Приостановить выполнение задачи"] = ("Pause task execution", "Aufgabenausführung anhalten", "Suspendre l'exécution de la tâche", "暂停执行任务", "タスクの実行を一時停止"),
            ["Приостановить задачу"] = ("Pause task", "Aufgabe anhalten", "Suspendre la tâche", "暂停任务", "タスクを一時停止"),
            ["Проверка"] = ("Verify", "Überprüfen", "Vérifier", "校验检查", "整合性チェック"),
            ["Проект / Порт"] = ("Project / Port", "Projekt / Portierung", "Projet / Portage", "项目 / 移植", "プロジェクト / 移植"),
            ["Производительность и сжатие"] = ("Performance & Compression", "Leistung & Komprimierung", "Performances et compression", "性能与压缩", "パフォーマンスと圧縮"),
            ["Просканировано файлов"] = ("Files scanned", "Gescannte Dateien", "Fichiers analysés", "已扫描文件数", "スキャン済みファイル数"),
            ["Прямая отправка на консоль, Инспектор RomFS, Мульти-картридж и Аудитор библиотеки"] = ("Direct Console Transfer, RomFS Inspector, Multi-Cartridge & Library Auditor", "Direktübertragung zur Konsole, RomFS-Inspektor, Multi-Cartridge & Bibliotheks-Auditor", "Envoi direct sur console, Inspecteur RomFS, Multi-Cartouche et Auditeur de bibliothèque", "直接发送至主机、RomFS 检查器、多合一卡带与游戏库审计工具", "本体直接送信、RomFSインスペクター、マルチカートリッジ＆ライブラリ監査ツール"),
            ["Прямой Multi-Content (Сшивание 1G+1U)"] = ("Direct Multi-Content (1G+1U Merging)", "Direkter Multi-Content (1G+1U Zusammenführung)", "Multi-Contenu direct (Fusion 1G+1U)", "直接多内容整合 (1G+1U 缝合)", "ダイレクトマルチコンテンツ（1G+1U 統合）"),
            ["Пути и организация файлов"] = ("Paths & File Organization", "Pfade & Dateiorganisation", "Chemins et organisation des fichiers", "路径与文件整理", "パスとファイル整理"),
            ["Путь или перетащите папку"] = ("Path or drag folder here", "Pfad oder Ordner hierher ziehen", "Chemin ou glissez le dossier ici", "路径或拖入文件夹", "パスを入力またはフォルダをドラッグ"),
            ["Путь к образу (.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi)"] = ("Path to image (.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi)", "Pfad zum Image (.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi)", "Chemin de l'image (.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi)", "镜像文件路径 (.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi)", "イメージファイルのパス（.nsp, .nsz, .xci, .xcz, .nca, .3ds, .cci, .cia, .cxi）"),
            ["Путь к папке коллекции игр Switch и 3DS (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia)"] = ("Path to Switch & 3DS game collection folder (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia)", "Pfad zum Switch- & 3DS-Spieleordner (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia)", "Chemin du dossier de collection Switch et 3DS (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia)", "Switch 和 3DS 游戏库文件夹路径 (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia)", "Switch・3DSゲームコレクションフォルダのパス（.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia）"),
            ["Путь к папке эмулятора"] = ("Emulator folder path", "Pfad zum Emulator-Ordner", "Chemin du dossier de l'émulateur", "模拟器文件夹路径", "エミュレータフォルダのパス"),
            ["Работает при конвертации NSP/NSZ → XCI/XCZ и при HardPatch (пересборка Base+Update)"] = ("Applies during NSP/NSZ → XCI/XCZ conversion and HardPatch (Base+Update)", "Funktioniert bei NSP/NSZ → XCI/XCZ Konvertierung und HardPatch", "S'applique lors de la conversion NSP/NSZ → XCI/XCZ et du HardPatch", "在 NSP/NSZ → XCI/XCZ 转换或 HardPatch 重建时生效", "NSP/NSZ → XCI/XCZ 変換時およびHardPatch時に適用"),
            ["Разбивает выходные файлы >4GB на части для совместимости со старыми FAT32 флешками."] = ("Splits output files >4GB into parts for compatibility with legacy FAT32 drives.", "Teilt Ausgabedateien >4GB in Teile auf für Kompatibilität mit FAT32-Laufwerken.", "Divise les fichiers >4 Go en plusieurs parties pour la compatibilité avec les clés FAT32.", "将大于 4GB 的输出文件分卷切片，以兼容旧版 FAT32 闪存盘。", "FAT32ドライブとの互換性のため、4GBを超える出力ファイルを分割します。"),
            ["Раздел поиска Nintendo Switch на RuTracker"] = ("RuTracker Nintendo Switch Search Section", "RuTracker Nintendo Switch-Suchbereich", "Section de recherche Nintendo Switch sur RuTracker", "RuTracker 上的 Nintendo Switch 搜索板块", "RuTrackerのNintendo Switch検索セクション"),
            ["Разделять файлы для FAT32 (> 4 GB)"] = ("Split Files for FAT32 (> 4 GB)", "Dateien für FAT32 aufteilen (> 4 GB)", "Diviser les fichiers pour FAT32 (> 4 Go)", "为 FAT32 分割文件 (> 4 GB)", "FAT32用にファイルを分割 (> 4 GB)"),
            ["Размер"] = ("Size", "Größe", "Taille", "大小", "サイズ"),
            ["Размер блока NCZBLOCK"] = ("NCZBLOCK Block Size", "NCZBLOCK-Blockgröße", "Taille de bloc NCZBLOCK", "NCZBLOCK 块大小", "NCZBLOCKブロックサイズ"),
            ["Размер блока сжатия при упаковке NCZ. 1 МБ обеспечивает идеальный баланс скорости распаковки и сжатия"] = ("Compression block size for NCZ packing. 1 MB offers the optimal balance of decompression speed and compression ratio", "Komprimierungsblockgröße beim NCZ-Packen. 1 MB bietet die ideale Balance aus Dekomprimierungsgeschwindigkeit und Kompression", "Taille du bloc de compression pour NCZ. 1 Mo offre un équilibre optimal entre vitesse de décompression et compression", "打包 NCZ 时的压缩块大小。1 MB 可在解压速度与压缩率之间取得最佳平衡", "NCZ圧縮時のブロックサイズ。1MBは展開速度と圧縮率の最適なバランスを提供します"),
            ["Размер локального кэша обложек: проверка..."] = ("Local cover cache size: checking...", "Größe des lokalen Cover-Caches: Überprüfung...", "Taille du cache local des jaquettes : vérification...", "本地封面缓存大小：正在检查...", "ローカルカバーキャッシュサイズ: 確認中..."),
            ["Размер сохранений:"] = ("Save Size:", "Speicherdaten-Größe:", "Taille des sauvegardes :", "存档大小：", "セーブデータ容量:"),
            ["Размер файла:"] = ("File Size:", "Dateigröße:", "Taille du fichier :", "文件大小：", "ファイルサイズ:"),
            ["Разница"] = ("Difference", "Differenz", "Différence", "差异", "差分"),
            ["Разработчик:"] = ("Developer:", "Entwickler:", "Développeur :", "开发商：", "開発元:"),
            ["Разрешать домен RuTracker через защищённый DoH (1.1.1.1 / 8.8.8.8) в обход DNS-блокировок провайдера"] = ("Resolve RuTracker domain via secure DoH (1.1.1.1 / 8.8.8.8) bypassing ISP DNS blocking", "RuTracker-Domain über sicheres DoH (1.1.1.1 / 8.8.8.8) auflösen, um ISP-DNS-Sperren zu umgehen", "Résoudre le domaine RuTracker via DoH sécurisé (1.1.1.1 / 8.8.8.8) pour contourner le blocage DNS", "通过安全 DoH (1.1.1.1 / 8.8.8.8) 解析 RuTracker 域名以绕过运营商 DNS 封锁", "プロバイダのDNSブロックを回避するため、安全なDoH（1.1.1.1 / 8.8.8.8）でRuTrackerドメインを解決する"),
            ["Распаковка"] = ("Unpack", "Entpacken", "Extraire", "解包", "展開"),
            ["Распаковка и перепаковка потоковых архивов (.zs / Zstandard RomFS)"] = ("Unpack & repack stream archives (.zs / Zstandard RomFS)", "Entpacken und Neupacken von Stream-Archiven (.zs / Zstandard RomFS)", "Extraction et réempaquetage d'archives de flux (.zs / Zstandard RomFS)", "解包与重构流式归档文件 (.zs / Zstandard RomFS)", "ストリームアーカイブ（.zs / Zstandard RomFS）の展開および再パック"),
            ["Распаковывает дельту обновления и запекает чистый RomFS в единый монолитный контейнер для гарантированной совместимости"] = ("Unpacks update delta and bakes clean RomFS into a single monolithic container for guaranteed compatibility", "Entpackt das Update-Delta und backt ein sauberes RomFS in einen monolithischen Container für garantierte Kompatibilität", "Extrait le delta de mise à jour et fusionne le RomFS propre dans un conteneur monolithique pour une compatibilité garantie", "解包更新增量补丁，并将干净的 RomFS 烘焙整合为单个单体容器，确保绝对兼容性", "互換性を保証するため、アップデートの差分を展開してクリーンなRomFSを単一のモノリシックコンテナに結合します"),
            ["Регион / Игроки:"] = ("Region / Players:", "Region / Spieler:", "Région / Joueurs :", "区域 / 玩家人数：", "地域 / プレイ人数:"),
            ["Регион:"] = ("Region:", "Region:", "Région :", "区域：", "地域:"),
            ["Редактировать иконку и название игры"] = ("Edit game icon and title", "Spielsymbol und Titel bearbeiten", "Modifier l'icône et le titre du jeu", "编辑游戏图标与标题", "ゲームアイコンとタイトルを編集"),
            ["Редактировать иконку и название..."] = ("Edit icon and title...", "Symbol und Titel bearbeiten...", "Modifier l'icône et le titre...", "编辑图标与标题...", "アイコンとタイトルを編集..."),
            ["Режим автопривязки модов к задаче"] = ("Mod auto-attachment mode for tasks", "Mod-Autoverknüpfungsmodus für Aufgaben", "Mode de liaison automatique des mods à la tâche", "模组自动附加到任务模式", "MOD自動関連付けモード"),
            ["Режим прямого вшивания обновлений в RomFS (Hard-Patch + 4 КБ Patch CNMT)"] = ("Direct RomFS update injection mode (Hard-Patch + 4 KB Patch CNMT)", "Direktes RomFS-Update-Integrationsmodus (Hard-Patch + 4 KB Patch-CNMT)", "Mode d'intégration directe des mises à jour dans RomFS (Hard-Patch + 4 Ko Patch CNMT)", "直接将更新硬合入 RomFS 模式 (Hard-Patch + 4 KB Patch CNMT)", "RomFS直接更新組み込みモード（Hard-Patch + 4KB Patch CNMT）"),
            ["Режим сборки (HardPatch / Multi-Content)..."] = ("Build mode (HardPatch / Multi-Content)...", "Build-Modus (HardPatch / Multi-Content)...", "Mode de construction (HardPatch / Multi-Contenu)...", "构建模式 (HardPatch / 多内容)...", "ビルドモード（HardPatch / マルチコンテンツ）..."),
            ["Рейтинг / ID:"] = ("Rating / ID:", "Bewertung / ID:", "Évaluation / ID :", "分级 / ID：", "レーティング / ID:"),
            ["Русский"] = ("Russian", "Russisch", "Russe", "俄语", "ロシア語"),
            ["Сброс"] = ("Reset", "Zurücksetzen", "Réinitialiser", "重置", "リセット"),
            ["Сбросить"] = ("Reset", "Zurücksetzen", "Réinitialiser", "重置", "リセット"),
            ["Сбросить все фильтры"] = ("Reset all filters", "Alle Filter zurücksetzen", "Réinitialiser tous les filtres", "重置所有筛选", "すべてのフィルターをリセット"),
            ["Сбросить выбранный путь"] = ("Reset selected path", "Ausgewählten Pfad zurücksetzen", "Réinitialiser le chemin sélectionné", "重置所选路径", "選択したパスをリセット"),
            ["Сведения об игре"] = ("Game Information", "Spielinformationen", "Informations sur le jeu", "游戏详情", "ゲーム情報"),
            ["Свернуть в системный трей (Zero-UI Background Worker)"] = ("Minimize to system tray (Zero-UI Background Worker)", "In den Infobereich minimieren (Zero-UI Background Worker)", "Réduire dans la zone de notification (Zero-UI Background Worker)", "最小化到系统托盘 (Zero-UI Background Worker)", "システムトレイに最小化（Zero-UI バックグラウンドワーカー）"),
            ["Свернуть все"] = ("Collapse All", "Alle einklappen", "Tout réduire", "全部折叠", "すべて折りたたむ"),
            ["Сверху слева"] = ("Top Left", "Oben links", "En haut à gauche", "左上角", "左上"),
            ["Сверху справа"] = ("Top Right", "Oben rechts", "En haut à droite", "右上角", "右上"),
            ["Сверху — итоговое имя выходного файла (.nsp/.nsz); во второй строке — внутреннее английское имя, внутреннее русское имя, издатель/разработчик и иконка NACP"] = ("Top: final output filename (.nsp/.nsz); second line: internal English name, internal Russian name, publisher/developer and NACP icon", "Oben: endgültiger Ausgabename (.nsp/.nsz); zweite Zeile: interner englischer Name, interner russischer Name, Publisher/Entwickler und NACP-Icon", "En haut : nom final du fichier (.nsp/.nsz) ; deuxième ligne : nom anglais interne, nom russe interne, éditeur/développeur et icône NACP", "上方为最终输出文件名 (.nsp/.nsz)；第二行为内置英文名、内置俄文名、发行商/开发商以及 NACP 图标", "上段: 最終出力ファイル名（.nsp/.nsz）、下段: 内部英語名、内部ロシア語名、発売元/開発元およびNACPアイコン"),
            ["Сверяет состав дополнений с глобальной базой TitleDB и сообщает о недостающих DLC в логе задачи"] = ("Checks DLC list against global TitleDB and reports missing DLC in the task log", "Gleicht DLCs mit der globalen TitleDB ab und meldet fehlende DLCs im Aufgabenprotokoll", "Vérifie la liste des DLC avec la TitleDB globale et signale les DLC manquants dans le journal", "对照全球 TitleDB 数据库核对 DLC 列表，并在任务日志中报告缺失的 DLC", "グローバルなTitleDBデータベースとDLC構成を照合し、不足しているDLCをタスクログに記録します"),
            ["Свой текст бейджа (напр. HD, 2.0, CHEAT)"] = ("Custom badge text (e.g. HD, 2.0, CHEAT)", "Benutzerdefinierter Badge-Text (z. B. HD, 2.0, CHEAT)", "Texte de badge personnalisé (ex. HD, 2.0, CHEAT)", "自定义徽章文本（例如 HD、2.0、CHEAT）", "カスタムバッジテキスト（例: HD, 2.0, CHEAT）"),
            ["Свой текст бейджа (напр. HD, 2.0, СБОРКА)"] = ("Custom badge text (e.g. HD, 2.0, MOD)", "Benutzerdefinierter Badge-Text (z. B. HD, 2.0, BUILD)", "Texte de badge personnalisé (ex. HD, 2.0, BUILD)", "自定义徽章文本（例如 HD、2.0、BUILD）", "カスタムバッジテキスト（例: HD, 2.0, BUILD）"),
            ["Сейвы"] = ("Saves", "Speicherstände", "Sauvegardes", "存档", "セーブデータ"),
            ["Сжатие"] = ("Compression", "Komprimierung", "Compression", "压缩", "圧縮"),
            ["Сжатие через внешний движок nsz.exe (--block, 1 МБ)"] = ("Compression via external nsz.exe engine (--block, 1 MB)", "Komprimierung über externe nsz.exe-Engine (--block, 1 MB)", "Compression via le moteur externe nsz.exe (--block, 1 Mo)", "通过外部 nsz.exe 引擎压缩 (--block, 1 MB)", "外部nsz.exeエンジンによる圧縮（--block, 1 MB）"),
            ["Сжимать результирующие дампы Switch 2 по спецификации NCZBLOCK v2 с произвольным доступом блоков"] = ("Compress resulting Switch 2 dumps using NCZBLOCK v2 spec with random block access", "Resultierende Switch 2-Dumps nach NCZBLOCK v2-Spezifikation mit wahlfreiem Blockzugriff komprimieren", "Compresser les dumps Switch 2 résultants selon la spécification NCZBLOCK v2 avec accès aléatoire aux blocs", "使用支持块随机访问的 NCZBLOCK v2 规范压缩 Switch 2 转储镜像", "ランダムブロックアクセス対応のNCZBLOCK v2仕様に従って出力Switch 2ダンプを圧縮する"),
            ["Система:"] = ("System:", "System:", "Système :", "系统：", "システム:"),
            ["Системный (Default)"] = ("System (Default)", "Systemstandard (Default)", "Système (Par défaut)", "系统预设 (Default)", "システム標準 (Default)"),
            ["Сканировать все"] = ("Scan All", "Alle scannen", "Tout analyser", "全部扫描", "すべてスキャン"),
            ["Сканировать папку на наличие дампов Switch 2 и отправить их в обработку"] = ("Scan folder for Switch 2 dumps and send them to processing", "Ordner nach Switch 2-Dumps durchsuchen und zur Verarbeitung senden", "Scanner le dossier pour les dumps Switch 2 et les envoyer en traitement", "扫描文件夹中的 Switch 2 游戏转储并提交处理", "フォルダ内のSwitch 2ダンプをスキャンして処理キューに追加"),
            ["Скачать и привязать мод / русификатор (RuTracker / Я.Диск / GDrive)..."] = ("Download & link mod / localization (RuTracker / Yandex Disk / GDrive)...", "Mod / Übersetzung herunterladen & verknüpfen (RuTracker / Yandex / GDrive)...", "Télécharger et lier mod / traduction (RuTracker / Y.Disk / GDrive)...", "下载并关联模组 / 汉化补丁 (RuTracker / Y.Disk / GDrive)...", "MOD・翻訳をダウンロードして関連付け（RuTracker / Y.Disk / GDrive）..."),
            ["Скачать игру / патч / мод (RuTracker / Я.Диск / GDrive)..."] = ("Download game / patch / mod (RuTracker / Yandex Disk / GDrive)...", "Spiel / Patch / Mod herunterladen (RuTracker / Yandex / GDrive)...", "Télécharger jeu / patch / mod (RuTracker / Y.Disk / GDrive)...", "下载游戏 / 补丁 / 模组 (RuTracker / Y.Disk / GDrive)...", "ゲーム・パッチ・MODをダウンロード（RuTracker / Y.Disk / GDrive）..."),
            ["Скопировать все логи в буфер обмена"] = ("Copy all logs to clipboard", "Alle Protokolle in die Zwischenablage kopieren", "Copier tous les journaux dans le presse-papiers", "复制全部日志到剪贴板", "すべてのログをクリップボードにコピー"),
            ["Скриншот 1 из 1"] = ("Screenshot 1 of 1", "Screenshot 1 von 1", "Capture d'écran 1 sur 1", "截图 1 / 1", "スクリーンショット 1 / 1"),
            ["Скриншоты и изображения:"] = ("Screenshots and Images:", "Screenshots und Bilder:", "Captures d'écran et images :", "截图与图片：", "スクリーンショット・画像:"),
            ["Следующая страница"] = ("Next page", "Nächste Seite", "Page suivante", "下一页", "次のページ"),
            ["Снять галочку с базовой игры и оставить только обновления, DLC и моды"] = ("Uncheck base game and keep only updates, DLC, and mods", "Basisspiel abwählen und nur Updates, DLCs und Mods behalten", "Décocher le jeu de base et ne conserver que les mises à jour, DLC et mods", "取消勾选基础游戏，仅保留更新补丁、DLC 与模组", "ベースゲームのチェックを外し、アップデート・DLC・MODのみ保持する"),
            ["Создает удобные вложенные папки формата [Название Игры]/[Формат] с TitleID и версией в названии"] = ("Creates folders in [Game Name]/[Format] format with TitleID and version", "Erstellt Unterordner im Format [Spieltitel]/[Format] mit TitleID und Version", "Crée des dossiers au format [Nom du jeu]/[Format] avec TitleID et version", "自动创建 [游戏名称]/[格式] 结构的子文件夹，并附带TitleID和版本号", "TitleIDとバージョンを含む[ゲーム名]/[形式]形式のサブフォルダを作成"),
            ["Создать отдельную задачу для этой игры / части"] = ("Create separate task for this game / part", "Separate Aufgabe für dieses Spiel / diesen Teil erstellen", "Créer une tâche distincte pour ce jeu / cette partie", "为此游戏 / 分部创建独立任务", "このゲーム/パート用に個別タスクを作成"),
            ["Сортировка каталога"] = ("Catalog Sorting", "Katalogsortierung", "Tri du catalogue", "目录排序", "カタログ並べ替え"),
            ["Сохранить"] = ("Save", "Speichern", "Enregistrer", "保存", "保存"),
            ["Сохранить обложку (PNG)"] = ("Save cover (PNG)", "Cover speichern (PNG)", "Enregistrer la jaquette (PNG)", "保存封面 (PNG)", "カバー画像を保存（PNG）"),
            ["Сохранить скриншот"] = ("Save screenshot", "Screenshot speichern", "Enregistrer la capture d'écran", "保存截图", "スクリーンショットを保存"),
            ["Специализированный комбайн для обработки, сжатия и конвертации игр Nintendo Switch и Nintendo 3DS. Проверка обновлений на GitHub и STORM GAMES WORLD."] = ("Dedicated Swiss-army suite for processing, compressing, and converting Nintendo Switch and Nintendo 3DS games. Check updates on GitHub and STORM GAMES WORLD.", "Spezialisiertes Komplettwerkzeug zur Verarbeitung, Komprimierung und Konvertierung von Nintendo Switch- und Nintendo 3DS-Spielen. Update-Prüfung auf GitHub und STORM GAMES WORLD.", "Suite tout-en-un spécialisée pour le traitement, la compression et la conversion des jeux Nintendo Switch et Nintendo 3DS. Vérification des mises à jour sur GitHub et STORM GAMES WORLD.", "专用于 Nintendo Switch 和 Nintendo 3DS 游戏处理、压缩与格式转换的一体化全能工具箱。支持在 GitHub 和 STORM GAMES WORLD 检查更新。", "Nintendo SwitchおよびNintendo 3DSゲームの処理・圧縮・変換に特化したオールインワンツール。GitHubおよびSTORM GAMES WORLDで更新を確認。"),
            ["Список дополнений"] = ("DLC List", "DLC-Liste", "Liste des DLC", "追加内容 (DLC) 列表", "追加コンテンツ (DLC) 一覧"),
            ["Справочник и расшифровка системных кодов ошибок Nintendo Switch и Atmosphère (2xxx-xxxx)"] = ("Nintendo Switch & Atmosphère system error codes reference (2xxx-xxxx)", "Referenz und Entschlüsselung von Nintendo Switch- & Atmosphère-Fehlercodes (2xxx-xxxx)", "Référence et décodage des codes d'erreur système Nintendo Switch et Atmosphère (2xxx-xxxx)", "Nintendo Switch 与 Atmosphère 系统错误代码查询手册 (2xxx-xxxx)", "Nintendo SwitchおよびAtmosphèreシステムエラーコード解説リファレンス（2xxx-xxxx）"),
            ["Сразу запустить сборку"] = ("Start build immediately", "Build sofort starten", "Lancer la création immédiatement", "立即开始构建", "すぐにビルドを開始"),
            ["Ссылки на Яндекс.Диск, Google Drive, Mega, MediaFire, GitHub Releases..."] = ("Links to Yandex Disk, Google Drive, Mega, MediaFire, GitHub Releases...", "Links zu Yandex Disk, Google Drive, Mega, MediaFire, GitHub Releases...", "Liens vers Yandex Disk, Google Drive, Mega, MediaFire, GitHub Releases...", "Yandex Disk、Google Drive、Mega、MediaFire、GitHub Releases 链接...", "Yandex Disk、Google Drive、Mega、MediaFire、GitHub Releasesへのリンク..."),
            ["Ссылки на скачивание DLC или архива с дополнениями / Unlocker..."] = ("Download links for DLC, add-on archive, or Unlocker...", "Download-Links für DLCs, Add-on-Archive oder Unlocker...", "Liens de téléchargement pour DLC, archive d'extensions ou Unlocker...", "DLC 或追加内容归档包 / 解锁补丁下载链接...", "DLCまたは追加コンテンツアーカイブ / Unlockerのダウンロードリンク..."),
            ["Ссылки на скачивание обновления игры..."] = ("Download links for game updates...", "Download-Links für Spiel-Updates...", "Liens de téléchargement de la mise à jour du jeu...", "游戏更新补丁下载链接...", "ゲームアップデートのダウンロードリンク..."),
            ["Стандарт (уровень 18)"] = ("Standard (Level 18)", "Standard (Stufe 18)", "Standard (Niveau 18)", "标准 (级别 18)", "標準（レベル18）"),
            ["Статус"] = ("Status", "Status", "Statut", "状态", "ステータス"),
            ["Статус: Гостевой режим (поиск по темам, Magnet API и облачные моды работают без входа)"] = ("Status: Guest mode (topic search, Magnet API, and cloud mods work without login)", "Status: Gastmodus (Themensuche, Magnet-API und Cloud-Mods funktionieren ohne Anmeldung)", "Statut : Mode invité (la recherche, l'API Magnet et les mods cloud fonctionnent sans connexion)", "状态：访客模式（主题搜索、Magnet API 与云端模组无需登录即可使用）", "ステータス: ゲストモード（トピック検索、Magnet API、クラウドMODはログインなしで動作）"),
            ["Статус: Патчер готов к работе. Выберите исполняемый файл эмулятора Switch 2 (.exe)"] = ("Status: Patcher is ready. Select Switch 2 emulator executable (.exe)", "Status: Patcher einsatzbereit. Wählen Sie die ausführbare Datei des Switch 2-Emulators (.exe)", "Statut : Patcheur prêt. Sélectionnez l'exécutable de l'émulateur Switch 2 (.exe)", "状态：补丁工具就绪。请选择 Switch 2 模拟器可执行程序 (.exe)", "ステータス: パッチャーの準備完了。Switch 2エミュレータ実行可能ファイル（.exe）を選択してください"),
            ["Степень сжатия Zstd (по умолчанию: 18). Уровни 19–22 обеспечивают сверхплотную упаковку при повышенной нагрузке"] = ("Zstd compression level (default: 18). Levels 19–22 provide ultra-tight packing at higher CPU load", "Zstd-Komprimierungsstufe (Standard: 18). Stufen 19–22 bieten ultrakompakte Packung bei höherer Last", "Niveau de compression Zstd (par défaut : 18). Les niveaux 19-22 offrent une compression maximale", "Zstd 压缩级别（默认: 18）。19–22 级提供超高压缩率，但 CPU 负载较高", "Zstd圧縮レベル（デフォルト: 18）。レベル19〜22は負荷が高くなりますが超高圧縮を実現します"),
            ["Столбцы"] = ("Columns", "Spalten", "Colonnes", "列", "カラム"),
            ["Страница 1 из 1"] = ("Page 1 of 1", "Seite 1 von 1", "Page 1 sur 1", "第 1 页，共 1 页", "1 / 1 ページ"),
            ["Стратегия сборки обновлений и мульти-контента (HardPatch)"] = ("Update & Multi-Content build strategy (HardPatch)", "Build-Strategie für Updates und Multi-Content (HardPatch)", "Stratégie de création de mises à jour et multi-contenu (HardPatch)", "更新与多内容整合构建策略 (HardPatch)", "アップデートおよびマルチコンテンツのビルド戦略（HardPatch）"),
            ["Структура"] = ("Structure", "Struktur", "Structure", "结构", "構造"),
            ["Студия и сеть"] = ("Studio and Network", "Studio und Netzwerk", "Studio et réseau", "工作室与网络", "スタジオとネットワーク"),
            ["Сшит правильно"] = ("Merged properly", "Korrekt zusammengeführt", "Fusionné correctement", "正确缝合", "正常に統合済み"),
            ["Сэйвы"] = ("Saves", "Speicherstände", "Sauvegardes", "存档", "セーブデータ"),
            ["Тема оформления интерфейса"] = ("Interface Theme", "Oberflächendesign", "Thème de l'interface", "界面主题风格", "UIテーマ"),
            ["Тип контента"] = ("Content Type", "Inhaltstyp", "Type de contenu", "内容类型", "コンテンツタイプ"),
            ["Только патчи/моды"] = ("Only patches/mods", "Nur Patches/Mods", "Seulement patchs/mods", "仅补丁/模组", "パッチ/MODのみ"),
            ["Удаление персональных сертификатов консоли, аккаунтных подписей и привязок билетов (чистый Ticketless контейнер)"] = ("Removal of console certificates, account signatures, and ticket bindings (clean Ticketless container)", "Entfernen von Konsolenzertifikaten, Kontosignaturen und Ticketbindungen (sauberer ticketloser Container)", "Suppression des certificats de console, signatures de compte et tickets (conteneur Ticketless propre)", "删除主机个人证书、账号签名与票据绑定（干净的无票据 Ticketless 容器）", "コンソール固有証明書、アカウント署名、チケットバインドの削除（クリーンなTicketlessコンテナ）"),
            ["Удалить"] = ("Remove", "Entfernen", "Supprimer", "移除", "削除"),
            ["Удалить Titlerights (Ticketless NSP)"] = ("Remove Titlerights (Ticketless NSP)", "Titlerights entfernen (Ticketless NSP)", "Supprimer Titlerights (NSP sans ticket)", "移除 Titlerights (无票据 NSP)", "Titlerights削除 (Ticketless NSP)"),
            ["Удалить задачу"] = ("Delete Task", "Aufgabe löschen", "Supprimer la tâche", "删除任务", "タスクを削除"),
            ["Удалить задачу из списка"] = ("Remove task from list", "Aufgabe aus der Liste entfernen", "Supprimer la tâche de la liste", "从列表中删除任务", "リストからタスクを削除"),
            ["Удалить последнюю часть из списка"] = ("Remove last part from list", "Letzten Teil aus der Liste entfernen", "Supprimer la dernière partie de la liste", "从列表中删除最后一个部分", "リストから最後のパートを削除"),
            ["Удалить путь"] = ("Delete path", "Pfad löschen", "Supprimer le chemin", "删除路径", "パスを削除"),
            ["Удаляет временные и системные файлы (.bak, .tmp, Thumbs.db, .DS_Store) и сжимает PNG без потерь через Oxipng (экономия 20–40% веса)"] = ("Removes temporary and system junk (.bak, .tmp, Thumbs.db, .DS_Store) and optimizes PNGs losslessly via Oxipng (20-40% size reduction)", "Entfernt temporäre und Systemdateien (.bak, .tmp, Thumbs.db, .DS_Store) und komprimiert PNGs verlustfrei mit Oxipng", "Supprime les fichiers temporaires (.bak, .tmp, Thumbs.db, .DS_Store) et compresse les PNG sans perte via Oxipng", "清理临时和系统垃圾（.bak、.tmp、Thumbs.db、.DS_Store），并通过 Oxipng 无损压缩 PNG 纹理（体积减少 20-40%）", "一時・システム不要ファイル (.bak, .tmp, Thumbs.db, .DS_Store) を削除し、OxipngでPNGテクスチャを可逆圧縮します"),
            ["Удаляет избыточные дельта-патчи из Update-файлов для экономии до 30% места (-ND true)"] = ("Removes redundant delta patches from updates to save up to 30% disk space (-ND true)", "Entfernt redundante Delta-Patches aus Updates für bis zu 30% Platzersparnis (-ND true)", "Supprime les fragments delta redondants pour économiser jusqu'à 30 % d'espace (-ND true)", "移除更新补丁中多余的增量数据，最高节省30%存储空间 (-ND true)", "不要な差分データを削除して最大30%容量を節約 (-ND true)"),
            ["Удаляет мусорные Delta NCA из файлов обновлений, кардинально снижая вес без потери данных."] = ("Removes redundant Delta NCAs from update files, dramatically reducing file size without data loss.", "Entfernt überflüssige Delta-NCAs aus Update-Dateien und reduziert die Größe drastisch ohne Datenverlust.", "Supprime les Delta NCA inutiles des fichiers de mise à jour, réduisant fortement la taille sans perte.", "从更新文件中移除无用的 Delta NCA 碎片，在不丢失任何数据的前提下大幅缩减体积。", "アップデートファイルから不要なDelta NCAを削除し、データを失うことなく大幅に容量を削減します。"),
            ["Удаляет неиспользуемые языковые данные из RomFS при конвертации или пересборке. Уменьшает размер файла."] = ("Removes unused language data from RomFS during conversion or rebuilding. Reduces file size.", "Entfernt ungenutzte Sprachdaten aus dem RomFS bei Konvertierung oder Rebuild. Reduziert die Dateigröße.", "Supprime les données linguistiques inutilisées de RomFS lors de la conversion ou reconstruction. Réduit la taille.", "在转换或重建时从 RomFS 中移除未使用的语言数据。减小文件体积。", "変換または再構築時にRomFSから未使用の言語データを削除します。ファイルサイズを縮小します。"),
            ["Удалять Delta NCA из обновлений"] = ("Remove Delta NCAs from Updates", "Delta-NCAs aus Updates entfernen", "Supprimer les Delta NCA des mises à jour", "从更新中移除 Delta NCA", "アップデートからDelta NCAを削除"),
            ["Удалять исходные архивы (.zip / .7z) и временные дампы после успешной сборки"] = ("Delete source archives (.zip / .7z) and temporary dumps after successful build", "Quellarchive (.zip / .7z) und temporäre Dumps nach erfolgreichem Build löschen", "Supprimer les archives sources (.zip / .7z) et dumps temporaires après succès", "构建成功后删除原始压缩包 (.zip / .7z) 与临时 Dump 文件", "ビルド成功後に元のアーカイブ（.zip / .7z）と一時ダンプを削除する"),
            ["Удалять исходные файлы после успешной обработки"] = ("Delete source files after successful processing", "Quelldateien nach erfolgreicher Verarbeitung löschen", "Supprimer les fichiers sources après un traitement réussi", "处理成功后删除源文件", "処理成功後に元のファイルを削除"),
            ["Удалять исходные файлы после успешной проверки выходного контейнера"] = ("Delete source files after successful output container verification", "Quelldateien nach erfolgreicher Überprüfung des Ausgabe-Containers löschen", "Supprimer les fichiers sources après vérification réussie du conteneur de sortie", "在输出容器校验成功后删除源文件", "出力コンテナの検証成功後にソースファイルを削除する"),
            ["Удалять неиспользуемые языки из RomFS"] = ("Trim Unused Languages from RomFS", "Ungenutzte Sprachen aus RomFS entfernen", "Supprimer les langues inutilisées de RomFS", "从 RomFS 中精简未使用的语言", "RomFSから不要な言語を削除"),
            ["Укажите одну или несколько папок эмуляторов (STORM SWITCH, Yuzu, Ryujinx, Suyu и др.) для автоматической синхронизации данных Homebrew (SDMC). Поддерживается Drag and Drop, выбор через диалог и ввод нескольких путей. Если указаны — программа использует только их, игнорируя остальные диски."] = ("Specify one or more emulator folders (STORM SWITCH, Yuzu, Ryujinx, Suyu, etc.) to automatically sync Homebrew data (SDMC). Drag & Drop, file dialog, and multiple paths are supported. If specified, the app uses only these paths, ignoring other drives.", "Geben Sie einen oder mehrere Emulator-Ordner (STORM SWITCH, Yuzu, Ryujinx, Suyu usw.) an, um Homebrew-Daten (SDMC) automatisch zu synchronisieren. Unterstützt Drag & Drop, Dialogauswahl und Mehrfacheingabe. Falls angegeben, werden nur diese Pfade verwendet.", "Spécifiez un ou plusieurs dossiers d'émulateurs (STORM SWITCH, Yuzu, Ryujinx, Suyu, etc.) pour synchroniser automatiquement les données Homebrew (SDMC). Glisser-déposer, sélection multiple supportés. Si spécifié, le programme n'utilise que ceux-ci.", "指定一个或多个模拟器文件夹 (STORM SWITCH, Yuzu, Ryujinx, Suyu 等) 以自动同步自制软件数据 (SDMC)。支持拖放、对话框多选及路径输入。若已指定，程序将仅使用这些路径并忽略其他驱动器。", "自作ソフト（SDMC）データを自動同期するエミュレータフォルダ（STORM SWITCH、Yuzu、Ryujinx、Suyuなど）を指定します。ドラッグ＆ドロップ、ダイアログ選択、複数パスに対応。指定時は他のドライブを無視し、これらのみを使用します。"),
            ["Ультра (уровень 22)"] = ("Ultra (Level 22)", "Ultra (Stufe 22)", "Ultra (Niveau 22)", "终极 (级别 22)", "ウルトラ（レベル22）"),
            ["Умная пересборка HardPatch с авто-откатом на Multi-Content"] = ("Smart HardPatch rebuilding with auto-fallback to Multi-Content", "Intelligenter HardPatch-Rebuild mit automatischem Fallback auf Multi-Content", "Reconstruction intelligente HardPatch avec repli automatique sur Multi-Contenu", "智能 HardPatch 整合重建（自动回退到多内容模式）", "スマートHardPatch再構築（Multi-Contentへの自動フォールバック対応）"),
            ["Умная сшивка, оптимизация и модификации (SmartShrink и Hard-Patch)"] = ("Smart Merging, Optimization & Modifications (SmartShrink & Hard-Patch)", "Intelligente Zusammenführung, Optimierung & Modifikationen (SmartShrink & Hard-Patch)", "Fusion intelligente, optimisation et modifications (SmartShrink et Hard-Patch)", "智能缝合、优化与修改 (SmartShrink 与 Hard-Patch)", "スマート統合・最適化・MOD適用（SmartShrinkおよびHard-Patch）"),
            ["Умный режим (Авто)"] = ("Smart Mode (Auto)", "Intelligenter Modus (Auto)", "Mode intelligent (Auto)", "智能模式 (自动)", "スマートモード (自動)"),
            ["Упаковка"] = ("Pack", "Packen", "Compiler", "打包", "パック"),
            ["Уровень сжатия Zstandard (1–22)"] = ("Zstandard compression level (1–22)", "Zstandard-Komprimierungsstufe (1–22)", "Niveau de compression Zstandard (1–22)", "Zstandard 压缩等级 (1–22)", "Zstandard圧縮レベル (1〜22)"),
            ["Уровень сжатия Zstandard (NSZ / XCZ)"] = ("Zstandard Compression Level (NSZ / XCZ)", "Zstandard-Komprimierungsstufe (NSZ / XCZ)", "Niveau de compression Zstandard (NSZ / XCZ)", "Zstandard 压缩级别 (NSZ / XCZ)", "Zstandard 圧縮レベル (NSZ / XCZ)"),
            ["Успешно"] = ("Success", "Erfolgreich", "Succès", "成功", "成功"),
            ["Установить на консоль по USB (DBI)..."] = ("Install to Console via USB (DBI)...", "Über USB auf Konsole installieren (DBI)...", "Installer sur la console via USB (DBI)...", "通过 USB 安装到主机 (DBI)...", "USB経由で本体にインストール（DBI）..."),
            ["Файл"] = ("File", "Datei", "Fichier", "文件", "ファイル"),
            ["Файл выбран"] = ("File selected", "Datei ausgewählt", "Fichier sélectionné", "密钥已加载", "キー読み込み済み"),
            ["Файл ключей 3DS (aes_keys.txt / boot9.bin / encTitleKeys.bin)"] = ("3DS Keys File (aes_keys.txt / boot9.bin / encTitleKeys.bin)", "3DS Schlüsseldatei (aes_keys.txt / boot9.bin / encTitleKeys.bin)", "Fichier de clés 3DS (aes_keys.txt / boot9.bin / encTitleKeys.bin)", "3DS 密钥文件 (aes_keys.txt / boot9.bin / encTitleKeys.bin)", "3DS キーファイル (aes_keys.txt / boot9.bin / encTitleKeys.bin)"),
            ["Файл ключей тайтлов Switch 2 (title.keys)"] = ("Switch 2 Title Keys File (title.keys)", "Switch 2-Titelschlüsseldatei (title.keys)", "Fichier de clés de titres Switch 2 (title.keys)", "Switch 2 游戏标题密钥文件 (title.keys)", "Switch 2 タイトルキーファイル（title.keys）"),
            ["Файл криптографических ключей (prod.keys / keys.txt)"] = ("Keys File (prod.keys / keys.txt)", "Schlüsseldatei (prod.keys / keys.txt)", "Fichier de clés (prod.keys / keys.txt)", "密钥文件 (prod.keys / keys.txt)", "キーファイル (prod.keys / keys.txt)"),
            ["Файл криптографических ключей Switch 2 (prod.keys / ounce.keys)"] = ("Switch 2 Cryptographic Keys File (prod.keys / ounce.keys)", "Kryptografische Switch 2-Schlüsseldatei (prod.keys / ounce.keys)", "Fichier de clés cryptographiques Switch 2 (prod.keys / ounce.keys)", "Switch 2 密码密钥文件 (prod.keys / ounce.keys)", "Switch 2 暗号化キーファイル（prod.keys / ounce.keys）"),
            ["Файлов: 0 | Общий размер: 0 Б"] = ("Files: 0 | Total size: 0 B", "Dateien: 0 | Gesamtgröße: 0 B", "Fichiers : 0 | Taille totale : 0 o", "文件数: 0 | 总大小: 0 B", "ファイル数: 0 | 合計サイズ: 0 B"),
            ["Файлы"] = ("Files", "Dateien", "Fichiers", "文件", "ファイル"),
            ["Фильтр"] = ("Filter", "Filter", "Filtre", "筛选", "フィルター"),
            ["Фоновая предзагрузка обложек всех игр и консолей на диск (офлайн-кэш)"] = ("Background preloading of all game & console covers to disk (offline cache)", "Hintergrund-Vorabladen aller Spiel- & Konsolen-Cover auf Festplatte (Offline-Cache)", "Préchargement en arrière-plan des jaquettes sur le disque (cache hors-ligne)", "在后台将所有游戏与主机的封面预加载到磁盘（离线缓存）", "全ゲーム・全機種のカバー画像をディスクへバックグラウンド事前取得（オフラインキャッシュ）"),
            ["Формат выходного контейнера Switch 2 по умолчанию"] = ("Default Switch 2 output container format", "Standard-Ausgabe-Containerformat für Switch 2", "Format de conteneur de sortie Switch 2 par défaut", "默认 Switch 2 输出容器格式", "Switch 2 のデフォルト出力コンテナ形式"),
            ["Формат контейнера и параметры сжатия Nintendo Switch 2"] = ("Nintendo Switch 2 container format and compression settings", "Containerformat und Komprimierungseinstellungen für Nintendo Switch 2", "Format de conteneur et paramètres de compression Nintendo Switch 2", "Nintendo Switch 2 容器格式与压缩设置", "Nintendo Switch 2 コンテナ形式および圧縮設定"),
            ["Формат по умолчанию для конвертации и сборки (.3ds / .cci для эмуляторов Citra/Lime3DS, .cia для установки)"] = ("Default output format for conversion and builds (.3ds/.cci for emulators, .cia for installation)", "Standardformat für Konvertierung (.3ds/.cci für Emulatoren, .cia zur Installation)", "Format par défaut pour la conversion et la compilation (.3ds/.cci ou .cia)", "默认转换与构建格式（.3ds/.cci 用于模拟器，.cia 用于主机安装）", "変換およびビルドのデフォルト形式（Citra/Lime3DS用.3ds/.cci、本体インストール用.cia）"),
            ["Формат файла"] = ("File Format", "Dateiformat", "Format de fichier", "文件格式", "ファイル形式"),
            ["Цветовой акцент интерфейса"] = ("Interface Accent Color", "Akzentfarbe der Benutzeroberfläche", "Couleur d'accentuation", "界面强调色", "アクセントカラー"),
            ["Целевой контейнер сборки: NSZ (блочный NCZBLOCK, рекомендуется), чистый NSP, картридж XCI или XCZ"] = ("Target build container: NSZ (blocked NCZBLOCK, recommended), clean NSP, XCI cartridge, or XCZ", "Ziel-Build-Container: NSZ (blockiertes NCZBLOCK, empfohlen), sauberes NSP, XCI-Cartridge oder XCZ", "Conteneur cible de création : NSZ (NCZBLOCK par blocs, recommandé), NSP propre, cartouche XCI ou XCZ", "目标构建容器：NSZ (分块 NCZBLOCK，推荐)、纯净 NSP、XCI 卡带或 XCZ", "ターゲットビルドコンテナ: NSZ（ブロック分割NCZBLOCK、推奨）、クリーンNSP、XCIカートリッジ、XCZ"),
            ["Целевой формат"] = ("Target Format", "Zielformat", "Format cible", "目标格式", "出力フォーマット"),
            ["Экономит место на диске, удаляя исходники только после 100% безошибочного завершения."] = ("Saves disk space by deleting sources only after 100% error-free completion.", "Spart Speicherplatz, indem Quelldateien erst nach 100% fehlerfreiem Abschluss gelöscht werden.", "Économise de l'espace disque en ne supprimant les sources qu'après une fin 100% sans erreur.", "仅在 100% 无错完成构建后删除源文件，以节省磁盘空间。", "処理がエラーなく100%正常終了した場合にのみソースを削除し、ディスク容量を節約します。"),
            ["Электрический Синий"] = ("Electric Blue", "Elektrisches Blau", "Bleu électrique", "电光蓝", "エレクトリックブルー"),
            ["Язык интерфейса"] = ("Interface Language", "Sprache", "Langue de l'interface", "界面语言", "表示言語"),
            ["• Nintendo 3DS: Откройте FBI ➔ Remote Install ➔ Scan QR Code и наведите камеру на QR-код выше.&#x0a;• Nintendo Switch: В Tinfoil / DBI укажите HTTP-источник с адресом сервера из поля выше."] = ("• Nintendo 3DS: Open FBI ➔ Remote Install ➔ Scan QR Code and point camera at QR code above.&#x0a;• Nintendo Switch: In Tinfoil / DBI, set HTTP source with server address above.", "• Nintendo 3DS: FBI ➔ Remote Install ➔ Scan QR Code öffnen und Kamera auf obigen QR-Code richten.&#x0a;• Nintendo Switch: In Tinfoil / DBI HTTP-Quelle mit obiger Serveradresse angeben.", "• Nintendo 3DS : Ouvrez FBI ➔ Remote Install ➔ Scan QR Code et visez le QR code ci-dessus.&#x0a;• Nintendo Switch : Dans Tinfoil / DBI, indiquez une source HTTP avec l'adresse ci-dessus.", "• Nintendo 3DS: 打开 FBI ➔ Remote Install ➔ Scan QR Code 并对准上方二维码。&#x0a;• Nintendo Switch: 在 Tinfoil / DBI 中添加以上方服务器地址为源的 HTTP 来源。", "• Nintendo 3DS: FBI ➔ Remote Install ➔ Scan QR Code を開き、上のQRコードにカメラを向けます。&#x0a;• Nintendo Switch: Tinfoil / DBI で上記サーバーアドレスのHTTPソースを指定してください。"),
            ["• Версия прошивки: Ожидание ключей"] = ("• Firmware Version: Awaiting Keys", "• Firmware-Version: Warten auf Schlüssel", "• Version du firmware : En attente des clés", "• 固件版本：等待密钥", "• ファームウェアバージョン: キー待機中"),
            ["↩ Восстановить оригинал (.bak)"] = ("↩ Restore Original (.bak)", "↩ Original wiederherstellen (.bak)", "↩ Restaurer l'original (.bak)", "↩ 还原原始文件 (.bak)", "↩ オリジナルを復元 (.bak)"),
            ["⏸ Пауза"] = ("⏸ Pause", "⏸ Pause", "⏸ Pause", "⏸ 暂停", "⏸ 一時停止"),
            ["⏸ Сохранено на паузе"] = ("⏸ Saved on Pause", "⏸ Pausiert gespeichert", "⏸ Sauvegardé en pause", "⏸ 已暂停保存", "⏸ 一時停止状態で保存"),
            ["⏹ Отменить задачу"] = ("⏹ Cancel Task", "⏹ Aufgabe abbrechen", "⏹ Annuler la tâche", "⏹ 取消任务", "⏹ タスクをキャンセル"),
            ["▶ Запустить отслеживание"] = ("▶ Start Monitoring", "▶ Überwachung starten", "▶ Démarrer la surveillance", "▶ 启动监控", "▶ 監視を開始"),
            ["○ Отслеживание остановлено"] = ("○ Monitoring stopped", "○ Überwachung gestoppt", "○ Surveillance arrêtée", "○ 监控已停止", "○ 停止中"),
            ["☁ Скачать только моды и привязать"] = ("☁ Download Mods Only and Attach", "☁ Nur Mods herunterladen und verknüpfen", "☁ Télécharger uniquement les mods et lier", "☁ 仅下载模组并关联", "☁ MODのみダウンロードして関連付け"),
            ["⚙ Доступ и DoH"] = ("⚙ Access & DoH", "⚙ Zugriff & DoH", "⚙ Accès et DoH", "⚙ 访问与 DoH", "⚙ アクセス＆DoH"),
            ["⚙️ Параметры и Лог задачи"] = ("⚙️ Task Parameters & Log", "⚙️ Aufgabenparameter & Protokoll", "⚙️ Paramètres et journal de la tâche", "⚙️ 任务参数与日志", "⚙️ タスク設定とログ"),
            ["⚠️ Ключи не загружены"] = ("⚠️ Keys Not Loaded", "⚠️ Schlüssel nicht geladen", "⚠️ Clés non chargées", "⚠️ 密钥未加载", "⚠️ キーが読み込まれていません"),
            ["⚡ 3DS авто-обработка: Мульти-контент в 3DS"] = ("⚡ 3DS Auto-Processing: Multi-Content to 3DS", "⚡ 3DS-Autoverarbeitung: Multi-Content in 3DS", "⚡ Traitement auto 3DS : Multi-Contenu vers 3DS", "⚡ 3DS 自动处理：多内容合并至 3DS", "⚡ 3DS自動処理: マルチコンテンツを3DSへ"),
            ["⚡ Switch авто-обработка: Сжатие в NSZ"] = ("⚡ Switch Auto-Processing: Compress to NSZ", "⚡ Switch-Autoverarbeitung: In NSZ komprimieren", "⚡ Traitement auto Switch : Compresser en NSZ", "⚡ Switch 自动处理：压缩为 NSZ", "⚡ Switch自動処理: NSZ形式へ圧縮"),
            ["⚡ Пропатчить .exe эмулятора"] = ("⚡ Patch Emulator .exe", "⚡ Emulator-.exe patchen", "⚡ Patcher l'.exe de l'émulateur", "⚡ 修补模拟器 .exe", "⚡ エミュレータの .exe をパッチ"),
            ["✂ Обрезать XCI/3DS и дешифровать 3DS"] = ("✂ Trim XCI/3DS & Decrypt 3DS", "✂ XCI/3DS trimmen & 3DS entschlüsseln", "✂ Rogner XCI/3DS et décrypter 3DS", "✂ 裁剪 XCI/3DS 并解密 3DS", "✂ XCI/3DSトリミング＆3DS復号"),
            ["✏️ Итоговое имя файла + внутр. имена (EN / RU / Издатель):"] = ("✏️ Final filename + internal names (EN / RU / Publisher):", "✏️ Endgültiger Dateiname + interne Namen (EN / RU / Publisher):", "✏️ Nom de fichier final + noms internes (EN / RU / Éditeur) :", "✏️ 最终文件名 + 内置名称 (EN / RU / 发行商):", "✏️ 最終ファイル名＋内部名（EN / RU / パブリッシャー）:"),
            ["✕ Очистить"] = ("✕ Clear", "✕ Leeren", "✕ Effacer", "✕ 清除", "✕ クリア"),
            ["✕ Сбросить"] = ("✕ Reset", "✕ Zurücksetzen", "✕ Réinitialiser", "✕ 重置", "✕ リセット"),
            ["➕ Добавить игры / патчи / DLC (.NSP/.NSZ/.XCI/.XCZ)"] = ("➕ Add Games / Patches / DLC (.NSP/.NSZ/.XCI/.XCZ)", "➕ Spiele / Patches / DLCs hinzufügen (.NSP/.NSZ/.XCI/.XCZ)", "➕ Ajouter des jeux / patchs / DLC (.NSP/.NSZ/.XCI/.XCZ)", "➕ 添加游戏 / 补丁 / DLC (.NSP/.NSZ/.XCI/.XCZ)", "➕ ゲーム / パッチ / DLCを追加 (.NSP/.NSZ/.XCI/.XCZ)"),
            ["➕ Игра"] = ("➕ Game", "➕ Spiel", "➕ Jeu", "➕ 游戏", "➕ ゲーム"),
            ["➕ Ручной ввод общих облачных ссылок (Обновления / DLC / Моды)"] = ("➕ Manual Input of Cloud Links (Updates / DLC / Mods)", "➕ Manuelle Eingabe von Cloud-Links (Updates / DLC / Mods)", "➕ Saisie manuelle des liens cloud (Mises à jour / DLC / Mods)", "➕ 手动输入共享网盘链接 (更新 / DLC / 模组)", "➕ クラウド共有リンクの手動入力（更新 / DLC / MOD）"),
            ["⬇️ Скачать ключи"] = ("⬇️ Download Keys", "⬇️ Schlüssel herunterladen", "⬇️ Télécharger les clés", "⬇️ 下载密钥", "⬇️ キーをダウンロード"),
            ["🌍 Открыть тему в браузере"] = ("🌍 Open Topic in Browser", "🌍 Thema im Browser öffnen", "🌍 Ouvrir le sujet dans le navigateur", "🌍 在浏览器中打开主题", "🌍 ブラウザでトピックを開く"),
            ["🌐 RuTracker и облачный загрузчик игр, патчей и модов (Nintendo Switch)"] = ("🌐 RuTracker & Cloud Downloader for Games, Patches & Mods (Nintendo Switch)", "🌐 RuTracker & Cloud-Downloader für Spiele, Patches & Mods (Nintendo Switch)", "🌐 RuTracker et téléchargeur cloud de jeux, patchs et mods (Nintendo Switch)", "🌐 RuTracker 与云端游戏/补丁/模组下载器 (Nintendo Switch)", "🌐 RuTracker＆ゲーム・パッチ・MODクラウドダウンローダー（Nintendo Switch）"),
            ["🌐 Онлайн-загрузка ключей Nintendo Switch (STORM SWITCH TOOLS)"] = ("🌐 Online Keys Downloader for Nintendo Switch (STORM SWITCH TOOLS)", "🌐 Online-Schlüssel-Download für Nintendo Switch (STORM SWITCH TOOLS)", "🌐 Téléchargement en ligne des clés Nintendo Switch (STORM SWITCH TOOLS)", "🌐 Nintendo Switch 在线密钥下载 (STORM SWITCH TOOLS)", "🌐 Nintendo Switch オンラインキー取得（STORM SWITCH TOOLS）"),
            ["🎁 Ручной ввод ссылок на Дополнения / DLC и UNLOCKER (по одной на строку):"] = ("🎁 Manual input of links for DLC / Add-ons and UNLOCKER (one per line):", "🎁 Manuelle Eingabe von Links für DLCs und UNLOCKER (einer pro Zeile):", "🎁 Saisie manuelle des liens pour DLC et UNLOCKER (un par ligne) :", "🎁 手动输入追加内容 / DLC 与 UNLOCKER 链接（每行一个）：", "🎁 追加コンテンツ / DLC および UNLOCKER リンクの手動入力（1行に1つ）:"),
            ["🎨 Студия метаданных, иконок и 3DS баннеров"] = ("🎨 Metadata, Icons & 3DS Banners Studio", "🎨 Studio für Metadaten, Icons & 3DS-Banner", "🎨 Studio de métadonnées, icônes et bannières 3DS", "🎨 元数据、图标与 3DS 横幅工作室", "🎨 メタデータ・アイコン・3DSバナースタジオ"),
            ["🎭 Ручной ввод ссылок на Русификаторы и Моды RomFS / ExeFS (по одной на строку):"] = ("🎭 Manual input of links for Localizations and RomFS / ExeFS Mods (one per line):", "🎭 Manuelle Eingabe von Links für Übersetzungen und RomFS / ExeFS-Mods (einer pro Zeile):", "🎭 Saisie manuelle des liens pour traductions et mods RomFS / ExeFS (un par ligne) :", "🎭 手动输入本地化补丁与 RomFS / ExeFS 模组链接（每行一个）：", "🎭 翻訳パッチおよび RomFS / ExeFS MOD リンクの手動入力（1行に1つ）:"),
            ["🎮 Игры / Части в раздаче (отдельные задачи)"] = ("🎮 Games / Parts in Torrent (Separate Tasks)", "🎮 Spiele / Teile im Release (separate Aufgaben)", "🎮 Jeux / Parties dans la distribution (tâches séparées)", "🎮 种子内的游戏 / 分卷（创建独立任务）", "🎮 配布内のゲーム / パート（個別タスク）"),
            ["🎵 Аудио-джингл 3D-баннера Nintendo 3DS (.WAV / .BCWAV -> CBMD)"] = ("🎵 Nintendo 3DS 3D Banner Audio Jingle (.WAV / .BCWAV -> CBMD)", "🎵 Nintendo 3DS 3D-Banner-Audio-Jingle (.WAV / .BCWAV -> CBMD)", "🎵 Jingle audio de bannière 3D Nintendo 3DS (.WAV / .BCWAV -> CBMD)", "🎵 Nintendo 3DS 3D横幅提示音 (.WAV / .BCWAV -> CBMD)", "🎵 Nintendo 3DS 3Dバナー用ジングル（.WAV / .BCWAV -> CBMD）"),
            ["🎵 Выбрать WAV/BCWAV"] = ("🎵 Select WAV/BCWAV", "🎵 WAV/BCWAV auswählen", "🎵 Sélectionner WAV/BCWAV", "🎵 选择 WAV/BCWAV", "🎵 WAV/BCWAVを選択"),
            ["🏷 Визуальные бейджи на иконке (256x256 Switch / 48x48 3DS SMDH)"] = ("🏷 Visual Badges on Icon (256x256 Switch / 48x48 3DS SMDH)", "🏷 Visuelle Badges auf Icon (256x256 Switch / 48x48 3DS SMDH)", "🏷 Badges visuels sur l'icône (256x256 Switch / 48x48 3DS SMDH)", "🏷 图标上的视觉角标徽章 (256x256 Switch / 48x48 3DS SMDH)", "🏷 アイコン上のビジュアルバッジ（256x256 Switch / 48x48 3DS SMDH）"),
            ["💡 Выберите раздачу слева. Если в раздаче несколько игр/частей (напр. Aggelos 1-2), настройте их в центральной колонке — для каждой части будет создана отдельная задача."] = ("💡 Select a torrent on the left. If it contains multiple games/parts (e.g. Aggelos 1-2), configure them in the center column — a separate task will be created for each part.", "💡 Wählen Sie links ein Release aus. Enthält es mehrere Spiele/Teile (z. B. Aggelos 1-2), passen Sie diese in der mittleren Spalte an — für jeden Teil wird eine separate Aufgabe erstellt.", "💡 Sélectionnez un torrent à gauche. S'il contient plusieurs jeux/parties (ex. Aggelos 1-2), configurez-les dans la colonne centrale — une tâche distincte sera créée pour chaque partie.", "💡 在左侧选择一个种子资源。如果该资源包含多款游戏/章节（例如 Aggelos 1-2），请在中间列进行配置——将为每个部分创建单独的任务。", "💡 左側でトレントを選択してください。複数のゲームやパートが含まれている場合（例: Aggelos 1-2）、中央のカラムで設定してください — パートごとに個別のタスクが作成されます。"),
            ["💡 Для Switch изменения вшиваются в Control NCA (NACP + 256x256 JPEG) при сборке задачи. Для 3DS автоматически генерируется 16-язычный SMDH (48x48 + 24x24 Morton RGB565)."] = ("💡 For Switch, changes are injected into Control NCA (NACP + 256x256 JPEG) during task build. For 3DS, a 16-language SMDH (48x48 + 24x24 Morton RGB565) is automatically generated.", "💡 Für Switch werden Änderungen beim Build in die Control-NCA (NACP + 256x256 JPEG) eingebunden. Für 3DS wird automatisch eine 16-sprachige SMDH (48x48 + 24x24 Morton RGB565) generiert.", "💡 Pour Switch, les modifications sont intégrées dans le Control NCA (NACP + 256x256 JPEG). Pour 3DS, un SMDH en 16 langues (48x48 + 24x24 Morton RGB565) est généré automatiquement.", "💡 对于 Switch，更改将在构建任务时嵌入到 Control NCA (NACP + 256x256 JPEG) 中。对于 3DS，将自动生成 16 种语言的 SMDH (48x48 + 24x24 Morton RGB565)。", "💡 Switchの場合、ビルド時にControl NCA（NACP + 256x256 JPEG）に変更が適用されます。3DSの場合、16言語対応SMDH（48x48 + 24x24 Morton RGB565）が自動生成されます。"),
            ["💡 Для Switch изменения вшиваются в Control NCA (NACP + 256x256 JPEG). Для 3DS автоматически генерируется 16-язычный SMDH (48x48 + 24x24 Morton RGB565) и вшивается аудио CWAV в banner.bin."] = ("💡 For Switch, changes are injected into Control NCA (NACP + 256x256 JPEG). For 3DS, a 16-language SMDH (48x48 + 24x24 Morton RGB565) is automatically generated and CWAV audio is injected into banner.bin.", "💡 Für Switch werden Änderungen in die Control-NCA (NACP + 256x256 JPEG) eingebunden. Für 3DS wird automatisch eine 16-sprachige SMDH (48x48 + 24x24 Morton RGB565) generiert und CWAV-Audio in banner.bin eingefügt.", "💡 Pour Switch, modifications intégrées dans Control NCA (NACP + JPEG 256x256). Pour 3DS, SMDH 16 langues (48x48 + 24x24 Morton RGB565) auto-généré et audio CWAV injecté dans banner.bin.", "💡 对于 Switch，更改会嵌入 Control NCA (NACP + 256x256 JPEG)。对于 3DS，将自动生成 16 种语言的 SMDH (48x48 + 24x24 Morton RGB565) 并将 CWAV 音频注入 banner.bin。", "💡 Switchの場合、Control NCA（NACP + 256x256 JPEG）に変更が適用されます。3DSの場合、16言語対応SMDH（48x48 + 24x24 Morton RGB565）が自動生成され、CWAV音声がbanner.binに組み込まれます。"),
            ["💡 Или перетащите папки эмуляторов прямо сюда"] = ("💡 Or drag and drop emulator folders directly here", "💡 Oder ziehen Sie Emulator-Ordner direkt hierher", "💡 Ou glissez les dossiers d'émulateurs directement ici", "💡 或者直接将模拟器文件夹拖放到此处", "💡 またはエミュレータフォルダをここに直接ドラッグ＆ドロップ"),
            ["💡 Подсказка: на Switch запустите DBI («Run FTP server», порт 5000) или sys-ftpd, на 3DS — приложение FTPD (порт 5000 или 21). Для .CIA папка по умолчанию: /cias."] = ("💡 Tip: On Switch, run DBI (\"Run FTP server\", port 5000) or sys-ftpd; on 3DS, run FTPD (port 5000 or 21). Default folder for .CIA: /cias.", "💡 Tipp: Auf der Switch DBI („Run FTP server“, Port 5000) oder sys-ftpd starten, auf dem 3DS die FTPD-App (Port 5000 oder 21). Standardordner für .CIA: /cias.", "💡 Astuce : Sur Switch, lancez DBI (« Run FTP server », port 5000) ou sys-ftpd ; sur 3DS, l'application FTPD (port 5000 ou 21). Dossier .CIA par défaut : /cias.", "💡 提示：在 Switch 上运行 DBI (\"Run FTP server\"，端口 5000) 或 sys-ftpd；在 3DS 上运行 FTPD 应用 (端口 5000 或 21)。对于 .CIA，默认目录为：/cias。", "💡 ヒント: SwitchではDBI（「Run FTP server」、ポート5000）またはsys-ftpdを、3DSではFTPDアプリ（ポート5000または21）を起動してください。.CIAのデフォルトフォルダ: /cias"),
            ["💬 Ссылки из раздачи и обсуждений (по играм)"] = ("💬 Links from Torrent & Discussions (by Game)", "💬 Links aus Release & Diskussionen (nach Spielen)", "💬 Liens du torrent et des discussions (par jeu)", "💬 资源与讨论区链接（按游戏分类）", "💬 配布・フォーラムからのリンク（ゲーム別）"),
            ["💾 Сбросить на SD / USB диск"] = ("💾 Dump to SD / USB Drive", "💾 Auf SD- / USB-Laufwerk speichern", "💾 Exporter vers la carte SD / clé USB", "💾 导出到 SD 卡 / USB 驱动器", "💾 SD / USBドライブへ書き出し"),
            ["💾 Сохранить обложку"] = ("💾 Save Cover", "💾 Cover speichern", "💾 Enregistrer la jaquette", "💾 保存封面", "💾 カバーを保存"),
            ["📁 Выбрать файл"] = ("📁 Select File", "📁 Datei auswählen", "📁 Sélectionner un fichier", "📁 选择文件", "📁 ファイルを選択"),
            ["📁 Папка..."] = ("📁 Folder...", "📁 Ordner...", "📁 Dossier...", "📁 文件夹...", "📁 フォルダ..."),
            ["📂 .torrent файл..."] = ("📂 .torrent File...", "📂 .torrent-Datei...", "📂 Fichier .torrent...", "📂 .torrent 文件...", "📂 .torrentファイル..."),
            ["📂 Выбрать файл"] = ("📂 Browse...", "📂 Datei wählen", "📂 Parcourir...", "📂 选择文件", "📂 ファイルを選択"),
            ["📂 Выбрать файлы (.NSP/.NSZ/.XCI/.CIA/.3DS)..."] = ("📂 Select Files (.NSP/.NSZ/.XCI/.CIA/.3DS)...", "📂 Dateien auswählen (.NSP/.NSZ/.XCI/.CIA/.3DS)...", "📂 Sélectionner les fichiers (.NSP/.NSZ/.XCI/.CIA/.3DS)...", "📂 选择文件 (.NSP/.NSZ/.XCI/.CIA/.3DS)...", "📂 ファイルを選択（.NSP/.NSZ/.XCI/.CIA/.3DS）..."),
            ["📂 Обзор"] = ("📂 Browse", "📂 Durchsuchen", "📂 Parcourir", "📂 浏览", "📂 参照"),
            ["📂 Обзор..."] = ("📂 Browse...", "📂 Durchsuchen...", "📂 Parcourir...", "📂 浏览...", "📂 参照..."),
            ["📂 Открыть образ..."] = ("📂 Open Image...", "📂 Image öffnen...", "📂 Ouvrir l'image...", "📂 打开镜像...", "📂 イメージを開く..."),
            ["📂 Папка кэша"] = ("📂 Cache Folder", "📂 Cache-Ordner", "📂 Dossier du cache", "📂 缓存文件夹", "📂 キャッシュフォルダ"),
            ["📂 Папка с логами"] = ("📂 Logs Folder", "📂 Protokollordner", "📂 Dossier des journaux", "📂 日志文件夹", "📂 ログフォルダ"),
            ["📄 Экспорт отчёта (.TXT)"] = ("📄 Export Report (.TXT)", "📄 Bericht exportieren (.TXT)", "📄 Exporter le rapport (.TXT)", "📄 导出报告 (.TXT)", "📄 レポート出力 (.TXT)"),
            ["📋 Журнал событий"] = ("📋 Event Log", "📋 Ereignisprotokoll", "📋 Journal des événements", "📋 事件日志", "📋 イベントログ"),
            ["📋 Копировать"] = ("📋 Copy", "📋 Kopieren", "📋 Copier", "📋 复制信息", "📋 情報をコピー"),
            ["📋 Раздачи RuTracker (Switch и 3DS)"] = ("📋 RuTracker Releases (Switch & 3DS)", "📋 RuTracker-Releases (Switch & 3DS)", "📋 Distributions RuTracker (Switch et 3DS)", "📋 RuTracker 资源 (Switch 与 3DS)", "📋 RuTracker配布一覧（Switch＆3DS）"),
            ["📡 Отправить по FTP (Switch / 3DS)"] = ("📡 Send via FTP (Switch / 3DS)", "📡 Über FTP senden (Switch / 3DS)", "📡 Envoyer par FTP (Switch / 3DS)", "📡 通过 FTP 发送 (Switch / 3DS)", "📡 FTP経由で送信（Switch / 3DS）"),
            ["📡 Прямая отправка на консоль Switch и 3DS (Wi-Fi / FTP / FBI QR / Tinfoil)"] = ("📡 Direct Transfer to Switch & 3DS (Wi-Fi / FTP / FBI QR / Tinfoil)", "📡 Direktübertragung zu Switch & 3DS (WLAN / FTP / FBI-QR / Tinfoil)", "📡 Transfert direct vers Switch et 3DS (Wi-Fi / FTP / QR FBI / Tinfoil)", "📡 直接发送至 Switch 与 3DS 主机 (Wi-Fi / FTP / FBI 二维码 / Tinfoil)", "📡 Switch・3DS本体へ直接送信（Wi-Fi / FTP / FBI QR / Tinfoil）"),
            ["📥 Извлечь выбранный"] = ("📥 Extract Selected", "📥 Ausgewählte extrahieren", "📥 Extraire la sélection", "📥 提取所选", "📥 選択項目を抽出"),
            ["📦 Извлечь все найденные"] = ("📦 Extract All Found", "📦 Alle gefundenen extrahieren", "📦 Extraire tous les éléments trouvés", "📦 提取所有找到的项目", "📦 見つかったすべてを抽出"),
            ["📦 Файлы в раздаче (сгруппированы по играм)"] = ("📦 Torrent Files (Grouped by Game)", "📦 Dateien im Release (nach Spielen gruppiert)", "📦 Fichiers du torrent (groupés par jeu)", "📦 种子内的文件（按游戏分组）", "📦 配布内ファイル（ゲームごとにグループ化）"),
            ["📱 Wi-Fi HTTP-сервер и QR-установка (3DS FBI / Switch Tinfoil)"] = ("📱 Wi-Fi HTTP Server & QR Install (3DS FBI / Switch Tinfoil)", "📱 WLAN-HTTP-Server & QR-Installation (3DS FBI / Switch Tinfoil)", "📱 Serveur HTTP Wi-Fi et installation QR (3DS FBI / Switch Tinfoil)", "📱 Wi-Fi HTTP 服务器与二维码安装 (3DS FBI / Switch Tinfoil)", "📱 Wi-Fi HTTPサーバー＆QRインストール（3DS FBI / Switch Tinfoil）"),
            ["🔄 Проверить обновления (GitHub + STORM GAMES WORLD)"] = ("🔄 Check for Updates (GitHub + STORM GAMES WORLD)", "🔄 Nach Updates suchen (GitHub + STORM GAMES WORLD)", "🔄 Vérifier les mises à jour (GitHub + STORM GAMES WORLD)", "🔄 检查更新 (GitHub + STORM GAMES WORLD)", "🔄 アップデートを確認（GitHub + STORM GAMES WORLD）"),
            ["🔄 Ручной ввод ссылок на Обновление / Update (по одной на строку — файл или архив .zip/.7z/.rar):"] = ("🔄 Manual input of links for Update (one per line — file or .zip/.7z/.rar archive):", "🔄 Manuelle Eingabe von Update-Links (einer pro Zeile — Datei oder .zip/.7z/.rar-Archiv):", "🔄 Saisie manuelle des liens de mise à jour (un par ligne — fichier ou archive .zip/.7z/.rar) :", "🔄 手动输入更新补丁 / Update 链接（每行一个 — 文件或 .zip/.7z/.rar 归档包）：", "🔄 アップデート / Update リンクの手動入力（1行に1つ — ファイルまたは .zip/.7z/.rar アーカイブ）:"),
            ["🔄 Сбросить"] = ("🔄 Reset", "🔄 Zurücksetzen", "🔄 Réinitialiser", "🔄 重置", "🔄 リセット"),
            ["🔍 Авто-поиск"] = ("🔍 Auto Search", "🔍 Automatische Suche", "🔍 Recherche auto", "🔍 自动搜索", "🔍 自動検索"),
            ["🔍 Визуальный инспектор и выборочный распаковщик RomFS / ExeFS (без полной распаковки)"] = ("🔍 Visual Inspector & Selective RomFS / ExeFS Extractor (without full unpack)", "🔍 Visueller Inspektor & selektiver RomFS / ExeFS-Entpacker (ohne vollständiges Entpacken)", "🔍 Inspecteur visuel et extracteur sélectif RomFS / ExeFS (sans extraction complète)", "🔍 可视化检查器与选择性 RomFS / ExeFS 提取工具（无需完整解包）", "🔍 ビジュアルインスペクター＆選択的RomFS / ExeFS展開ツール（完全展開不要）"),
            ["🔍 Найти / Открыть"] = ("🔍 Find / Open", "🔍 Finden / Öffnen", "🔍 Trouver / Ouvrir", "🔍 查找 / 打开", "🔍 検索 / 開く"),
            ["🔍 Найти консоль в LAN"] = ("🔍 Find Console in LAN", "🔍 Konsole im LAN suchen", "🔍 Trouver la console sur le réseau local", "🔍 在局域网中搜索主机", "🔍 LAN内のコンソールを検索"),
            ["🔍 Поиск по задачам..."] = ("🔍 Search tasks...", "🔍 Aufgaben durchsuchen...", "🔍 Rechercher dans les tâches...", "🔍 搜索任务...", "🔍 タスクを検索..."),
            ["🔍 Поиск..."] = ("🔍 Search...", "🔍 Suchen...", "🔍 Rechercher...", "🔍 搜索...", "🔍 検索..."),
            ["🔍 Сканировать и добавить в задачник"] = ("🔍 Scan and Add to Task Queue", "🔍 Scannen und zur Aufgabenliste hinzufügen", "🔍 Scanner et ajouter à la file d'attente", "🔍 扫描并添加到任务队列", "🔍 スキャンしてタスクキューに追加"),
            ["🔍 Сканировать коллекцию"] = ("🔍 Scan Collection", "🔍 Sammlung scannen", "🔍 Scanner la collection", "🔍 扫描游戏库", "🔍 コレクションをスキャン"),
            ["🔎 Быстрый фильтр по имени или маске (напр. .msbt, .bntx, .lua, control.nacp, code, icon)..."] = ("🔎 Quick filter by name or mask (e.g. .msbt, .bntx, .lua, control.nacp, code, icon)...", "🔎 Schnellfilter nach Name oder Maske (z. B. .msbt, .bntx, .lua, control.nacp, code, icon)...", "🔎 Filtre rapide par nom ou masque (ex. .msbt, .bntx, .lua, control.nacp, code, icon)...", "🔎 按名称或通配符快速筛选 (例如 .msbt, .bntx, .lua, control.nacp, code, icon)...", "🔎 ファイル名やマスクでクイックフィルタ（例: .msbt, .bntx, .lua, control.nacp, code, icon）..."),
            ["🔑 Войти"] = ("🔑 Sign In", "🔑 Anmelden", "🔑 Se connecter", "🔑 登录", "🔑 サインイン"),
            ["🔓 Дешифровать 3DS"] = ("🔓 Decrypt 3DS", "🔓 3DS entschlüsseln", "🔓 Décrypter la 3DS", "🔓 解密 3DS", "🔓 3DSを復号"),
            ["🖼 Выбрать иконку (PNG/JPG)"] = ("🖼 Select Icon (PNG/JPG)", "🖼 Icon auswählen (PNG/JPG)", "🖼 Sélectionner une icône (PNG/JPG)", "🖼 选择图标 (PNG/JPG)", "🖼 アイコンを選択（PNG/JPG）"),
            ["🖼 Иконка..."] = ("🖼 Icon...", "🖼 Icon...", "🖼 Icône...", "🖼 图标...", "🖼 アイコン..."),
            ["🗂 Конструктор мульти-картриджей XCI / XCZ (Multi-Game Cartridge Builder)"] = ("🗂 Multi-Game Cartridge Builder (XCI / XCZ)", "🗂 Multi-Game-Cartridge-Builder (XCI / XCZ)", "🗂 Créateur de multi-cartouches (XCI / XCZ)", "🗂 XCI / XCZ 多合一卡带构建器 (Multi-Game Cartridge Builder)", "🗂 XCI / XCZ マルチゲームカートリッジビルダー"),
            ["🗑 Очистить список"] = ("🗑 Clear List", "🗑 Liste leeren", "🗑 Effacer la liste", "🗑 清空列表", "🗑 リストをクリア"),
            ["🗑 Удалить выбранные старые патчи"] = ("🗑 Remove Selected Obsolete Patches", "🗑 Ausgewählte veraltete Patches entfernen", "🗑 Supprimer les anciens patchs sélectionnés", "🗑 删除所选的旧版本补丁", "🗑 選択した古いパッチを削除"),
            ["🗑️ Очистить"] = ("🗑️ Clear", "🗑️ Leeren", "🗑️ Effacer", "🗑️ 清除", "🗑️ クリア"),
            ["🗑️ Очистить кэш обложек"] = ("🗑️ Clear Cover Cache", "🗑️ Cover-Cache leeren", "🗑️ Vider le cache des jaquettes", "🗑️ 清空封面缓存", "🗑️ カバーキャッシュを削除"),
            ["🚀 В Задачник (сшить / сжать выбранные)"] = ("🚀 Queue Tasks (Merge / Compress Selected)", "🚀 Zur Aufgabenliste (ausgewählte zusammenführen / komprimieren)", "🚀 Ajouter aux tâches (fusionner / compresser la sélection)", "🚀 加入任务队列 (缝合 / 压缩所选项目)", "🚀 タスクキューに追加（選択項目を統合 / 圧縮）"),
            ["🚀 Прямая отправка по FTP и на SD/USB накопитель"] = ("🚀 Direct Transfer via FTP & to SD/USB Drive", "🚀 Direktübertragung über FTP & auf SD-/USB-Laufwerk", "🚀 Envoi direct via FTP et sur support SD/USB", "🚀 直接通过 FTP 发送及导出到 SD/USB 存储设备", "🚀 FTP送信およびSD/USBストレージへ直接転送"),
            ["🚀 Скачать раздачу + моды и создать задачи"] = ("🚀 Download Torrent + Mods & Queue Tasks", "🚀 Release + Mods herunterladen & Aufgaben erstellen", "🚀 Télécharger torrent + mods et créer les tâches", "🚀 下载资源与模组并创建任务", "🚀 トレントとMODをダウンロードしてタスクを作成"),
            ["🚀 Собрать мульти-картридж"] = ("🚀 Build Multi-Cartridge", "🚀 Multi-Cartridge erstellen", "🚀 Créer la multi-cartouche", "🚀 构建多合一卡带", "🚀 マルチカートリッジをビルド"),
            ["🛠 Параметры модификаций (сохраняются в коллекции и подстановках)"] = ("🛠 Mod Parameters (Saved in Collection & Presets)", "🛠 Mod-Parameter (in Sammlung & Voreinstellungen gespeichert)", "🛠 Paramètres des modifications (enregistrés dans la collection)", "🛠 模组修改参数（保存在游戏库与预设中）", "🛠 MOD適用パラメータ（コレクションおよびプリセットに保存）"),
            ["🛡 Аудитор и дедупликатор коллекции игр (Library Health and Space Saver)"] = ("🛡 Game Collection Auditor & Deduplicator (Library Health & Space Saver)", "🛡 Sammlungs-Auditor & Deduplizierer (Library Health & Space Saver)", "🛡 Auditeur et déduplicateur de collection (Santé de la bibliothèque)", "🛡 游戏库审计与重复项清理工具 (Library Health and Space Saver)", "🛡 ゲームコレクション監査＆重複排除ツール（Library Health and Space Saver）"),
            ["🛡️ RuTracker: проверка Cloudflare Turnstile"] = ("🛡️ RuTracker: Cloudflare Turnstile Verification", "🛡️ RuTracker: Cloudflare Turnstile-Prüfung", "🛡️ RuTracker : Vérification Cloudflare Turnstile", "🛡️ RuTracker: Cloudflare Turnstile 人机验证", "🛡️ RuTracker: Cloudflare Turnstile 認証"),
            ["🪄 Авто"] = ("🪄 Auto", "🪄 Auto", "🪄 Auto", "🪄 自动", "🪄 自動"),
        };
    }
}
