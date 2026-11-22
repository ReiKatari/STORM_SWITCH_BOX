package com.storm.switchbox

import java.io.ByteArrayInputStream
import java.io.File
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.ConcurrentHashMap
import java.util.zip.GZIPInputStream

/**
 * Офлайн-индекс реальных раздач RuTracker, раздел #1605 «Игры для Nintendo Switch».
 *
 * Источник — `assets/rutracker_switch_index.json.gz` (схема v1: номер темы, BTIH, сиды, размер, время регистрации,
 * имя торрента из метаданных, настоящий заголовок темы, название игры, TitleID и РЕАЛЬНЫЙ список файлов раздачи).
 * Индекс загружается один раз в фоне, держит компактные структуры и заранее нормализованные ключи поиска,
 * поэтому поиск по ~7 000 темам занимает единицы миллисекунд. Во время работы индекс дополняется:
 *  • живыми сидами / размером / новыми BTIH из API RuTracker (pvc, кэш 24 ч, см. [RuTrackerCloudEngine]);
 *  • настоящими заголовками новых тем из atom-ленты раздела #1605;
 *  • более свежим индексом из релиза GitHub (кэш в filesDir, только если `v`/`generated` новее, без ошибок офлайн).
 * Никаких выдуманных тем, хэшей, сидов или файлов: если данных нет, поле остаётся пустым.
 */
object RuTrackerSwitchIndex {

    const val ASSET_NAME = "rutracker_switch_index.json.gz"
    const val ASSET_NAME_PLAIN = "rutracker_switch_index.json"
    const val REMOTE_URL = "https://github.com/ReiKatari/STORM_SWITCH_BOX/releases/latest/download/rutracker_switch_index.json.gz"
    private const val CACHED_FILE_NAME = "rutracker_switch_index.json.gz"
    private const val REMOTE_STAMP_NAME = "rutracker_switch_index.remote.stamp"
    private const val REMOTE_CHECK_INTERVAL_MS = 24L * 60L * 60L * 1000L

    class IndexFile(
        val path: String,
        val size: Long,
        val titleId: String,
        val role: String,
        val version: String
    ) {
        val fileName: String get() = path.substringAfterLast('/')
        val extension: String get() = path.substringAfterLast('.', "").lowercase()
    }

    class IndexTopic(
        val topicId: String,
        val infoHash: String,
        val seeds: Int,
        val sizeBytes: Long,
        val regTime: Long,
        val torrentName: String,
        topicTitle: String,
        val gameName: String,
        val titleId: String,
        val files: List<IndexFile>,
        val fromIndex: Boolean
    ) {
        @Volatile var topicTitle: String = topicTitle
            private set
        // Заранее нормализованные ключи поиска
        internal var gameKey: String = ""
        internal var gameKeyPadded: String = ""
        internal var blobPadded: String = ""
        internal var words: Array<String> = emptyArray()
        internal var gameWordCount: Int = 0
        internal var skeleton: String = ""
        internal var tidBlob: String = ""

        internal fun rebuildKeys() {
            gameKey = norm(gameName.ifBlank { topicTitle.ifBlank { torrentName } })
            gameKeyPadded = " $gameKey"
            val blob = StringBuilder(gameKey.length * 4)
            blob.append(gameKey).append(' ').append(norm(topicTitle)).append(' ').append(norm(torrentName))
            for (f in files) blob.append(' ').append(norm(f.fileName.substringBeforeLast('.')))
            val blobStr = blob.toString()
            blobPadded = " $blobStr"
            val gw = gameKey.split(' ').filter { it.isNotEmpty() }
            val rest = blobStr.split(' ').filter { it.isNotEmpty() && it !in gw }
            val all = LinkedHashSet<String>(gw)
            all.addAll(rest)
            words = all.toTypedArray()
            gameWordCount = LinkedHashSet(gw).size
            skeleton = RuTrackerCloudEngine.phoneticSkeleton(gameKey)
            val tids = StringBuilder()
            if (titleId.isNotEmpty()) tids.append(titleId.lowercase()).append(' ')
            for (f in files) if (f.titleId.isNotEmpty()) tids.append(f.titleId.lowercase()).append(' ')
            tidBlob = tids.toString()
        }

        internal fun updateTopicTitle(newTitle: String) {
            if (newTitle.isBlank() || newTitle == topicTitle) return
            topicTitle = newTitle
            rebuildKeys()
        }
    }

