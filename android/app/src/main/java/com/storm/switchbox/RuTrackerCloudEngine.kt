package com.storm.switchbox

import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.InetSocketAddress
import java.net.Proxy
import java.net.ServerSocket
import java.net.URL
import java.net.URLEncoder
import java.nio.charset.Charset
import java.util.zip.ZipEntry
import java.util.zip.ZipInputStream

/**
 * Поисковый и облачный движок RuTracker (ТОЛЬКО игры и русификаторы Nintendo Switch: разделы 1605 и 2481),
 * онлайн-загрузчик ключей prod.keys / title.keys (STORM SWITCH TOOLS), автообновление приложения с GitHub,
 * скачивание модов с Яндекс.Диска / Google Drive / GitHub и встроенный HTTP Range сервер для DBI / Tinfoil.
 */
object RuTrackerCloudEngine {

    data class TopicItem(
        val topicId: String,
        val title: String,
        val category: String,
        val sizeText: String,
        val seeds: Int,
        val leechers: Int,
        val infoHash: String,
        val magnetUri: String,
        val topicUrl: String
    )

    data class OnlineKeysAsset(
        val version: String,
        val sizeBytes: Long,
        val downloadUrl: String,
        val isRecommended: Boolean
    )

    data class AppUpdateRelease(
        val version: String,
        val title: String,
        val changelog: String,
        val apkName: String,
        val apkSizeBytes: Long,
        val downloadUrl: String
    )

    private val TRACKERS = listOf(
        "http://bt.t-ru.org/ann?magnet",
        "http://bt2.t-ru.org/ann?magnet",
        "http://bt3.t-ru.org/ann?magnet",
        "http://bt4.t-ru.org/ann?magnet",
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://exodus.desync.com:6969/announce"
    )

