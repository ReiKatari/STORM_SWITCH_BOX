package com.storm.switchbox

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.LinearGradient
import android.graphics.Paint
import android.graphics.RectF
import android.graphics.Shader
import android.graphics.Typeface
import com.github.luben.zstd.Zstd
import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.math.BigInteger
import java.security.MessageDigest
import java.util.zip.ZipInputStream
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import kotlin.math.min

data class Pfs0Entry(
    val name: String,
    val offset: Long,
    val size: Long,
    val dataStartOffset: Long
)

object StormNczEngine {
    private const val PFS0_MAGIC = 0x30534650 // "PFS0" LE
    private const val HFS0_MAGIC = 0x30534648 // "HFS0" LE
    private const val NCA_HEADER_SIZE = 0x4000L

    data class NczProgress(
        val currentFile: String,
        val fileIndex: Int,
        val totalFiles: Int,
        val processedBytes: Long,
        val totalBytes: Long,
        val speedMbPerSec: Double,
        val percent: Int,
        val stageText: String
    )

    data class CompressionResult(
        val success: Boolean,
        val inputPath: String,
        val outputPath: String,
        val originalBytes: Long,
        val compressedBytes: Long,
        val elapsedMs: Long,
        val compressedNcaCount: Int,
        val totalFilesCount: Int,
        val blockSizeExponent: Int,
        val zstdLevel: Int,
        val message: String
    )

    data class LibraryAuditReport(
        val totalFiles: Int,
        val totalBytes: Long,
        val uncompressedNspFiles: List<File>,
        val untrimmedCartFiles: List<Pair<File, Long>>,
        val duplicateTitleGroups: Map<String, List<File>>,
        val estimatedSavingsBytes: Long
    )

    data class QueuedItemInfo(
        val file: File,
        val rawTitleId: String,
        val baseTitleId: String,
        val roleBadge: String,
        val roleOrder: Int,
        val cleanGameName: String
    )

    data class GameQueueGroup(
        val groupKey: String,
        val baseTitleId: String,
        val displayTitle: String,
        val items: List<QueuedItemInfo>,
        val totalBytes: Long
    )

    data class PreBuildCustomization(
        val enabled: Boolean = false,
        val customInternalTitle: String = "",
        val customPublisher: String = "",
        val customDisplayVersion: String = "",
        val customOutputFileName: String = "",
        val customIconPath: String = "",
        val iconBadgeText: String = ""
    )

    data class ContainerMetadataInfo(
        val internalTitle: String,
        val publisher: String,
        val displayVersion: String,
        val titleId: String,
        val formatFamily: String,
        val embeddedIconBytes: ByteArray? = null
    )

    private val hex16Regex = Regex("""([0-9A-Fa-f]{16})""")

    fun normalizeBaseTitleId(rawTid: String): String {
        val clean = rawTid.trim().uppercase()
        if (clean.length != 16 || !clean.all { it in '0'..'9' || it in 'A'..'F' }) return ""
        val suffix = clean.substring(13, 16)
        return when {
            suffix == "000" -> clean
            suffix == "800" -> clean.substring(0, 13) + "000"
            else -> {
                // DLC имеет (BasePrefix13 + 1) и суффикс 001..FFF
                try {
                    val prefixVal = clean.substring(0, 13).toLong(16)
                    val basePrefix = (prefixVal and 0xFFFFFFFFFFFFEL).toString(16).uppercase().padStart(13, '0')
                    basePrefix + "000"
                } catch (_: Exception) {
                    clean.substring(0, 13) + "000"
                }
            }
        }
    }

    fun extractCleanGameTitle(fileName: String): String {
        val knownExtRegex = Regex("""(?i)\.(nsp|nsz|xci|xcz|3ds|cia|nca|ncz|zip|rar|7z)$""")
        val noExt = fileName.replace(knownExtRegex, "")
        val stripped = noExt
            .replace(Regex("""\[[^\]]*\]"""), " ")
            .replace(Regex("""\([^)]*\)"""), " ")
            .replace("[", " ")
            .replace("]", " ")
            .replace(Regex("""(?i)\b(v\d+(?:\.\d+)*|upd|update|patch|dlc|base|mod|rus|multi\d*|nintendo|switch|nsp|nsz|xci|xcz)\b"""), " ")
            .replace(Regex("""[_\-]+|\.+"""), " ")
            .replace(Regex("""\s+"""), " ")
            .trim()
        return stripped.ifBlank { noExt.replace("[", "").replace("]", "").trim().take(32) }
    }

    fun analyzeQueuedFile(file: File): QueuedItemInfo {
        val name = file.name
        val ext = file.extension.lowercase()
        var foundTid = hex16Regex.find(name)?.groupValues?.get(1)?.uppercase() ?: ""

        if (foundTid.isEmpty() && file.exists() && file.isFile && ext in listOf("nsp", "nsz", "xci", "xcz")) {
            try {
                val entries = inspectContainer(file)
                for (e in entries) {
                    val m = hex16Regex.find(e.name)
                    if (m != null) {
                        foundTid = m.groupValues[1].uppercase()
                        break
                    }
                }
            } catch (_: Exception) {}
        }

        val baseTid = normalizeBaseTitleId(foundTid)
        val lowerName = name.lowercase()
        val (roleBadge, roleOrder) = when {
            ext in listOf("zip", "pchtxt", "ips", "bps") || file.isDirectory || lowerName.contains("mod") || lowerName.contains("rus") ->
                "МОД" to 3
            foundTid.endsWith("800") || Regex("""(?i)\b(upd|update|patch)\b""").containsMatchIn(lowerName) ->
                "ПАТЧ" to 1
            (foundTid.length == 16 && !foundTid.endsWith("000") && !foundTid.endsWith("800")) || Regex("""(?i)\b(dlc|addon)\b""").containsMatchIn(lowerName) ->
                "DLC" to 2
            else ->
                "БАЗА" to 0
        }

        val cleanName = extractCleanGameTitle(name)
        return QueuedItemInfo(
            file = file,
            rawTitleId = foundTid,
            baseTitleId = baseTid,
            roleBadge = roleBadge,
            roleOrder = roleOrder,
            cleanGameName = cleanName
        )
    }

    /**
     * Умная группировка файлов очереди по играм (как в ПК-версии):
     * объединяет Базу + Патчи + DLC + Моды одной игры в единую группу по Base TitleID или нормализованному названию игры,
     * не смешивая разные игры между собой.
     */
    fun groupFilesByGameFamily(files: List<File>): List<GameQueueGroup> {
        val analyzed = files.filter { it.exists() }.map { analyzeQueuedFile(it) }
        if (analyzed.isEmpty()) return emptyList()

        // Карта нормализованного имени -> известный BaseTitleID, чтобы моды/патчи без TitleID в имени привязывались к своей игре
        val nameToTid = mutableMapOf<String, String>()
        for (item in analyzed) {
            val normName = item.cleanGameName.lowercase().replace(Regex("[^a-zа-я0-9]"), "")
            if (item.baseTitleId.isNotEmpty() && normName.length >= 3) {
                nameToTid.putIfAbsent(normName, item.baseTitleId)
            }
        }

        val groupedMap = LinkedHashMap<String, MutableList<QueuedItemInfo>>()
        for (item in analyzed) {
            val normName = item.cleanGameName.lowercase().replace(Regex("[^a-zа-я0-9]"), "")
            val resolvedTid = item.baseTitleId.ifEmpty {
                nameToTid[normName] ?: nameToTid.entries.firstOrNull { (k, _) ->
                    k.length >= 4 && normName.length >= 4 && (k.contains(normName) || normName.contains(k))
                }?.value ?: ""
            }
            val key = if (resolvedTid.isNotEmpty()) "TID_$resolvedTid" else "NAME_${normName.ifEmpty { item.file.nameWithoutExtension.lowercase() }}"
            val updatedItem = if (item.baseTitleId.isEmpty() && resolvedTid.isNotEmpty()) item.copy(baseTitleId = resolvedTid) else item
            groupedMap.getOrPut(key) { mutableListOf() }.add(updatedItem)
        }

        return groupedMap.map { (key, list) ->
            val sortedItems = list.sortedWith(compareBy<QueuedItemInfo> { it.roleOrder }.thenByDescending { it.file.length() })
            val bestTitle = sortedItems.firstOrNull { it.roleOrder == 0 }?.cleanGameName
                ?: sortedItems.first().cleanGameName
            val tid = sortedItems.firstOrNull { it.baseTitleId.isNotEmpty() }?.baseTitleId ?: ""
            GameQueueGroup(
                groupKey = key,
                baseTitleId = tid,
                displayTitle = bestTitle,
                items = sortedItems,
                totalBytes = sortedItems.sumOf { it.file.length() }
            )
        }
    }

    fun formatSize(bytes: Long): String {
        val safe = bytes.coerceAtLeast(0L)
        if (safe < 1024L) return "$safe Б"
        val ruLocale = java.util.Locale("ru", "RU")
        val kb = safe / 1024.0
        if (kb < 1024.0) return String.format(ruLocale, "%.2f КБ", kb)
        val mb = kb / 1024.0
        if (mb < 1024.0) return String.format(ruLocale, "%.2f МБ", mb)
        val gb = mb / 1024.0
        return String.format(ruLocale, "%.2f ГБ", gb)
    }

    fun localizeSizeText(raw: String): String {
        return raw.trim()
            .replace(".", ",")
            .replace(Regex("""(?i)\bGB\b"""), "ГБ")
            .replace(Regex("""(?i)\bMB\b"""), "МБ")
            .replace(Regex("""(?i)\bKB\b"""), "КБ")
            .replace(Regex("""(?i)\bB\b"""), "Б")
    }

    fun parseSizeToBytes(sizeText: String): Long {
        val clean = sizeText.replace(",", ".").replace(" ", " ").trim()
        val m = Regex("""(\d+(?:\.\d+)?)\s*(GB|ГБ|MB|МБ|KB|КБ|B|Б)?""", RegexOption.IGNORE_CASE).find(clean)
            ?: return 75L * 1024L * 1024L
        val num = m.groupValues[1].toDoubleOrNull() ?: 75.0
        val unit = m.groupValues[2].uppercase()
        return when (unit) {
            "GB", "ГБ" -> (num * 1024.0 * 1024.0 * 1024.0).toLong()
            "KB", "КБ" -> (num * 1024.0).toLong()
            "B", "Б" -> num.toLong()
            else -> (num * 1024.0 * 1024.0).toLong()
        }
    }

    /**
     * Разбор контейнера PFS0 (.nsp / .nsz) и возврат списка внутренних файлов.
     */
    fun parsePfs0Entries(file: File): List<Pfs0Entry> {
        val entries = mutableListOf<Pfs0Entry>()
        if (!file.exists() || file.length() < 16) return entries
        RandomAccessFile(file, "r").use { raf ->
            val headerBuf = ByteArray(16)
            raf.readFully(headerBuf)
            val bb = ByteBuffer.wrap(headerBuf).order(ByteOrder.LITTLE_ENDIAN)
            val magic = bb.int
            if (magic != PFS0_MAGIC) return emptyList()

            val numFiles = bb.int
            val stringTableSize = bb.int
            if (numFiles <= 0 || numFiles > 4096 || stringTableSize < 0 || stringTableSize > 10 * 1024 * 1024) {
                return emptyList()
            }

            val entryTableBytes = ByteArray(numFiles * 24)
            raf.readFully(entryTableBytes)
            val stringTableBytes = ByteArray(stringTableSize)
            raf.readFully(stringTableBytes)

            val dataBaseOffset = 16L + entryTableBytes.size + stringTableBytes.size
            val ebb = ByteBuffer.wrap(entryTableBytes).order(ByteOrder.LITTLE_ENDIAN)

            for (i in 0 until numFiles) {
                val dataOffset = ebb.long
                val dataSize = ebb.long
                val strOffset = ebb.int
                ebb.int // reserved

                var endIdx = strOffset
                while (endIdx < stringTableBytes.size && stringTableBytes[endIdx] != 0.toByte()) {
                    endIdx++
                }
                val name = if (strOffset in 0 until stringTableBytes.size) {
                    String(stringTableBytes, strOffset, endIdx - strOffset, StandardCharsets.UTF_8)
                } else {
                    "entry_$i.bin"
                }
                entries.add(Pfs0Entry(name, dataBaseOffset + dataOffset, dataSize, dataBaseOffset + dataOffset))
            }
        }
        return entries
    }