    class IndexData(
        val version: Int,
        val generated: Long,
        val topics: List<IndexTopic>,
        val byId: Map<String, IndexTopic>,
        val byHash: Map<String, IndexTopic>,
        val source: String
    )

    /** Открывает поток ассета индекса (задаётся из MainActivity: assets.open(ASSET_NAME)). */
    @Volatile var assetOpener: (() -> InputStream)? = null
    /** Каталог кэша (filesDir) для более свежего индекса из релиза GitHub. */
    @Volatile var cacheDir: File? = null

    @Volatile private var data: IndexData? = null
    @Volatile var lastLoadError: String? = null
        private set
    @Volatile var lastLoadMs: Long = 0L
        private set
    @Volatile private var warmupStarted = false
    private val loadLock = Any()

    // Темы раздела #1605, которых нет в индексе, но настоящий заголовок известен (atom-лента, имя торрента с pvc-хэшем)
    private val extraTopics = ConcurrentHashMap<String, IndexTopic>()
    private val atomTitles = ConcurrentHashMap<String, String>()

    fun isLoaded(): Boolean = data != null
    fun current(): IndexData? = data
    fun count(): Int = data?.topics?.size ?: 0
    fun generatedUnix(): Long = data?.generated ?: 0L

    fun clearLoadedStateForTesting() {
        synchronized(loadLock) {
            data = null
            lastLoadError = null
            lastLoadMs = 0L
            extraTopics.clear()
        }
    }

    // =========================================================================
    // ЗАГРУЗКА
    // =========================================================================

    private data class Header(val version: Int, val generated: Long)
    private val HEADER_VERSION_REGEX = Regex(""""v"\s*:\s*(\d+)""")
    private val HEADER_GENERATED_REGEX = Regex(""""generated"\s*:\s*(\d+)""")

    private fun maybeGunzip(input: InputStream, bufSize: Int = 8192): InputStream {
        val pb = java.io.PushbackInputStream(input.buffered(bufSize), 2)
        val b0 = pb.read()
        val b1 = pb.read()
        if (b1 >= 0) pb.unread(b1)
        if (b0 >= 0) pb.unread(b0)
        return if (b0 == 0x1F && b1 == 0x8B) GZIPInputStream(pb, bufSize) else pb
    }

    private fun readHeader(open: () -> InputStream): Header? = try {
        open().use { raw ->
            maybeGunzip(raw, 8192).use { src ->
                val buf = ByteArray(512)
                var n = 0
                while (n < buf.size) {
                    val r = src.read(buf, n, buf.size - n)
                    if (r <= 0) break
                    n += r
                }
                val head = String(buf, 0, n, Charsets.UTF_8)
                val v = HEADER_VERSION_REGEX.find(head)?.groupValues?.get(1)?.toIntOrNull() ?: return@use null
                val g = HEADER_GENERATED_REGEX.find(head)?.groupValues?.get(1)?.toLongOrNull() ?: 0L
                Header(v, g)
            }
        }
    } catch (_: Throwable) { null }

    private class IndexJsonReader(private val s: String) {
        private var pos = 0
        private val n = s.length

        private fun skipWs() {
            while (pos < n) {
                val c = s[pos]
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t') pos++ else break
            }
        }

        fun peek(): Char {
            skipWs()
            return if (pos < n) s[pos] else '\u0000'
        }

        fun advance() {
            if (pos < n) pos++
        }

        fun expect(c: Char) {
            skipWs()
            if (pos >= n || s[pos] != c) {
                throw IllegalArgumentException("Malformed JSON: expected '$c' at offset $pos")
            }
            pos++
        }

        fun readString(): String {
            expect('"')
            val start = pos
            while (pos < n) {
                val c = s[pos]
                if (c == '"') {
                    val plain = s.substring(start, pos)
                    pos++
                    return plain
                }
                if (c == '\\') break
                pos++
            }
            val sb = StringBuilder(pos - start + 16)
            if (pos > start) sb.append(s, start, pos)
            while (pos < n) {
                val c = s[pos++]
                if (c == '"') return sb.toString()
                if (c == '\\') {
                    if (pos >= n) break
                    when (val esc = s[pos++]) {
                        '"', '\\', '/' -> sb.append(esc)
                        'b' -> sb.append('\b')
                        'f' -> sb.append('\u000C')
                        'n' -> sb.append('\n')
                        'r' -> sb.append('\r')
                        't' -> sb.append('\t')
                        'u' -> {
                            val code = if (pos + 4 <= n) s.substring(pos, pos + 4).toIntOrNull(16) else null
                            if (code != null) {
                                sb.append(Char(code))
                                pos += 4
                            } else {
                                sb.append('u')
                            }
                        }
                        else -> sb.append(esc)
                    }
                } else {
                    sb.append(c)
                }
            }
            throw IllegalArgumentException("Unterminated JSON string")
        }