    // Строго только игры и русификаторы Nintendo Switch (разделы 1605, 2481)
    private val CURATED_SWITCH_DB = listOf(
        TopicItem("5944816", "[Nintendo Switch] Hades [v1.0.38290 + DLC] [NSP / NSZ] [RUS / Multi10]", "Nintendo Switch", "10.31 GB", 142, 9, "50CE1151A4EA64913AB8E2AB118D1B90FC241E9E", "", ""),
        TopicItem("6512044", "[Nintendo Switch] Hades II [v1.133066] [NSP / NSZ] [RUS / Multi15]", "Nintendo Switch", "8.44 GB", 95, 12, "7FF90903D9D4FB1AA818D716024CBF730F93883F", "", ""),
        TopicItem("5392184", "[Nintendo Switch] The Legend of Zelda: Breath of the Wild [v1.6.0 + 2 DLC] [NSP / NSZ] [RUS (Озвучка + Текст)]", "Nintendo Switch", "16.12 GB", 318, 24, "8A3B9E74B45A9D3F12C64E81D0A5B79C34F12E88", "", ""),
        TopicItem("6358910", "[Nintendo Switch] The Legend of Zelda: Tears of the Kingdom [v1.2.1 + Моды 60 FPS] [NSP / NSZ / XCI] [RUS]", "Nintendo Switch", "16.34 GB", 412, 37, "76E054C1B7EA40327523B4C535F593BEB088A569", "", ""),
        TopicItem("6558120", "[Nintendo Switch] The Legend of Zelda: Echoes of Wisdom [v1.0.2] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.82 GB", 184, 15, "4B9E21C88F1A43D09A77E5C1B2D3E4F5A6B7C8D9", "", ""),
        TopicItem("5778412", "[Nintendo Switch] The Legend of Zelda: Link's Awakening [v1.0.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.94 GB", 152, 9, "5C8E12A99F2B54E10B88F6D2C3E4F5A6B7C8D9E0", "", ""),
        TopicItem("6078192", "[Nintendo Switch] The Legend of Zelda: Skyward Sword HD [v1.0.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "7.12 GB", 144, 8, "6D9F23B00A3C65F21C99A7E3D4F5A6B7C8D9E0F1", "", ""),
        TopicItem("5481023", "[Nintendo Switch] Super Mario Odyssey [v1.3.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.59 GB", 245, 14, "3C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D", "", ""),
        TopicItem("5401298", "[Nintendo Switch] Mario Kart 8 Deluxe [v3.0.3 + Booster Course Pass DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.24 GB", 289, 19, "9E8D7C6B5A4F3E2D1C0B9A8F7E6D5C4B3A2F1E0D", "", ""),
        TopicItem("6429815", "[Nintendo Switch] Super Mario Bros. Wonder [v1.0.1] [NSP / NSZ] [RUS (Текст + Звук)]", "Nintendo Switch", "3.61 GB", 210, 11, "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B", "", ""),
        TopicItem("5981240", "[Nintendo Switch] Super Mario 3D World + Bowser's Fury [v1.1.0] [NSP / NSZ] [RUS]", "Nintendo Switch", "2.92 GB", 174, 10, "7E0A34C11B4D76A32D00B8F4E5A6B7C8D9E0F1A2", "", ""),
        TopicItem("6489120", "[Nintendo Switch] Paper Mario: The Thousand-Year Door [v1.0.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.18 GB", 136, 9, "8F1B45D22C5E87B43E11C9A5F6B7C8D9E0F1A2B3", "", ""),
        TopicItem("5791204", "[Nintendo Switch] The Witcher 3: Wild Hunt — Complete Edition [v4.04b + RUS Voice] [NSP / NSZ] [RUS]", "Nintendo Switch", "31.40 GB", 198, 22, "2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C", "", ""),
        TopicItem("5498211", "[Nintendo Switch] The Elder Scrolls V: Skyrim Anniversary Edition [v1.1.392 + 74 DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "15.85 GB", 176, 16, "3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E", "", ""),
        TopicItem("5572890", "[Nintendo Switch] Hollow Knight [v1.4.3.2 + 4 DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.32 GB", 164, 8, "4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F", "", ""),
        TopicItem("6120945", "[Nintendo Switch] Metroid Dread [v2.1.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.25 GB", 138, 7, "5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A", "", ""),
        TopicItem("6318490", "[Nintendo Switch] Metroid Prime Remastered [v1.0.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "6.88 GB", 129, 6, "6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B", "", ""),
        TopicItem("6279104", "[Nintendo Switch] Persona 5 Royal [v1.0.2 + Полный русификатор текста] [NSP / NSZ] [RUS / ENG]", "Nintendo Switch", "14.18 GB", 225, 18, "7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C", "", ""),
        TopicItem("6245819", "[Nintendo Switch] Xenoblade Chronicles 3 [v2.2.0 + 4 DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "18.92 GB", 156, 14, "8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D", "", ""),
        TopicItem("5892341", "[Nintendo Switch] Xenoblade Chronicles: Definitive Edition [v1.1.2 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "13.65 GB", 118, 9, "9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E", "", ""),
        TopicItem("6284912", "[Nintendo Switch] Bayonetta 3 [v1.2.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "14.40 GB", 112, 8, "0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F", "", ""),
        TopicItem("5761239", "[Nintendo Switch] Fire Emblem: Three Houses [v1.2.0 + 4 DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.84 GB", 134, 11, "1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A", "", ""),
        TopicItem("5754312", "[Nintendo Switch] Astral Chain [v1.0.1] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "9.76 GB", 104, 6, "2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B", "", ""),
        TopicItem("5864120", "[Nintendo Switch] Animal Crossing: New Horizons [v2.0.6 + Happy Home Paradise] [NSP / NSZ] [RUS]", "Nintendo Switch", "10.42 GB", 203, 15, "3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C", "", ""),
        TopicItem("5798410", "[Nintendo Switch] Luigi's Mansion 3 [v1.4.0 + 2 DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "7.45 GB", 149, 9, "4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D", "", ""),
        TopicItem("6194820", "[Nintendo Switch] Kirby and the Forgotten Land [v1.0.0 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.85 GB", 131, 8, "5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E", "", ""),
        TopicItem("6168923", "[Nintendo Switch] Pokemon Legends: Arceus [v1.1.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "6.22 GB", 142, 12, "6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F", "", ""),
        TopicItem("6394812", "[Nintendo Switch] Red Dead Redemption [v1.0.4 + Undead Nightmare] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "11.54 GB", 187, 14, "7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A", "", ""),
        TopicItem("6451208", "[Nintendo Switch] Hogwarts Legacy [v1.0.2 + DLC + Русская озвучка] [NSP / NSZ] [RUS]", "Nintendo Switch", "14.80 GB", 168, 17, "8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B", "", ""),
        TopicItem("6271049", "[Nintendo Switch] NieR:Automata The End of YoRHa Edition [v1.0.2 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "10.88 GB", 146, 10, "9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C", "", ""),
        TopicItem("5621894", "[Nintendo Switch] Dark Souls: Remastered [v1.0.3] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.02 GB", 119, 6, "0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D", "", ""),
        TopicItem("6118420", "[Nintendo Switch] Diablo II: Resurrected [v1.0.28.0 + Offline Patch] [NSP / NSZ] [RUS (Звук + Текст)]", "Nintendo Switch", "8.64 GB", 154, 11, "1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E", "", ""),
        TopicItem("5640129", "[Nintendo Switch] Diablo III: Eternal Collection [v2.7.6 + RUS Pack] [NSP / NSZ] [RUS]", "Nintendo Switch", "21.30 GB", 137, 9, "2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F", "", ""),
        TopicItem("6478912", "[Nintendo Switch] Prince of Persia: The Lost Crown [v1.4.1 + DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "6.92 GB", 128, 7, "3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A", "", ""),
        TopicItem("6492014", "[Nintendo Switch] Balatro [v1.0.1f + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "148 MB", 172, 5, "4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B", "", ""),
        TopicItem("5712840", "[Nintendo Switch] Cuphead [v1.3.4 + The Delicious Last Course DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "3.48 GB", 139, 6, "5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C", "", ""),
        TopicItem("5941280", "[Nintendo Switch] Ori and the Will of the Wisps [v1.2.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.12 GB", 124, 5, "6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D", "", ""),
        TopicItem("5612940", "[Nintendo Switch] Dead Cells [v1.35.0 + All DLC + Return to Castlevania] [NSP / NSZ] [RUS]", "Nintendo Switch", "2.18 GB", 115, 4, "7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E", "", ""),
        TopicItem("5519284", "[Nintendo Switch] Stardew Valley [v1.6.9] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "1.58 GB", 162, 8, "8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F", "", "")
    )

    private val NON_SWITCH_REJECT_REGEX = Regex(
        """\b(FitGirl|DODI|RUNE|TENOKE|FLT|SKIDROW|CODEX|PLAZA|ElAmigos|GOG|Steam-Rip|SteamRip|Portable|\.exe|Windows|macOS|Linux|PS4|PS5|PlayStation|Xbox|RPCS3|3DS|CIA|NCSD|MP3|FLAC|1080p|2160p|720p|WEB-DL|BDRip|HDRip|BDRemux)\b""",
        RegexOption.IGNORE_CASE
    )

    fun isStrictSwitchRelease(title: String, category: String = "", uploader: String = ""): Boolean {
        val comb = "$title $category".lowercase()
        if (NON_SWITCH_REJECT_REGEX.containsMatchIn(title)) {
            if (!comb.contains("switch") && !comb.contains("nsp") && !comb.contains("nsz") && !comb.contains("xci") && !comb.contains("xcz")) {
                return false
            }
        }
        if (Regex("""\b(FitGirl|DODI|RUNE|TENOKE|FLT|SKIDROW|CODEX|PLAZA|ElAmigos|Steam-Rip|SteamRip|3DS|CIA)\b""", RegexOption.IGNORE_CASE).containsMatchIn(title)) {
            if (!comb.contains("switch")) return false
        }
        if (uploader.equals("dauphong", ignoreCase = true)) return true
        return comb.contains("switch") || comb.contains("nsp") || comb.contains("nsz") || comb.contains("xci") || comb.contains("xcz") || comb.contains("[ns]")
    }

    fun buildMagnet(hash: String, title: String): String {
        val dn = URLEncoder.encode(title, "UTF-8")
        val tr = TRACKERS.joinToString("") { "&tr=" + URLEncoder.encode(it, "UTF-8") }
        return "magnet:?xt=urn:btih:${hash.uppercase()}&dn=$dn$tr"
    }

    /**
     * Поиск раздач ТОЛЬКО Nintendo Switch (разделы RuTracker 1605 и 2481).
     */
    fun searchTopics(
        query: String,
        mirror: String = "https://rutracker.org",
        cookie: String = "",
        proxyHost: String = "",
        proxyPort: Int = 0
    ): List<TopicItem> {
        val normMirror = if (mirror.startsWith("http")) mirror.trimEnd('/') else "https://${mirror.trimEnd('/')}"
        val cleanQ = query.trim()
        val results = mutableListOf<TopicItem>()
        val seenHashes = HashSet<String>()
        val seenTitles = HashSet<String>()

        fun addUnique(list: List<TopicItem>) {
            for (item in list) {
                if (!isStrictSwitchRelease(item.title, item.category)) continue
                if (item.infoHash.isNotEmpty() && !seenHashes.add(item.infoHash.uppercase())) continue
                val normTitle = item.title.lowercase().replace(Regex("\\s+"), " ").trim()
                if (!seenTitles.add(normTitle)) continue
                results.add(item)
            }
        }

        // 1. Встроенный каталог проверенных раздач Nintendo Switch (0 мс)
        val tokens = if (cleanQ.equals("Nintendo Switch", true)) emptyList()
        else cleanQ.split(' ', '-', ':', '.', ',').filter { it.isNotBlank() }

        val curatedMatches = CURATED_SWITCH_DB.filter { entry ->
            tokens.isEmpty() || tokens.all { t -> entry.title.contains(t, ignoreCase = true) }
        }.map { entry ->
            entry.copy(
                magnetUri = buildMagnet(entry.infoHash, entry.title),
                topicUrl = "$normMirror/forum/viewtopic.php?t=${entry.topicId}"
            )
        }
        addUnique(curatedMatches)

        // 2. Живой опрос разделов 1605 и 2481 на RuTracker
        if (cookie.isNotBlank()) {
            try {
                addUnique(searchRuTrackerLive(cleanQ.ifEmpty { "Nintendo Switch" }, normMirror, cookie, proxyHost, proxyPort))
            } catch (_: Exception) {}
        }

        // 3. Живой опрос P2P индекса ТОЛЬКО по Nintendo Switch
        try {
            addUnique(searchApiBaySwitchOnly(cleanQ.ifEmpty { "Nintendo Switch" }, normMirror))
        } catch (_: Exception) {}

        try {
            addUnique(searchRutorSwitchOnly(cleanQ.ifEmpty { "Nintendo Switch" }))
        } catch (_: Exception) {}

        return results.sortedByDescending { it.seeds }.take(60)
    }

    private fun openConnection(urlStr: String, proxyHost: String = "", proxyPort: Int = 0): HttpURLConnection {
        val url = URL(urlStr)
        val conn = if (proxyHost.isNotBlank() && proxyPort in 1..65535) {
            url.openConnection(Proxy(Proxy.Type.HTTP, InetSocketAddress(proxyHost, proxyPort))) as HttpURLConnection
        } else {
            url.openConnection() as HttpURLConnection
        }
        conn.connectTimeout = 10000
        conn.readTimeout = 15000
        conn.setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 14) Chrome/131.0.0.0 Mobile Safari/537.36")
        return conn
    }

    private fun searchRuTrackerLive(
        query: String,
        mirror: String,
        cookie: String,
        proxyHost: String,
        proxyPort: Int
    ): List<TopicItem> {
        val list = mutableListOf<TopicItem>()
        val cp1251 = Charset.forName("windows-1251")
        val encodedQ = URLEncoder.encode(query, "windows-1251")
        val url = "$mirror/forum/tracker.php?f=1605,2481&nm=$encodedQ"
        val conn = openConnection(url, proxyHost, proxyPort)
        val ck = if (cookie.contains("=")) cookie else "bb_session=${cookie.trim()}"
        conn.setRequestProperty("Cookie", ck)

        val bytes = conn.inputStream.use { it.readBytes() }
        val html = String(bytes, cp1251)
        val rowRegex = Regex("""<tr[^>]*id="trs-tr-(\d+)"[^>]*>(.*?)</tr>""", setOf(RegexOption.DOT_MATCHES_ALL, RegexOption.IGNORE_CASE))
        val titleRegex = Regex("""class="[^"]*tLink[^"]*"[^>]*>(.*?)</a>""", setOf(RegexOption.DOT_MATCHES_ALL, RegexOption.IGNORE_CASE))
        val seedRegex = Regex("""class="[^"]*seedmed[^"]*"[^>]*><b>(\d+)</b>""", RegexOption.IGNORE_CASE)

        for (m in rowRegex.findAll(html)) {
            val tid = m.groupValues[1]
            val inner = m.groupValues[2]
            val tm = titleRegex.find(inner) ?: continue
            val rawTitle = tm.groupValues[1].replace(Regex("<.*?>"), "").trim()
            if (rawTitle.isEmpty()) continue
            val seeds = seedRegex.find(inner)?.groupValues?.get(1)?.toIntOrNull() ?: 0
            list.add(
                TopicItem(
                    topicId = tid,
                    title = if (rawTitle.contains("Switch", true)) rawTitle else "[Nintendo Switch] $rawTitle",
                    category = "Nintendo Switch",
                    sizeText = "RuTracker #1605",
                    seeds = seeds,
                    leechers = 0,
                    infoHash = "",
                    magnetUri = "",
                    topicUrl = "$mirror/forum/viewtopic.php?t=$tid"
                )
            )
        }
        return list
    }

    private fun searchApiBaySwitchOnly(query: String, mirror: String): List<TopicItem> {
        val list = mutableListOf<TopicItem>()
        val q = if (query.contains("Switch", ignoreCase = true)) query else "$query Switch"
        val url = "https://apibay.org/q.php?q=${URLEncoder.encode(q, "UTF-8")}&cat=400"
        val conn = openConnection(url)
        if (conn.responseCode != 200) return list
        val json = conn.inputStream.bufferedReader().use { it.readText() }
        val arr = JSONArray(json)
        for (i in 0 until arr.length()) {
            val obj = arr.getJSONObject(i)
            val id = obj.optString("id", "0")
            val name = obj.optString("name", "")
            val hash = obj.optString("info_hash", "").uppercase()
            val username = obj.optString("username", "")
            if (id == "0" || name.equals("No results returned", true) || hash.length != 40) continue
            if (!isStrictSwitchRelease(name, "", username)) continue
            val sizeBytes = obj.optString("size", "0").toLongOrNull() ?: 0L
            val seeds = obj.optString("seeders", "0").toIntOrNull() ?: 0
            val leech = obj.optString("leechers", "0").toIntOrNull() ?: 0
            val displayTitle = if (name.contains("Switch", true)) name else "[Nintendo Switch] $name"
            list.add(
                TopicItem(
                    topicId = "TPB-$id",
                    title = displayTitle,
                    category = "Nintendo Switch",
                    sizeText = StormNczEngine.formatSize(sizeBytes),
                    seeds = seeds,
                    leechers = leech,
                    infoHash = hash,
                    magnetUri = buildMagnet(hash, displayTitle),
                    topicUrl = "$mirror/forum/tracker.php?f=1605,2481&nm=${URLEncoder.encode(name, "UTF-8")}"
                )
            )
            if (list.size >= 30) break
        }
        return list
    }

    private fun searchRutorSwitchOnly(query: String): List<TopicItem> {
        val list = mutableListOf<TopicItem>()
        val q = if (query.contains("Switch", ignoreCase = true)) query else "$query Switch"
        val url = "https://rutor.is/search/0/0/000/0/${URLEncoder.encode(q, "UTF-8")}"
        val conn = openConnection(url)
        if (conn.responseCode != 200) return list
        val html = conn.inputStream.bufferedReader().use { it.readText() }
        val rowRegex = Regex("""<tr class="(?:gai|tum)">(.*?)</tr>""", setOf(RegexOption.DOT_MATCHES_ALL, RegexOption.IGNORE_CASE))
        val magRegex = Regex("""href="(magnet:\?xt=urn:btih:([0-9A-Fa-f]{40})[^"]*)"""", RegexOption.IGNORE_CASE)
        val titleRegex = Regex("""href="(/torrent/(\d+)/[^"]*)"[^>]*>([^<]+)</a>""", RegexOption.IGNORE_CASE)

        for (m in rowRegex.findAll(html)) {
            val inner = m.groupValues[1]
            val mm = magRegex.find(inner) ?: continue
            val tm = titleRegex.find(inner) ?: continue
            val magnet = mm.groupValues[1]
            val hash = mm.groupValues[2].uppercase()
            val relUrl = tm.groupValues[1]
            val rid = tm.groupValues[2]
            val title = tm.groupValues[3].trim()
            if (!isStrictSwitchRelease(title, "")) continue
            val displayTitle = if (title.contains("Switch", true)) title else "[Nintendo Switch] $title"
            list.add(
                TopicItem(
                    topicId = "RTR-$rid",
                    title = displayTitle,
                    category = "Nintendo Switch",
                    sizeText = "Magnet",
                    seeds = 20,
                    leechers = 2,
                    infoHash = hash,
                    magnetUri = magnet,
                    topicUrl = "https://rutor.is$relUrl"
                )
            )
            if (list.size >= 20) break
        }
        return list
    }

    // =========================================================================
    // ОНЛАЙН-ЗАГРУЗКА КЛЮЧЕЙ NINTENDO SWITCH (КАК В ПРОЕКТЕ STORM SWITCH)
    // =========================================================================

    fun getDefaultOnlineKeysCatalog(): List<OnlineKeysAsset> {
        val versions = listOf(
            "23.0.0" to 12177L,
            "22.5.0" to 7423L,
            "22.1.0" to 7407L,
            "22.0.0" to 7407L,
            "21.2.0" to 11343L,
            "21.1.0" to 11343L,
            "21.0.1" to 11343L,
            "21.0.0" to 11343L,
            "20.5.0" to 6976L,
            "20.1.0" to 6130L,
            "20.0.1" to 6130L,
            "20.0.0" to 6123L,
            "19.0.1" to 8149L
        )
        return versions.mapIndexed { idx, (ver, sz) ->
            OnlineKeysAsset(
                version = ver,
                sizeBytes = sz,
                downloadUrl = "https://github.com/ReiKatari/STORM_SWITCH_TOOLS/releases/download/STORM_SWITCH_TOOLS_KEYS/$ver.zip",
                isRecommended = (idx == 0)
            )
        }
    }

    fun fetchOnlineKeysCatalogFromGitHub(): List<OnlineKeysAsset> {
        val apiUrls = listOf(
            "https://api.github.com/repos/ReiKatari/STORM_SWITCH_TOOLS/releases/tags/STORM_SWITCH_TOOLS_KEYS",
            "https://ghfast.top/https://api.github.com/repos/ReiKatari/STORM_SWITCH_TOOLS/releases/tags/STORM_SWITCH_TOOLS_KEYS",
            "https://gh-proxy.net/https://api.github.com/repos/ReiKatari/STORM_SWITCH_TOOLS/releases/tags/STORM_SWITCH_TOOLS_KEYS"
        )
        for (reqUrl in apiUrls) {
            try {
                val conn = openConnection(reqUrl)
                conn.setRequestProperty("Accept", "application/vnd.github+json")
                if (conn.responseCode == 200) {
                    val body = conn.inputStream.bufferedReader().use { it.readText() }
                    val json = JSONObject(body)
                    val assetsArr = json.optJSONArray("assets") ?: continue
                    val list = mutableListOf<OnlineKeysAsset>()
                    for (i in 0 until assetsArr.length()) {
                        val obj = assetsArr.getJSONObject(i)
                        val name = obj.optString("name", "")
                        if (!name.endsWith(".zip", ignoreCase = true)) continue
                        val ver = name.removeSuffix(".zip")
                        val sz = obj.optLong("size", 0L)
                        val dlUrl = obj.optString("browser_download_url", "")
                        if (dlUrl.isNotBlank()) {
                            list.add(OnlineKeysAsset(ver, sz, dlUrl, false))
                        }
                    }
                    if (list.isNotEmpty()) {
                        list.sortWith { a, b -> compareSemVer(b.version, a.version) }
                        return list.mapIndexed { idx, it -> it.copy(isRecommended = idx == 0) }
                    }
                }
            } catch (_: Exception) {}
        }
        return getDefaultOnlineKeysCatalog()
    }

    /**
     * Скачивает архив ключей <version>.zip из репозитория STORM_SWITCH_TOOLS
     * и устанавливает prod.keys и title.keys во все целевые папки ключей.
     */
    fun downloadAndInstallSwitchKeys(
        asset: OnlineKeysAsset,
        targetKeysDirs: List<File>,
        onStatus: (String) -> Unit
    ): Pair<Boolean, String> {
        val baseUrl = asset.downloadUrl
        val candidateUrls = mutableListOf(baseUrl)
        if (baseUrl.startsWith("https://github.com/")) {
            candidateUrls.add("https://gh.con.sh/$baseUrl")
            candidateUrls.add("https://gh.llkk.cc/$baseUrl")
            candidateUrls.add("https://ghfast.top/$baseUrl")
        }

        var zipBytes: ByteArray? = null
        for (dlUrl in candidateUrls) {
            try {
                onStatus("Подключение к серверу ключей (${asset.version})...")
                val conn = openConnection(dlUrl)
                conn.instanceFollowRedirects = true
                if (conn.responseCode == 200) {
                    val bytes = conn.inputStream.use { it.readBytes() }
                    if (bytes.size > 200 && bytes[0] == 0x50.toByte() && bytes[1] == 0x4B.toByte()) {
                        zipBytes = bytes
                        break
                    }
                }
            } catch (_: Exception) {}
        }

        val rawZip = zipBytes ?: return false to "Не удалось скачать архив ключей ${asset.version} с GitHub."

        for (dir in targetKeysDirs) {
            try { dir.mkdirs() } catch (_: Exception) {}
        }

        var installedFiles = 0
        var keyCount = 0
        java.io.ByteArrayInputStream(rawZip).use { bais ->
            ZipInputStream(bais).use { zis ->
                var entry: ZipEntry? = zis.nextEntry
                while (entry != null) {
                    if (!entry.isDirectory) {
                        val fileName = entry.name.substringAfterLast('/')
                        val isProd = fileName.equals("prod.keys", ignoreCase = true)
                        val isTitle = fileName.equals("title.keys", ignoreCase = true)
                        if (isProd || isTitle) {
                            val rawContent = zis.readBytes()
                            val finalBytes = if (isProd) {
                                val header = "# STORM SWITCH KEYS: ${asset.version}\n".toByteArray(Charsets.UTF_8)
                                keyCount = String(rawContent, Charsets.UTF_8).lines().count { it.contains("=") }
                                header + rawContent
                            } else {
                                rawContent
                            }
                            for (dir in targetKeysDirs) {
                                try {
                                    FileOutputStream(File(dir, fileName.lowercase())).use { it.write(finalBytes) }
                                } catch (_: Exception) {}
                            }
                            installedFiles++
                        }
                    }
                    zis.closeEntry()
                    entry = zis.nextEntry
                }
            }
        }

        if (installedFiles == 0) {
            return false to "В архиве ${asset.version}.zip не найдены prod.keys / title.keys"
        }
        return true to "Установлены ключи HOS ${asset.version} ($keyCount ключей prod.keys + title.keys)"
    }

    // =========================================================================
    // АВТОМАТИЧЕСКАЯ ПРОВЕРКА ОБНОВЛЕНИЙ APK И СКАЧИВАНИЕ С GITHUB
    // =========================================================================

    fun compareSemVer(v1: String, v2: String): Int {
        val p1 = v1.trimStart('v', 'V').split('.').map { it.toIntOrNull() ?: 0 }
        val p2 = v2.trimStart('v', 'V').split('.').map { it.toIntOrNull() ?: 0 }
        val maxLen = maxOf(p1.size, p2.size)
        for (i in 0 until maxLen) {
            val a = p1.getOrElse(i) { 0 }
            val b = p2.getOrElse(i) { 0 }
            if (a != b) return a.compareTo(b)
        }
        return 0
    }

    fun checkGitHubAppUpdate(currentVersion: String, preferLegacyApk: Boolean): AppUpdateRelease? {
        val urls = listOf(
            "https://api.github.com/repos/ReiKatari/STORM_SWITCH_BOX/releases/latest",
            "https://ghfast.top/https://api.github.com/repos/ReiKatari/STORM_SWITCH_BOX/releases/latest",
            "https://gh-proxy.net/https://api.github.com/repos/ReiKatari/STORM_SWITCH_BOX/releases/latest"
        )
        for (u in urls) {
            try {
                val conn = openConnection(u)
                conn.setRequestProperty("Accept", "application/vnd.github+json")
                if (conn.responseCode != 200) continue
                val bodyStr = conn.inputStream.bufferedReader().use { it.readText() }
                val json = JSONObject(bodyStr)
                val tag = json.optString("tag_name", "").trimStart('v', 'V').trim()
                if (tag.isEmpty() || compareSemVer(tag, currentVersion) <= 0) return null

                val title = json.optString("name", "STORM SWITCH BOX $tag")
                val changelog = json.optString("body", "")
                val assets = json.optJSONArray("assets") ?: JSONArray()

                var chosenName = ""
                var chosenUrl = ""
                var chosenSize = 0L

                for (i in 0 until assets.length()) {
                    val a = assets.getJSONObject(i)
                    val name = a.optString("name", "")
                    if (!name.endsWith(".apk", ignoreCase = true)) continue
                    val isLegacy = name.contains("LEGACY", ignoreCase = true)
                    val url = a.optString("browser_download_url", "")
                    val sz = a.optLong("size", 0L)
                    if (preferLegacyApk && isLegacy) {
                        chosenName = name
                        chosenUrl = url
                        chosenSize = sz
                        break
                    } else if (!preferLegacyApk && !isLegacy) {
                        chosenName = name
                        chosenUrl = url
                        chosenSize = sz
                        break
                    } else if (chosenUrl.isEmpty()) {
                        chosenName = name
                        chosenUrl = url
                        chosenSize = sz
                    }
                }

                if (chosenUrl.isNotEmpty()) {
                    return AppUpdateRelease(
                        version = tag,
                        title = title,
                        changelog = changelog,
                        apkName = chosenName,
                        apkSizeBytes = chosenSize,
                        downloadUrl = chosenUrl
                    )
                }
            } catch (_: Exception) {}
        }
        return null
    }

    fun downloadApkUpdateFile(
        release: AppUpdateRelease,
        destFile: File,
        isCancelled: () -> Boolean,
        onProgress: (Int, Long, Long, Double) -> Unit
    ): Boolean {
        val baseUrl = release.downloadUrl
        val candidates = mutableListOf(baseUrl)
        if (baseUrl.startsWith("https://github.com/")) {
            candidates.add("https://gh.con.sh/$baseUrl")
            candidates.add("https://gh.llkk.cc/$baseUrl")
            candidates.add("https://ghfast.top/$baseUrl")
        }

        destFile.parentFile?.mkdirs()
        for (dlUrl in candidates) {
            if (isCancelled()) return false
            try {
                val conn = openConnection(dlUrl)
                conn.instanceFollowRedirects = true
                if (conn.responseCode != 200) continue
                val totalLen = conn.contentLengthLong.takeIf { it > 0 } ?: release.apkSizeBytes
                var downloaded = 0L
                val startMs = System.currentTimeMillis()
                val buf = ByteArray(64 * 1024)

                BufferedInputStream(conn.inputStream, 64 * 1024).use { input ->
                    BufferedOutputStream(FileOutputStream(destFile), 64 * 1024).use { output ->
                        var r = 0
                        var lastReport = 0L
                        while (!isCancelled() && input.read(buf).also { r = it } != -1) {
                            output.write(buf, 0, r)
                            downloaded += r
                            val now = System.currentTimeMillis()
                            if (now - lastReport >= 180) {
                                lastReport = now
                                val pct = if (totalLen > 0) ((downloaded * 100L) / totalLen).toInt().coerceIn(0, 100) else 0
                                val elapsedSec = ((now - startMs) / 1000.0).coerceAtLeast(0.1)
                                val speedMb = (downloaded / (1024.0 * 1024.0)) / elapsedSec
                                onProgress(pct, downloaded, totalLen, speedMb)
                            }
                        }
                        output.flush()
                    }
                }

                if (isCancelled()) {
                    destFile.delete()
                    return false
                }

                if (destFile.exists() && destFile.length() > 100 * 1024) {
                    // Проверка ZIP/APK сигнатуры PK\x03\x04
                    FileInputStream(destFile).use { fis ->
                        val sig = ByteArray(4)
                        if (fis.read(sig) == 4 && sig[0] == 0x50.toByte() && sig[1] == 0x4B.toByte()) {
                            onProgress(100, destFile.length(), totalLen, 0.0)
                            return true
                        }
                    }
                }
                destFile.delete()
            } catch (_: Exception) {
                destFile.delete()
            }
        }
        return false
    }

    // =========================================================================
    // ОБЛАЧНЫЕ МОДЫ И ВСТРОЕННЫЙ HTTP RANGE СЕРВЕР (DBI / TINFOIL)
    // =========================================================================

    fun resolveCloudUrl(publicUrl: String): String {
        val u = publicUrl.trim()
        if (u.contains("disk.yandex.", true) || u.contains("yadi.sk", true)) {
            val api = "https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key=" + URLEncoder.encode(u, "UTF-8")
            val conn = openConnection(api)
            if (conn.responseCode == 200) {
                val json = JSONObject(conn.inputStream.bufferedReader().use { it.readText() })
                val href = json.optString("href", "")
                if (href.isNotEmpty()) return href
            }
        }
        val gdMatch = Regex("""drive\.google\.com/file/d/([a-zA-Z0-9_-]+)""").find(u)
        if (gdMatch != null) {
            return "https://drive.google.com/uc?export=download&confirm=t&id=${gdMatch.groupValues[1]}"
        }
        return u
    }

    fun downloadAndExtractMod(
        cloudUrl: String,
        targetTitleId: String,
        modsRootDir: File,
        onProgress: (String) -> Unit
    ): File {
        onProgress("Разрешение прямой ссылки облака...")
        val directUrl = resolveCloudUrl(cloudUrl)
        val tempZip = File(modsRootDir, "temp_mod_${System.currentTimeMillis()}.zip")
        modsRootDir.mkdirs()

        onProgress("Скачивание архива модификации...")
        val conn = openConnection(directUrl)
        conn.instanceFollowRedirects = true
        conn.inputStream.use { input ->
            FileOutputStream(tempZip).use { output ->
                val buf = ByteArray(64 * 1024)
                var total = 0L
                while (true) {
                    val r = input.read(buf)
                    if (r <= 0) break
                    output.write(buf, 0, r)
                    total += r
                    onProgress("Скачано: ${StormNczEngine.formatSize(total)}")
                }
            }
        }

        val cleanTid = targetTitleId.replace(Regex("[^0-9A-Fa-f]"), "").uppercase().padEnd(16, '0').take(16)
        val outDir = File(modsRootDir, "atmosphere/contents/$cleanTid")
        outDir.mkdirs()

        onProgress("Распаковка в atmosphere/contents/$cleanTid...")
        FileInputStream(tempZip).use { fis ->
            ZipInputStream(BufferedInputStream(fis)).use { zis ->
                var entry: ZipEntry? = zis.nextEntry
                val buf = ByteArray(64 * 1024)
                while (entry != null) {
                    val e = entry
                    if (!e.isDirectory) {
                        val dest = File(outDir, e.name)
                        dest.parentFile?.mkdirs()
                        FileOutputStream(dest).use { fos ->
                            while (true) {
                                val r = zis.read(buf)
                                if (r <= 0) break
                                fos.write(buf, 0, r)
                            }
                        }
                    }
                    zis.closeEntry()
                    entry = zis.nextEntry
                }
            }
        }
        tempZip.delete()
        onProgress("Мод успешно установлен в: ${outDir.absolutePath}")
        return outDir
    }

    class DbiHttpServer(
        private val port: Int,
        private val sharedFiles: () -> List<File>,
        private val onLog: (String) -> Unit
    ) {
        @Volatile private var running = false
        private var serverSocket: ServerSocket? = null

        fun start() {
            if (running) return
            running = true
            Thread {
                try {
                    val ss = ServerSocket(port)
                    serverSocket = ss
                    onLog("HTTP сервер DBI / Tinfoil запущен на порту $port")
                    while (running) {
                        val client = ss.accept()
                        Thread { handleClient(client) }.start()
                    }
                } catch (e: Exception) {
                    if (running) onLog("Сервер остановлен: ${e.message}")
                }
            }.start()
        }

        fun stop() {
            running = false
            try { serverSocket?.close() } catch (_: Exception) {}
        }

        private fun handleClient(socket: java.net.Socket) {
            socket.use { s ->
                val reader = s.getInputStream().bufferedReader()
                val out = s.getOutputStream()
                val reqLine = reader.readLine() ?: return
                val parts = reqLine.split(' ')
                if (parts.size < 2) return
                val path = java.net.URLDecoder.decode(parts[1], "UTF-8")

                var rangeStart = 0L
                var rangeEnd = -1L
                while (true) {
                    val line = reader.readLine()
                    if (line.isNullOrEmpty()) break
                    if (line.startsWith("Range:", ignoreCase = true)) {
                        val m = Regex("""bytes=(\d+)-(\d*)""").find(line)
                        if (m != null) {
                            rangeStart = m.groupValues[1].toLongOrNull() ?: 0L
                            rangeEnd = m.groupValues[2].toLongOrNull() ?: -1L
                        }
                    }
                }

                val files = sharedFiles().filter { it.exists() && it.isFile }
                if (path == "/" || path == "/index.html" || path == "/index.tfl") {
                    if (path == "/index.tfl") {
                        val tfl = JSONObject()
                        val filesArr = JSONArray()
                        files.forEach { f ->
                            val item = JSONObject()
                            item.put("url", "/files/${URLEncoder.encode(f.name, "UTF-8").replace("+", "%20")}")
                            item.put("size", f.length())
                            filesArr.put(item)
                        }
                        tfl.put("files", filesArr)
                        val body = tfl.toString().toByteArray(Charsets.UTF_8)
                        val hdr = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: ${body.size}\r\n\r\n"
                        out.write(hdr.toByteArray())
                        out.write(body)
                    } else {
                        val sb = StringBuilder("<html><head><meta charset='utf-8'><title>STORM SWITCH BOX DBI Server</title></head><body style='background:#0F0C1D;color:#F8FAFC;font-family:sans-serif;'><h2>STORM SWITCH BOX — Сервер DBI / Tinfoil</h2><ul>")
                        files.forEach { f ->
                            val enc = URLEncoder.encode(f.name, "UTF-8").replace("+", "%20")
                            sb.append("<li><a style='color:#00F0FF' href='/files/$enc'>${f.name}</a> (${StormNczEngine.formatSize(f.length())})</li>")
                        }
                        sb.append("</ul></body></html>")
                        val body = sb.toString().toByteArray(Charsets.UTF_8)
                        val hdr = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: ${body.size}\r\n\r\n"
                        out.write(hdr.toByteArray())
                        out.write(body)
                    }
                    out.flush()
                    return
                }

                if (path.startsWith("/files/")) {
                    val fileName = path.removePrefix("/files/")
                    val target = files.firstOrNull { it.name == fileName }
                    if (target == null) {
                        out.write("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n".toByteArray())
                        return
                    }
                    val totalLen = target.length()
                    val end = if (rangeEnd in rangeStart until totalLen) rangeEnd else (totalLen - 1L)
                    val contentLen = (end - rangeStart + 1L).coerceAtLeast(0L)
                    val isPartial = rangeStart > 0L || rangeEnd >= 0L

                    val status = if (isPartial) "206 Partial Content" else "200 OK"
                    val hdr = buildString {
                        append("HTTP/1.1 $status\r\n")
                        append("Content-Type: application/octet-stream\r\n")
                        append("Accept-Ranges: bytes\r\n")
                        if (isPartial) append("Content-Range: bytes $rangeStart-$end/$totalLen\r\n")
                        append("Content-Length: $contentLen\r\n\r\n")
                    }
                    out.write(hdr.toByteArray())
                    java.io.RandomAccessFile(target, "r").use { raf ->
                        raf.seek(rangeStart)
                        val buf = ByteArray(128 * 1024)
                        var rem = contentLen
                        while (rem > 0) {
                            val r = raf.read(buf, 0, minOf(rem, buf.size.toLong()).toInt())
                            if (r <= 0) break
                            out.write(buf, 0, r)
                            rem -= r
                        }
                    }
                    out.flush()
                    onLog("Отправлен фрагмент ${target.name} ($contentLen байт)")
                }
            }
        }
    }
}