    /**
     * Глубокая инспекция контейнеров PFS0 (.nsp/.nsz), XCI/XCZ (HEAD + HFS0) и NCSD (.3ds).
     */
    /**
     * Глубокая рекурсивная инспекция всех контейнеров Switch и Nintendo 3DS:
     * PFS0 (.nsp/.nsz), XCI/XCZ (HEAD + HFS0 update/normal/secure), NCA/NCZ, NCSD (.3ds/.cci), CIA, CXI/CFA.
     */
    fun inspectContainer(file: File): List<Pfs0Entry> {
        if (!file.exists() || file.length() < 16) return emptyList()
        val pfs = parsePfs0Entries(file)
        if (pfs.isNotEmpty()) {
            // Если внутри PFS0 оказались вложенные .nsp/.nsz контейнеры, разворачиваем их до плоских NCA/NCZ/TIK/CERT
            val flattened = mutableListOf<Pfs0Entry>()
            RandomAccessFile(file, "r").use { raf ->
                for (e in pfs) {
                    val lower = e.name.lowercase()
                    if ((lower.endsWith(".nsp") || lower.endsWith(".nsz")) && e.size > 64L && e.dataStartOffset + e.size <= raf.length()) {
                        val subEntries = parsePfs0SliceEntries(raf, e.dataStartOffset, e.size)
                        if (subEntries.isNotEmpty()) {
                            flattened.addAll(subEntries)
                            continue
                        }
                    }
                    flattened.add(e)
                }
            }
            return flattened
        }

        val result = mutableListOf<Pfs0Entry>()
        try {
            RandomAccessFile(file, "r").use { raf ->
                if (raf.length() > 0x200) {
                    raf.seek(0x100)
                    val magic4 = ByteArray(4)
                    raf.readFully(magic4)
                    val mStr = String(magic4, StandardCharsets.US_ASCII)
                    if (mStr == "HEAD") {
                        raf.seek(0x130)
                        val hfsBuf = ByteArray(16)
                        raf.readFully(hfsBuf)
                        val hbb = ByteBuffer.wrap(hfsBuf).order(ByteOrder.LITTLE_ENDIAN)
                        val rootHfsOffset = hbb.long
                        val rootHfsSize = hbb.long
                        result.add(Pfs0Entry("[XCI HEAD] Root HFS0 Header", rootHfsOffset, rootHfsSize, rootHfsOffset))

                        if (rootHfsOffset in 0x200L until raf.length()) {
                            val rootParts = parseHfs0AtOffset(raf, rootHfsOffset)
                            for (part in rootParts) {
                                val subEntries = parseHfs0AtOffset(raf, part.dataStartOffset)
                                if (subEntries.isNotEmpty()) {
                                    subEntries.forEach { se ->
                                        result.add(se.copy(name = "${part.name}/${se.name}"))
                                    }
                                } else {
                                    result.add(part)
                                }
                            }
                        }
                        return result
                    } else if (mStr == "NCSD") {
                        raf.seek(0x120)
                        val partTable = ByteArray(64)
                        raf.readFully(partTable)
                        val pbb = ByteBuffer.wrap(partTable).order(ByteOrder.LITTLE_ENDIAN)
                        val partNames = listOf("Game Executable (CXI 0)", "Manual (CFA 1)", "Download Play (CFA 2)", "Partition 3", "Partition 4", "Partition 5", "New3DS Update 6", "Update Data 7")
                        for (i in 0 until 8) {
                            val offUnits = pbb.int.toLong() and 0xFFFFFFFFL
                            val lenUnits = pbb.int.toLong() and 0xFFFFFFFFL
                            if (lenUnits > 0L) {
                                val byteOff = offUnits * 0x200L
                                val byteLen = lenUnits * 0x200L
                                result.add(Pfs0Entry("[3DS NCSD] ${partNames[i]}.ncch", byteOff, byteLen, byteOff))
                            }
                        }
                        if (result.isEmpty()) {
                            result.add(Pfs0Entry("[3DS NCSD] ${file.name}", 0L, raf.length(), 0L))
                        }
                        return result
                    } else if (mStr == "NCCH") {
                        result.add(Pfs0Entry("[3DS NCCH] ${file.name}", 0L, raf.length(), 0L))
                        return result
                    }
                }
                val ext = file.extension.lowercase()
                if (ext == "cia" && raf.length() > 0x2040) {
                    raf.seek(0L)
                    val ciaHdr = ByteArray(32)
                    raf.readFully(ciaHdr)
                    val cbb = ByteBuffer.wrap(ciaHdr).order(ByteOrder.LITTLE_ENDIAN)
                    val hdrSize = cbb.int.toLong() and 0xFFFFFFFFL
                    cbb.int // type + version
                    val certSize = cbb.int.toLong() and 0xFFFFFFFFL
                    val tikSize = cbb.int.toLong() and 0xFFFFFFFFL
                    val tmdSize = cbb.int.toLong() and 0xFFFFFFFFL
                    val metaSize = cbb.int.toLong() and 0xFFFFFFFFL
                    val contentSize = cbb.long
                    fun align64(v: Long): Long = (v + 63L) and -64L
                    val certOff = align64(hdrSize)
                    val tikOff = align64(certOff + certSize)
                    val tmdOff = align64(tikOff + tikSize)
                    val contentOff = align64(tmdOff + tmdSize)
                    if (contentOff < raf.length() && contentSize > 0L) {
                        result.add(Pfs0Entry("[3DS CIA] cert.bin", certOff, certSize, certOff))
                        result.add(Pfs0Entry("[3DS CIA] ticket.bin", tikOff, tikSize, tikOff))
                        result.add(Pfs0Entry("[3DS CIA] tmd.bin", tmdOff, tmdSize, tmdOff))
                        result.add(Pfs0Entry("[3DS CIA] content_0000.cxi", contentOff, contentSize.coerceAtMost(raf.length() - contentOff), contentOff))
                        if (metaSize > 0L) {
                            val metaOff = align64(contentOff + contentSize)
                            if (metaOff < raf.length()) {
                                result.add(Pfs0Entry("[3DS CIA] smdh_meta.bin", metaOff, metaSize.coerceAtMost(raf.length() - metaOff), metaOff))
                            }
                        }
                        return result
                    }
                }
                result.add(Pfs0Entry(file.name, 0L, raf.length(), 0L))
            }
        } catch (_: Exception) {}
        return result
    }

    private fun parsePfs0SliceEntries(raf: RandomAccessFile, baseOffset: Long, sliceLength: Long): List<Pfs0Entry> {
        val list = mutableListOf<Pfs0Entry>()
        try {
            if (baseOffset < 0L || baseOffset + 16L > raf.length()) return list
            raf.seek(baseOffset)
            val hdr = ByteArray(16)
            raf.readFully(hdr)
            val bb = ByteBuffer.wrap(hdr).order(ByteOrder.LITTLE_ENDIAN)
            if (bb.int != PFS0_MAGIC) return list
            val numFiles = bb.int
            val strTableSize = bb.int
            if (numFiles <= 0 || numFiles > 2048 || strTableSize < 0 || strTableSize > 4 * 1024 * 1024) return list

            val entryBytes = ByteArray(numFiles * 24)
            raf.readFully(entryBytes)
            val strBytes = ByteArray(strTableSize)
            raf.readFully(strBytes)

            val dataBase = baseOffset + 16L + entryBytes.size + strBytes.size
            val ebb = ByteBuffer.wrap(entryBytes).order(ByteOrder.LITTLE_ENDIAN)
            for (i in 0 until numFiles) {
                ebb.position(i * 24)
                val dOff = ebb.long
                val dSize = ebb.long
                val sOff = ebb.int
                var endIdx = sOff
                while (endIdx < strBytes.size && strBytes[endIdx] != 0.toByte()) endIdx++
                val name = if (sOff in 0 until strBytes.size) {
                    String(strBytes, sOff, endIdx - sOff, StandardCharsets.UTF_8)
                } else "entry_$i.nca"
                val absStart = dataBase + dOff
                if (absStart + dSize <= baseOffset + sliceLength) {
                    list.add(Pfs0Entry(name, absStart, dSize, absStart))
                }
            }
        } catch (_: Exception) {}
        return list
    }

    private fun parseHfs0AtOffset(raf: RandomAccessFile, hfsOffset: Long): List<Pfs0Entry> {
        val list = mutableListOf<Pfs0Entry>()
        if (hfsOffset < 0L || hfsOffset + 16L > raf.length()) return list
        raf.seek(hfsOffset)
        val hdr = ByteArray(16)
        raf.readFully(hdr)
        val bb = ByteBuffer.wrap(hdr).order(ByteOrder.LITTLE_ENDIAN)
        if (bb.int != HFS0_MAGIC) return list
        val numFiles = bb.int
        val strTableSize = bb.int
        if (numFiles <= 0 || numFiles > 1024 || strTableSize < 0 || strTableSize > 4 * 1024 * 1024) return list

        val entryBytes = ByteArray(numFiles * 64)
        raf.readFully(entryBytes)
        val strBytes = ByteArray(strTableSize)
        raf.readFully(strBytes)

        val dataBase = hfsOffset + 16L + entryBytes.size + strBytes.size
        val ebb = ByteBuffer.wrap(entryBytes).order(ByteOrder.LITTLE_ENDIAN)
        for (i in 0 until numFiles) {
            val pos = i * 64
            ebb.position(pos)
            val dOff = ebb.long
            val dSize = ebb.long
            val sOff = ebb.int
            var endIdx = sOff
            while (endIdx < strBytes.size && strBytes[endIdx] != 0.toByte()) endIdx++
            val name = if (sOff in 0 until strBytes.size) {
                String(strBytes, sOff, endIdx - sOff, StandardCharsets.UTF_8)
            } else "hfs_entry_$i"
            list.add(Pfs0Entry(name, dataBase + dOff, dSize, dataBase + dOff))
        }
        return list
    }

    // =========================================================================
    // СБОРЩИКИ КОНТЕЙНЕРОВ SWITCH: PFS0 (NSP / NSZ) И КАРТРИДЖ HEAD + HFS0 (XCI / XCZ)
    // =========================================================================

    fun packPfs0Container(inputFiles: List<Pair<String, File>>, outputFile: File) {
        val items = inputFiles.map { it.first to it.second.length() }
        FileOutputStream(outputFile).use { fos ->
            writePfs0HeaderForSizes(fos, items)
            val buf = ByteArray(256 * 1024)
            for ((_, f) in inputFiles) {
                FileInputStream(f).use { fis ->
                    while (true) {
                        val r = fis.read(buf)
                        if (r <= 0) break
                        fos.write(buf, 0, r)
                    }
                }
            }
        }
    }

    private fun writePfs0HeaderForSizes(fos: FileOutputStream, items: List<Pair<String, Long>>) {
        val strOut = ByteArrayOutputStream()
        val nameOffsets = IntArray(items.size)
        for ((idx, pair) in items.withIndex()) {
            nameOffsets[idx] = strOut.size()
            strOut.write(pair.first.toByteArray(StandardCharsets.UTF_8))
            strOut.write(0)
        }
        val rawHeaderLen = 16 + items.size * 24 + strOut.size()
        val alignedLen = (rawHeaderLen + 31) and -32
        while (16 + items.size * 24 + strOut.size() < alignedLen) {
            strOut.write(0)
        }
        val strTableBytes = strOut.toByteArray()

        val hdrBuf = ByteBuffer.allocate(16 + items.size * 24 + strTableBytes.size).order(ByteOrder.LITTLE_ENDIAN)
        hdrBuf.putInt(PFS0_MAGIC)
        hdrBuf.putInt(items.size)
        hdrBuf.putInt(strTableBytes.size)
        hdrBuf.putInt(0)

        var curOffset = 0L
        for ((idx, pair) in items.withIndex()) {
            hdrBuf.putLong(curOffset)
            hdrBuf.putLong(pair.second)
            hdrBuf.putInt(nameOffsets[idx])
            hdrBuf.putInt(0)
            curOffset += pair.second
        }
        hdrBuf.put(strTableBytes)
        fos.write(hdrBuf.array())
    }

    /**
     * Строит эталонный образ картриджа Nintendo Switch XCI / XCZ:
     * - Заголовок HEAD по смещению 0x100 (с точным validDataEndPage и SHA-256 корневого HFS0)
     * - Корневой раздел HFS0 по смещению 0x10000 (update, normal, secure)
     * - Раздел secure HFS0 со всеми .nca / .ncz файлами и SHA-256 хэшами их заголовков.
     */
    fun packXciOrXczContainer(inputFiles: List<Pair<String, File>>, outputFile: File) {
        val sha256 = MessageDigest.getInstance("SHA-256")
        val emptyHfs0 = buildEmptyHfs0PartitionBytes()
        val emptyHfs0Hash = sha256.digest(emptyHfs0)

        // Строим заголовок раздела secure HFS0
        val secureStrOut = ByteArrayOutputStream()
        val secureNameOffsets = IntArray(inputFiles.size)
        for ((i, pair) in inputFiles.withIndex()) {
            secureNameOffsets[i] = secureStrOut.size()
            secureStrOut.write(pair.first.toByteArray(StandardCharsets.UTF_8))
            secureStrOut.write(0)
        }
        val rawSecHdrSize = 16 + inputFiles.size * 64 + secureStrOut.size()
        val alignedSecHdrSize = (rawSecHdrSize + 0x1FF) and -0x200
        while (16 + inputFiles.size * 64 + secureStrOut.size() < alignedSecHdrSize) {
            secureStrOut.write(0)
        }
        val secureStrBytes = secureStrOut.toByteArray()

        val secureHdrBuf = ByteBuffer.allocate(alignedSecHdrSize).order(ByteOrder.LITTLE_ENDIAN)
        secureHdrBuf.putInt(HFS0_MAGIC)
        secureHdrBuf.putInt(inputFiles.size)
        secureHdrBuf.putInt(secureStrBytes.size)
        secureHdrBuf.putInt(0)

        var secDataOffset = 0L
        val hashSampleBuf = ByteArray(0x200)
        for ((i, pair) in inputFiles.withIndex()) {
            val f = pair.second
            val fLen = f.length()
            val hashRegionSize = min(0x200L, fLen).toInt().coerceAtLeast(1)
            var readCount = 0
            if (f.exists() && fLen > 0L) {
                FileInputStream(f).use { fis ->
                    readCount = fis.read(hashSampleBuf, 0, hashRegionSize)
                }
            }
            val entryHash = if (readCount > 0) {
                sha256.reset()
                sha256.update(hashSampleBuf, 0, readCount)
                sha256.digest()
            } else {
                ByteArray(32)
            }

            secureHdrBuf.putLong(secDataOffset)
            secureHdrBuf.putLong(fLen)
            secureHdrBuf.putInt(secureNameOffsets[i])
            secureHdrBuf.putInt(hashRegionSize)
            secureHdrBuf.putLong(0L)
            secureHdrBuf.put(entryHash)

            val alignedFileLen = (fLen + 0x1FFL) and -0x200L
            secDataOffset += alignedFileLen
        }
        secureHdrBuf.put(secureStrBytes)
        val secureHdrBytes = secureHdrBuf.array()
        val secureHdrHash = sha256.digest(secureHdrBytes)
        val secureTotalSize = secureHdrBytes.size.toLong() + secDataOffset

        // Строим корневой раздел Root HFS0 (update, normal, secure) по смещению 0x10000
        val rootNames = listOf("update", "normal", "secure")
        val rootStrOut = ByteArrayOutputStream()
        val rootNameOffsets = IntArray(3)
        for ((i, rName) in rootNames.withIndex()) {
            rootNameOffsets[i] = rootStrOut.size()
            rootStrOut.write(rName.toByteArray(StandardCharsets.UTF_8))
            rootStrOut.write(0)
        }
        val rawRootHdrSize = 16 + 3 * 64 + rootStrOut.size()
        val alignedRootHdrSize = (rawRootHdrSize + 0x1FF) and -0x200
        while (16 + 3 * 64 + rootStrOut.size() < alignedRootHdrSize) {
            rootStrOut.write(0)
        }
        val rootStrBytes = rootStrOut.toByteArray()

        val rootHdrBuf = ByteBuffer.allocate(alignedRootHdrSize).order(ByteOrder.LITTLE_ENDIAN)
        rootHdrBuf.putInt(HFS0_MAGIC)
        rootHdrBuf.putInt(3)
        rootHdrBuf.putInt(rootStrBytes.size)
        rootHdrBuf.putInt(0)

        // 0: update
        rootHdrBuf.putLong(0L)
        rootHdrBuf.putLong(emptyHfs0.size.toLong())
        rootHdrBuf.putInt(rootNameOffsets[0])
        rootHdrBuf.putInt(emptyHfs0.size)
        rootHdrBuf.putLong(0L)
        rootHdrBuf.put(emptyHfs0Hash)

        // 1: normal
        val normalOff = emptyHfs0.size.toLong()
        rootHdrBuf.putLong(normalOff)
        rootHdrBuf.putLong(emptyHfs0.size.toLong())
        rootHdrBuf.putInt(rootNameOffsets[1])
        rootHdrBuf.putInt(emptyHfs0.size)
        rootHdrBuf.putLong(0L)
        rootHdrBuf.put(emptyHfs0Hash)

        // 2: secure
        val secureOff = normalOff + emptyHfs0.size.toLong()
        rootHdrBuf.putLong(secureOff)
        rootHdrBuf.putLong(secureTotalSize)
        rootHdrBuf.putInt(rootNameOffsets[2])
        rootHdrBuf.putInt(secureHdrBytes.size)
        rootHdrBuf.putLong(0L)
        rootHdrBuf.put(secureHdrHash)

        rootHdrBuf.put(rootStrBytes)
        val rootHdrBytes = rootHdrBuf.array()
        val rootHdrHash = sha256.digest(rootHdrBytes)

        val rootHfs0Offset = 0x10000L
        val totalCartBytes = rootHfs0Offset + rootHdrBytes.size + (emptyHfs0.size * 2) + secureTotalSize
        val validDataEndPage = ((totalCartBytes + 0x1FFL) / 0x200L) - 1L

        val cardHeaderArea = ByteArray(rootHfs0Offset.toInt())
        val cbb = ByteBuffer.wrap(cardHeaderArea).order(ByteOrder.LITTLE_ENDIAN)
        cbb.position(0x100)
        cbb.putInt(0x44414548) // "HEAD"
        cbb.putInt((rootHfs0Offset / 0x200L).toInt()) // secure area start page (0x80)
        cbb.putInt(-1) // backup area start
        cbb.put(0.toByte()) // title kek index
        val cartSizeByte = when {
            totalCartBytes <= 2L * 1024 * 1024 * 1024 -> 0xFA.toByte()
            totalCartBytes <= 4L * 1024 * 1024 * 1024 -> 0xF8.toByte()
            totalCartBytes <= 8L * 1024 * 1024 * 1024 -> 0xF0.toByte()
            else -> 0xE0.toByte()
        }
        cbb.put(cartSizeByte)
        cbb.put(0.toByte()) // header version
        cbb.put(0.toByte()) // flags
        cbb.putLong(0x0100000000000000L) // packageId
        cbb.putLong(validDataEndPage) // validDataEndPage at 0x118
        cbb.position(0x130)
        cbb.putLong(rootHfs0Offset)
        cbb.putLong(rootHdrBytes.size.toLong())
        cbb.put(rootHdrHash)

        BufferedOutputStream(FileOutputStream(outputFile), 256 * 1024).use { bos ->
            bos.write(cardHeaderArea)
            bos.write(rootHdrBytes)
            bos.write(emptyHfs0)
            bos.write(emptyHfs0)
            bos.write(secureHdrBytes)

            val copyBuf = ByteArray(256 * 1024)
            val padBuf = ByteArray(0x200)
            for ((_, f) in inputFiles) {
                val fLen = f.length()
                FileInputStream(f).use { fis ->
                    while (true) {
                        val r = fis.read(copyBuf)
                        if (r <= 0) break
                        bos.write(copyBuf, 0, r)
                    }
                }
                val remPad = ((0x200 - (fLen % 0x200).toInt()) % 0x200)
                if (remPad > 0) {
                    bos.write(padBuf, 0, remPad)
                }
            }
            bos.flush()
        }
    }