        private fun skipLiteral(lit: String) {
            skipWs()
            if (!s.regionMatches(pos, lit, 0, lit.length)) {
                throw IllegalArgumentException("Expected '$lit' at offset $pos")
            }
            pos += lit.length
        }

        private fun readNumberToken(): String {
            skipWs()
            val start = pos
            while (pos < n) {
                val c = s[pos]
                if ((c in '0'..'9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') {
                    pos++
                } else {
                    break
                }
            }
            if (pos == start) throw IllegalArgumentException("Expected number at offset $pos")
            return s.substring(start, pos)
        }

        fun readLongOrZero(): Long = when (peek()) {
            'n' -> { skipLiteral("null"); 0L }
            '"' -> readString().trim().toLongOrNull() ?: 0L
            '-', in '0'..'9' -> {
                val tok = readNumberToken()
                tok.toLongOrNull() ?: tok.toDoubleOrNull()?.toLong() ?: 0L
            }
            else -> { skipValue(); 0L }
        }

        fun readIntOrZero(): Int = readLongOrZero().toInt()

        fun readScalarAsString(): String = when (peek()) {
            '"' -> readString()
            'n' -> { skipLiteral("null"); "" }
            't' -> { skipLiteral("true"); "true" }
            'f' -> { skipLiteral("false"); "false" }
            '-', in '0'..'9' -> readNumberToken()
            else -> { skipValue(); "" }
        }

        fun skipValue() {
            when (val c = peek()) {
                '"' -> readString()
                '{' -> {
                    pos++
                    if (peek() == '}') { pos++; return }
                    while (pos < n) {
                        readString()
                        expect(':')
                        skipValue()
                        val sep = peek()
                        if (sep == ',') { pos++; continue }
                        if (sep == '}') { pos++; return }
                        throw IllegalArgumentException("Malformed object at offset $pos")
                    }
                }
                '[' -> {
                    pos++
                    if (peek() == ']') { pos++; return }
                    while (pos < n) {
                        skipValue()
                        val sep = peek()
                        if (sep == ',') { pos++; continue }
                        if (sep == ']') { pos++; return }
                        throw IllegalArgumentException("Malformed array at offset $pos")
                    }
                }
                't' -> skipLiteral("true")
                'f' -> skipLiteral("false")
                'n' -> skipLiteral("null")
                '-', in '0'..'9' -> readNumberToken()
                else -> throw IllegalArgumentException("Unexpected char '$c' at offset $pos")
            }
        }

        fun readIndexFiles(): List<IndexFile> {
            if (peek() != '[') { skipValue(); return emptyList() }
            pos++
            if (peek() == ']') { pos++; return emptyList() }
            val out = ArrayList<IndexFile>()
            while (pos < n) {
                if (peek() == '[') {
                    pos++
                    if (peek() == ']') {
                        pos++
                    } else {
                        var idx = 0
                        var fPath = ""
                        var fSize = 0L
                        var fTid = ""
                        var fRole = ""
                        var fVer = ""
                        while (pos < n) {
                            when (idx) {
                                0 -> fPath = readScalarAsString().replace('\\', '/')
                                1 -> fSize = readLongOrZero()
                                2 -> fTid = readScalarAsString().uppercase()
                                3 -> fRole = readScalarAsString()
                                4 -> fVer = readScalarAsString()
                                else -> skipValue()
                            }
                            idx++
                            val innerSep = peek()
                            if (innerSep == ',') { pos++; continue }
                            if (innerSep == ']') { pos++; break }
                            throw IllegalArgumentException("Malformed file tuple at offset $pos")
                        }
                        if (fPath.isNotEmpty()) out.add(IndexFile(fPath, fSize, fTid, fRole, fVer))
                    }
                } else {
                    skipValue()
                }
                val sep = peek()
                if (sep == ',') { pos++; continue }
                if (sep == ']') { pos++; break }
                throw IllegalArgumentException("Malformed files array at offset $pos")
            }
            return out
        }

        fun readTopicObject(): IndexTopic? {
            if (peek() != '{') { skipValue(); return null }
            pos++
            if (peek() == '}') { pos++; return null }
            var tid = ""
            var hash = ""
            var seeds = 0
            var sizeBytes = 0L
            var regTime = 0L
            var torrentName = ""
            var topicTitle = ""
            var gameName = ""
            var titleId = ""
            var files: List<IndexFile> = emptyList()
            while (pos < n) {
                val key = readString()
                expect(':')
                when (key) {
                    "t" -> tid = readScalarAsString().trim()
                    "h" -> hash = readScalarAsString().trim().uppercase()
                    "sd" -> seeds = readIntOrZero()
                    "sz" -> sizeBytes = readLongOrZero()
                    "rt" -> regTime = readLongOrZero()
                    "n" -> torrentName = readScalarAsString()
                    "tt" -> topicTitle = readScalarAsString()
                    "g" -> gameName = readScalarAsString()
                    "tid" -> titleId = readScalarAsString().uppercase()
                    "f" -> files = readIndexFiles()
                    else -> skipValue()
                }
                val sep = peek()
                if (sep == ',') { pos++; continue }
                if (sep == '}') { pos++; break }
                throw IllegalArgumentException("Malformed topic object at offset $pos")
            }
            if (tid.isEmpty() || !tid.all { it.isDigit() }) return null
            val topic = IndexTopic(
                topicId = tid,
                infoHash = if (isHash(hash)) hash else "",
                seeds = seeds,
                sizeBytes = sizeBytes,
                regTime = regTime,
                torrentName = torrentName,
                topicTitle = topicTitle,
                gameName = gameName,
                titleId = titleId,
                files = files,
                fromIndex = true
            )
            topic.rebuildKeys()
            return topic
        }
    }

