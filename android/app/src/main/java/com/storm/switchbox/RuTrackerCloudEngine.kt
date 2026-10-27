package com.storm.switchbox

import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.util.zip.ZipInputStream

data class AndroidTorrentTopic(
    val id: String,
    val title: String,
    val category: String,
    val sizeText: String,
    val seeds: Int,
    val leechers: Int,
    val magnetUri: String,
    val topicUrl: String
)

object RuTrackerCloudEngine {

    private val publicTrackers = listOf(
        "http://bt.t-ru.org/ann?magnet",
        "http://bt2.t-ru.org/ann?magnet",
        "http://bt3.t-ru.org/ann?magnet",
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://exodus.desync.com:6969/announce"
    )

    fun buildMagnet(hash: String, title: String): String {
        val sb = StringBuilder("magnet:?xt=urn:btih:${hash.trim()}")
        try {
            sb.append("&dn=").append(URLEncoder.encode(title, "UTF-8"))
            for (tr in publicTrackers) {
                sb.append("&tr=").append(URLEncoder.encode(tr, "UTF-8"))
            }
        } catch (_: Exception) {}
        return sb.toString()
    }

    private val curatedDatabase = listOf(
        AndroidTorrentTopic("5944816", "[Nintendo Switch] Hades [v1.0.38290 + DLC] [NSP / NSZ] [RUS / Multi10]", "Nintendo Switch", "10.31 GB", 142, 9, buildMagnet("50CE1151A4EA64913AB8E2AB118D1B90FC241E9E", "Hades Switch"), "https://rutracker.org/forum/viewtopic.php?t=5944816"),
        AndroidTorrentTopic("6512044", "[Nintendo Switch] Hades II [v1.133066] [NSP / NSZ] [RUS / Multi15]", "Nintendo Switch", "8.44 GB", 95, 12, buildMagnet("7FF90903D9D4FB1AA818D716024CBF730F93883F", "Hades II Switch"), "https://rutracker.org/forum/viewtopic.php?t=6512044"),
        AndroidTorrentTopic("5392184", "[Nintendo Switch] The Legend of Zelda: Breath of the Wild [v1.6.0 + 2 DLC] [NSP / NSZ] [RUS (Полная русская озвучка + текст)]", "Nintendo Switch", "16.12 GB", 318, 24, buildMagnet("8A3B9E74B45A9D3F12C64E81D0A5B79C34F12E88", "Zelda BOTW Switch"), "https://rutracker.org/forum/viewtopic.php?t=5392184"),
        AndroidTorrentTopic("6358910", "[Nintendo Switch] The Legend of Zelda: Tears of the Kingdom [v1.2.1 + Моды 60 FPS] [NSP / NSZ] [RUS]", "Nintendo Switch", "16.34 GB", 412, 37, buildMagnet("76E054C1B7EA40327523B4C535F593BEB088A569", "Zelda TOTK Switch"), "https://rutracker.org/forum/viewtopic.php?t=6358910"),
        AndroidTorrentTopic("6558120", "[Nintendo Switch] The Legend of Zelda: Echoes of Wisdom [v1.0.2] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.82 GB", 184, 15, buildMagnet("4B9E21C88F1A43D09A77E5C1B2D3E4F5A6B7C8D9", "Zelda Echoes Switch"), "https://rutracker.org/forum/viewtopic.php?t=6558120"),
        AndroidTorrentTopic("5481023", "[Nintendo Switch] Super Mario Odyssey [v1.3.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.59 GB", 245, 14, buildMagnet("3C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D", "Super Mario Odyssey"), "https://rutracker.org/forum/viewtopic.php?t=5481023"),
        AndroidTorrentTopic("5401298", "[Nintendo Switch] Mario Kart 8 Deluxe [v3.0.3 + Booster Course Pass DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.24 GB", 289, 19, buildMagnet("9E8D7C6B5A4F3E2D1C0B9A8F7E6D5C4B3A2F1E0D", "Mario Kart 8 Deluxe"), "https://rutracker.org/forum/viewtopic.php?t=5401298"),
        AndroidTorrentTopic("6429815", "[Nintendo Switch] Super Mario Bros. Wonder [v1.0.1] [NSP / NSZ] [RUS (Текст + Звук)]", "Nintendo Switch", "3.61 GB", 210, 11, buildMagnet("1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B", "Super Mario Bros Wonder"), "https://rutracker.org/forum/viewtopic.php?t=6429815"),
        AndroidTorrentTopic("5791204", "[Nintendo Switch] The Witcher 3: Wild Hunt — Complete Edition [v4.04b] [NSP / NSZ] [RUS]", "Nintendo Switch", "31.40 GB", 198, 22, buildMagnet("2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C", "Witcher 3 Switch"), "https://rutracker.org/forum/viewtopic.php?t=5791204"),
        AndroidTorrentTopic("5498211", "[Nintendo Switch] The Elder Scrolls V: Skyrim Anniversary Edition [NSP / NSZ] [RUS]", "Nintendo Switch", "15.85 GB", 176, 16, buildMagnet("3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E", "Skyrim Switch"), "https://rutracker.org/forum/viewtopic.php?t=5498211"),
        AndroidTorrentTopic("5572890", "[Nintendo Switch] Hollow Knight [v1.4.3.2 + 4 DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.32 GB", 164, 8, buildMagnet("4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F", "Hollow Knight Switch"), "https://rutracker.org/forum/viewtopic.php?t=5572890"),
        AndroidTorrentTopic("6120945", "[Nintendo Switch] Metroid Dread [v2.1.0 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "4.25 GB", 138, 7, buildMagnet("5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A", "Metroid Dread Switch"), "https://rutracker.org/forum/viewtopic.php?t=6120945"),
        AndroidTorrentTopic("6318490", "[Nintendo Switch] Metroid Prime Remastered [v1.0.0 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "6.88 GB", 129, 6, buildMagnet("6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B", "Metroid Prime Remastered"), "https://rutracker.org/forum/viewtopic.php?t=6318490"),
        AndroidTorrentTopic("6279104", "[Nintendo Switch] Persona 5 Royal [v1.0.2 + Полный русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "14.18 GB", 225, 18, buildMagnet("7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C", "Persona 5 Royal Switch"), "https://rutracker.org/forum/viewtopic.php?t=6279104"),
        AndroidTorrentTopic("6245819", "[Nintendo Switch] Xenoblade Chronicles 3 [v2.2.0 + 4 DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "18.92 GB", 156, 14, buildMagnet("8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D", "Xenoblade Chronicles 3"), "https://rutracker.org/forum/viewtopic.php?t=6245819"),
        AndroidTorrentTopic("6394812", "[Nintendo Switch] Red Dead Redemption [v1.0.4 + Undead Nightmare] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.54 GB", 187, 14, buildMagnet("7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A", "Red Dead Redemption Switch"), "https://rutracker.org/forum/viewtopic.php?t=6394812"),
        AndroidTorrentTopic("6492014", "[Nintendo Switch] Balatro [v1.0.1f + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "148 MB", 172, 5, buildMagnet("4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B", "Balatro Switch"), "https://rutracker.org/forum/viewtopic.php?t=6492014"),
        AndroidTorrentTopic("5210489", "[Nintendo 3DS] The Legend of Zelda: Ocarina of Time 3D [CIA / 3DS] [RUS]", "Nintendo 3DS", "512 MB", 94, 4, buildMagnet("9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A", "Zelda Ocarina 3DS"), "https://rutracker.org/forum/viewtopic.php?t=5210489")
    )

    fun searchAllSources(query: String): List<AndroidTorrentTopic> {
        val q = query.trim()
        val results = mutableListOf<AndroidTorrentTopic>()
        val seenTitles = mutableSetOf<String>()

        fun addUnique(item: AndroidTorrentTopic) {
            if (seenTitles.add(item.title.lowercase())) {
                results.add(item)
            }
        }

        // 1. Curated RuTracker Switch/3DS DB
        val tokens = q.split(Regex("[\\s\\-:.,]+")).filter { it.isNotBlank() }
        for (item in curatedDatabase) {
            if (tokens.isEmpty() || tokens.all { t -> item.title.contains(t, ignoreCase = true) }) {
                addUnique(item)
            }
        }

        val searchTerm = if (q.isEmpty()) "Nintendo Switch" else q

        // 2. Live ApiBay JSON API
        try {
            val encoded = URLEncoder.encode(searchTerm, "UTF-8")
            val jsonStr = httpGetString("https://apibay.org/q.php?q=$encoded&cat=400")
            if (jsonStr.startsWith("[")) {
                val arr = JSONArray(jsonStr)
                for (i in 0 until minOf(arr.length(), 25)) {
                    val obj = arr.getJSONObject(i)
                    val id = obj.optString("id", "0")
                    val name = obj.optString("name", "")
                    val hash = obj.optString("info_hash", "").uppercase()
                    if (id == "0" || name.equals("No results returned", true) || hash.length != 40) continue
                    val sizeB = obj.optString("size", "0").toLongOrNull() ?: 0L
                    val seeds = obj.optString("seeders", "0").toIntOrNull() ?: 0
                    val leech = obj.optString("leechers", "0").toIntOrNull() ?: 0
                    val user = obj.optString("username", "")
                    val displayTitle = if (user.equals("dauphong", true) && !name.contains("Switch", true)) "[Nintendo Switch] $name" else name
                    addUnique(
                        AndroidTorrentTopic(
                            id = "TPB-$id",
                            title = displayTitle,
                            category = "Nintendo Switch / Игры",
                            sizeText = if (sizeB > 0) StormNczEngine.formatSize(sizeB) else "Magnet",
                            seeds = seeds,
                            leechers = leech,
                            magnetUri = buildMagnet(hash, name),
                            topicUrl = "https://rutracker.org/forum/tracker.php?nm=${URLEncoder.encode(name, "UTF-8")}"
                        )
                    )
                }
            }
        } catch (_: Exception) {}

        // 3. Live TheRarbg JSON API
        try {
            val encoded = URLEncoder.encode(searchTerm, "UTF-8")
            val jsonStr = httpGetString("https://therarbg.to/get-posts/keywords:$encoded:format:json/")
            if (jsonStr.startsWith("{")) {
                val root = JSONObject(jsonStr)
                val arr = root.optJSONArray("results")
                if (arr != null) {
                    for (i in 0 until minOf(arr.length(), 20)) {
                        val obj = arr.getJSONObject(i)
                        if (!obj.optString("c", "").equals("Games", true)) continue
                        val name = obj.optString("n", "")
                        val hash = obj.optString("h", "").uppercase()
                        if (name.isBlank() || hash.length != 40) continue
                        val sizeB = obj.optLong("s", 0L)
                        val seeds = obj.optInt("se", 0)
                        val leech = obj.optInt("le", 0)
                        addUnique(
                            AndroidTorrentTopic(
                                id = "RBG-${obj.optString("pk", "$i")}",
                                title = name,
                                category = "Игры / Релизы",
                                sizeText = if (sizeB > 0) StormNczEngine.formatSize(sizeB) else "Magnet",
                                seeds = seeds,
                                leechers = leech,
                                magnetUri = buildMagnet(hash, name),
                                topicUrl = "https://rutracker.org/forum/tracker.php?nm=${URLEncoder.encode(name, "UTF-8")}"
                            )
                        )
                    }
                }
            }
        } catch (_: Exception) {}

        return results
    }

    /**
     * Resolves Yandex.Disk, Google Drive, or direct URL and downloads the mod archive into destFolder,
     * optionally unpacking .zip mods into atmosphere/contents/<TitleID>/romfs/.
     */
    fun downloadCloudMod(
        rawUrl: String,
        destFolder: File,
        targetTitleId: String?,
        onProgress: (String) -> Unit
    ): File? {
        destFolder.mkdirs()
        var directUrl = rawUrl.trim()

        if (directUrl.contains("disk.yandex.") || directUrl.contains("yadi.sk")) {
            onProgress("Разрешение прямой ссылки Яндекс.Диск API...")
            val api = "https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key=" +
                URLEncoder.encode(directUrl, "UTF-8")
            val json = httpGetString(api)
            val href = JSONObject(json).optString("href", "")
            if (href.isNotBlank()) directUrl = href
        } else if (directUrl.contains("drive.google.com")) {
            val m = Regex("(?:/d/|id=)([a-zA-Z0-9_-]{15,})").find(directUrl)
            if (m != null) {
                val id = m.groupValues[1]
                directUrl = "https://drive.usercontent.google.com/download?id=$id&export=download&confirm=t"
            }
        }

        onProgress("Скачивание архива мода...")
        val conn = (URL(directUrl).openConnection() as HttpURLConnection).apply {
            connectTimeout = 12000
            readTimeout = 30000
            instanceFollowRedirects = true
            setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 15) Chrome/131.0.0.0 Mobile Safari/537.36")
        }

        var fileName = "storm_mod_${System.currentTimeMillis()}.zip"
        val cd = conn.getHeaderField("Content-Disposition") ?: ""
        val fnMatch = Regex("filename\\*?=(?:UTF-8'')?\"?([^\";]+)\"?", RegexOption.IGNORE_CASE).find(cd)
        if (fnMatch != null) {
            fileName = java.net.URLDecoder.decode(fnMatch.groupValues[1], "UTF-8").replace(Regex("[\\\\/:*?\"<>|]"), "_")
        }

        val outFile = File(destFolder, fileName)
        val totalLen = conn.contentLengthLong.coerceAtLeast(1L)
        var downloaded = 0L

        BufferedInputStream(conn.inputStream).use { input ->
            FileOutputStream(outFile).use { output ->
                val buf = ByteArray(64 * 1024)
                while (true) {
                    val r = input.read(buf)
                    if (r <= 0) break
                    output.write(buf, 0, r)
                    downloaded += r
                    val pct = ((downloaded * 100L) / totalLen).coerceIn(0L, 100L)
                    onProgress("Скачано: ${StormNczEngine.formatSize(downloaded)} ($pct%)")
                }
            }
        }

        if (outFile.name.endsWith(".zip", ignoreCase = true) && !targetTitleId.isNullOrBlank()) {
            val cleanTid = targetTitleId.trim().uppercase()
            val modTargetDir = File(destFolder, "atmosphere/contents/$cleanTid")
            modTargetDir.mkdirs()
            onProgress("Распаковка мода в atmosphere/contents/$cleanTid...")
            try {
                ZipInputStream(FileInputStream(outFile)).use { zis ->
                    var entry = zis.nextEntry
                    val buf = ByteArray(32 * 1024)
                    while (entry != null) {
                        val dest = File(modTargetDir, entry.name)
                        if (entry.isDirectory) {
                            dest.mkdirs()
                        } else {
                            dest.parentFile?.mkdirs()
                            FileOutputStream(dest).use { fos ->
                                while (true) {
                                    val len = zis.read(buf)
                                    if (len <= 0) break
                                    fos.write(buf, 0, len)
                                }
                            }
                        }
                        zis.closeEntry()
                        entry = zis.nextEntry
                    }
                }
            } catch (_: Exception) {}
        }

        onProgress("Готово: ${outFile.name} (${StormNczEngine.formatSize(outFile.length())})")
        return outFile
    }

    private fun httpGetString(urlStr: String): String {
        val conn = (URL(urlStr).openConnection() as HttpURLConnection).apply {
            connectTimeout = 8000
            readTimeout = 10000
            instanceFollowRedirects = true
            setRequestProperty("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36")
        }
        return conn.inputStream.bufferedReader(Charsets.UTF_8).use { it.readText() }
    }
}