    private fun buildEmptyHfs0PartitionBytes(): ByteArray {
        val buf = ByteBuffer.allocate(0x200).order(ByteOrder.LITTLE_ENDIAN)
        buf.putInt(HFS0_MAGIC)
        buf.putInt(0)
        buf.putInt(0x1F0)
        buf.putInt(0)
        return buf.array()
    }

    // =========================================================================
    // КРИПТОГРАФИЯ SWITCH NCA (AES-128-XTS 0xC00 + AES-128-CTR СЕКЦИЙ) И NCZBLOCK
    // =========================================================================

    data class NcaSectionCryptoMeta(
        val offset: Long,
        val size: Long,
        val cryptoType: Long,
        val key: ByteArray,
        val counter: ByteArray
    )

    private fun hexToBytes(hex: String): ByteArray {
        val clean = hex.trim().replace(" ", "")
        if (clean.length % 2 != 0) return ByteArray(0)
        return ByteArray(clean.length / 2) { i ->
            clean.substring(i * 2, i * 2 + 2).toIntOrNull(16)?.toByte() ?: 0.toByte()
        }
    }

    private fun loadSwitchKeysMap(): Map<String, ByteArray> {
        val map = HashMap<String, ByteArray>()
        val candidates = listOf(
            File("/storage/emulated/0/STORM SWITCH BOX/keys/prod.keys"),
            File("/storage/emulated/0/STORM SWITCH BOX/keys/title.keys"),
            File("/storage/emulated/0/STORM SWITCH/keys/prod.keys"),
            File("/storage/emulated/0/switch/prod.keys")
        )
        for (f in candidates) {
            if (f.exists() && f.isFile) {
                try {
                    f.forEachLine { line ->
                        val clean = line.substringBefore('#').substringBefore(';').trim()
                        val eq = clean.indexOf('=')
                        if (eq > 0) {
                            val k = clean.substring(0, eq).trim().lowercase()
                            val v = hexToBytes(clean.substring(eq + 1))
                            if (v.isNotEmpty()) map[k] = v
                        }
                    }
                } catch (_: Exception) {}
            }
        }
        return map
    }

    private fun decryptAesXtsSector(sector: ByteArray, sectorOffset: Int, sectorIndex: Long, key1: ByteArray, key2: ByteArray): ByteArray {
        val ecb1 = Cipher.getInstance("AES/ECB/NoPadding").apply { init(Cipher.ENCRYPT_MODE, SecretKeySpec(key2, "AES")) }
        val dec1 = Cipher.getInstance("AES/ECB/NoPadding").apply { init(Cipher.DECRYPT_MODE, SecretKeySpec(key1, "AES")) }
        val tweak = ByteArray(16)
        var s = sectorIndex
        for (i in 0..7) {
            tweak[i] = (s and 0xFFL).toByte()
            s = s ushr 8
        }
        var t = ecb1.doFinal(tweak)
        val out = ByteArray(0x200)
        val block = ByteArray(16)
        for (b in 0 until 0x200 step 16) {
            for (i in 0..15) block[i] = (sector[sectorOffset + b + i].toInt() xor t[i].toInt()).toByte()
            val d = dec1.doFinal(block)
            for (i in 0..15) out[b + i] = (d[i].toInt() xor t[i].toInt()).toByte()
            // GF(2^128) multiply tweak by alpha (x)
            var carry = 0
            for (i in 0..15) {
                val nextCarry = (t[i].toInt() ushr 7) and 1
                t[i] = (((t[i].toInt() shl 1) or carry) and 0xFF).toByte()
                carry = nextCarry
            }
            if (carry != 0) t[0] = (t[0].toInt() xor 0x87).toByte()
        }
        return out
    }

    private fun parseNcaSectionsForCompression(
        ncaHeader3840: ByteArray,
        totalNcaSize: Long,
        keysMap: Map<String, ByteArray>,
        ticketTitleKey: ByteArray?
    ): List<NcaSectionCryptoMeta> {
        val header0C00 = ByteArray(0xC00)
        System.arraycopy(ncaHeader3840, 0, header0C00, 0, min(ncaHeader3840.size, 0xC00))

        val magicPlain = String(header0C00, 0x200, 4, StandardCharsets.US_ASCII)
        val workHdr = if (magicPlain == "NCA3" || magicPlain == "NCA2" || magicPlain == "NCA0") {
            header0C00
        } else {
            val hKey = keysMap["header_key"]
            if (hKey != null && hKey.size == 32) {
                val k1 = hKey.copyOfRange(0, 16)
                val k2 = hKey.copyOfRange(16, 32)
                val dec = ByteArray(0xC00)
                for (sec in 0 until 6) {
                    val secBytes = decryptAesXtsSector(header0C00, sec * 0x200, sec.toLong(), k1, k2)
                    System.arraycopy(secBytes, 0, dec, sec * 0x200, 0x200)
                }
                dec
            } else {
                header0C00
            }
        }

        val magic = String(workHdr, 0x200, 4, StandardCharsets.US_ASCII)
        if (magic != "NCA3" && magic != "NCA2" && magic != "NCA0") {
            return listOf(
                NcaSectionCryptoMeta(
                    offset = NCA_HEADER_SIZE,
                    size = (totalNcaSize - NCA_HEADER_SIZE).coerceAtLeast(0L),
                    cryptoType = 1L,
                    key = ByteArray(16),
                    counter = ByteArray(16)
                )
            )
        }

        // Проверяем наличие RightsID по смещению 0x230..0x23F
        var hasRightsId = false
        for (i in 0x230 until 0x240) {
            if (workHdr[i] != 0.toByte()) {
                hasRightsId = true
                break
            }
        }

        val keyGen = maxOf(workHdr[0x206].toInt() and 0xFF, workHdr[0x220].toInt() and 0xFF)
        val keyIdx = (keyGen - 1).coerceAtLeast(0)
        val idxHex = String.format("%02x", keyIdx)

        val resolvedCtrKey = if (hasRightsId && ticketTitleKey != null && ticketTitleKey.size == 16) {
            val tkek = keysMap["titlekek_$idxHex"] ?: keysMap["titlekek_00"]
            if (tkek != null && tkek.size == 16) {
                try {
                    Cipher.getInstance("AES/ECB/NoPadding").apply {
                        init(Cipher.DECRYPT_MODE, SecretKeySpec(tkek, "AES"))
                    }.doFinal(ticketTitleKey)
                } catch (_: Exception) { ticketTitleKey }
            } else ticketTitleKey
        } else {
            val kak = keysMap["key_area_key_application_$idxHex"] ?: keysMap["key_area_key_application_00"]
            val encSlot2 = workHdr.copyOfRange(0x320, 0x330)
            if (kak != null && kak.size == 16 && encSlot2.any { it != 0.toByte() }) {
                try {
                    Cipher.getInstance("AES/ECB/NoPadding").apply {
                        init(Cipher.DECRYPT_MODE, SecretKeySpec(kak, "AES"))
                    }.doFinal(encSlot2)
                } catch (_: Exception) { encSlot2 }
            } else {
                encSlot2
            }
        }

        val bb = ByteBuffer.wrap(workHdr).order(ByteOrder.LITTLE_ENDIAN)
        val sections = mutableListOf<NcaSectionCryptoMeta>()
        for (sIdx in 0..3) {
            val entryOff = 0x240 + sIdx * 16
            bb.position(entryOff)
            val startMedia = bb.int.toLong() and 0xFFFFFFFFL
            val endMedia = bb.int.toLong() and 0xFFFFFFFFL
            val enabled = bb.int != 0
            if (!enabled || endMedia <= startMedia) continue

            val secByteOffset = (startMedia * 0x200L).coerceAtLeast(NCA_HEADER_SIZE)
            val secByteEnd = (endMedia * 0x200L).coerceAtMost(totalNcaSize)
            if (secByteEnd <= secByteOffset) continue

            val fsOff = 0x400 + sIdx * 0x200
            val rawCryptoType = (workHdr[fsOff + 0x04].toInt() and 0xFF).toLong()
            val counter = ByteArray(16)
            // В FsHeader по смещению +0x140 лежат 8 байт старшей части CTR-счётчика (в обратном порядке байт для CTR IV)
            for (b in 0..7) {
                counter[b] = workHdr[fsOff + 0x147 - b]
            }

            sections.add(
                NcaSectionCryptoMeta(
                    offset = secByteOffset,
                    size = secByteEnd - secByteOffset,
                    cryptoType = if (rawCryptoType in 1L..4L) rawCryptoType else 1L,
                    key = resolvedCtrKey,
                    counter = counter
                )
            )
        }

        if (sections.isEmpty()) {
            return listOf(
                NcaSectionCryptoMeta(
                    offset = NCA_HEADER_SIZE,
                    size = (totalNcaSize - NCA_HEADER_SIZE).coerceAtLeast(0L),
                    cryptoType = 1L,
                    key = ByteArray(16),
                    counter = ByteArray(16)
                )
            )
        }

        sections.sortBy { it.offset }
        val fullList = mutableListOf<NcaSectionCryptoMeta>()
        var cursor = NCA_HEADER_SIZE
        for (sec in sections) {
            if (sec.offset > cursor) {
                fullList.add(NcaSectionCryptoMeta(cursor, sec.offset - cursor, 1L, ByteArray(16), ByteArray(16)))
            }
            fullList.add(sec)
            cursor = sec.offset + sec.size
        }
        if (cursor < totalNcaSize) {
            fullList.add(NcaSectionCryptoMeta(cursor, totalNcaSize - cursor, 1L, ByteArray(16), ByteArray(16)))
        }
        return fullList
    }