    /** Разбирает gzip или обычный JSON индекса (схема v1/v2). */
    fun parse(input: InputStream, source: String): IndexData {
        val text = maybeGunzip(input, 65536).use { it.readBytes() }.toString(Charsets.UTF_8)
        val reader = IndexJsonReader(text)
        var version = 0
        var generated = 0L
        val topics = ArrayList<IndexTopic>(7500)
        val byId = HashMap<String, IndexTopic>(16384)
        val byHash = HashMap<String, IndexTopic>(16384)
        if (reader.peek() != '{') return IndexData(version, generated, topics, byId, byHash, source)
        reader.advance()
        if (reader.peek() == '}') {
            reader.advance()
            return IndexData(version, generated, topics, byId, byHash, source)
        }
        while (true) {
            val key = reader.readString()
            reader.expect(':')
            when (key) {
                "v" -> version = reader.readIntOrZero()
                "generated" -> generated = reader.readLongOrZero()
                "topics" -> {
                    if (reader.peek() == '[') {
                        reader.advance()
                        if (reader.peek() != ']') {
                            while (true) {
                                val t = reader.readTopicObject()
                                if (t != null) {
                                    topics.add(t)
                                    byId[t.topicId] = t
                                    if (t.infoHash.isNotEmpty()) byHash[t.infoHash] = t
                                }
                                val sep = reader.peek()
                                if (sep == ',') { reader.advance(); continue }
                                if (sep == ']') { reader.advance(); break }
                                throw IllegalArgumentException("Malformed topics array")
                            }
                        } else {
                            reader.advance()
                        }
                    } else {
                        reader.skipValue()
                    }
                }
                else -> reader.skipValue()
            }
            val rootSep = reader.peek()
            if (rootSep == ',') { reader.advance(); continue }
            if (rootSep == '}') { reader.advance(); break }
            throw IllegalArgumentException("Malformed root object")
        }
        return IndexData(version, generated, topics, byId, byHash, source)
    }

    /** Загрузка индекса из файла (используется для JVM-проверок и кэша GitHub). */
    fun loadFromFile(file: File): IndexData? {
        val t0 = System.nanoTime()
        return try {
            val d = file.inputStream().buffered().use { parse(it, file.name) }
            install(d)
            lastLoadMs = (System.nanoTime() - t0) / 1_000_000L
            d
        } catch (e: Throwable) {
            lastLoadError = "Индекс ${file.name} не прочитан: ${e.javaClass.simpleName}: ${e.message}"
            null
        }
    }

