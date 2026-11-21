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
    fun verifyZ3dsFormatMatchesAzaharSpecAndRoundTrips() {
        val tempRoot = Files.createTempDirectory("storm_z3ds_test").toFile()
        try {
            fun makeSmdh(shortTitle: String, publisher: String): ByteArray {
                val smdh = ByteArray(0x36C0)
                "SMDH".toByteArray(StandardCharsets.US_ASCII).copyInto(smdh, 0)
                val tBytes = shortTitle.toByteArray(Charsets.UTF_16LE)
                tBytes.copyInto(smdh, 8 + 0x80, 0, minOf(tBytes.size, 0x80))
                val pBytes = publisher.toByteArray(Charsets.UTF_16LE)
                pBytes.copyInto(smdh, 8 + 0x180, 0, minOf(pBytes.size, 0x80))
                return smdh
            }

            fun makeNcch(programId: Long, version: Int, smdhTitle: String, payloadSize: Int): ByteArray {
                val totalLen = maxOf(0x4000, payloadSize)
                val buf = ByteArray(totalLen)
                // Pattern across multiple 256 KiB frames to verify Seekable frame indexing & cross-frame read
                for (i in 0x400 until totalLen step 4) {
                    val v = i / 4
                    buf[i] = (v and 0xFF).toByte()
                    buf[i + 1] = ((v ushr 8) and 0xFF).toByte()
                    buf[i + 2] = ((v xor 0x5A) and 0xFF).toByte()
                    buf[i + 3] = 0x33
                }
                "NCCH".toByteArray(StandardCharsets.US_ASCII).copyInto(buf, 0x100)
                val bb = ByteBuffer.wrap(buf).order(ByteOrder.LITTLE_ENDIAN)
                bb.putInt(0x104, totalLen / 0x200)
                bb.putLong(0x108, programId)
                bb.putShort(0x112, version.toShort())
                bb.putLong(0x118, programId)
                buf[0x18F] = 0x04 // NoCrypto
                val smdh = makeSmdh(smdhTitle, "STORM TEST")
                smdh.copyInto(buf, 0x400)
                return buf
            }

            // 1. NCSD (.cci) > 512 KiB (multi-frame Seekable Zstd)
            val cciData = ByteArray(0x4000 + 600 * 1024)
            "NCSD".toByteArray(StandardCharsets.US_ASCII).copyInto(cciData, 0x100)
            val cciBb = ByteBuffer.wrap(cciData).order(ByteOrder.LITTLE_ENDIAN)
            val cciPart0Len = 600 * 1024
            cciBb.putInt(0x104, cciData.size / 0x200)
            cciBb.putLong(0x108, 0x0004000000123400L)
            cciBb.putInt(0x120, 0x4000 / 0x200)
            cciBb.putInt(0x124, cciPart0Len / 0x200)
            val ncchForCci = makeNcch(0x0004000000123400L, 5, "Z3DS CCI Game", cciPart0Len)
            ncchForCci.copyInto(cciData, 0x4000)
            val cciFile = File(tempRoot, "test_game.cci").apply { writeBytes(cciData) }

            // 2. NCCH (.cxi)
            val cxiData = makeNcch(0x0004000000567800L, 2, "Z3DS CXI Game", 300 * 1024)
            val cxiFile = File(tempRoot, "test_app.cxi").apply { writeBytes(cxiData) }

            // 3. CIA (.cia) with TMD + NCCH + Meta SMDH
            val hdrSize = 0x2020
            val certSize = 0x40
            val tikSize = 0x40
            val tmdSize = 0x140 + 0x9C4 + 0x30
            val ciaNcch = makeNcch(0x0004000000999900L, 7, "Z3DS CIA Game", 0x4000)
            val metaSize = 0x400 + 0x36C0
            val certOff = ((hdrSize + 63) and -64)
            val tikOff = ((certOff + certSize + 63) and -64)
            val tmdOff = ((tikOff + tikSize + 63) and -64)
            val contentOff = ((tmdOff + tmdSize + 63) and -64)
            val metaOff = ((contentOff + ciaNcch.size + 63) and -64)
            val ciaData = ByteArray(metaOff + metaSize)
            val hbb = ByteBuffer.wrap(ciaData).order(ByteOrder.LITTLE_ENDIAN)
            hbb.putInt(0x00, hdrSize)
            hbb.putInt(0x08, certSize)
            hbb.putInt(0x0C, tikSize)
            hbb.putInt(0x10, tmdSize)
            hbb.putInt(0x14, metaSize)
            hbb.putLong(0x18, ciaNcch.size.toLong())
            val tmdBb = ByteBuffer.wrap(ciaData, tmdOff, tmdSize).slice().order(ByteOrder.BIG_ENDIAN)
            tmdBb.putInt(0, 0x00010004)
            tmdBb.putLong(0x140 + 0x4C, 0x0004000000999900L)
            tmdBb.putShort(0x140 + 0x9E, 1)
            tmdBb.putLong(0x140 + 0x9C4 + 8, ciaNcch.size.toLong())
            ciaNcch.copyInto(ciaData, contentOff)
            makeSmdh("Z3DS CIA Meta Title", "STORM CIA").copyInto(ciaData, metaOff + 0x400)
            val ciaFile = File(tempRoot, "test_pkg.cia").apply { writeBytes(ciaData) }

            // 4. 3DSX (.3dsx) with extended SMDH header
            val dsxData = ByteArray(0x4000)
            "3DSX".toByteArray(StandardCharsets.US_ASCII).copyInto(dsxData, 0)
            val dbb = ByteBuffer.wrap(dsxData).order(ByteOrder.LITTLE_ENDIAN)
            dbb.putShort(4, 0x2C)
            dbb.putInt(0x20, 0x100)
            dbb.putInt(0x24, 0x36C0)
            makeSmdh("Z3DS Homebrew App", "STORM HB").copyInto(dsxData, 0x100)
            val dsxFile = File(tempRoot, "test_hb.3dsx").apply { writeBytes(dsxData) }

            val cases = listOf(
                Triple(cciFile, File(tempRoot, "test_game.zcci"), "NCSD"),
                Triple(cxiFile, File(tempRoot, "test_app.zcxi"), "NCCH"),
                Triple(ciaFile, File(tempRoot, "test_pkg.zcia"), "CIA"),
                Triple(dsxFile, File(tempRoot, "test_hb.z3dsx"), "3DSX")
            )

            val sha = MessageDigest.getInstance("SHA-256")
            for ((src, dst, expectedMagic) in cases) {
                val res = StormNczEngine.compressToZ3ds(src, dst, zstdLevel = 3) {}
                assertTrue("compressToZ3ds failed for ${src.name}: ${res.message}", res.success)
                assertTrue(dst.exists())
                assertTrue(StormNczEngine.isZ3dsFile(dst))

                RandomAccessFile(dst, "r").use { raf ->
                    val hdr = ByteArray(0x20)
                    raf.readFully(hdr)
                    assertEquals("Z3DS", String(hdr, 0, 4, StandardCharsets.US_ASCII))
                    assertEquals(expectedMagic, String(hdr, 4, 4, StandardCharsets.US_ASCII).trimEnd('\u0000'))
                    assertEquals(1, hdr[8].toInt() and 0xFF)
                    val zbb = ByteBuffer.wrap(hdr).order(ByteOrder.LITTLE_ENDIAN)
                    assertEquals(0x20, zbb.getShort(0x0A).toInt() and 0xFFFF)
                    val metaSizeVal = zbb.getInt(0x0C).toLong() and 0xFFFFFFFFL
                    assertEquals(0L, metaSizeVal % 16L)
                    assertEquals(src.length(), zbb.getLong(0x18))

                    // Verify trailing Seekable Zstd magic 0x8F92EAB1
                    raf.seek(raf.length() - 4L)
                    val tail4 = ByteArray(4)
                    raf.readFully(tail4)
                    val tailMagic = ByteBuffer.wrap(tail4).order(ByteOrder.LITTLE_ENDIAN).int.toLong() and 0xFFFFFFFFL
                    assertEquals(0x8F92EAB1L, tailMagic)
                }

                // Random-access Z3dsReadHandle read (spanning frames on CCI)
                StormNczEngine.Z3dsReadHandle(dst).use { handle ->
                    assertTrue(handle.getMetadataString("compressor")?.contains("STORM SWITCH BOX") == true)
                    assertTrue(handle.getMetadataString("maxframesize") != null)
                    if (expectedMagic != "3DSX") {
                        assertNotNull(handle.getMetadataBytes("titleinfo"))
                    }
                    if (expectedMagic == "CIA") {
                        assertNotNull(handle.getMetadataBytes("smdh"))
                    }
                    if (expectedMagic == "NCSD") {
                        val crossStart = 256 * 1024L - 128L
                        val crossSlice = handle.readAt(crossStart, 512)
                        assertArrayEquals(cciData.copyOfRange(crossStart.toInt(), crossStart.toInt() + 512), crossSlice)
                    }
                }

                // Inspector & Metadata checks
                assertTrue(StormNczEngine.inspectContainer(dst).isNotEmpty())
                val cMeta = StormNczEngine.readContainerMetadata(dst)
                assertEquals("3DS", cMeta.formatFamily)

                // Bit-for-bit decompression round-trip check
                val restored = File(tempRoot, "restored_${src.name}")
                val decRes = StormNczEngine.decompressZ3ds(dst, restored) {}
                assertTrue("decompressZ3ds failed for ${dst.name}: ${decRes.message}", decRes.success)
                assertArrayEquals("SHA-256 mismatch for ${dst.name}", sha.digest(src.readBytes()), sha.digest(restored.readBytes()))
            }
        } finally {
            tempRoot.deleteRecursively()
        }
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