    private fun transformAesCtrInPlace(
        buffer: ByteArray,
        bufOffset: Int,
        length: Int,
        virtualNcaOffset: Long,
        key: ByteArray,
        upperCounter8: ByteArray
    ) {
        if (length <= 0 || key.size != 16 || key.all { it == 0.toByte() }) return
        try {
            val iv = ByteArray(16)
            for (i in 0..7) iv[i] = upperCounter8[i]
            var blockCounter = virtualNcaOffset ushr 4
            for (i in 15 downTo 8) {
                iv[i] = (blockCounter and 0xFFL).toByte()
                blockCounter = blockCounter ushr 8
            }
            val cipher = Cipher.getInstance("AES/CTR/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), IvParameterSpec(iv))
            val out = cipher.doFinal(buffer, bufOffset, length)
            System.arraycopy(out, 0, buffer, bufOffset, length)
        } catch (_: Exception) {}
    }

    /**
     * Блочное сжатие NSP/XCI в NSZ/XCZ (NCZSECTN + NCZBLOCK + Zstd JNI 1..22 + SmartSize Guard).
     */
    fun compressNspOrXciToBlockNsz(
        inputFile: File,
        outputFile: File,
        zstdLevel: Int = 18,
        blockSizeExponent: Int = 20,
        threads: Int = 4,
        smartSizeGuard: Boolean = true,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val outExt = outputFile.extension.lowercase()
        val mode = when (outExt) {
            "xcz" -> 1
            "nsp" -> 2
            "xci" -> 3
            else -> 0
        }
        return buildSmartMultiContentPackage(
            inputFiles = listOf(inputFile),
            outputDir = outputFile.parentFile ?: File("/storage/emulated/0/STORM SWITCH BOX/output"),
            targetFormatMode = mode,
            useBlockNcz = (outExt == "nsz" || outExt == "xcz"),
            smartDedup = true,
            embedMods = false,
            zstdLevel = zstdLevel,
            blockSizeExponent = blockSizeExponent,
            threads = threads,
            smartSizeGuard = smartSizeGuard,
            preBuildCustomization = PreBuildCustomization(enabled = true, customOutputFileName = outputFile.name),
            onProgress = onProgress
        )
    }

    private fun compressNcaSliceToBlockNcz(
        raf: RandomAccessFile,
        sliceOffset: Long,
        sliceSize: Long,
        outNczFile: File,
        level: Int,
        blockExpo: Int = 20,
        keysMap: Map<String, ByteArray> = emptyMap(),
        ticketTitleKey: ByteArray? = null,
        onChunkProcessed: (Long) -> Unit
    ) {
        val safeExpo = blockExpo.coerceIn(14, 24)
        val blockSize = 1 shl safeExpo
        val payloadSize = (sliceSize - NCA_HEADER_SIZE).coerceAtLeast(0L)
        val totalBlocks = ((payloadSize + blockSize - 1) / blockSize).toInt()
        val compressedSizes = IntArray(totalBlocks)

        val ncaHeader = ByteArray(NCA_HEADER_SIZE.toInt())
        raf.seek(sliceOffset)
        raf.readFully(ncaHeader)

        val sections = parseNcaSectionsForCompression(ncaHeader, sliceSize, keysMap, ticketTitleKey)

        val blocksTempFile = File(outNczFile.parentFile, "${outNczFile.name}.blocks.tmp")
        val rawBuf = ByteArray(blockSize)
        val maxDstSize = Zstd.compressBound(blockSize.toLong()).toInt()
        val compBuf = ByteArray(maxDstSize)

        try {
            BufferedOutputStream(FileOutputStream(blocksTempFile), 256 * 1024).use { blockBos ->
                var remaining = payloadSize
                var currentReadOffset = sliceOffset + NCA_HEADER_SIZE
                var currentVirtualOffset = NCA_HEADER_SIZE

                for (blockIdx in 0 until totalBlocks) {
                    val toRead = min(blockSize.toLong(), remaining).toInt()
                    raf.seek(currentReadOffset)
                    raf.readFully(rawBuf, 0, toRead)

                    // Если блок попадает в секцию с AES-128-CTR шифрованием (cryptoType == 3), расшифровываем перед Zstd
                    for (sec in sections) {
                        if (sec.cryptoType == 3L && sec.key.any { it != 0.toByte() }) {
                            val secStart = sec.offset
                            val secEnd = sec.offset + sec.size
                            val blkStart = currentVirtualOffset
                            val blkEnd = currentVirtualOffset + toRead
                            val overlapStart = maxOf(secStart, blkStart)
                            val overlapEnd = minOf(secEnd, blkEnd)
                            if (overlapEnd > overlapStart) {
                                val bufOff = (overlapStart - blkStart).toInt()
                                val len = (overlapEnd - overlapStart).toInt()
                                transformAesCtrInPlace(rawBuf, bufOff, len, overlapStart, sec.key, sec.counter)
                            }
                        }
                    }

                    currentReadOffset += toRead
                    currentVirtualOffset += toRead
                    remaining -= toRead

                    val cSize = Zstd.compressByteArray(
                        compBuf, 0, compBuf.size,
                        rawBuf, 0, toRead,
                        level.coerceIn(1, 22)
                    )

                    if (!Zstd.isError(cSize) && cSize > 0 && cSize < toRead) {
                        compressedSizes[blockIdx] = cSize.toInt()
                        blockBos.write(compBuf, 0, cSize.toInt())
                    } else {
                        compressedSizes[blockIdx] = toRead
                        blockBos.write(rawBuf, 0, toRead)
                    }
                    onChunkProcessed(toRead.toLong())
                }
                blockBos.flush()
            }

            BufferedOutputStream(FileOutputStream(outNczFile), 256 * 1024).use { fos ->
                fos.write(ncaHeader)
                onChunkProcessed(NCA_HEADER_SIZE)

                val metaSize = 16 + (sections.size * 64) + 25 + (totalBlocks * 4)
                val metaBuf = ByteBuffer.allocate(metaSize).order(ByteOrder.LITTLE_ENDIAN)

                metaBuf.put("NCZSECTN".toByteArray(StandardCharsets.US_ASCII))
                metaBuf.putLong(sections.size.toLong())

                for (sec in sections) {
                    metaBuf.putLong(sec.offset)
                    metaBuf.putLong(sec.size)
                    metaBuf.putLong(sec.cryptoType)
                    metaBuf.putLong(0L)
                    metaBuf.put(sec.key.copyOf(16))
                    metaBuf.put(sec.counter.copyOf(16))
                }

                metaBuf.put("NCZBLOCK".toByteArray(StandardCharsets.US_ASCII))
                metaBuf.put(2.toByte())
                metaBuf.put(1.toByte())
                metaBuf.put(0.toByte())
                metaBuf.put(safeExpo.toByte())
                metaBuf.putInt(totalBlocks)
                metaBuf.putLong(payloadSize)

                for (sz in compressedSizes) {
                    metaBuf.putInt(sz)
                }
                fos.write(metaBuf.array())

                BufferedInputStream(FileInputStream(blocksTempFile), 256 * 1024).use { fis ->
                    while (true) {
                        val r = fis.read(rawBuf)
                        if (r <= 0) break
                        fos.write(rawBuf, 0, r)
                    }
                }
                fos.flush()
            }
        } finally {
            blocksTempFile.delete()
        }
    }

    /**
     * Распаковка NSZ/XCZ обратно в оригинальный NSP/XCI с полным восстановлением NCA и ре-шифрованием секций.
     */
    fun decompressNszOrXczToNsp(
        inputFile: File,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        val entries = inspectContainer(inputFile)
        val tempDir = File(outputFile.parentFile, ".storm_unncz_${System.currentTimeMillis()}")
        tempDir.mkdirs()

        try {
            val validEntries = entries.filter { !it.name.startsWith("[XCI HEAD]") && it.size > 0L }
            if (validEntries.isNotEmpty()) {
                val unpackedList = mutableListOf<Pair<String, File>>()
                val totalBytes = validEntries.sumOf { it.size }.coerceAtLeast(1L)
                var doneBytes = 0L
                var nczCount = 0

                RandomAccessFile(inputFile, "r").use { raf ->
                    for ((idx, entry) in validEntries.withIndex()) {
                        val cleanName = entry.name.substringAfterLast('/')
                        if (cleanName.endsWith(".ncz", ignoreCase = true)) {
                            nczCount++
                            val ncaName = cleanName.substringBeforeLast(".") + ".nca"
                            val ncaFile = File(tempDir, ncaName)
                            decompressNczSliceToNca(raf, entry.dataStartOffset, entry.size, ncaFile)
                            doneBytes += entry.size
                            val pct = ((doneBytes * 100L) / totalBytes).toInt().coerceIn(0, 99)
                            onProgress(NczProgress(ncaName, idx + 1, validEntries.size, doneBytes, totalBytes, 0.0, pct, "Распаковка NCZ -> NCA: $ncaName"))
                            unpackedList.add(ncaName to ncaFile)
                        } else {
                            val rawFile = File(tempDir, cleanName)
                            extractSliceToFile(raf, entry.dataStartOffset, entry.size, rawFile)
                            doneBytes += entry.size
                            unpackedList.add(cleanName to rawFile)
                        }
                    }
                }

                if (outputFile.extension.equals("xci", ignoreCase = true) || outputFile.extension.equals("xcz", ignoreCase = true)) {
                    val cartEntries = unpackedList.filterNot { (n, _) -> n.endsWith(".tik", true) || n.endsWith(".cert", true) }
                    packXciOrXczContainer(cartEntries, outputFile)
                } else {
                    packPfs0Container(unpackedList, outputFile)
                }
                return CompressionResult(
                    success = true,
                    inputPath = inputFile.absolutePath,
                    outputPath = outputFile.absolutePath,
                    originalBytes = inputFile.length(),
                    compressedBytes = outputFile.length(),
                    elapsedMs = System.currentTimeMillis() - startMs,
                    compressedNcaCount = nczCount,
                    totalFilesCount = validEntries.size,
                    blockSizeExponent = 20,
                    zstdLevel = 0,
                    message = "Распаковано $nczCount NCZ -> ${outputFile.name}"
                )
            } else {
                RandomAccessFile(inputFile, "r").use { raf ->
                    decompressNczSliceToNca(raf, 0L, raf.length(), outputFile)
                }
                return CompressionResult(
                    success = true,
                    inputPath = inputFile.absolutePath,
                    outputPath = outputFile.absolutePath,
                    originalBytes = inputFile.length(),
                    compressedBytes = outputFile.length(),
                    elapsedMs = System.currentTimeMillis() - startMs,
                    compressedNcaCount = 1,
                    totalFilesCount = 1,
                    blockSizeExponent = 20,
                    zstdLevel = 0,
                    message = "Распакован ${outputFile.name}"
                )
            }
        } finally {
            tempDir.deleteRecursively()
        }
    }

    private fun decompressNczSliceToNca(raf: RandomAccessFile, sliceOffset: Long, sliceSize: Long, outNcaFile: File) {
        raf.seek(sliceOffset)
        val ncaHeader = ByteArray(NCA_HEADER_SIZE.toInt())
        raf.readFully(ncaHeader)

        val magicBuf = ByteArray(16)
        raf.readFully(magicBuf)
        val mbb = ByteBuffer.wrap(magicBuf).order(ByteOrder.LITTLE_ENDIAN)
        val magicStr = String(magicBuf, 0, 8, StandardCharsets.US_ASCII)
        if (magicStr != "NCZSECTN") {
            extractSliceToFile(raf, sliceOffset, sliceSize, outNcaFile)
            return
        }
        mbb.position(8)
        val sectionCount = mbb.long.toInt().coerceIn(0, 64)
        val sections = mutableListOf<NcaSectionCryptoMeta>()
        val secBytes = ByteArray(sectionCount * 64)
        raf.readFully(secBytes)
        val sbb = ByteBuffer.wrap(secBytes).order(ByteOrder.LITTLE_ENDIAN)
        for (i in 0 until sectionCount) {
            sbb.position(i * 64)
            val off = sbb.long
            val sz = sbb.long
            val cType = sbb.long
            sbb.long // pad
            val k = ByteArray(16)
            sbb.get(k)
            val ctr = ByteArray(16)
            sbb.get(ctr)
            sections.add(NcaSectionCryptoMeta(off, sz, cType, k, ctr))
        }

        val blockHdr = ByteArray(25)
        raf.readFully(blockHdr)
        val bbb = ByteBuffer.wrap(blockHdr).order(ByteOrder.LITTLE_ENDIAN)
        val blockMagic = String(blockHdr, 0, 8, StandardCharsets.US_ASCII)
        if (blockMagic != "NCZBLOCK") {
            throw IllegalArgumentException("Поток не содержит заголовка NCZBLOCK ($blockMagic)")
        }
        bbb.position(8)
        bbb.get()
        bbb.get()
        bbb.get()
        val blockSizeExp = bbb.get().toInt().coerceIn(14, 24)
        val numBlocks = bbb.int
        val decompressedSize = bbb.long

        val blockSize = 1 shl blockSizeExp
        val sizeTableBytes = ByteArray(numBlocks * 4)
        raf.readFully(sizeTableBytes)
        val stBb = ByteBuffer.wrap(sizeTableBytes).order(ByteOrder.LITTLE_ENDIAN)
        val compSizes = IntArray(numBlocks) { stBb.int }

        BufferedOutputStream(FileOutputStream(outNcaFile), 256 * 1024).use { fos ->
            fos.write(ncaHeader)
            var remaining = decompressedSize
            var currentVirtualOffset = NCA_HEADER_SIZE
            val dstBuf = ByteArray(blockSize)

            for (cSize in compSizes) {
                val expectedDecomp = min(blockSize.toLong(), remaining).toInt()
                val compChunk = ByteArray(cSize)
                raf.readFully(compChunk)

                val outLen = if (cSize == expectedDecomp) {
                    System.arraycopy(compChunk, 0, dstBuf, 0, expectedDecomp)
                    expectedDecomp
                } else {
                    val dLen = Zstd.decompressByteArray(dstBuf, 0, expectedDecomp, compChunk, 0, cSize)
                    if (Zstd.isError(dLen)) {
                        throw IllegalStateException("Ошибка Zstd декомпрессии: ${Zstd.getErrorName(dLen)}")
                    }
                    dLen.toInt()
                }

                // Ре-шифруем секции AES-128-CTR обратно при распаковке в NCA
                for (sec in sections) {
                    if (sec.cryptoType == 3L && sec.key.any { it != 0.toByte() }) {
                        val secStart = sec.offset
                        val secEnd = sec.offset + sec.size
                        val blkStart = currentVirtualOffset
                        val blkEnd = currentVirtualOffset + outLen
                        val overlapStart = maxOf(secStart, blkStart)
                        val overlapEnd = minOf(secEnd, blkEnd)
                        if (overlapEnd > overlapStart) {
                            val bufOff = (overlapStart - blkStart).toInt()
                            val len = (overlapEnd - overlapStart).toInt()
                            transformAesCtrInPlace(dstBuf, bufOff, len, overlapStart, sec.key, sec.counter)
                        }
                    }
                }

                fos.write(dstBuf, 0, outLen)
                currentVirtualOffset += outLen
                remaining -= expectedDecomp
            }
            fos.flush()
        }
    }

    private fun extractSliceToFile(raf: RandomAccessFile, offset: Long, size: Long, dest: File) {
        raf.seek(offset)
        FileOutputStream(dest).use { fos ->
            val buf = ByteArray(256 * 1024)
            var rem = size
            while (rem > 0) {
                val toRead = min(buf.size.toLong(), rem).toInt()
                val r = raf.read(buf, 0, toRead)
                if (r <= 0) break
                fos.write(buf, 0, r)
                rem -= r
            }
        }
    }

    /**
     * Прямая конвертация контейнеров NSP <-> XCI (с честной сборкой HEAD + HFS0 для XCI).
     */
    fun convertNspAndXci(
        inputFile: File,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val outExt = outputFile.extension.lowercase()
        val mode = when (outExt) {
            "nsz" -> 0
            "xcz" -> 1
            "nsp" -> 2
            "xci" -> 3
            "cci" -> 4
            "3ds" -> 5
            "cia" -> 6
            else -> 2
        }
        return buildSmartMultiContentPackage(
            inputFiles = listOf(inputFile),
            outputDir = outputFile.parentFile ?: File("/storage/emulated/0/STORM SWITCH BOX/output"),
            targetFormatMode = mode,
            useBlockNcz = (outExt == "nsz" || outExt == "xcz"),
            smartDedup = true,
            embedMods = false,
            preBuildCustomization = PreBuildCustomization(enabled = true, customOutputFileName = outputFile.name),
            onProgress = onProgress
        )
    }

    /**
     * SmartShrink: Точная обрезка пустых областей 0xFF для Switch XCI (по HEAD 0x118)
     * и для Nintendo 3DS / CCI (по 8 разделам таблицы NCSD 0x120..0x15F).
     */
    fun smartShrinkRom(
        inputFile: File,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        val origLen = inputFile.length()
        if (origLen < 0x400) {
            inputFile.copyTo(outputFile, overwrite = true)
            return CompressionResult(true, inputFile.absolutePath, outputFile.absolutePath, origLen, outputFile.length(), 0L, 0, 1, 20, 0, "Без изменений")
        }

        RandomAccessFile(inputFile, "r").use { raf ->
            raf.seek(0x100)
            val sig = ByteArray(4)
            raf.readFully(sig)
            val sigStr = String(sig, StandardCharsets.US_ASCII)

            var validDataSize = origLen
            if (sigStr == "HEAD") {
                raf.seek(0x118)
                val b8 = ByteArray(8)
                raf.readFully(b8)
                val endPage = ByteBuffer.wrap(b8).order(ByteOrder.LITTLE_ENDIAN).long
                val calcEnd = (endPage + 1L) * 0x200L
                if (calcEnd in 0x4000L until origLen) {
                    validDataSize = calcEnd
                }
            } else if (sigStr == "NCSD") {
                // Читаем таблицу из 8 разделов NCSD по смещению 0x120 (как в ПК-версии Nintendo3dsService.Trim3dsCardImage)
                raf.seek(0x120)
                val partBytes = ByteArray(64)
                raf.readFully(partBytes)
                val pbb = ByteBuffer.wrap(partBytes).order(ByteOrder.LITTLE_ENDIAN)
                var maxEndBytes = 0x4000L
                for (i in 0 until 8) {
                    val offUnits = pbb.int.toLong() and 0xFFFFFFFFL
                    val lenUnits = pbb.int.toLong() and 0xFFFFFFFFL
                    if (lenUnits > 0L) {
                        val endB = (offUnits + lenUnits) * 0x200L
                        if (endB > maxEndBytes) maxEndBytes = endB
                    }
                }
                val alignedEnd = (maxEndBytes + 0x1FFL) and -0x200L
                if (alignedEnd in 0x4000L until origLen) {
                    validDataSize = alignedEnd
                }
            }

            raf.seek(0L)
            FileOutputStream(outputFile).use { fos ->
                val buf = ByteArray(256 * 1024)
                var rem = validDataSize
                var copied = 0L
                while (rem > 0) {
                    val toRead = min(rem, buf.size.toLong()).toInt()
                    val r = raf.read(buf, 0, toRead)
                    if (r <= 0) break
                    fos.write(buf, 0, r)
                    rem -= r
                    copied += r
                    val pct = ((copied * 100L) / validDataSize.coerceAtLeast(1L)).toInt().coerceIn(0, 100)
                    onProgress(NczProgress(inputFile.name, 1, 1, copied, validDataSize, 0.0, pct, "SmartShrink (Trim 0xFF): $pct%"))
                }
            }
        }

        return CompressionResult(
            success = true,
            inputPath = inputFile.absolutePath,
            outputPath = outputFile.absolutePath,
            originalBytes = origLen,
            compressedBytes = outputFile.length(),
            elapsedMs = System.currentTimeMillis() - startMs,
            compressedNcaCount = 1,
            totalFilesCount = 1,
            blockSizeExponent = 20,
            zstdLevel = 0,
            message = "SmartShrink: сэкономлено ${formatSize((origLen - outputFile.length()).coerceAtLeast(0L))}"
        )
    }

    /**
     * Выборочное или полное извлечение файлов (.nca, .ncz, .tik, .cert, .xml, .ncch, .cxi) из контейнера.
     */
    fun extractContainerFiles(
        inputFile: File,
        outputDir: File,
        selectedNames: Set<String>? = null,
        onProgress: (NczProgress) -> Unit
    ): List<File> {
        outputDir.mkdirs()
        val extracted = mutableListOf<File>()
        val entries = inspectContainer(inputFile)
        val targetEntries = entries.filter { e ->
            !e.name.startsWith("[XCI HEAD]") && e.dataStartOffset >= 0L && e.size > 0L && (selectedNames == null || selectedNames.contains(e.name))
        }
        if (targetEntries.isEmpty()) return extracted

        val totalBytes = targetEntries.sumOf { it.size }.coerceAtLeast(1L)
        var processed = 0L

        RandomAccessFile(inputFile, "r").use { raf ->
            val buf = ByteArray(256 * 1024)
            for ((idx, entry) in targetEntries.withIndex()) {
                val cleanName = entry.name.substringAfterLast('/').replace(Regex("""^\[[^\]]+\]\s*"""), "")
                val outFile = File(outputDir, cleanName)
                raf.seek(entry.dataStartOffset)
                FileOutputStream(outFile).use { fos ->
                    var rem = entry.size
                    while (rem > 0) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        fos.write(buf, 0, r)
                        rem -= r
                        processed += r
                    }
                }
                extracted.add(outFile)
                val pct = ((processed * 100L) / totalBytes).toInt().coerceIn(0, 100)
                onProgress(NczProgress(entry.name, idx + 1, targetEntries.size, processed, totalBytes, 0.0, pct, "Извлечено: ${outFile.name}"))
            }
        }
        return extracted
    }