    private fun install(d: IndexData) {
        data = d
        lastLoadError = null
        // Повторно применяем уже известные заголовки atom-ленты
        for ((tid, title) in atomTitles) applyAtomTitle(tid, title)
    }

    /** Блокирующая (один раз) загрузка индекса: свежий кэш GitHub или ассет приложения — что новее. */
    fun ensureLoaded(): IndexData? {
        data?.let { return it }
        synchronized(loadLock) {
            data?.let { return it }
            val t0 = System.nanoTime()
            val cached = cacheDir?.let { File(it, CACHED_FILE_NAME) }?.takeIf { it.isFile && it.length() > 64L }
            val opener = assetOpener
            val assetHeader = opener?.let { readHeader(it) }
            val cachedHeader = cached?.let { f -> readHeader { f.inputStream() } }
            val useCached = cachedHeader != null && (assetHeader == null ||
                cachedHeader.version > assetHeader.version ||
                (cachedHeader.version == assetHeader.version && cachedHeader.generated > assetHeader.generated))
            val errors = ArrayList<String>()
            var loaded: IndexData? = null
            val preferredCache = if (useCached) cached else null
            if (preferredCache != null) {
                loaded = try { preferredCache.inputStream().buffered().use { parse(it, "GitHub (кэш)") } } catch (e: Throwable) {
                    errors.add("кэш GitHub: ${e.javaClass.simpleName}"); null
                }
            }
            if (loaded == null && opener != null) {
                loaded = try { opener().buffered().use { parse(it, "встроенный индекс") } } catch (e: Throwable) {
                    errors.add("ассет $ASSET_NAME: ${e.javaClass.simpleName}: ${e.message}"); null
                }
            }
            if (loaded == null && cached != null && !useCached) {
                loaded = try { cached.inputStream().buffered().use { parse(it, "GitHub (кэш)") } } catch (_: Throwable) { null }
            }
            if (loaded == null) {
                lastLoadError = if (opener == null && cached == null) "Офлайн-индекс раздела #1605 не подключён" else
                    "Офлайн-индекс раздела #1605 не загружен (${errors.joinToString("; ")})"
                return null
            }
            install(loaded)
            lastLoadMs = (System.nanoTime() - t0) / 1_000_000L
            return loaded
        }
    }

    /** Фоновая загрузка индекса + обновление (pvc, atom-лента, свежий индекс из релиза GitHub). Вызов идемпотентен. */
    fun warmupAsync() {
        if (warmupStarted) return
        warmupStarted = true
        val th = Thread({
            try { ensureLoaded() } catch (_: Throwable) {}
            try { RuTrackerCloudEngine.loadRuTrackerSwitchForumMapsBlocking() } catch (_: Throwable) {}
            try { RuTrackerCloudEngine.refreshAtomFeedTitles() } catch (_: Throwable) {}
            try { refreshRemoteIndexBlocking() } catch (_: Throwable) {}
        }, "rt-index-warmup")
        th.isDaemon = true
        th.priority = Thread.MIN_PRIORITY + 1
        th.start()
    }

    /** Скачивает более свежий индекс из релиза GitHub (не чаще 1 раза в сутки, тихо при отсутствии сети). */
    fun refreshRemoteIndexBlocking(timeoutMs: Int = 15000): Boolean {
        val dir = cacheDir ?: return false
        val stamp = File(dir, REMOTE_STAMP_NAME)
        if (stamp.isFile && System.currentTimeMillis() - stamp.lastModified() < REMOTE_CHECK_INTERVAL_MS) return false
        return try {
            val conn = URL(REMOTE_URL).openConnection() as HttpURLConnection
            conn.connectTimeout = timeoutMs
            conn.readTimeout = timeoutMs
            conn.instanceFollowRedirects = true
            conn.setRequestProperty("User-Agent", "STORM-SWITCH-BOX")
            val code = conn.responseCode
            try { dir.mkdirs(); stamp.writeText(System.currentTimeMillis().toString()) } catch (_: Exception) {}
            if (code != 200) return false
            val bytes = conn.inputStream.use { it.readBytes() }
            if (bytes.size < 64) return false
            val hdr = readHeader { ByteArrayInputStream(bytes) } ?: return false
            val cur = data
            val newer = cur == null || hdr.version > cur.version || (hdr.version == cur.version && hdr.generated > cur.generated)
            if (!newer) return false
            val parsed = parse(ByteArrayInputStream(bytes), "GitHub (обновление)")
            if (parsed.topics.isEmpty()) return false
            val tmp = File(dir, "$CACHED_FILE_NAME.tmp")
            tmp.writeBytes(bytes)
            val dst = File(dir, CACHED_FILE_NAME)
            if (dst.exists()) dst.delete()
            if (!tmp.renameTo(dst)) { tmp.copyTo(dst, overwrite = true); tmp.delete() }
            install(parsed)
            true
        } catch (_: Throwable) { false }
    }

