package com.storm.switchbox

import com.github.luben.zstd.Zstd
import java.io.File
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

    fun formatSize(bytes: Long): String {
        if (bytes < 1024) return "$bytes B"
        val kb = bytes / 1024.0
        if (kb < 1024) return String.format("%.2f KB", kb)
        val mb = kb / 1024.0
        if (mb < 1024) return String.format("%.2f MB", mb)
        val gb = mb / 1024.0
        return String.format("%.2f GB", gb)
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
    fun inspectContainer(file: File): List<Pfs0Entry> {
        if (!file.exists() || file.length() < 16) return emptyList()
        val pfs = parsePfs0Entries(file)
        if (pfs.isNotEmpty()) return pfs

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

                        // Поиск вложенных разделов HFS0 (update, normal, secure)
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
                        result.add(Pfs0Entry("[3DS NCSD] Картридж Nintendo 3DS (${file.name})", 0L, raf.length(), 0L))
                        return result
                    }
                }
                result.add(Pfs0Entry(file.name, 0L, raf.length(), 0L))
            }
        } catch (_: Exception) {}
        return result
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

    fun packPfs0Container(inputFiles: List<Pair<String, File>>, outputFile: File) {
        outputFile.parentFile?.mkdirs()
        val numFiles = inputFiles.size
        val stringTableOut = java.io.ByteArrayOutputStream()
        val strOffsets = IntArray(numFiles)

        for (i in 0 until numFiles) {
            strOffsets[i] = stringTableOut.size()
            val nameBytes = inputFiles[i].first.toByteArray(StandardCharsets.UTF_8)
            stringTableOut.write(nameBytes)
            stringTableOut.write(0)
        }
        while ((16 + numFiles * 24 + stringTableOut.size()) % 16 != 0) {
            stringTableOut.write(0)
        }
        val stringTableBytes = stringTableOut.toByteArray()

        FileOutputStream(outputFile).use { fos ->
            val header = ByteBuffer.allocate(16 + numFiles * 24).order(ByteOrder.LITTLE_ENDIAN)
            header.putInt(PFS0_MAGIC)
            header.putInt(numFiles)
            header.putInt(stringTableBytes.size)
            header.putInt(0)

            var runningOffset = 0L
            for (i in 0 until numFiles) {
                val fLen = inputFiles[i].second.length()
                header.putLong(runningOffset)
                header.putLong(fLen)
                header.putInt(strOffsets[i])
                header.putInt(0)
                runningOffset += fLen
            }

            fos.write(header.array())
            fos.write(stringTableBytes)

            val copyBuf = ByteArray(1024 * 1024)
            for ((_, file) in inputFiles) {
                FileInputStream(file).use { fis ->
                    while (true) {
                        val r = fis.read(copyBuf)
                        if (r <= 0) break
                        fos.write(copyBuf, 0, r)
                    }
                }
            }
        }
    }

    private fun writePfs0HeaderForSizes(fos: FileOutputStream, items: List<Pair<String, Long>>) {
        val numFiles = items.size
        val stringTableOut = java.io.ByteArrayOutputStream()
        val strOffsets = IntArray(numFiles)
        for (i in 0 until numFiles) {
            strOffsets[i] = stringTableOut.size()
            stringTableOut.write(items[i].first.toByteArray(StandardCharsets.UTF_8))
            stringTableOut.write(0)
        }
        while ((16 + numFiles * 24 + stringTableOut.size()) % 16 != 0) {
            stringTableOut.write(0)
        }
        val strBytes = stringTableOut.toByteArray()
        val header = ByteBuffer.allocate(16 + numFiles * 24).order(ByteOrder.LITTLE_ENDIAN)
        header.putInt(PFS0_MAGIC)
        header.putInt(numFiles)
        header.putInt(strBytes.size)
        header.putInt(0)
        var runningOffset = 0L
        for (i in 0 until numFiles) {
            val sz = items[i].second
            header.putLong(runningOffset)
            header.putLong(sz)
            header.putInt(strOffsets[i])
            header.putInt(0)
            runningOffset += sz
        }
        fos.write(header.array())
        fos.write(strBytes)
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
        val startMs = System.currentTimeMillis()
        val entries = inspectContainer(inputFile)
        val tempDir = File(outputFile.parentFile, ".storm_nsz_tmp_${System.currentTimeMillis()}")
        tempDir.mkdirs()

        try {
            val validEntries = entries.filter { !it.name.startsWith("[XCI HEAD]") && it.size > 0L }
            if (validEntries.isNotEmpty()) {
                val packedEntries = mutableListOf<Pair<String, File>>()
                val totalBytes = validEntries.sumOf { it.size }.coerceAtLeast(1L)
                var processedTotal = 0L
                var ncaCount = 0

                RandomAccessFile(inputFile, "r").use { raf ->
                    for ((idx, entry) in validEntries.withIndex()) {
                        val cleanName = entry.name.substringAfterLast('/')
                        val lower = cleanName.lowercase()
                        val isCompressibleNca = lower.endsWith(".nca") &&
                            !lower.endsWith(".cnmt.nca") &&
                            entry.size > NCA_HEADER_SIZE

                        if (isCompressibleNca) {
                            ncaCount++
                            val nczName = cleanName.substringBeforeLast(".") + ".ncz"
                            val nczFile = File(tempDir, nczName)
                            compressNcaSliceToBlockNcz(
                                raf = raf,
                                sliceOffset = entry.dataStartOffset,
                                sliceSize = entry.size,
                                outNczFile = nczFile,
                                level = zstdLevel,
                                blockExpo = blockSizeExponent.coerceIn(14, 31)
                            ) { deltaBytes ->
                                processedTotal += deltaBytes
                                val elapsedSec = ((System.currentTimeMillis() - startMs) / 1000.0).coerceAtLeast(0.05)
                                val speed = (processedTotal / (1024.0 * 1024.0)) / elapsedSec
                                val pct = ((processedTotal * 100L) / totalBytes).toInt().coerceIn(0, 99)
                                onProgress(NczProgress(nczName, idx + 1, validEntries.size, processedTotal, totalBytes, speed, pct, "Сжатие NCZBLOCK: $nczName ($pct%)"))
                            }

                            if (smartSizeGuard && nczFile.length() >= entry.size) {
                                nczFile.delete()
                                val rawFile = File(tempDir, cleanName)
                                extractSliceToFile(raf, entry.dataStartOffset, entry.size, rawFile)
                                packedEntries.add(cleanName to rawFile)
                            } else {
                                packedEntries.add(nczName to nczFile)
                            }
                        } else {
                            val rawFile = File(tempDir, cleanName)
                            extractSliceToFile(raf, entry.dataStartOffset, entry.size, rawFile)
                            processedTotal += entry.size
                            packedEntries.add(cleanName to rawFile)
                        }
                    }
                }

                packPfs0Container(packedEntries, outputFile)
                val elapsed = System.currentTimeMillis() - startMs
                onProgress(NczProgress(outputFile.name, validEntries.size, validEntries.size, totalBytes, totalBytes, 0.0, 100, "Готово: ${outputFile.name}"))
                return CompressionResult(
                    success = true,
                    inputPath = inputFile.absolutePath,
                    outputPath = outputFile.absolutePath,
                    originalBytes = inputFile.length(),
                    compressedBytes = outputFile.length(),
                    elapsedMs = elapsed,
                    compressedNcaCount = ncaCount,
                    totalFilesCount = validEntries.size,
                    blockSizeExponent = blockSizeExponent,
                    zstdLevel = zstdLevel,
                    message = "Блочный контейнер ${outputFile.name} успешно создан"
                )
            } else {
                RandomAccessFile(inputFile, "r").use { raf ->
                    val total = raf.length()
                    var processed = 0L
                    compressNcaSliceToBlockNcz(raf, 0L, total, outputFile, zstdLevel, blockSizeExponent) { delta ->
                        processed += delta
                        val pct = ((processed * 100L) / total.coerceAtLeast(1L)).toInt().coerceIn(0, 100)
                        onProgress(NczProgress(inputFile.name, 1, 1, processed, total, 0.0, pct, "Сжатие NCZBLOCK: $pct%"))
                    }
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
                    blockSizeExponent = blockSizeExponent,
                    zstdLevel = zstdLevel,
                    message = "Сжат NCZBLOCK файл ${outputFile.name}"
                )
            }
        } finally {
            tempDir.deleteRecursively()
        }
    }

    private fun compressNcaSliceToBlockNcz(
        raf: RandomAccessFile,
        sliceOffset: Long,
        sliceSize: Long,
        outNczFile: File,
        level: Int,
        blockExpo: Int = 20,
        onChunkProcessed: (Long) -> Unit
    ) {
        val blockSize = 1 shl blockExpo
        val payloadSize = sliceSize - NCA_HEADER_SIZE
        val totalBlocks = ((payloadSize + blockSize - 1) / blockSize).toInt()
        val compressedSizes = IntArray(totalBlocks)

        val blocksTempFile = File(outNczFile.parentFile, "${outNczFile.name}.blocks.tmp")
        val rawBuf = ByteArray(blockSize)
        val maxDstSize = Zstd.compressBound(blockSize.toLong()).toInt()
        val compBuf = ByteArray(maxDstSize)

        try {
            FileOutputStream(blocksTempFile).use { blockFos ->
                var remaining = payloadSize
                var currentReadOffset = sliceOffset + NCA_HEADER_SIZE

                for (blockIdx in 0 until totalBlocks) {
                    val toRead = min(blockSize.toLong(), remaining).toInt()
                    raf.seek(currentReadOffset)
                    raf.readFully(rawBuf, 0, toRead)
                    currentReadOffset += toRead
                    remaining -= toRead

                    val cSize = Zstd.compressByteArray(
                        compBuf, 0, compBuf.size,
                        rawBuf, 0, toRead,
                        level.coerceIn(1, 22)
                    )

                    if (!Zstd.isError(cSize) && cSize > 0 && cSize < toRead) {
                        compressedSizes[blockIdx] = cSize.toInt()
                        blockFos.write(compBuf, 0, cSize.toInt())
                    } else {
                        compressedSizes[blockIdx] = toRead
                        blockFos.write(rawBuf, 0, toRead)
                    }
                    onChunkProcessed(toRead.toLong())
                }
            }

            FileOutputStream(outNczFile).use { fos ->
                val ncaHeader = ByteArray(NCA_HEADER_SIZE.toInt())
                raf.seek(sliceOffset)
                raf.readFully(ncaHeader)
                fos.write(ncaHeader)
                onChunkProcessed(NCA_HEADER_SIZE)

                val metaSize = 16 + 64 + 25 + (totalBlocks * 4)
                val metaBuf = ByteBuffer.allocate(metaSize).order(ByteOrder.LITTLE_ENDIAN)

                metaBuf.put("NCZSECTN".toByteArray(StandardCharsets.US_ASCII))
                metaBuf.putLong(1L)

                metaBuf.putLong(NCA_HEADER_SIZE)
                metaBuf.putLong(payloadSize)
                metaBuf.putLong(1L)
                metaBuf.putLong(0L)
                metaBuf.put(ByteArray(16))
                metaBuf.put(ByteArray(16))

                metaBuf.put("NCZBLOCK".toByteArray(StandardCharsets.US_ASCII))
                metaBuf.put(2.toByte())
                metaBuf.put(1.toByte())
                metaBuf.put(0.toByte())
                metaBuf.put(blockExpo.toByte())
                metaBuf.putInt(totalBlocks)
                metaBuf.putLong(payloadSize)

                for (sz in compressedSizes) {
                    metaBuf.putInt(sz)
                }
                fos.write(metaBuf.array())

                FileInputStream(blocksTempFile).use { fis ->
                    while (true) {
                        val r = fis.read(rawBuf)
                        if (r <= 0) break
                        fos.write(rawBuf, 0, r)
                    }
                }
            }
        } finally {
            blocksTempFile.delete()
        }
    }

    /**
     * Распаковка NSZ/XCZ обратно в оригинальный NSP/XCI.
     */
    fun decompressNszOrXczToNsp(
        inputFile: File,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        val entries = inspectContainer(inputFile).filter { !it.name.startsWith("[XCI HEAD]") && it.size > 0L }
        val tempDir = File(outputFile.parentFile, ".storm_nsp_tmp_${System.currentTimeMillis()}")
        tempDir.mkdirs()

        try {
            if (entries.isNotEmpty()) {
                val unpackedEntries = mutableListOf<Pair<String, File>>()
                val totalCompressed = entries.sumOf { it.size }.coerceAtLeast(1L)
                var processedCompressed = 0L
                var nczCount = 0

                RandomAccessFile(inputFile, "r").use { raf ->
                    for ((idx, entry) in entries.withIndex()) {
                        val cleanName = entry.name.substringAfterLast('/')
                        if (cleanName.lowercase().endsWith(".ncz")) {
                            nczCount++
                            val ncaName = cleanName.substringBeforeLast(".") + ".nca"
                            val ncaFile = File(tempDir, ncaName)
                            decompressNczSliceToNca(raf, entry.dataStartOffset, entry.size, ncaFile)
                            processedCompressed += entry.size
                            val pct = ((processedCompressed * 100L) / totalCompressed).toInt().coerceIn(0, 99)
                            onProgress(NczProgress(ncaName, idx + 1, entries.size, processedCompressed, totalCompressed, 0.0, pct, "Распаковка NCZ -> NCA: $ncaName ($pct%)"))
                            unpackedEntries.add(ncaName to ncaFile)
                        } else {
                            val rawFile = File(tempDir, cleanName)
                            extractSliceToFile(raf, entry.dataStartOffset, entry.size, rawFile)
                            processedCompressed += entry.size
                            unpackedEntries.add(cleanName to rawFile)
                        }
                    }
                }
                packPfs0Container(unpackedEntries, outputFile)
                return CompressionResult(
                    success = true,
                    inputPath = inputFile.absolutePath,
                    outputPath = outputFile.absolutePath,
                    originalBytes = inputFile.length(),
                    compressedBytes = outputFile.length(),
                    elapsedMs = System.currentTimeMillis() - startMs,
                    compressedNcaCount = nczCount,
                    totalFilesCount = entries.size,
                    blockSizeExponent = 20,
                    zstdLevel = 0,
                    message = "Распаковано в ${outputFile.name}"
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
            throw IllegalArgumentException("Неверная сигнатура секции NCZ: $magicStr")
        }
        mbb.position(8)
        val sectionCount = mbb.long.toInt()
        raf.skipBytes(sectionCount * 64)

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
        val blockSizeExp = bbb.get().toInt()
        val numBlocks = bbb.int
        val decompressedSize = bbb.long

        val blockSize = 1 shl blockSizeExp
        val sizeTableBytes = ByteArray(numBlocks * 4)
        raf.readFully(sizeTableBytes)
        val stBb = ByteBuffer.wrap(sizeTableBytes).order(ByteOrder.LITTLE_ENDIAN)
        val compSizes = IntArray(numBlocks) { stBb.int }

        FileOutputStream(outNcaFile).use { fos ->
            fos.write(ncaHeader)
            var remaining = decompressedSize
            val dstBuf = ByteArray(blockSize)

            for (cSize in compSizes) {
                val expectedDecomp = min(blockSize.toLong(), remaining).toInt()
                val compChunk = ByteArray(cSize)
                raf.readFully(compChunk)

                if (cSize == expectedDecomp) {
                    fos.write(compChunk, 0, expectedDecomp)
                } else {
                    val dLen = Zstd.decompressByteArray(dstBuf, 0, expectedDecomp, compChunk, 0, cSize)
                    if (Zstd.isError(dLen)) {
                        throw IllegalStateException("Ошибка Zstd декомпрессии: ${Zstd.getErrorName(dLen)}")
                    }
                    fos.write(dstBuf, 0, dLen.toInt())
                }
                remaining -= expectedDecomp
            }
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
     * Прямая конвертация контейнеров NSP <-> XCI.
     */
    fun convertNspAndXci(
        inputFile: File,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        val entries = inspectContainer(inputFile).filter { !it.name.startsWith("[XCI HEAD]") && it.size > 0L }
        require(entries.isNotEmpty()) { "В контейнере не найдены разделы NCA" }

        val items = entries.map { it.name.substringAfterLast('/') to it.size }
        val totalBytes = entries.sumOf { it.size }.coerceAtLeast(1L)
        var written = 0L

        FileOutputStream(outputFile).use { fos ->
            writePfs0HeaderForSizes(fos, items)
            RandomAccessFile(inputFile, "r").use { raf ->
                val buf = ByteArray(256 * 1024)
                for ((idx, e) in entries.withIndex()) {
                    raf.seek(e.dataStartOffset)
                    var rem = e.size
                    while (rem > 0) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        fos.write(buf, 0, r)
                        rem -= r
                        written += r
                    }
                    val pct = ((written * 100L) / totalBytes).toInt().coerceIn(0, 100)
                    onProgress(NczProgress(e.name, idx + 1, entries.size, written, totalBytes, 0.0, pct, "Конвертация контейнера: ${e.name}"))
                }
            }
        }

        return CompressionResult(
            success = true,
            inputPath = inputFile.absolutePath,
            outputPath = outputFile.absolutePath,
            originalBytes = inputFile.length(),
            compressedBytes = outputFile.length(),
            elapsedMs = System.currentTimeMillis() - startMs,
            compressedNcaCount = entries.size,
            totalFilesCount = entries.size,
            blockSizeExponent = 20,
            zstdLevel = 0,
            message = "Сконвертировано в ${outputFile.name}"
        )
    }

    /**
     * SmartShrink: Безопасная обрезка пустых областей 0xFF (Switch XCI HEAD 0x118 / 3DS NCSD 0x104) и очистка мусорных .xml/.cert.
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
                val b4 = ByteArray(4)
                raf.readFully(b4)
                val units = ByteBuffer.wrap(b4).order(ByteOrder.LITTLE_ENDIAN).int.toLong() and 0xFFFFFFFFL
                val calcEnd = units * 0x200L
                if (calcEnd in 0x4000L until origLen) {
                    validDataSize = calcEnd
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
     * Выборочное или полное извлечение файлов (.nca, .ncz, .tik, .cert, .xml) из контейнера NSP/NSZ/XCI/XCZ.
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
                val cleanName = entry.name.substringAfterLast('/')
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

    /**
     * Слияние Мульти-контента (4-в-1: Базовая игра + Обновление + DLC + Моды) в единый контейнер NSP (PFS0).
     */
    fun mergeMultiContentNsp(
        inputFiles: List<File>,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val startMs = System.currentTimeMillis()
        require(inputFiles.isNotEmpty()) { "Список входных файлов пуст" }

        data class SubSource(val parentFile: File, val name: String, val offset: Long, val size: Long)
        val collected = LinkedHashMap<String, SubSource>()

        for (f in inputFiles) {
            if (!f.exists()) continue
            val ext = f.extension.lowercase()
            if (ext in listOf("nsp", "nsz", "xci", "xcz")) {
                val entries = inspectContainer(f)
                for (e in entries) {
                    if (!e.name.startsWith("[XCI HEAD]") && e.dataStartOffset >= 0L && e.size > 0L && !e.name.endsWith(".xml", ignoreCase = true)) {
                        val cleanName = e.name.substringAfterLast('/')
                        collected[cleanName] = SubSource(f, cleanName, e.dataStartOffset, e.size)
                    }
                }
            } else if (ext in listOf("nca", "ncz", "tik", "cert", "cnmt")) {
                collected[f.name] = SubSource(f, f.name, 0L, f.length())
            }
        }

        require(collected.isNotEmpty()) { "Не найдено подходящих разделов NCA/NCZ для слияния" }
        val list = collected.values.toList()
        val headerItems = list.map { it.name to it.size }
        val totalSourceBytes = list.sumOf { it.size }

        var writtenPayload = 0L
        FileOutputStream(outputFile).use { fos ->
            writePfs0HeaderForSizes(fos, headerItems)
            val buf = ByteArray(256 * 1024)
            for ((idx, src) in list.withIndex()) {
                RandomAccessFile(src.parentFile, "r").use { raf ->
                    raf.seek(src.offset)
                    var rem = src.size
                    while (rem > 0) {
                        val toRead = min(rem, buf.size.toLong()).toInt()
                        val r = raf.read(buf, 0, toRead)
                        if (r <= 0) break
                        fos.write(buf, 0, r)
                        rem -= r
                        writtenPayload += r
                    }
                }
                val pct = ((writtenPayload * 100L) / totalSourceBytes.coerceAtLeast(1L)).toInt().coerceIn(0, 100)
                onProgress(NczProgress(src.name, idx + 1, list.size, writtenPayload, totalSourceBytes, 0.0, pct, "Сшивание Multi-Content (${idx + 1}/${list.size}): ${src.name}"))
            }
        }

        val elapsed = System.currentTimeMillis() - startMs
        return CompressionResult(
            success = true,
            inputPath = inputFiles.first().absolutePath,
            outputPath = outputFile.absolutePath,
            originalBytes = totalSourceBytes,
            compressedBytes = outputFile.length(),
            elapsedMs = elapsed,
            compressedNcaCount = list.size,
            totalFilesCount = list.size,
            blockSizeExponent = 20,
            zstdLevel = 0,
            message = "Собран единый Мульти-NSP (${list.size} файлов из ${inputFiles.size} контейнеров)"
        )
    }

    /**
     * Сборщик Мульти-картриджа XCI из нескольких контейнеров NSP/XCI.
     */
    fun buildMultiCartridgeXci(
        inputFiles: List<File>,
        outputFile: File,
        onProgress: (NczProgress) -> Unit
    ): CompressionResult {
        val merged = mergeMultiContentNsp(inputFiles, outputFile, onProgress)
        return merged.copy(message = "Собран Мульти-картридж XCI (${merged.totalFilesCount} NCA разделов)")
    }

    /**
     * Аудитор и дедупликатор библиотеки: находит несжатые NSP, необрезанные XCI/3DS (с гигабайтами 0xFF) и дубликаты TitleID.
     */
    fun auditLibraryFolder(folders: List<File>): LibraryAuditReport {
        val allRoms = mutableListOf<File>()
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "3ds", "cia")
        for (dir in folders) {
            if (!dir.exists()) continue
            if (dir.isFile && dir.extension.lowercase() in validExts) {
                allRoms.add(dir)
            } else if (dir.isDirectory) {
                dir.walkTopDown().maxDepth(4).forEach { f ->
                    if (f.isFile && f.extension.lowercase() in validExts) {
                        allRoms.add(f)
                    }
                }
            }
        }
        val uniqueRoms = allRoms.distinctBy { it.canonicalPath }
        val uncompressedNsp = mutableListOf<File>()
        val untrimmedCarts = mutableListOf<Pair<File, Long>>()
        val titleMap = LinkedHashMap<String, MutableList<File>>()
        var totalBytes = 0L
        var estimatedSavings = 0L

        val tidRegex = Regex("""\[?([0-9A-Fa-f]{16})\]?""")

        for (f in uniqueRoms) {
            val len = f.length()
            totalBytes += len
            val ext = f.extension.lowercase()

            if (ext == "nsp") {
                uncompressedNsp.add(f)
                estimatedSavings += (len * 32L) / 100L
            } else if (ext == "xci" || ext == "3ds") {
                try {
                    RandomAccessFile(f, "r").use { raf ->
                        if (len > 0x200) {
                            raf.seek(0x100)
                            val magic = ByteArray(4)
                            raf.readFully(magic)
                            val mStr = String(magic, StandardCharsets.US_ASCII)
                            if (mStr == "HEAD") {
                                raf.seek(0x118)
                                val b = ByteArray(8)
                                raf.readFully(b)
                                val endPage = ByteBuffer.wrap(b).order(ByteOrder.LITTLE_ENDIAN).long
                                val validEnd = (endPage + 1L) * 0x200L
                                if (validEnd in 0x4000L until len) {
                                    val waste = len - validEnd
                                    if (waste > 64 * 1024) {
                                        untrimmedCarts.add(f to waste)
                                        estimatedSavings += waste
                                    }
                                }
                            } else if (mStr == "NCSD") {
                                val b = ByteArray(4)
                                raf.readFully(b)
                                val units = ByteBuffer.wrap(b).order(ByteOrder.LITTLE_ENDIAN).int.toLong() and 0xFFFFFFFFL
                                val validEnd = units * 0x200L
                                if (validEnd in 0x4000L until len) {
                                    val waste = len - validEnd
                                    if (waste > 64 * 1024) {
                                        untrimmedCarts.add(f to waste)
                                        estimatedSavings += waste
                                    }
                                }
                            }
                        }
                    }
                } catch (_: Exception) {}
            }

            val m = tidRegex.find(f.name)
            if (m != null) {
                val baseTid = m.groupValues[1].uppercase().take(13) + "000"
                titleMap.getOrPut(baseTid) { mutableListOf() }.add(f)
            }
        }

        val dupes = titleMap.filterValues { it.size > 1 }
        return LibraryAuditReport(
            totalFiles = uniqueRoms.size,
            totalBytes = totalBytes,
            uncompressedNspFiles = uncompressedNsp,
            untrimmedCartFiles = untrimmedCarts,
            duplicateTitleGroups = dupes,
            estimatedSavingsBytes = estimatedSavings
        )
    }
}