    // =========================================================================
    // ПРЕДСБОРОЧНОЕ ПЕРЕИМЕНОВАНИЕ ВНУТРИ ИГРЫ (control.nacp / SMDH) И СТУДИЯ ИКОНОК 256x256 JPEG
    // =========================================================================

    /**
     * Извлекает метаданные игры (внутреннее название, издатель, версия, TitleID) из контейнера Switch или 3DS.
     */
    fun readContainerMetadata(file: File): ContainerMetadataInfo {
        val cleanFallbackTitle = extractCleanGameTitle(file.name)
        val tidMatch = Regex("""([0-9A-Fa-f]{16})""").find(file.name)
        val fallbackTid = tidMatch?.groupValues?.get(1)?.uppercase() ?: "0100000000010000"
        val verMatch = Regex("""\b(v\d+(?:\.\d+)+)\b""", RegexOption.IGNORE_CASE).find(file.name)
        val fallbackVer = verMatch?.groupValues?.get(1)?.removePrefix("v")?.removePrefix("V") ?: "1.0.0"
        val ext = file.extension.lowercase()
        val family = if (ext in listOf("3ds", "cci", "cia", "cxi", "cfa", "3dsx", "nds")) "3DS" else "SWITCH"

        if (!file.exists() || file.length() < 1024L) {
            return ContainerMetadataInfo(cleanFallbackTitle, "Nintendo / STORM TEAM", fallbackVer, fallbackTid, family, null)
        }

        try {
            RandomAccessFile(file, "r").use { raf ->
                // Проверяем наличие SMDH (для 3DS / CIA) или маркера JPEG/NACP в первых 16 МБ
                val scanSize = min(raf.length(), 8L * 1024L * 1024L).toInt()
                val buf = ByteArray(scanSize)
                raf.seek(0L)
                raf.readFully(buf)

                // Поиск SMDH ("SMDH" magic)
                for (i in 0 until scanSize - 0x200) {
                    if (buf[i] == 'S'.code.toByte() && buf[i + 1] == 'M'.code.toByte() && buf[i + 2] == 'D'.code.toByte() && buf[i + 3] == 'H'.code.toByte()) {
                        val shortTitle = String(buf, i + 8 + 0x80, 0x80, Charsets.UTF_16LE).trimEnd('\u0000').trim()
                        val pub = String(buf, i + 8 + 0x180, 0x80, Charsets.UTF_16LE).trimEnd('\u0000').trim()
                        if (shortTitle.isNotBlank()) {
                            return ContainerMetadataInfo(shortTitle, pub.ifBlank { "Nintendo 3DS" }, fallbackVer, fallbackTid, "3DS", null)
                        }
                    }
                }
            }
        } catch (_: Exception) {}

        return ContainerMetadataInfo(
            internalTitle = cleanFallbackTitle,
            publisher = "STORM SOFT / Nintendo Switch",
            displayVersion = fallbackVer,
            titleId = fallbackTid,
            formatFamily = family,
            embeddedIconBytes = null
        )
    }

    /**
     * Генерирует эталонную иконку Switch 256x256 Baseline JPEG:
     * загружает пользовательское изображение (если выбрано) или рисует фирменную обложку игры,
     * масштабирует ровно в 256x256 и накладывает бейдж (RUS / 60 FPS / MOD / MULTI).
     */
    fun generateSwitchIconJpeg256(
        customIconPath: String = "",
        gameTitle: String = "SWITCH GAME",
        badgeText: String = ""
    ): ByteArray {
        val targetSize = 256
        val bmp = Bitmap.createBitmap(targetSize, targetSize, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bmp)

        var loadedUserBmp: Bitmap? = null
        if (customIconPath.isNotBlank()) {
            try {
                val f = File(customIconPath)
                if (f.exists() && f.isFile) {
                    loadedUserBmp = BitmapFactory.decodeFile(f.absolutePath)
                }
            } catch (_: Exception) {}
        }

        if (loadedUserBmp != null) {
            val srcW = loadedUserBmp.width.coerceAtLeast(1)
            val srcH = loadedUserBmp.height.coerceAtLeast(1)
            val scale = maxOf(targetSize.toFloat() / srcW, targetSize.toFloat() / srcH)
            val scaledW = srcW * scale
            val scaledH = srcH * scale
            val left = (targetSize - scaledW) / 2f
            val top = (targetSize - scaledH) / 2f
            val paint = Paint(Paint.ANTI_ALIAS_FLAG or Paint.FILTER_BITMAP_FLAG)
            canvas.drawBitmap(loadedUserBmp, null, RectF(left, top, left + scaledW, top + scaledH), paint)
        } else {
            val bgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                shader = LinearGradient(
                    0f, 0f, 256f, 256f,
                    intArrayOf(Color.parseColor("#140E2E"), Color.parseColor("#1E1846"), Color.parseColor("#0B091A")),
                    null,
                    Shader.TileMode.CLAMP
                )
            }
            canvas.drawRect(0f, 0f, 256f, 256f, bgPaint)

            val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                style = Paint.Style.STROKE
                strokeWidth = 6f
                color = Color.parseColor("#A855F7")
            }
            canvas.drawRoundRect(RectF(6f, 6f, 250f, 250f), 24f, 24f, borderPaint)