    // =========================================================================
    // ДОПОЛНЕНИЕ ИНДЕКСА ЖИВЫМИ ДАННЫМИ
    // =========================================================================

    private fun applyAtomTitle(tid: String, title: String) {
        val d = data
        val t = d?.byId?.get(tid)
        if (t != null) {
            if (t.topicTitle.isBlank()) t.updateTopicTitle(title)
            return
        }
        // Тема есть в API раздела #1605, но отсутствует в индексе: показываем только с настоящим заголовком
        val live = RuTrackerCloudEngine.liveTopicData(tid) ?: return
        val existing = extraTopics[tid]
        if (existing != null) { existing.updateTopicTitle(title); return }
        val et = IndexTopic(tid, live.first, live.third, live.second, 0L, "", title, cleanTitleToGame(title), extractTid(title), emptyList(), false)
        et.rebuildKeys()
        extraTopics[tid] = et
    }

    /** Регистрирует настоящие заголовки тем из atom-ленты раздела #1605. */
    fun registerAtomTitles(titles: Map<String, String>) {
        for ((tid, title) in titles) {
            if (title.isBlank()) continue
            atomTitles[tid] = title
            applyAtomTitle(tid, title)
        }
    }

    /** Регистрирует тему раздела #1605, найденную по реальному BTIH раздачи RuTracker #1605. */
    fun registerExternalTopic(tid: String, torrentName: String) {
        if (tid.isBlank() || torrentName.isBlank()) return
        val existing = extraTopics[tid] ?: data?.byId?.get(tid)
        if (existing != null && existing.torrentName.isNotBlank()) return
        val live = RuTrackerCloudEngine.liveTopicData(tid) ?: return
        val et = IndexTopic(tid, live.first, live.third, live.second, 0L, torrentName, atomTitles[tid].orEmpty(), cleanTitleToGame(torrentName), extractTid(torrentName), emptyList(), false)
        et.rebuildKeys()
        extraTopics[tid] = et
    }

    fun topicById(topicId: String): IndexTopic? {
        val tid = topicId.trim()
        return data?.byId?.get(tid) ?: extraTopics[tid]
    }

    fun topicByHash(hash: String): IndexTopic? {
        val h = hash.trim().uppercase()
        if (h.length != 40) return null
        data?.byHash?.get(h)?.let { return it }
        return extraTopics.values.firstOrNull { it.infoHash == h }
    }

    // =========================================================================
    // ПОИСК (опечатки, раскладка, транслит, русские названия, TitleID)
    // =========================================================================

    private class QVariant(val text: String, val padded: String, val words: List<String>, val skeleton: String)

    private val STOP_WORDS = setOf("the", "and", "for", "of", "a", "an", "nintendo", "switch", "edition")

    private fun buildVariants(rawQuery: String): List<QVariant> {
        val clean = rawQuery.trim().lowercase()
        val raw = LinkedHashSet<String>()
        raw.addAll(RuTrackerCloudEngine.generateSmartQueryVariants(clean))
        val swappedRu = RuTrackerCloudEngine.swapKeyboardLayout(clean, ruToEn = false)
        if (swappedRu != clean) raw.add(RuTrackerCloudEngine.transliterateRuToEn(swappedRu))
        val out = ArrayList<QVariant>()
        val seen = HashSet<String>()
        for (r in raw) {
            val n = norm(r)
            if (n.isEmpty() || !seen.add(n)) continue
            val ws = n.split(' ').filter { it.isNotEmpty() }
            val sig = ws.filter { it !in STOP_WORDS }.ifEmpty { ws }
            out.add(QVariant(n, " $n", sig, if (n.length >= 4) RuTrackerCloudEngine.phoneticSkeleton(n) else ""))
        }
        return out
    }

