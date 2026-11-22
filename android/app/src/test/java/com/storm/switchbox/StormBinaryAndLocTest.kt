package com.storm.switchbox

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.ByteArrayInputStream
import java.io.File
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.security.MessageDigest
import java.util.zip.GZIPInputStream

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

    @Test
    fun verifyRuTrackerRoleClassificationSmartSelectionAndCleanDlcCopy() {
        val paths = listOf(
            "Xenoblade Chronicles X Definitive Edition [0100453019AA8000][v0].nsp",
            "Xenoblade Chronicles X Definitive Edition [0100453019AA8800][v65536].nsp",
            "Xenoblade Chronicles X Definitive Edition [0100453019AA8800][v131072].nsp",
            "Xenoblade Chronicles X Definitive Edition [DLC Early Purchase Bonus Exploration Support Pack] [0100453019AA9001][v0].nsp",
            "7 DLC/Xenoblade Chronicles 2 [DLC New Challenge Mode Pack] [0100E95004033005][v65536].nsz",
            "7 DLC/Xenoblade Chronicles 2 [DLC Torna The Golden Country (Brand New Story)] [0100E95004033007][v0].nsz",
            "Russian Machine Translation (11.04.2025)/Xenoblade Chronicles X Definitive Edition [0100453019AA8800][v65536].nsp",
            "Russian Machine Translation (11.04.2025)/atmosphere.zip",
            "Russian Machine Translation (11.04.2025)/ReadMe_Rus.txt",
            "Saves/100_percent.sav"
        )
        assertEquals("ИГРА", RuTrackerCloudEngine.classifyTorrentFileRole(paths[0]))
        assertEquals("ОБНОВЛЕНИЕ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[2]))
        assertEquals("ДОПОЛНЕНИЯ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[3]))
        assertEquals("ДОПОЛНЕНИЯ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[4]))
        assertEquals("ДОПОЛНЕНИЯ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[5]))
        assertEquals("ОБНОВЛЕНИЕ (РУС)", RuTrackerCloudEngine.classifyTorrentFileRole(paths[6]))
        assertEquals("МОДЫ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[7]))
        assertEquals("ИНФО", RuTrackerCloudEngine.classifyTorrentFileRole(paths[8]))
        assertEquals("СЭЙВЫ", RuTrackerCloudEngine.classifyTorrentFileRole(paths[9]))

        assertEquals(
            "Early Purchase Bonus Exploration Support Pack",
            RuTrackerCloudEngine.extractCleanDlcName(paths[3])
        )
        assertEquals(
            "Torna The Golden Country (Brand New Story)",
            RuTrackerCloudEngine.extractCleanDlcName(paths[5])
        )

        val xcxFiles = listOf(paths[0], paths[1], paths[2], paths[3], paths[6], paths[7], paths[8], paths[9]).map { p ->
            val tid = Regex("""(01[0-9A-Fa-f]{14})""").find(p)?.groupValues?.get(1).orEmpty()
            RuTrackerCloudEngine.ReleaseContentFile(
                fileName = p.substringAfterLast('/'),
                roleBadge = RuTrackerCloudEngine.classifyTorrentFileRole(p),
                sizeBytes = 1024L * 1024L,
                titleId = tid,
                selected = true,
                isRealTorrentFile = true,
                relativePath = p
            )
        }
        val slots = RuTrackerCloudEngine.detectGameSlotsForFiles("Xenoblade Chronicles X", xcxFiles)
        assertEquals("Base, updates, and DLC 0100453019AA9001 must merge into 1 slot", 1, slots.size)

        RuTrackerCloudEngine.applySmartFileSelection(xcxFiles)
        assertTrue(xcxFiles[0].selected) // ИГРА
        assertTrue(!xcxFiles[1].selected) // older v65536 update
        assertTrue(xcxFiles[2].selected) // latest v131072 update
        assertTrue(xcxFiles[3].selected) // ДОПОЛНЕНИЯ
        assertTrue(!xcxFiles[4].selected) // ОБНОВЛЕНИЕ (РУС)
        assertTrue(!xcxFiles[5].selected) // МОДЫ
        assertTrue(!xcxFiles[6].selected) // ИНФО
        assertTrue(!xcxFiles[7].selected) // СЭЙВЫ
        assertTrue(!xcxFiles[3].isMod) // "Mode" / "Bonus" in filename must not force isMod
    }

    @Test
    fun verifyMultiGameSlotDetectionAndUncheckSync() {
        val files = listOf(
            RuTrackerCloudEngine.ReleaseContentFile("Aggelos [010004D00A9C0000][v0].nsp", "ИГРА", 100_000L, "010004D00A9C0000", true, true, relativePath = "Aggelos/Aggelos [010004D00A9C0000][v0].nsp"),
            RuTrackerCloudEngine.ReleaseContentFile("Aggelos [010004D00A9C0800][v131072].nsp", "ОБНОВЛЕНИЕ", 112_000L, "010004D00A9C0800", true, true, relativePath = "Aggelos/Aggelos [010004D00A9C0800][v131072].nsp"),
            RuTrackerCloudEngine.ReleaseContentFile("Aggelos 2 [010040F01EC60000][v0].nsp", "ИГРА", 120_000L, "010040F01EC60000", true, true, relativePath = "Aggelos 2/Aggelos 2 [010040F01EC60000][v0].nsp"),
            RuTrackerCloudEngine.ReleaseContentFile("Aggelos 2 [010040F01EC60800][v393216].nsp", "ОБНОВЛЕНИЕ", 105_000L, "010040F01EC60800", true, true, relativePath = "Aggelos 2/Aggelos 2 [010040F01EC60800][v393216].nsp")
        )
        val slots = RuTrackerCloudEngine.detectGameSlotsForFiles("[Nintendo Switch] Aggelos 1-2", files)
        assertEquals(2, slots.size)
        assertEquals(1, files[0].targetGameSlot)
        assertEquals(1, files[1].targetGameSlot)
        assertEquals(2, files[2].targetGameSlot)
        assertEquals(2, files[3].targetGameSlot)

        // Simulate unchecking Game #1 -> all slot 1 files turn off, slot 2 stays on
        slots[0].isEnabled = false
        files.filter { it.targetGameSlot == 1 }.forEach { it.selected = false }
        assertTrue(!files[0].selected && !files[1].selected)
        assertTrue(files[2].selected && files[3].selected)
    }

    @Test
    fun verifySeedingPersistenceAndLibraryUpdateChecker() {
        val tempRoot = Files.createTempDirectory("storm_seed_test").toFile()
        try {
            val stateFile = File(tempRoot, "seeding.json")
            RuTrackerCloudEngine.seedingEntries.clear()
            RuTrackerCloudEngine.seedingEntries.add(
                RuTrackerCloudEngine.RuTrackerSeedingEntry(
                    infoHash = "0123456789ABCDEF0123456789ABCDEF01234567",
                    topicId = "6525297",
                    title = "Animal Well",
                    saveDir = tempRoot.absolutePath,
                    torrentFilePath = File(tempRoot, "t.torrent").absolutePath,
                    selectedRelativePaths = listOf("Animal Well/base.nsp"),
                    totalSizeBytes = 1000L,
                    uploadedBytes = 2500L,
                    isPaused = true
                )
            )
            RuTrackerCloudEngine.saveSeedingStateToFile(stateFile)
            val loaded = RuTrackerCloudEngine.loadSeedingEntriesFromFile(stateFile)
            assertEquals(1, loaded.size)
            assertEquals(2.5, loaded[0].ratio, 0.001)
            assertTrue(loaded[0].isPaused)
            assertEquals(listOf("Animal Well/base.nsp"), loaded[0].selectedRelativePaths)
            RuTrackerCloudEngine.seedingEntries.clear()

            val localFile = File(tempRoot, "Castlevania Belmont's Curse (2026) [WW] [RUS] (1.1.0 - 131072 - 01005F4027E94000) (1G+1U+1D).nsz").apply {
                writeBytes(ByteArray(16))
            }
            val fakeCatalog = listOf(
                RuTrackerCloudEngine.TopicItem(
                    "900001",
                    "[Nintendo Switch] Castlevania: Belmont's Curse [v1.1.0 + 1 DLC] Standalone [NSZ]",
                    "Nintendo Switch", "2 GB", 50, 1, "", "", ""
                ),
                RuTrackerCloudEngine.TopicItem(
                    "900002",
                    "[Nintendo Switch] Castlevania: Belmont's Curse [v1.2.0 + 2 DLC] [NSZ]",
                    "Nintendo Switch", "2.1 GB", 80, 2, "", "", ""
                )
            )
            val updates = RuTrackerCloudEngine.checkLibraryUpdatesAgainstCatalog(listOf(localFile), fakeCatalog)
            assertEquals(1, updates.size)
            assertEquals("900002", updates[0].remoteTopicId)
            assertTrue(updates[0].hasNewerVersion)
            assertTrue(updates[0].hasNewerDlc)
            assertEquals("1.2.0", updates[0].remoteVersion)
            assertEquals(2, updates[0].remoteDlcCount)
        } finally {
            tempRoot.deleteRecursively()
        }
    }

    @Test
    fun verifyRuTrackerIndexLoadsPlainAndGzipAndSearchesMandragora() {
        val gzFile = listOf(
            File("src/main/assets/rutracker_switch_index.json.gz"),
            File("app/src/main/assets/rutracker_switch_index.json.gz"),
            File("android/app/src/main/assets/rutracker_switch_index.json.gz")
        ).firstOrNull { it.isFile }
        assertNotNull("rutracker_switch_index.json.gz asset file must exist", gzFile)

        val gzBytes = gzFile!!.readBytes()
        val plainBytes = GZIPInputStream(ByteArrayInputStream(gzBytes)).use { it.readBytes() }
        assertTrue("Decompressed plain JSON must be > 1 MB", plainBytes.size > 1_000_000)

        val prevCtx = RuTrackerCloudEngine.appContext
        val prevCacheRoot = RuTrackerCloudEngine.cacheRootDir
        try {
            // 1. Parse both GZIP and plain UTF-8 JSON (matching AAPT2 asset packaging in APK)
            val parsedGz = RuTrackerSwitchIndex.parse(ByteArrayInputStream(gzBytes), "test-gz")
            val parsedPlain = RuTrackerSwitchIndex.parse(ByteArrayInputStream(plainBytes), "test-plain")
            assertTrue("Expected >= 7000 topics in gzip index", parsedGz.topics.size >= 7000)
            assertEquals(parsedGz.topics.size, parsedPlain.topics.size)
            assertNotNull("Mandragora topic 6739954 must exist in plain JSON index", parsedPlain.byId["6739954"])

            // 2. Simulate Android APK where AAPT2 stripped .gz and provides plain JSON via assets.open()
            RuTrackerSwitchIndex.clearLoadedStateForTesting()
            RuTrackerCloudEngine.clearRealIndexCacheForTesting()
            RuTrackerCloudEngine.appContext = null
            RuTrackerCloudEngine.cacheRootDir = null
            RuTrackerSwitchIndex.cacheDir = null
            RuTrackerSwitchIndex.assetOpener = { ByteArrayInputStream(plainBytes) }

            val mandragoraInstant = RuTrackerCloudEngine.searchTopicsInstant("mandragora")
            assertEquals(null, RuTrackerSwitchIndex.lastLoadError)
            assertTrue("searchTopicsInstant('mandragora') must return results from plain index", mandragoraInstant.isNotEmpty())
            val firstMatch = mandragoraInstant.firstOrNull { it.topicId == "6739954" }
            assertNotNull("Expected Mandragora topic 6739954 in instant results", firstMatch)
            assertTrue(firstMatch!!.title.contains("Mandragora", ignoreCase = true))
            assertEquals(31, firstMatch.seeds)
            assertEquals("2F33F6E746620024731F8093D9343A5BD7A796B5", firstMatch.infoHash)
            assertTrue(firstMatch.magnetUri.startsWith("magnet:?xt=urn:btih:2F33F6E746620024731F8093D9343A5BD7A796B5"))

            // 3. Ensure previously false-blocked Switch titles pass isStrictSwitchRelease and search
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] TES 5 The Elder Scrolls V: Skyrim Anniversary Edition - Special Edition + Dawnguard", "Nintendo Switch"))
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] Overcooked Special Edition / Overcooked! 2", "Nintendo Switch"))
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] Deltarune Chap. 1-5 [NSZ]", "Nintendo Switch"))
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] Persona 3 Portable (P3P)", "Nintendo Switch"))
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] Rune Factory 4 Special", "Nintendo Switch"))
            assertTrue(RuTrackerCloudEngine.isStrictSwitchRelease("[Nintendo Switch] Mandragora: Whispers of the Witch Tree + 9 DLC", "Nintendo Switch"))
            assertFalse(RuTrackerCloudEngine.isStrictSwitchRelease("Cyberpunk 2077 [FitGirl Repack] [PC]", ""))
            assertFalse(RuTrackerCloudEngine.isStrictSwitchRelease("God of War Ragnarok [PS4] [PKG]", ""))
            assertFalse(RuTrackerCloudEngine.isStrictSwitchRelease("Game Setup.exe [Windows]", ""))

            assertTrue("searchTopicsInstant('skyrim') must return Switch results", RuTrackerCloudEngine.searchTopicsInstant("skyrim").isNotEmpty())
            assertTrue("searchTopicsInstant('deltarune') must return Switch results", RuTrackerCloudEngine.searchTopicsInstant("deltarune").isNotEmpty())
        } finally {
            RuTrackerSwitchIndex.assetOpener = null
            RuTrackerSwitchIndex.cacheDir = null
            RuTrackerSwitchIndex.clearLoadedStateForTesting()
            RuTrackerCloudEngine.clearRealIndexCacheForTesting()
            RuTrackerCloudEngine.appContext = prevCtx
            RuTrackerCloudEngine.cacheRootDir = prevCacheRoot
        }
    }

    @Test
    fun verifyAndroidSearchKeystrokeLatencyIsInstant() {
        val gzFile = listOf(
            File("src/main/assets/rutracker_switch_index.json.gz"),
            File("app/src/main/assets/rutracker_switch_index.json.gz"),
            File("android/app/src/main/assets/rutracker_switch_index.json.gz")
        ).firstOrNull { it.isFile }
        val tdbFile = listOf(
            File("src/main/assets/titledb_full.tsv.gz"),
            File("app/src/main/assets/titledb_full.tsv.gz"),
            File("android/app/src/main/assets/titledb_full.tsv.gz")
        ).firstOrNull { it.isFile }
        assertNotNull("rutracker_switch_index.json.gz must exist", gzFile)
        assertNotNull("titledb_full.tsv.gz must exist", tdbFile)

        val plainBytes = GZIPInputStream(ByteArrayInputStream(gzFile!!.readBytes())).use { it.readBytes() }
        val tdbList = StormNczEngine.parseTitleDbTsvStream(tdbFile!!.inputStream())
        assertTrue("Expected > 5000 Switch TitleDB entries, got ${tdbList.size}", tdbList.size > 5000)

        val prevCtx = RuTrackerCloudEngine.appContext
        val prevCacheRoot = RuTrackerCloudEngine.cacheRootDir
        try {
            RuTrackerSwitchIndex.clearLoadedStateForTesting()
            RuTrackerCloudEngine.clearRealIndexCacheForTesting()
            RuTrackerCloudEngine.appContext = null
            RuTrackerCloudEngine.cacheRootDir = null
            RuTrackerSwitchIndex.cacheDir = null
            RuTrackerSwitchIndex.assetOpener = { ByteArrayInputStream(plainBytes) }
            StormNczEngine.setFullTitleDbForTesting(tdbList)

            // Warm up search caches once (done at app startup in production)
            val warmRt = RuTrackerCloudEngine.searchTopicsInstant("zelda")
            assertTrue(warmRt.isNotEmpty())
            val warmTdb = RuTrackerCloudEngine.filterTitleDbCatalogFuzzy(tdbList, "Switch", "zelda")
            assertTrue(warmTdb.isNotEmpty())

            val word = "mandragora"
            var maxSingleMs = 0L
            val tStartNs = System.nanoTime()
            var finalResults: List<RuTrackerCloudEngine.TopicItem> = emptyList()
            for (len in 1..word.length) {
                val prefix = word.substring(0, len)
                val t0 = System.nanoTime()
                finalResults = RuTrackerCloudEngine.searchTopicsInstant(prefix)
                val tdbMatches = RuTrackerCloudEngine.filterTitleDbCatalogFuzzy(tdbList, "Switch", prefix)
                val elapsedMs = (System.nanoTime() - t0) / 1_000_000L
                if (elapsedMs > maxSingleMs) maxSingleMs = elapsedMs
                assertTrue("Keystroke '$prefix' took ${elapsedMs}ms (> 150ms limit)", elapsedMs < 150L)
                assertTrue("Expected matches for prefix '$prefix'", finalResults.isNotEmpty() || tdbMatches.isNotEmpty())
            }
            val totalKeystrokesMs = (System.nanoTime() - tStartNs) / 1_000_000L
            val mandragora = finalResults.firstOrNull { it.topicId == "6739954" }
            assertNotNull("Expected Mandragora topic 6739954 in final results", mandragora)
            assertEquals(31, mandragora!!.seeds)
            assertTrue("Total 10 keystrokes took ${totalKeystrokesMs}ms (> 500ms limit)", totalKeystrokesMs < 500L)

            val all60 = RuTrackerCloudEngine.searchTopicsInstant("").take(60)
            val tTid0 = System.nanoTime()
            for (item in all60) {
                RuTrackerCloudEngine.resolveAuthenticBaseTitleId(item.title, item.infoHash, item.topicId)
            }
            val tidElapsedMs = (System.nanoTime() - tTid0) / 1_000_000L
            assertTrue("60x resolveAuthenticBaseTitleId took ${tidElapsedMs}ms (> 50ms limit)", tidElapsedMs < 50L)
            println("[BENCHMARK] 10 keystrokes max=${maxSingleMs}ms total=${totalKeystrokesMs}ms | 60x TitleID=${tidElapsedMs}ms")
        } finally {
            StormNczEngine.setFullTitleDbForTesting(null)
            RuTrackerSwitchIndex.assetOpener = null
            RuTrackerSwitchIndex.cacheDir = null
            RuTrackerSwitchIndex.clearLoadedStateForTesting()
            RuTrackerCloudEngine.clearRealIndexCacheForTesting()
            RuTrackerCloudEngine.appContext = prevCtx
            RuTrackerCloudEngine.cacheRootDir = prevCacheRoot
        }
    }
}