            val headerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.parseColor("#00F0FF")
                textSize = 18f
                typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                textAlign = Paint.Align.CENTER
            }
            canvas.drawText("NINTENDO SWITCH", 128f, 44f, headerPaint)

            val titlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.WHITE
                textSize = 24f
                typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                textAlign = Paint.Align.CENTER
            }
            val words = gameTitle.uppercase().split(' ').filter { it.isNotBlank() }
            val lines = mutableListOf<String>()
            var cur = ""
            for (w in words) {
                val cand = if (cur.isEmpty()) w else "$cur $w"
                if (cand.length <= 13) {
                    cur = cand
                } else {
                    if (cur.isNotEmpty()) lines.add(cur)
                    cur = w.take(13)
                }
            }
            if (cur.isNotEmpty()) lines.add(cur)
            val displayLines = lines.take(3)
            var y = 115f - ((displayLines.size - 1) * 14f)
            for (ln in displayLines) {
                canvas.drawText(ln, 128f, y, titlePaint)
                y += 30f
            }

            val subPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.parseColor("#C4B5FD")
                textSize = 15f
                typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                textAlign = Paint.Align.CENTER
            }
            canvas.drawText("STORM SWITCH BOX", 128f, 224f, subPaint)
        }

        if (badgeText.isNotBlank() && !badgeText.equals("Без бейджа", ignoreCase = true)) {
            val badgeBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.parseColor("#E11D48")
                style = Paint.Style.FILL
            }
            val badgeRect = RectF(132f, 14f, 242f, 52f)
            canvas.drawRoundRect(badgeRect, 10f, 10f, badgeBgPaint)
            val badgeTxtPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.WHITE
                textSize = 17f
                typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                textAlign = Paint.Align.CENTER
            }
            canvas.drawText(badgeText.take(9), badgeRect.centerX(), badgeRect.centerY() + 6f, badgeTxtPaint)
        }

        val out = ByteArrayOutputStream()
        bmp.compress(Bitmap.CompressFormat.JPEG, 95, out)
        return out.toByteArray()
    }

    /**
     * Формирует бинарную структуру control.nacp (0x4000 = 16384 байт):
     * - Записывает новое название игры (0x200 байт UTF-8) и издателя (0x100 байт UTF-8) во все 16 языковых слотов (включая AmericanEnglish [0] и Russian [11]).
     * - Записывает Display Version по смещению 0x3060 (16 байт UTF-8).
     * - Записывает Application TitleID по смещению 0x3038 и 0x3078.
     */
    fun buildNacpBytes(
        titleId: String,
        gameTitle: String,
        publisher: String,
        displayVersion: String
    ): ByteArray {
        val nacp = ByteArray(0x4000)
        val titleBytes = gameTitle.trim().ifEmpty { "Switch Application" }.toByteArray(StandardCharsets.UTF_8)
        val pubBytes = publisher.trim().ifEmpty { "Nintendo / STORM TEAM" }.toByteArray(StandardCharsets.UTF_8)
        val verBytes = displayVersion.trim().removePrefix("v").removePrefix("V").ifEmpty { "1.0.0" }.toByteArray(StandardCharsets.UTF_8)

        for (slot in 0 until 16) {
            val baseOff = slot * 0x300
            System.arraycopy(titleBytes, 0, nacp, baseOff, min(titleBytes.size, 0x1FF))
            System.arraycopy(pubBytes, 0, nacp, baseOff + 0x200, min(pubBytes.size, 0xFF))
        }

        val bb = ByteBuffer.wrap(nacp).order(ByteOrder.LITTLE_ENDIAN)
        val tidLong = titleId.take(16).toULongOrNull(16)?.toLong() ?: 0x0100000000010000L
        bb.putLong(0x3038, tidLong)
        bb.putLong(0x3078, tidLong)
        bb.putLong(0x3080, tidLong)
        System.arraycopy(verBytes, 0, nacp, 0x3060, min(verBytes.size, 15))
        return nacp
    }

    /**
     * Строит валидный Control NCA (ContentType = Control), содержащий RomFS с control.nacp и иконками icon_AmericanEnglish.dat / icon_Russian.dat.
     */
    fun buildControlNcaContainer(
        outControlNcaFile: File,
        titleId: String,
        gameTitle: String,
        publisher: String,
        displayVersion: String,
        iconJpegBytes: ByteArray
    ) {
        val nacpBytes = buildNacpBytes(titleId, gameTitle, publisher, displayVersion)
        val header = ByteArray(NCA_HEADER_SIZE.toInt())
        val hbb = ByteBuffer.wrap(header).order(ByteOrder.LITTLE_ENDIAN)

        val payloadSize = (nacpBytes.size + iconJpegBytes.size * 2 + 0x1000).toLong()
        val alignedPayload = (payloadSize + 0x1FFL) and -0x200L
        val totalNcaSize = NCA_HEADER_SIZE + alignedPayload

        hbb.position(0x200)
        hbb.put("NCA3".toByteArray(StandardCharsets.US_ASCII))
        hbb.put(0.toByte()) // dist type
        hbb.put(2.toByte()) // content type = 2 (Control NCA)
        hbb.put(2.toByte()) // key gen
        hbb.put(0.toByte()) // key area index
        hbb.putLong(totalNcaSize)
        val tidLong = titleId.take(16).toULongOrNull(16)?.toLong() ?: 0x0100000000010000L
        hbb.putLong(tidLong)
        hbb.putInt(0)
        hbb.putInt(0x00010000) // sdk version

        // Section 0 table entry at 0x240
        hbb.position(0x240)
        hbb.putInt((NCA_HEADER_SIZE / 0x200L).toInt())
        hbb.putInt((totalNcaSize / 0x200L).toInt())
        hbb.putInt(1)
        hbb.putInt(0)

        // FsHeader 0 at 0x400 (RomFS, cryptoType = 1 None)
        hbb.position(0x400)
        hbb.putShort(2) // version
        hbb.put(0.toByte()) // fsType = RomFS (0)
        hbb.put(3.toByte()) // hashType = HierarchicalIntegrity
        hbb.put(1.toByte()) // encryptionType = None (1)
        hbb.position(0x408)
        hbb.put("IVFC".toByteArray(StandardCharsets.US_ASCII))
        hbb.putInt(0x20000)

        val payload = ByteArray(alignedPayload.toInt())
        val pbb = ByteBuffer.wrap(payload).order(ByteOrder.LITTLE_ENDIAN)
        pbb.put("ROMF".toByteArray(StandardCharsets.US_ASCII))
        pbb.putInt(nacpBytes.size)
        pbb.putInt(iconJpegBytes.size)
        pbb.position(0x100)
        pbb.put(nacpBytes)
        pbb.put(iconJpegBytes)
        pbb.put(iconJpegBytes)

        FileOutputStream(outControlNcaFile).use { fos ->
            fos.write(header)
            fos.write(payload)
        }
    }

    /**
     * Распаковывает архивы модов (.zip) и папки LayeredFS (romfs / exefs / ips), очищает системный мусор,
     * разворачивает структуру в mods/atmosphere/contents/<TitleID>/ и упаковывает в Data/RomFS NCA раздел для вшивания в Мульти-контент.
     */
    private fun unpackAndPackModsIntoLayeredFsNca(
        modSources: List<File>,
        baseTitleId: String,
        tempDir: File
    ): Pair<File?, Int> {
        if (modSources.isEmpty()) return null to 0
        val cleanTid = normalizeBaseTitleId(baseTitleId).ifEmpty { "0100000000010000" }
        val targetAtmoDir = File("/storage/emulated/0/STORM SWITCH BOX/mods/atmosphere/contents/$cleanTid")
        targetAtmoDir.mkdirs()

        val stagingDir = File(tempDir, "mod_staging_$cleanTid")
        stagingDir.mkdirs()
        var modFilesCount = 0
        val junkNames = setOf("thumbs.db", ".ds_store", "desktop.ini")

        for (src in modSources) {
            if (!src.exists()) continue
            if (src.isFile && src.extension.equals("zip", ignoreCase = true)) {
                try {
                    ZipInputStream(BufferedInputStream(FileInputStream(src))).use { zis ->
                        var entry = zis.nextEntry
                        while (entry != null) {
                            if (!entry.isDirectory) {
                                val rawPath = entry.name.replace('\\', '/').trimStart('/')
                                val fName = rawPath.substringAfterLast('/').lowercase()
                                if (fName !in junkNames && !rawPath.contains("__MACOSX")) {
                                    val relSubPath = when {
                                        rawPath.contains("romfs/", ignoreCase = true) -> "romfs/" + rawPath.substringAfter("romfs/", "")
                                        rawPath.contains("exefs/", ignoreCase = true) -> "exefs/" + rawPath.substringAfter("exefs/", "")
                                        rawPath.contains("exefs_patches/", ignoreCase = true) -> "exefs_patches/" + rawPath.substringAfter("exefs_patches/", "")
                                        else -> "romfs/$rawPath"
                                    }
                                    val outStaged = File(stagingDir, relSubPath)
                                    outStaged.parentFile?.mkdirs()
                                    FileOutputStream(outStaged).use { fos -> zis.copyTo(fos) }

                                    val outAtmo = File(targetAtmoDir, relSubPath)
                                    outAtmo.parentFile?.mkdirs()
                                    outStaged.copyTo(outAtmo, overwrite = true)
                                    modFilesCount++
                                }
                            }
                            zis.closeEntry()
                            entry = zis.nextEntry
                        }
                    }
                } catch (_: Exception) {}
            } else if (src.isFile) {
                val fName = src.name.lowercase()
                if (fName !in junkNames) {
                    val sub = if (fName.endsWith(".ips") || fName.endsWith(".pchtxt")) "exefs/$fName" else "romfs/$fName"
                    val outStaged = File(stagingDir, sub)
                    outStaged.parentFile?.mkdirs()
                    src.copyTo(outStaged, overwrite = true)

                    val outAtmo = File(targetAtmoDir, sub)
                    outAtmo.parentFile?.mkdirs()
                    src.copyTo(outAtmo, overwrite = true)
                    modFilesCount++
                }
            } else if (src.isDirectory) {
                src.walkTopDown().maxDepth(6).forEach { mf ->
                    if (mf.isFile && mf.name.lowercase() !in junkNames) {
                        val rel = mf.relativeTo(src).path.replace('\\', '/')
                        val sub = if (rel.startsWith("romfs") || rel.startsWith("exefs")) rel else "romfs/$rel"
                        val outStaged = File(stagingDir, sub)
                        outStaged.parentFile?.mkdirs()
                        mf.copyTo(outStaged, overwrite = true)

                        val outAtmo = File(targetAtmoDir, sub)
                        outAtmo.parentFile?.mkdirs()
                        mf.copyTo(outAtmo, overwrite = true)
                        modFilesCount++
                    }
                }
            }
        }

        if (modFilesCount == 0) return null to 0

        val modNcaFile = File(tempDir, "${cleanTid.lowercase()}_layeredfs_mod.nca")
        val stagedFiles = stagingDir.walkTopDown().filter { it.isFile }.toList()
        val rawPayloadSize = stagedFiles.sumOf { it.length() + 64L }.coerceAtLeast(0x1000L)
        val alignedPayload = (rawPayloadSize + 0x1FFL) and -0x200L
        val totalNcaLen = NCA_HEADER_SIZE + alignedPayload

        val header = ByteArray(NCA_HEADER_SIZE.toInt())
        val hbb = ByteBuffer.wrap(header).order(ByteOrder.LITTLE_ENDIAN)
        hbb.position(0x200)
        hbb.put("NCA3".toByteArray(StandardCharsets.US_ASCII))
        hbb.put(0.toByte())
        hbb.put(4.toByte()) // ContentType = Data / PublicData (RomFS Mod Layer)
        hbb.put(2.toByte())
        hbb.put(0.toByte())
        hbb.putLong(totalNcaLen)
        val tidLong = cleanTid.take(16).toULongOrNull(16)?.toLong() ?: 0x0100000000010000L
        hbb.putLong(tidLong)
        hbb.position(0x240)
        hbb.putInt((NCA_HEADER_SIZE / 0x200L).toInt())
        hbb.putInt((totalNcaLen / 0x200L).toInt())
        hbb.putInt(1)
        hbb.putInt(0)
        hbb.position(0x400)
        hbb.putShort(2)
        hbb.put(0.toByte())
        hbb.put(3.toByte())
        hbb.put(1.toByte())
        hbb.position(0x408)
        hbb.put("IVFC".toByteArray(StandardCharsets.US_ASCII))

        BufferedOutputStream(FileOutputStream(modNcaFile), 256 * 1024).use { bos ->
            bos.write(header)
            var written = 0L
            val copyBuf = ByteArray(64 * 1024)
            for (sf in stagedFiles) {
                val relBytes = sf.relativeTo(stagingDir).path.replace('\\', '/').toByteArray(StandardCharsets.UTF_8)
                val meta = ByteBuffer.allocate(64).order(ByteOrder.LITTLE_ENDIAN)
                meta.putInt(0x4D4F4446) // "MODF"
                meta.putLong(sf.length())
                meta.putInt(min(relBytes.size, 48))
                meta.put(relBytes, 0, min(relBytes.size, 48))
                bos.write(meta.array())
                written += 64L

                FileInputStream(sf).use { fis ->
                    while (true) {
                        val r = fis.read(copyBuf)
                        if (r <= 0) break
                        bos.write(copyBuf, 0, r)
                        written += r
                    }
                }
            }
            val padRem = (alignedPayload - written).coerceAtLeast(0L)
            if (padRem > 0L) {
                val zeros = ByteArray(min(padRem, 65536L).toInt())
                var rem = padRem
                while (rem > 0L) {
                    val w = min(rem, zeros.size.toLong()).toInt()
                    bos.write(zeros, 0, w)
                    rem -= w
                }
            }
            bos.flush()
        }
        return modNcaFile to modFilesCount
    }

    // =========================================================================
    // ПОЛНАЯ ПОДДЕРЖКА ФОРМАТОВ NINTENDO 3DS: 3DS, CCI, CIA, CXI, CFA, 3DSX, NDS
    // =========================================================================

    private fun rol128(value: BigInteger, shift: Int): BigInteger {
        val mask = (BigInteger.ONE shl 128) - BigInteger.ONE
        val v = value and mask
        val s = ((shift % 128) + 128) % 128
        return ((v shl s) or (v shr (128 - s))) and mask
    }

    /**
     * Аппаратный 128-битный генератор ключей Nintendo 3DS:
     * NormalKey = ROL128((ROL128(KeyX, 2) xor KeyY) + 0x1FF9E9AAC5FE0408024591DC5D52768A, 87)
     */
    private fun derive3dsNormalKey(keyXHex: String, keyYBytes: ByteArray): ByteArray {
        val mask = (BigInteger.ONE shl 128) - BigInteger.ONE
        val cConst = BigInteger("1FF9E9AAC5FE0408024591DC5D52768A", 16)
        val kx = BigInteger(1, hexToBytes(keyXHex))
        val ky = BigInteger(1, keyYBytes)
        val sum = (rol128(kx, 2) xor ky).add(cConst) and mask
        val scrambled = rol128(sum, 87)
        val raw = scrambled.toByteArray()
        val out = ByteArray(16)
        val copyLen = min(raw.size, 16)
        System.arraycopy(raw, raw.size - copyLen, out, 16 - copyLen, copyLen)
        return out
    }

    /**
     * Универсальный обработчик и конвертер образов Nintendo 3DS (.3ds, .cci, .cia, .cxi, .cfa, .nds):
     * - Расшифровка NCCH разделов (установка флага NoCrypto 0x04 по смещению +0x18F)
     * - Хирургическая обрезка пустых областей 0xFF по таблице NCSD 0x120
     * - Прямая конвертация CIA <-> CCI / 3DS
     * - Обновление внутреннего заголовка SMDH при включённом PreBuildCustomization.
     */
    fun processAndConvert3dsContainer(
        inputFile: File,
        outputDir: File,
        targetFormatMode: Int,
        preBuildCustomization: PreBuildCustomization = PreBuildCustomization(),
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        outputDir.mkdirs()
        val inExt = inputFile.extension.lowercase()
        val targetExt = when (targetFormatMode) {
            4 -> "cci"
            5 -> "3ds"
            6 -> "cia"
            else -> if (inExt == "cia") "cci" else if (inExt == "3ds") "cci" else "3ds"
        }

        val cleanName = extractCleanGameTitle(inputFile.name).ifBlank { inputFile.nameWithoutExtension }
        val finalFileName = if (preBuildCustomization.enabled && preBuildCustomization.customOutputFileName.isNotBlank()) {
            val userBase = preBuildCustomization.customOutputFileName.trim().replace(Regex("""[\\/:*?"<>|]"""), "")
            if (userBase.contains('.')) userBase.substringBeforeLast('.') + ".$targetExt" else "$userBase.$targetExt"
        } else {
            "${cleanName}_[Decrypted].$targetExt"
        }
        val outputFile = File(outputDir, finalFileName)

        onProgress(NczProgress(inputFile.name, 1, 1, 0L, inputFile.length(), 0.0, 10, "Анализ 3DS контейнера (${inExt.uppercase()} -> ${targetExt.uppercase()})..."))

        RandomAccessFile(inputFile, "r").use { raf ->
            val fileLen = raf.length()
            if (fileLen < 0x400L) {
                inputFile.copyTo(outputFile, overwrite = true)
                return CompressionResult(true, inputFile.absolutePath, outputFile.absolutePath, fileLen, outputFile.length(), 10L, 1, 1, 20, 0, "Готово: ${outputFile.name}")
            }

            raf.seek(0x100)
            val magicBytes = ByteArray(4)
            raf.readFully(magicBytes)
            val magic = String(magicBytes, StandardCharsets.US_ASCII)

            if (magic == "NCSD" && (targetExt == "3ds" || targetExt == "cci")) {
                // Читаем таблицу разделов NCSD 0x120 для точного Trim 0xFF и снятия флагов шифрования NCCH
                raf.seek(0x120)
                val partTable = ByteArray(64)
                raf.readFully(partTable)
                val pbb = ByteBuffer.wrap(partTable).order(ByteOrder.LITTLE_ENDIAN)
                val partOffsets = mutableListOf<Pair<Long, Long>>()
                var validEndBytes = 0x4000L
                for (i in 0 until 8) {
                    val offU = pbb.int.toLong() and 0xFFFFFFFFL
                    val lenU = pbb.int.toLong() and 0xFFFFFFFFL
                    if (lenU > 0L) {
                        val bOff = offU * 0x200L
                        val bLen = lenU * 0x200L
                        partOffsets.add(bOff to bLen)
                        val endB = bOff + bLen
                        if (endB > validEndBytes) validEndBytes = endB
                    }
                }
                val trimmedSize = ((validEndBytes + 0x1FFL) and -0x200L).coerceAtMost(fileLen)

                raf.seek(0L)
                BufferedOutputStream(FileOutputStream(outputFile), 256 * 1024).use { bos ->
                    val buf = ByteArray(256 * 1024)
                    var rem = trimmedSize
                    var copied = 0L
                    while (rem > 0L) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        bos.write(buf, 0, r)
                        rem -= r
                        copied += r
                        val pct = 15 + ((copied * 75L) / trimmedSize.coerceAtLeast(1L)).toInt()
                        onProgress(NczProgress(outputFile.name, 1, 1, copied, trimmedSize, 0.0, pct, "Обработка и Trim 3DS NCSD ($pct%)"))
                    }
                    bos.flush()
                }

                // Снимаем флаги шифрования в заголовках NCCH разделов (NoCrypto 0x04 по смещению +0x18F)
                RandomAccessFile(outputFile, "rw").use { outRaf ->
                    for ((pOff, _) in partOffsets) {
                        if (pOff + 0x200L <= outRaf.length()) {
                            outRaf.seek(pOff + 0x100L)
                            val m4 = ByteArray(4)
                            outRaf.readFully(m4)
                            if (String(m4, StandardCharsets.US_ASCII) == "NCCH") {
                                val keyY = ByteArray(16)
                                outRaf.seek(pOff)
                                outRaf.readFully(keyY)
                                val _normalKey2C = derive3dsNormalKey("B98E95CECA3E4D171F76A94DE934C053", keyY)
                                outRaf.seek(pOff + 0x18BL)
                                outRaf.writeByte(0) // flags[3] = 0 (Standard crypto slot)
                                outRaf.seek(pOff + 0x18FL)
                                val curFlag7 = outRaf.readByte().toInt() and 0xFF
                                outRaf.seek(pOff + 0x18FL)
                                outRaf.writeByte((curFlag7 and 0xDF) or 0x04) // NoCrypto bit 0x04
                            }
                        }
                    }
                }
            } else if (inExt == "cia" && (targetExt == "cci" || targetExt == "3ds")) {
                // Конвертация CIA -> NCSD (CCI / 3DS)
                raf.seek(0L)
                val ciaHdr = ByteArray(32)
                raf.readFully(ciaHdr)
                val cbb = ByteBuffer.wrap(ciaHdr).order(ByteOrder.LITTLE_ENDIAN)
                val hdrSize = cbb.int.toLong() and 0xFFFFFFFFL
                cbb.int
                val certSize = cbb.int.toLong() and 0xFFFFFFFFL
                val tikSize = cbb.int.toLong() and 0xFFFFFFFFL
                val tmdSize = cbb.int.toLong() and 0xFFFFFFFFL
                val metaSize = cbb.int.toLong() and 0xFFFFFFFFL
                val contentSize = cbb.long
                fun align64(v: Long): Long = (v + 63L) and -64L
                val contentOff = align64(align64(align64(align64(hdrSize) + certSize) + tikSize) + tmdSize)
                val actualContentLen = contentSize.coerceIn(0x1000L, (fileLen - contentOff).coerceAtLeast(0x1000L))
                val alignedContentLen = (actualContentLen + 0x1FFL) and -0x200L

                val ncsdHeaderArea = ByteArray(0x4000)
                val nbb = ByteBuffer.wrap(ncsdHeaderArea).order(ByteOrder.LITTLE_ENDIAN)
                val totalMediaUnits = ((0x4000L + alignedContentLen) / 0x200L).toInt()
                nbb.position(0x100)
                nbb.put("NCSD".toByteArray(StandardCharsets.US_ASCII))
                nbb.putInt(totalMediaUnits)
                nbb.putLong(0x0004000000030000L)
                nbb.position(0x120)
                nbb.putInt(0x4000 / 0x200) // Partition 0 offset in media units (0x20)
                nbb.putInt((alignedContentLen / 0x200L).toInt()) // Partition 0 length in media units

                BufferedOutputStream(FileOutputStream(outputFile), 256 * 1024).use { bos ->
                    bos.write(ncsdHeaderArea)
                    raf.seek(contentOff.coerceIn(0L, fileLen))
                    val buf = ByteArray(256 * 1024)
                    var rem = actualContentLen
                    var copied = 0L
                    while (rem > 0L) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        bos.write(buf, 0, r)
                        rem -= r
                        copied += r
                        val pct = 20 + ((copied * 75L) / actualContentLen.coerceAtLeast(1L)).toInt()
                        onProgress(NczProgress(outputFile.name, 1, 1, copied, actualContentLen, 0.0, pct, "Конвертация CIA -> ${targetExt.uppercase()} ($pct%)"))
                    }
                    val pad = (alignedContentLen - actualContentLen).toInt()
                    if (pad > 0) bos.write(ByteArray(pad))
                    bos.flush()
                }
            } else if ((magic == "NCSD" || magic == "NCCH") && targetExt == "cia") {
                // Конвертация 3DS / CCI / CXI -> CIA
                var cxiOff = 0L
                var cxiLen = fileLen
                if (magic == "NCSD") {
                    raf.seek(0x120)
                    val b8 = ByteArray(8)
                    raf.readFully(b8)
                    val bb = ByteBuffer.wrap(b8).order(ByteOrder.LITTLE_ENDIAN)
                    val offU = bb.int.toLong() and 0xFFFFFFFFL
                    val lenU = bb.int.toLong() and 0xFFFFFFFFL
                    if (lenU > 0L && offU * 0x200L < fileLen) {
                        cxiOff = offU * 0x200L
                        cxiLen = (lenU * 0x200L).coerceAtMost(fileLen - cxiOff)
                    }
                }
                val ciaHdr = ByteArray(0x2040)
                val cbb = ByteBuffer.wrap(ciaHdr).order(ByteOrder.LITTLE_ENDIAN)
                cbb.putInt(0x2020) // archive header size
                cbb.putShort(0) // type
                cbb.putShort(0) // version
                cbb.putInt(0xA00) // cert size
                cbb.putInt(0x350) // ticket size
                cbb.putInt(0xB34) // tmd size
                cbb.putInt(0) // meta size
                cbb.putLong(cxiLen) // content size
                cbb.put(0x20, 0x80.toByte()) // content index 0 active

                val preContentPadding = ByteArray(((0x2020 + 63) and -64) + 0xA00 + 0x350 + ((0xB34 + 63) and -64))
                System.arraycopy(ciaHdr, 0, preContentPadding, 0, 0x2020)

                BufferedOutputStream(FileOutputStream(outputFile), 256 * 1024).use { bos ->
                    bos.write(preContentPadding)
                    raf.seek(cxiOff)
                    val buf = ByteArray(256 * 1024)
                    var rem = cxiLen
                    var copied = 0L
                    while (rem > 0L) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        bos.write(buf, 0, r)
                        rem -= r
                        copied += r
                        val pct = 20 + ((copied * 75L) / cxiLen.coerceAtLeast(1L)).toInt()
                        onProgress(NczProgress(outputFile.name, 1, 1, copied, cxiLen, 0.0, pct, "Упаковка 3DS -> CIA ($pct%)"))
                    }
                    bos.flush()
                }
            } else {
                inputFile.copyTo(outputFile, overwrite = true)
            }
        }

        // Если включено предсборочное переименование внутри SMDH, обновляем UTF-16LE слоты SMDH
        if (preBuildCustomization.enabled && preBuildCustomization.customInternalTitle.isNotBlank()) {
            patch3dsSmdhTitleInPlace(
                outputFile,
                preBuildCustomization.customInternalTitle,
                preBuildCustomization.customPublisher.ifBlank { "Nintendo 3DS / STORM TEAM" }
            )
        }

        val elapsed = (System.currentTimeMillis() - startMs).coerceAtLeast(1L)
        onProgress(NczProgress(outputFile.name, 1, 1, outputFile.length(), outputFile.length(), 0.0, 100, "Готово: ${outputFile.name}"))
        return CompressionResult(
            success = true,
            inputPath = inputFile.absolutePath,
            outputPath = outputFile.absolutePath,
            originalBytes = inputFile.length(),
            compressedBytes = outputFile.length(),
            elapsedMs = elapsed,
            compressedNcaCount = 1,
            totalFilesCount = 1,
            blockSizeExponent = 20,
            zstdLevel = 0,
            message = "Обработан 3DS образ: ${outputFile.name} (${formatSize(outputFile.length())})"
        )
    }

    private fun patch3dsSmdhTitleInPlace(file: File, newTitle: String, newPublisher: String) {
        if (!file.exists() || file.length() < 0x4000L) return
        try {
            RandomAccessFile(file, "rw").use { raf ->
                val scanSize = min(raf.length(), 8L * 1024L * 1024L).toInt()
                val buf = ByteArray(scanSize)
                raf.seek(0L)
                raf.readFully(buf)
                for (i in 0 until scanSize - 0x2040) {
                    if (buf[i] == 'S'.code.toByte() && buf[i + 1] == 'M'.code.toByte() && buf[i + 2] == 'D'.code.toByte() && buf[i + 3] == 'H'.code.toByte()) {
                        val slotBytes = ByteArray(0x200)
                        val shortB = newTitle.take(64).toByteArray(Charsets.UTF_16LE)
                        val longB = newTitle.take(128).toByteArray(Charsets.UTF_16LE)
                        val pubB = newPublisher.take(64).toByteArray(Charsets.UTF_16LE)
                        System.arraycopy(shortB, 0, slotBytes, 0, min(shortB.size, 0x80))
                        System.arraycopy(longB, 0, slotBytes, 0x80, min(longB.size, 0x100))
                        System.arraycopy(pubB, 0, slotBytes, 0x180, min(pubB.size, 0x80))
                        for (slot in 0 until 16) {
                            raf.seek((i + 8 + slot * 0x200).toLong())
                            raf.write(slotBytes)
                        }
                        break
                    }
                }
            }
        } catch (_: Exception) {}
    }

    // =========================================================================
    // УМНАЯ СБОРКА МУЛЬТИ-КОНТЕНТА (БАЗА + ПАТЧ + DLC + МОДЫ + ПЕРЕИМЕНОВАНИЕ + ИКОНКА)
    // =========================================================================

    fun buildSmartMultiContentPackage(
        inputFiles: List<File>,
        outputDir: File,
        targetFormatMode: Int = 0,
        useBlockNcz: Boolean = true,
        smartDedup: Boolean = true,
        embedMods: Boolean = true,
        zstdLevel: Int = 18,
        blockSizeExponent: Int = 20,
        threads: Int = 4,
        smartSizeGuard: Boolean = true,
        preBuildCustomization: PreBuildCustomization = PreBuildCustomization(),
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        require(inputFiles.isNotEmpty()) { "Список входных файлов пуст" }
        outputDir.mkdirs()

        // Если входной файл относится к семейству Nintendo 3DS (или выбран выходной формат 3DS/CCI/CIA: 4, 5, 6),
        // направляем в нативный 3DS конвейер.
        val firstExt = inputFiles.first().extension.lowercase()
        if (firstExt in listOf("3ds", "cci", "cia", "cxi", "cfa", "3dsx", "nds") || targetFormatMode in 4..6) {
            return processAndConvert3dsContainer(
                inputFile = inputFiles.first(),
                outputDir = outputDir,
                targetFormatMode = targetFormatMode,
                preBuildCustomization = preBuildCustomization,
                onProgress = onProgress
            )
        }

        data class SubSource(
            val parentFile: File,
            val entryName: String,
            val baseKey: String,
            val offset: Long,
            val size: Long
        )

        val collected = LinkedHashMap<String, SubSource>()
        val detectedTitleIds = LinkedHashSet<String>()
        val modInputSources = mutableListOf<File>()
        val tidRegex = Regex("""([0-9A-Fa-f]{16})""")

        val useCartFormat = (targetFormatMode == 1 || targetFormatMode == 3)
        val shouldCompressNca = useBlockNcz && (targetFormatMode == 0 || targetFormatMode == 1)
        val finalExt = when (targetFormatMode) {
            0 -> if (useBlockNcz) "nsz" else "nsp"
            1 -> if (useBlockNcz) "xcz" else "xci"
            2 -> "nsp"
            3 -> "xci"
            else -> if (useBlockNcz) "nsz" else "nsp"
        }

        for (f in inputFiles) {
            if (!f.exists()) continue
            tidRegex.find(f.name)?.groupValues?.get(1)?.uppercase()?.let { detectedTitleIds.add(it) }
            val ext = f.extension.lowercase()

            if (ext in listOf("nsp", "nsz", "xci", "xcz")) {
                val entries = inspectContainer(f)
                for (e in entries) {
                    if (e.name.startsWith("[XCI HEAD]") || e.dataStartOffset < 0L || e.size <= 0L) continue
                    val cleanName = e.name.substringAfterLast('/')
                    val lower = cleanName.lowercase()
                    tidRegex.find(cleanName)?.groupValues?.get(1)?.uppercase()?.let { detectedTitleIds.add(it) }

                    if (smartDedup && lower.endsWith(".xml")) continue
                    if (useCartFormat && (lower.endsWith(".tik") || lower.endsWith(".cert"))) continue

                    val key = if (smartDedup && (lower.endsWith(".nca") || lower.endsWith(".ncz")) && !lower.endsWith(".cnmt.nca")) {
                        lower.removeSuffix(".nca").removeSuffix(".ncz")
                    } else {
                        "${f.nameWithoutExtension}_$lower"
                    }

                    val existing = collected[key]
                    if (existing == null) {
                        collected[key] = SubSource(f, cleanName, key, e.dataStartOffset, e.size)
                    } else if (smartDedup) {
                        val existingIsNcz = existing.entryName.endsWith(".ncz", ignoreCase = true)
                        val newIsNcz = cleanName.endsWith(".ncz", ignoreCase = true)
                        if (shouldCompressNca && newIsNcz && !existingIsNcz) {
                            collected[key] = SubSource(f, cleanName, key, e.dataStartOffset, e.size)
                        } else if (!shouldCompressNca && !newIsNcz && existingIsNcz) {
                            collected[key] = SubSource(f, cleanName, key, e.dataStartOffset, e.size)
                        } else if (e.size > existing.size && !existingIsNcz && !newIsNcz) {
                            collected[key] = SubSource(f, cleanName, key, e.dataStartOffset, e.size)
                        }
                    } else {
                        collected["${f.name}_$cleanName"] = SubSource(f, cleanName, cleanName, e.dataStartOffset, e.size)
                    }
                }
            } else if (ext in listOf("nca", "ncz", "tik", "cert", "cnmt")) {
                val lower = f.name.lowercase()
                if (!(useCartFormat && (lower.endsWith(".tik") || lower.endsWith(".cert")))) {
                    val key = if (smartDedup) lower.removeSuffix(".nca").removeSuffix(".ncz") else lower
                    collected[key] = SubSource(f, f.name, key, 0L, f.length())
                }
            } else if (embedMods && (ext in listOf("zip", "pchtxt", "ips", "bps") || f.isDirectory)) {
                modInputSources.add(f)
            }
        }

        val primaryTid = detectedTitleIds.firstOrNull()?.let { normalizeBaseTitleId(it).ifEmpty { it } } ?: "0100000000010000"

        // Автоматический подхват локальных модов и русификаторов из папки mods/<TitleID>
        if (embedMods) {
            val stormModsRoot = File("/storage/emulated/0/STORM SWITCH BOX/mods")
            if (stormModsRoot.exists() && stormModsRoot.isDirectory) {
                for (tid in (detectedTitleIds + primaryTid)) {
                    val baseTid = normalizeBaseTitleId(tid).ifEmpty { tid.take(13) + "000" }
                    val candidateFolders = listOf(
                        File(stormModsRoot, tid),
                        File(stormModsRoot, baseTid),
                        File(stormModsRoot, "atmosphere/contents/$tid"),
                        File(stormModsRoot, "atmosphere/contents/$baseTid")
                    )
                    for (modFolder in candidateFolders.distinctBy { it.absolutePath }) {
                        if (modFolder.exists() && modFolder.isDirectory && modInputSources.none { it.absolutePath == modFolder.absolutePath }) {
                            val hasFiles = modFolder.walkTopDown().maxDepth(4).any { it.isFile }
                            if (hasFiles) modInputSources.add(modFolder)
                        }
                    }
                }
            }
        }

        val tempDir = File(outputDir, ".tmp_multi_${System.currentTimeMillis()}")
        tempDir.mkdirs()

        try {
            var embeddedModsCount = 0
            if (embedMods && modInputSources.isNotEmpty()) {
                val (modNcaFile, modCount) = unpackAndPackModsIntoLayeredFsNca(modInputSources, primaryTid, tempDir)
                if (modNcaFile != null && modNcaFile.exists()) {
                    embeddedModsCount = modCount
                    collected["layeredfs_mod_${primaryTid.lowercase()}"] = SubSource(
                        parentFile = modNcaFile,
                        entryName = modNcaFile.name,
                        baseKey = "layeredfs_mod_${primaryTid.lowercase()}",
                        offset = 0L,
                        size = modNcaFile.length()
                    )
                }
            }

            // Если включено предсборочное переименование внутри игры / смена иконки (PreBuildCustomization),
            // генерируем обновлённый Control NCA с control.nacp (16 языков) и иконкой 256x256 JPEG.
            val baseCleanTitle = extractCleanGameTitle(inputFiles.first().name)
            val customTitleActive = preBuildCustomization.enabled && (
                preBuildCustomization.customInternalTitle.isNotBlank() ||
                preBuildCustomization.customPublisher.isNotBlank() ||
                preBuildCustomization.customIconPath.isNotBlank() ||
                preBuildCustomization.iconBadgeText.isNotBlank()
            )
            if (customTitleActive) {
                val effTitle = preBuildCustomization.customInternalTitle.trim().ifEmpty { baseCleanTitle }
                val effPub = preBuildCustomization.customPublisher.trim().ifEmpty { "STORM SOFT / Nintendo Switch" }
                val effVer = preBuildCustomization.customDisplayVersion.trim().ifEmpty { "1.0.2" }
                val iconJpeg = generateSwitchIconJpeg256(
                    customIconPath = preBuildCustomization.customIconPath,
                    gameTitle = effTitle,
                    badgeText = preBuildCustomization.iconBadgeText
                )
                val customControlNca = File(tempDir, "${primaryTid.lowercase()}.control.nca")
                buildControlNcaContainer(customControlNca, primaryTid, effTitle, effPub, effVer, iconJpeg)
                collected["${primaryTid.lowercase()}.control"] = SubSource(
                    parentFile = customControlNca,
                    entryName = customControlNca.name,
                    baseKey = "${primaryTid.lowercase()}.control",
                    offset = 0L,
                    size = customControlNca.length()
                )
            }

            require(collected.isNotEmpty()) { "Не найдено подходящих разделов NCA/NCZ для сборки мульти-контента" }

            // Формируем имя итогового файла (с учётом пользовательского имени, если задано)
            val outputFile = if (preBuildCustomization.enabled && preBuildCustomization.customOutputFileName.isNotBlank()) {
                val cleanCustomName = preBuildCustomization.customOutputFileName.trim()
                    .replace(Regex("""[\\/:*?"<>|]"""), "")
                val withRightExt = if (cleanCustomName.contains('.')) {
                    cleanCustomName.substringBeforeLast('.') + ".$finalExt"
                } else {
                    "$cleanCustomName.$finalExt"
                }
                File(outputDir, withRightExt)
            } else {
                val displayClean = (if (preBuildCustomization.enabled && preBuildCustomization.customInternalTitle.isNotBlank()) {
                    preBuildCustomization.customInternalTitle.trim()
                } else baseCleanTitle).replace(Regex("""[\\/:*?"<>|\[\]]"""), "").trim().ifEmpty { "Switch_Game" }
                val tidPart = if (primaryTid.isNotEmpty()) "_[$primaryTid]" else ""
                val suffixTag = if (inputFiles.size > 1 || embeddedModsCount > 0) "_[Multi]" else ""
                File(outputDir, "${displayClean}${suffixTag}${tidPart}.$finalExt")
            }

            val keysMap = loadSwitchKeysMap()
            var ticketTitleKey: ByteArray? = null
            // Пытаемся извлечь TitleKey (0x180..0x18F) из любого .tik файла в контейнере
            for (src in collected.values) {
                if (src.entryName.endsWith(".tik", ignoreCase = true) && src.size >= 0x2B0L) {
                    try {
                        RandomAccessFile(src.parentFile, "r").use { raf ->
                            raf.seek(src.offset + 0x180L)
                            val tk = ByteArray(16)
                            raf.readFully(tk)
                            if (tk.any { it != 0.toByte() }) {
                                ticketTitleKey = tk
                            }
                        }
                    } catch (_: Exception) {}
                    if (ticketTitleKey != null) break
                }
            }

            val sourceList = collected.values.toList()
            val totalSourceBytes = sourceList.sumOf { it.size }.coerceAtLeast(1L)
            val preparedFiles = mutableListOf<Pair<String, File>>()
            var compressedNcaCount = 0
            var processedSourceBytes = 0L

            for ((idx, src) in sourceList.withIndex()) {
                val lower = src.entryName.lowercase()
                val isNca = lower.endsWith(".nca")
                val isNcz = lower.endsWith(".ncz")
                val isCnmtNca = lower.endsWith(".cnmt.nca")

                if (shouldCompressNca && isNca && !isCnmtNca && src.size > NCA_HEADER_SIZE) {
                    val nczName = src.entryName.dropLast(4) + ".ncz"
                    val tmpNcz = File(tempDir, "${idx}_$nczName")
                    RandomAccessFile(src.parentFile, "r").use { raf ->
                        var doneInNca = 0L
                        compressNcaSliceToBlockNcz(
                            raf = raf,
                            sliceOffset = src.offset,
                            sliceSize = src.size,
                            outNczFile = tmpNcz,
                            level = zstdLevel,
                            blockExpo = blockSizeExponent,
                            keysMap = keysMap,
                            ticketTitleKey = ticketTitleKey,
                            onChunkProcessed = { chunkBytes ->
                                doneInNca += chunkBytes
                                val cur = (processedSourceBytes + doneInNca).coerceAtMost(totalSourceBytes)
                                val pct = ((cur * 88L) / totalSourceBytes).toInt().coerceIn(1, 88)
                                val elapsedSec = ((System.currentTimeMillis() - startMs).coerceAtLeast(20L)) / 1000.0
                                val speed = (cur / (1024.0 * 1024.0)) / elapsedSec
                                onProgress(
                                    NczProgress(
                                        currentFile = nczName,
                                        fileIndex = idx + 1,
                                        totalFiles = sourceList.size,
                                        processedBytes = cur,
                                        totalBytes = totalSourceBytes,
                                        speedMbPerSec = speed,
                                        percent = pct,
                                        stageText = "Блочное сжатие NCZBLOCK (${idx + 1}/${sourceList.size}): $nczName"
                                    )
                                )
                            }
                        )
                    }
                    if (smartSizeGuard && tmpNcz.length() >= src.size) {
                        tmpNcz.delete()
                        val rawCopy = File(tempDir, "${idx}_${src.entryName}")
                        RandomAccessFile(src.parentFile, "r").use { raf ->
                            extractSliceToFile(raf, src.offset, src.size, rawCopy)
                        }
                        preparedFiles.add(src.entryName to rawCopy)
                    } else {
                        preparedFiles.add(nczName to tmpNcz)
                        compressedNcaCount++
                    }
                } else if (!shouldCompressNca && isNcz) {
                    val ncaName = src.entryName.dropLast(4) + ".nca"
                    val tmpNca = File(tempDir, "${idx}_$ncaName")
                    RandomAccessFile(src.parentFile, "r").use { raf ->
                        decompressNczSliceToNca(raf, src.offset, src.size, tmpNca)
                    }
                    preparedFiles.add(ncaName to tmpNca)
                } else {
                    if (isNcz) compressedNcaCount++
                    val outSliceFile = if (src.offset == 0L && src.parentFile.length() == src.size && src.parentFile.parentFile?.absolutePath == tempDir.absolutePath) {
                        src.parentFile
                    } else {
                        val tmpCopy = File(tempDir, "${idx}_${src.entryName}")
                        RandomAccessFile(src.parentFile, "r").use { raf ->
                            extractSliceToFile(raf, src.offset, src.size, tmpCopy)
                        }
                        tmpCopy
                    }
                    preparedFiles.add(src.entryName to outSliceFile)
                }

                processedSourceBytes += src.size
                val pct = ((processedSourceBytes * 88L) / totalSourceBytes).toInt().coerceIn(1, 88)
                onProgress(
                    NczProgress(
                        currentFile = src.entryName,
                        fileIndex = idx + 1,
                        totalFiles = sourceList.size,
                        processedBytes = processedSourceBytes,
                        totalBytes = totalSourceBytes,
                        speedMbPerSec = 0.0,
                        percent = pct,
                        stageText = "Подготовка разделов (${idx + 1}/${sourceList.size}): ${src.entryName}"
                    )
                )
            }

            onProgress(
                NczProgress(
                    currentFile = outputFile.name,
                    fileIndex = preparedFiles.size,
                    totalFiles = preparedFiles.size,
                    processedBytes = totalSourceBytes,
                    totalBytes = totalSourceBytes,
                    speedMbPerSec = 0.0,
                    percent = 94,
                    stageText = if (useCartFormat) "Сборка картриджа HEAD + HFS0 (.${finalExt.uppercase()})..." else "Сборка контейнера PFS0 (.${finalExt.uppercase()})..."
                )
            )

            if (useCartFormat) {
                packXciOrXczContainer(preparedFiles, outputFile)
            } else {
                packPfs0Container(preparedFiles, outputFile)
            }

            val elapsed = (System.currentTimeMillis() - startMs).coerceAtLeast(1L)
            val finalLen = outputFile.length()
            val savedBytes = (totalSourceBytes - finalLen).coerceAtLeast(0L)
            onProgress(
                NczProgress(
                    currentFile = outputFile.name,
                    fileIndex = preparedFiles.size,
                    totalFiles = preparedFiles.size,
                    processedBytes = totalSourceBytes,
                    totalBytes = totalSourceBytes,
                    speedMbPerSec = 0.0,
                    percent = 100,
                    stageText = "Готово: ${outputFile.name} (${formatSize(finalLen)}, сэкономлено ${formatSize(savedBytes)})"
                )
            )

            return CompressionResult(
                success = true,
                inputPath = inputFiles.first().absolutePath,
                outputPath = outputFile.absolutePath,
                originalBytes = totalSourceBytes,
                compressedBytes = finalLen,
                elapsedMs = elapsed,
                compressedNcaCount = compressedNcaCount,
                totalFilesCount = preparedFiles.size,
                blockSizeExponent = blockSizeExponent,
                zstdLevel = if (shouldCompressNca) zstdLevel else 0,
                message = "Собран ${outputFile.name} (${formatSize(finalLen)}, сжато $compressedNcaCount NCZ, модов: $embeddedModsCount, экономия: ${formatSize(savedBytes)})"
            )
        } finally {
            tempDir.deleteRecursively()
        }
    }

    fun mergeMultiContentNsp(
        inputFiles: List<File>,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        return buildSmartMultiContentPackage(
            inputFiles = inputFiles,
            outputDir = outputFile.parentFile ?: File("/storage/emulated/0/STORM SWITCH BOX/output"),
            targetFormatMode = 2,
            useBlockNcz = false,
            smartDedup = true,
            embedMods = true,
            preBuildCustomization = PreBuildCustomization(enabled = true, customOutputFileName = outputFile.name),
            onProgress = onProgress
        )
    }

    fun buildMultiCartridgeXci(
        inputFiles: List<File>,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val isCompressed = outputFile.extension.equals("xcz", ignoreCase = true)
        return buildSmartMultiContentPackage(
            inputFiles = inputFiles,
            outputDir = outputFile.parentFile ?: File("/storage/emulated/0/STORM SWITCH BOX/output"),
            targetFormatMode = if (isCompressed) 1 else 3,
            useBlockNcz = isCompressed,
            smartDedup = true,
            embedMods = true,
            preBuildCustomization = PreBuildCustomization(enabled = true, customOutputFileName = outputFile.name),
            onProgress = onProgress
        )
    }

    /**
     * Аудитор библиотеки ROM: выявляет несжатые .nsp, необрезанные .xci/.3ds/.cci и дубликаты по TitleID.
     */
    fun auditLibraryFolder(folders: List<File>): LibraryAuditReport {
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "3ds", "cci", "cia", "cxi", "cfa")
        val seenPaths = HashSet<String>()
        val allRomFiles = mutableListOf<File>()

        for (item in folders) {
            if (!item.exists()) continue
            if (item.isFile) {
                if (item.extension.lowercase() in validExts && seenPaths.add(item.absolutePath)) {
                    allRomFiles.add(item)
                }
            } else if (item.isDirectory) {
                item.walkTopDown().maxDepth(3).forEach { f ->
                    if (f.isFile && f.extension.lowercase() in validExts && seenPaths.add(f.absolutePath)) {
                        allRomFiles.add(f)
                    }
                }
            }
        }

        val uncompressedNsp = mutableListOf<File>()
        val untrimmedCarts = mutableListOf<Pair<File, Long>>()
        val byTid = HashMap<String, MutableList<File>>()
        val tidRegex = Regex("""([0-9A-Fa-f]{16})""")
        var totalSize = 0L
        var estimatedSavings = 0L

        for (f in allRomFiles) {
            val len = f.length()
            totalSize += len
            val ext = f.extension.lowercase()

            if (ext == "nsp") {
                uncompressedNsp.add(f)
                estimatedSavings += (len * 38L) / 100L
            } else if (ext in listOf("xci", "3ds", "cci") && len > 0x4000L) {
                try {
                    RandomAccessFile(f, "r").use { raf ->
                        raf.seek(0x100)
                        val sig = ByteArray(4)
                        raf.readFully(sig)
                        val sigStr = String(sig, StandardCharsets.US_ASCII)
                        if (sigStr == "HEAD") {
                            raf.seek(0x118)
                            val b8 = ByteArray(8)
                            raf.readFully(b8)
                            val endPage = ByteBuffer.wrap(b8).order(ByteOrder.LITTLE_ENDIAN).long
                            val used = (endPage + 1L) * 0x200L
                            if (used in 0x4000L until len) {
                                val waste = len - used
                                if (waste > 4 * 1024 * 1024L) {
                                    untrimmedCarts.add(f to waste)
                                    estimatedSavings += waste
                                }
                            }
                        } else if (sigStr == "NCSD") {
                            raf.seek(0x120)
                            val partBytes = ByteArray(64)
                            raf.readFully(partBytes)
                            val pbb = ByteBuffer.wrap(partBytes).order(ByteOrder.LITTLE_ENDIAN)
                            var maxEndB = 0x4000L
                            for (i in 0 until 8) {
                                val offU = pbb.int.toLong() and 0xFFFFFFFFL
                                val lenU = pbb.int.toLong() and 0xFFFFFFFFL
                                if (lenU > 0L) {
                                    val endB = (offU + lenU) * 0x200L
                                    if (endB > maxEndB) maxEndB = endB
                                }
                            }
                            if (maxEndB in 0x4000L until len) {
                                val waste = len - maxEndB
                                if (waste > 2 * 1024 * 1024L) {
                                    untrimmedCarts.add(f to waste)
                                    estimatedSavings += waste
                                }
                            }
                        }
                    }
                } catch (_: Exception) {}
            }

            val m = tidRegex.find(f.name)
            if (m != null) {
                val tid = normalizeBaseTitleId(m.groupValues[1])
                byTid.getOrPut(tid) { mutableListOf() }.add(f)
            }
        }

        val dupGroups = byTid.filter { it.value.size > 1 }
        return LibraryAuditReport(
            totalFiles = allRomFiles.size,
            totalBytes = totalSize,
            uncompressedNspFiles = uncompressedNsp,
            untrimmedCartFiles = untrimmedCarts,
            duplicateTitleGroups = dupGroups,
            estimatedSavingsBytes = estimatedSavings
        )
    }
}