    private fun wordMatch(qw: String, gw: String): Int {
        if (qw == gw) return 0
        if (qw.length >= 2 && gw.startsWith(qw) && !qw.all { it.isDigit() }) return 0
        if (qw.length < 4 || gw.length < 4) return 99
        if (kotlin.math.abs(qw.length - gw.length) > 2 && gw.length < qw.length) return 99
        val sameStart = qw[0] == gw[0] || (qw[0] == gw[1] && qw[1] == gw[0])
        if (!sameStart) return 99
        val maxDist = if (qw.length <= 5) 1 else 2
        if (kotlin.math.abs(qw.length - gw.length) <= 2) {
            val d = RuTrackerCloudEngine.damerauLevenshtein(qw, gw)
            if (d <= maxDist) return d
        }
        if (gw.length > qw.length) {
            val dp = RuTrackerCloudEngine.damerauLevenshtein(qw, gw.substring(0, qw.length))
            if (dp <= 1) return dp + 1
        }
        return 99
    }

    private fun wordScore(t: IndexTopic, v: QVariant): Int {
        if (v.words.isEmpty()) return 0
        var total = 0
        var inGame = 0
        for (qw in v.words) {
            var best = 99
            var bestInGame = false
            for (wi in t.words.indices) {
                val d = wordMatch(qw, t.words[wi])
                if (d < best || (d == best && wi < t.gameWordCount && !bestInGame)) {
                    best = d
                    bestInGame = wi < t.gameWordCount
                    if (d == 0 && bestInGame) break
                }
            }
            if (best >= 99) return 0
            total += best
            if (bestInGame) inGame++
        }
        val base = if (total == 0) 980 else (840 - total * 60)
        val gameBonus = if (inGame == v.words.size) 40 else 0
        return (base + gameBonus).coerceAtLeast(600)
    }

    private fun scoreTopic(t: IndexTopic, variants: List<QVariant>, tidQuery: String?): Int {
        if (tidQuery != null && t.tidBlob.contains(tidQuery)) return 1300
        var best = 0
        for ((idx, v) in variants.withIndex()) {
            val s = v.text
            var sc = when {
                t.gameKey == s -> 1250
                t.gameKey.startsWith(s) -> 1180
                t.gameKeyPadded.contains(v.padded) -> 1120
                s.length >= 3 && t.blobPadded.contains(v.padded) -> 1040
                s.length >= 5 && t.blobPadded.contains(s) -> 900
                else -> 0
            }
            if (sc == 0 && s.length >= 3) sc = wordScore(t, v)
            if (sc == 0 && v.skeleton.length >= 4 && t.skeleton.contains(v.skeleton)) sc = 640
            if (sc > 0) {
                sc -= if (idx == 0) 0 else 35
                if (sc > best) best = sc
                if (best >= 1250) break
            }
        }
        return best
    }

    /**
     * Поиск по индексу. Возвращает пары (тема, балл). Пустой запрос — все темы (сортирует вызывающий по сидам).
     * Не выполняет сетевых запросов. Если индекс ещё не загружен — загружает его (вызывать вне UI-потока).
     */
    fun search(rawQuery: String, isCancelled: () -> Boolean = { false }): List<Pair<IndexTopic, Int>> {
        val d = ensureLoaded()
        val all: Collection<IndexTopic> = if (extraTopics.isEmpty()) d?.topics.orEmpty() else (d?.topics.orEmpty() + extraTopics.values)
        val q = rawQuery.trim()
        if (q.isEmpty()) return all.map { it to 500 }
        val qn = norm(q)
        val hex = qn.replace(" ", "")
        val tidQuery = if (hex.length >= 6 && hex.all { it in '0'..'9' || it in 'a'..'f' } && hex.any { it.isDigit() }) hex else null
        val topicNumber = if (qn.length >= 5 && qn.all { it.isDigit() }) qn else null
        val variants = buildVariants(q)
        val out = ArrayList<Pair<IndexTopic, Int>>()
        var n = 0
        for (t in all) {
            if ((++n and 511) == 0 && isCancelled()) return emptyList()
            if (topicNumber != null && t.topicId == topicNumber) { out.add(t to 1400); continue }
            val sc = scoreTopic(t, variants, tidQuery)
            if (sc > 0) out.add(t to sc)
        }
        return out
    }

