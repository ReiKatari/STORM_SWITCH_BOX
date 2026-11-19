package com.storm.switchbox

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.security.MessageDigest

class StormBinaryAndLocTest {

    @Test
    fun verifyNupContainerMatchesCppSpecAndRoundTrips() {
        val tempRoot = Files.createTempDirectory("storm_nup_test").toFile()
        try {
            val inputDir = File(tempRoot, "input_game").apply { mkdirs() }
            val exefsDir = File(inputDir, "exefs").apply { mkdirs() }
            val romfsDir = File(inputDir, "romfs").apply { mkdirs() }

            val npdmFile = File(exefsDir, "main.npdm").apply {
                writeBytes(StormNczEngine.buildValidNpdmBytes("0400123456780000", "TestSwitch2"))
            }
            val textData = ("Storm Switch 2 NUP Test Payload! ".repeat(64)).toByteArray(StandardCharsets.UTF_8)
            val romFile = File(romfsDir, "data.txt").apply { writeBytes(textData) }
            val emptyFile = File(romfsDir, "empty.bin").apply { writeBytes(ByteArray(0)) }

            val nupOut = File(tempRoot, "test_game.nup")
            val res = StormNczEngine.packToNupContainer(
                inputFiles = listOf(inputDir),
                outputFile = nupOut,
                zstdLevel = 3
            )
            assertTrue("packToNupContainer failed: ${res.message}", res.success)
            assertTrue(nupOut.exists() && nupOut.length() > 0x80L)

            // Byte-for-byte check matching nup_container.cpp
            RandomAccessFile(nupOut, "r").use { raf ->
                val actualLen = raf.length()
                val sb = ByteArray(0x80)
                raf.readFully(sb)
                assertEquals("NUP\u0001", String(sb, 0, 4, StandardCharsets.US_ASCII))
                val sbb = ByteBuffer.wrap(sb).order(ByteOrder.LITTLE_ENDIAN)
                val headerSize = sbb.getShort(0x04).toInt() and 0xFFFF
                val headerVersion = sbb.getShort(0x06).toInt() and 0xFFFF
                val tocOffset = sbb.getLong(0x08)
                val tocSize = sbb.getLong(0x10)
                val tocSha = sb.copyOfRange(0x18, 0x38)
                val blobOffset = sbb.getLong(0x38)
                val fileSize = sbb.getLong(0x40)
                val sbPkgCount = sbb.getInt(0x48)
                val sbFlags = sbb.getInt(0x4C)

                assertTrue("header_size >= 0x80", headerSize >= 0x80)
                assertEquals(1, headerVersion)
                assertEquals(0, sbFlags)
                assertEquals(1, sbPkgCount)
                assertEquals(actualLen, fileSize)
                assertTrue("toc_offset >= 0x80", tocOffset >= 0x80L)
                assertTrue("blob_offset >= toc_offset + toc_size", blobOffset >= tocOffset + tocSize)
                assertEquals("blob_offset % 16 == 0", 0L, blobOffset % 16L)

                val tocBytes = ByteArray(tocSize.toInt())
                raf.seek(tocOffset)
                raf.readFully(tocBytes)
                assertArrayEquals(MessageDigest.getInstance("SHA-256").digest(tocBytes), tocSha)

                val tbb = ByteBuffer.wrap(tocBytes).order(ByteOrder.LITTLE_ENDIAN)
                assertEquals("TOC\u0001", String(tocBytes, 0, 4, StandardCharsets.US_ASCII))
                val pkgCount = tbb.getInt(0x04)
                val dirCount = tbb.getLong(0x08)
                val fileCount = tbb.getLong(0x10)
                val poolOffset = tbb.getLong(0x18)
                val poolSize = tbb.getLong(0x20)
                assertEquals(sbPkgCount, pkgCount)
                assertEquals(3L, dirCount)
                assertEquals(3L, fileCount)

                val needed = 0x30L + pkgCount * 0x40L + dirCount * 0x18L + fileCount * 0x50L
                val childrenBytes = poolOffset - needed
                assertTrue(poolOffset >= needed)
                assertEquals(0L, childrenBytes % 4L)
                assertTrue(poolOffset + poolSize <= tocSize)

                // PackageRecord checks
                val rootDir = tbb.getInt(0x30 + 0x08)
                val exefsDirIdx = tbb.getInt(0x30 + 0x0C)
                val romfsDirIdx = tbb.getInt(0x30 + 0x10)
                val metaOffset = tbb.getLong(0x30 + 0x20)
                val metaSize = tbb.getInt(0x30 + 0x28).toLong() and 0xFFFFFFFFL
                assertEquals(0, rootDir)
                assertTrue(exefsDirIdx in 1 until dirCount.toInt())
                assertTrue(romfsDirIdx in 1 until dirCount.toInt())
                assertTrue("meta_offset must be absolute (>= tocOffset + needed)", metaOffset >= tocOffset + needed)
                assertTrue("meta_offset + meta_size <= file_size", metaOffset + metaSize <= fileSize)

                // Verify TLV header at absolute metaOffset
                val firstTlv = ByteArray(8)
                raf.seek(metaOffset)
                raf.readFully(firstTlv)
                val tlvBb = ByteBuffer.wrap(firstTlv).order(ByteOrder.LITTLE_ENDIAN)
                val firstTag = tlvBb.getShort(0).toInt() and 0xFFFF
                val firstLen = tlvBb.getInt(4)
                assertTrue("Valid TLV tag", firstTag in 1..0x10)
                assertTrue("Valid TLV len", firstLen > 0 && 8L + firstLen <= metaSize)

                // Verify FileRecords
                val filesStart = (0x30L + pkgCount * 0x40L + dirCount * 0x18L).toInt()
                for (i in 0 until fileCount.toInt()) {
                    val fOff = filesStart + i * 0x50
                    val fSize = tbb.getLong(fOff + 0x08)
                    val extOff = tbb.getLong(fOff + 0x10)
                    val extSz = tbb.getLong(fOff + 0x18)
                    val codec = tocBytes[fOff + 0x40].toInt() and 0xFF
                    assertTrue(codec <= 1)
                    if (fSize == 0L) {
                        assertEquals(0L, extOff)
                        assertEquals(0L, extSz)
                    } else {
                        assertTrue("extent_offset >= blob_offset", extOff >= blobOffset)
                        assertEquals("extent_offset % 16 == 0", 0L, extOff % 16L)
                        assertTrue("extent bounds", extOff + extSz <= fileSize)
                        if (codec == 0) assertEquals(fSize, extSz)
                    }
                }

                // Verify dir_parents[root_dir] == 0
                val childrenArrOffset = needed.toInt()
                val dirParents = IntArray(dirCount.toInt())
                for (cIdx in 0 until (childrenBytes / 4L).toInt()) {
                    val child = tbb.getInt(childrenArrOffset + cIdx * 4)
                    assertTrue(child >= 0 && child < (dirCount + fileCount).toInt())
                    if (child < dirCount.toInt()) {
                        dirParents[child]++
                        assertEquals(1, dirParents[child])
                    }
                }
                assertEquals(0, dirParents[rootDir])
            }

            // Verify inspect + extract round-trip
            val info = StormNczEngine.inspectNupFile(nupOut)
            assertNotNull(info)
            assertTrue(info!!.hasBase)

            val extractRoot = File(tempRoot, "extracted")
            val gameOut = StormNczEngine.extractNupToTempNspOrDir(nupOut, extractRoot)
            val baseOut = File(gameOut, "base")
            assertArrayEquals(npdmFile.readBytes(), File(baseOut, "exefs/main.npdm").readBytes())
            assertArrayEquals(textData, File(baseOut, "romfs/data.txt").readBytes())
            assertEquals(0L, File(baseOut, "romfs/empty.bin").length())
        } finally {
            tempRoot.deleteRecursively()
        }
    }