    // =========================================================================
    // УТИЛИТЫ
    // =========================================================================

    internal fun isHash(h: String): Boolean = h.length == 40 && h.all { it in '0'..'9' || it in 'A'..'F' }

    /** Нормализация для поиска: нижний регистр, ё→е, всё кроме букв/цифр → пробел, без повторных пробелов. */
    fun norm(s: String): String {
        if (s.isEmpty()) return ""
        val sb = StringBuilder(s.length)
        var space = true
        for (c0 in s) {
            var c = c0.lowercaseChar()
            if (c == 'ё') c = 'е'
            if (c.isLetterOrDigit()) {
                sb.append(c)
                space = false
            } else if (!space) {
                sb.append(' ')
                space = true
            }
        }
        if (sb.isNotEmpty() && sb[sb.length - 1] == ' ') sb.setLength(sb.length - 1)
        return sb.toString()
    }

    private val TID_REGEX = Regex("""(?<![0-9A-Fa-f])(01[0-9A-Fa-f]{14})(?![0-9A-Fa-f])""")

    fun extractTid(s: String): String {
        val m = TID_REGEX.find(s)?.groupValues?.get(1)?.uppercase() ?: return ""
        return m.take(13) + if (m.endsWith("800")) "000" else m.takeLast(3).let { if (it == "000") "000" else it }
    }

    /** Название игры из настоящего заголовка темы / имени торрента (без тегов в скобках и версий). */
    fun cleanTitleToGame(title: String): String {
        var s = title.replace(Regex("""^\s*\[[^\]]*Switch[^\]]*]\s*""", RegexOption.IGNORE_CASE), "")
        s = s.replace(Regex("""\[[^\]]*]|\([^)]*\)|\{[^}]*}"""), " ")
        s = s.replace(Regex("""\b[vV]\d[\d.]*\b"""), " ")
        s = s.replace(Regex("""\b(NSP|NSZ|XCI|XCZ|RUS|ENG|Multi\d*)\b""", RegexOption.IGNORE_CASE), " ")
        s = s.replace('_', ' ').replace(Regex("""\s+"""), " ").trim().trim('-', '/', '|', '.', ',', ' ')
        return s
    }

    /** Размер в русской локали без зависимостей от Android. */
    fun formatSizeRu(bytes: Long): String {
        if (bytes <= 0L) return "нет данных"
        val units = arrayOf("Б", "КБ", "МБ", "ГБ", "ТБ")
        var v = bytes.toDouble()
        var u = 0
        while (v >= 1024.0 && u < units.size - 1) { v /= 1024.0; u++ }
        return if (u == 0) "$bytes Б" else String.format(java.util.Locale("ru", "RU"), "%.2f %s", v, units[u])
    }

    /** Версии раздачи из РЕАЛЬНЫХ файлов: «v0 + v65536», DLC отдельно. */
    fun versionsText(t: IndexTopic): String {
        val vers = t.files.filter { it.version.isNotBlank() && !it.role.equals("DLC", true) }.map { it.version }.distinct()
        val dlc = t.files.count { it.role.equals("DLC", true) }
        val parts = ArrayList<String>()
        if (vers.isNotEmpty()) parts.add(vers.joinToString(" + "))
        if (dlc > 0) parts.add("$dlc DLC")
        return parts.joinToString(" • ")
    }

    fun formatsText(t: IndexTopic): String =
        t.files.map { it.extension.uppercase() }.filter { it in setOf("NSP", "NSZ", "XCI", "XCZ") }.distinct().joinToString(" / ")

    /** Заголовок карточки: настоящий заголовок темы, иначе собирается только из реальных данных файлов. */
    fun displayTitle(t: IndexTopic): String {
        if (t.topicTitle.isNotBlank()) return t.topicTitle
        val name = t.gameName.ifBlank { t.torrentName }.ifBlank { "Тема t=${t.topicId}" }
        val sb = StringBuilder("[Nintendo Switch] ").append(name)
        val v = versionsText(t)
        if (v.isNotEmpty()) sb.append(" [").append(v).append(']')
        val f = formatsText(t)
        if (f.isNotEmpty()) sb.append(" [").append(f).append(']')
        return sb.toString()
    }
}