    @Test
    fun verifyNacpOffsetsAndSizes() {
        val nacp = StormNczEngine.buildNacpBytes(
            titleId = "0100123456780000",
            gameTitle = "Storm Test",
            publisher = "STORM SOFT",
            displayVersion = "1.2.3"
        )
        assertEquals(0x4000, nacp.size)
        val bb = ByteBuffer.wrap(nacp).order(ByteOrder.LITTLE_ENDIAN)
        assertEquals(0x0100123456781000L, bb.getLong(0x3070)) // AddOnContentBaseId
        assertEquals(0x0100123456780000L, bb.getLong(0x3078)) // SaveDataOwnerId
        assertEquals(0x01000000L, bb.getLong(0x3080)) // UserAccountSaveDataSize
        assertEquals(0x01000000L, bb.getLong(0x3088)) // UserAccountSaveDataJournalSize
        assertEquals(0L, bb.getLong(0x30A0))          // BcatDeliveryCacheStorageSize must be 0
        assertEquals(0x0100123456780000L, bb.getLong(0x30B0)) // LocalCommunicationId[0]
    }

    @Test
    fun verifyLocalizationRegexesAndTemplates() {
        assertEquals("12.5 MB/s", StormLocalization.tr("12.5 МБ/с", "en"))
        assertEquals("12.5 Mo/s", StormLocalization.tr("12.5 МБ/с", "fr"))
        assertEquals("512 MB", StormLocalization.tr("512 МБ", "en"))
        assertEquals("Games in queue: 3", StormLocalization.tr("Игр в очереди: 3", "en"))
        assertEquals("Files in queue: 7", StormLocalization.tr("Файлов в очереди: 7", "en"))
        assertEquals("Ready for processing (Games in queue: 2, files: 5)", StormLocalization.tr("Готов к обработке (Игр в очереди: 2, файлов: 5)", "en"))
    }
}
