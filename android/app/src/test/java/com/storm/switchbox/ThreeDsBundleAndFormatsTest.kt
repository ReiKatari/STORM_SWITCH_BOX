package com.storm.switchbox

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import java.nio.file.Files

class ThreeDsBundleAndFormatsTest {

    private fun buildSyntheticNcch(titleId: Long, shortTitle: String, publisher: String, payloadKb: Int = 32): ByteArray {
        val totalSize = 0x4000 + payloadKb * 1024
        val buf = ByteArray(totalSize)
        val bb = ByteBuffer.wrap(buf).order(ByteOrder.LITTLE_ENDIAN)
        "NCCH".toByteArray(StandardCharsets.US_ASCII).copyInto(buf, 0x100)
        bb.putInt(0x104, totalSize / 0x200)
        bb.putLong(0x108, titleId)
        bb.putLong(0x118, titleId)
        buf[0x18F] = 0x04 // NoCrypto

        // Embed SMDH inside NCCH payload at 0x2000 for SMDH scan tests
        val smdhOff = 0x2000
        "SMDH".toByteArray(StandardCharsets.US_ASCII).copyInto(buf, smdhOff)
        val sb = shortTitle.toByteArray(Charsets.UTF_16LE)
        val pb = publisher.toByteArray(Charsets.UTF_16LE)
        sb.copyInto(buf, smdhOff + 8 + 0x80, 0, minOf(sb.size, 0x80))
        pb.copyInto(buf, smdhOff + 8 + 0x180, 0, minOf(pb.size, 0x80))

        // Semi-compressible repeated pattern + deterministic tail
        for (i in 0x6000 until totalSize) {
            buf[i] = ((i * 31) xor (i ushr 7)).toByte()
        }
        return buf
    }

    private fun buildSyntheticNcsd(titleId: Long, shortTitle: String, publisher: String): ByteArray {
        val part0 = buildSyntheticNcch(titleId, shortTitle, publisher, payloadKb = 48)
        val part1 = buildSyntheticNcch(titleId, shortTitle, publisher, payloadKb = 16) // Manual CFA dummy partition
        val ncsd = ByteArray(0x4000 + part0.size + part1.size)
        val nbb = ByteBuffer.wrap(ncsd).order(ByteOrder.LITTLE_ENDIAN)
        "NCSD".toByteArray(StandardCharsets.US_ASCII).copyInto(ncsd, 0x100)
        nbb.putInt(0x104, ncsd.size / 0x200)
        nbb.putLong(0x108, titleId)
        nbb.putInt(0x120, 0x4000 / 0x200)
        nbb.putInt(0x124, part0.size / 0x200)
        nbb.putInt(0x128, (0x4000 + part0.size) / 0x200)
        nbb.putInt(0x12C, part1.size / 0x200)
        part0.copyInto(ncsd, 0x4000)
        part1.copyInto(ncsd, 0x4000 + part0.size)
        return ncsd
    }

    private fun buildSyntheticDecryptedCia(titleId: Long, version: Int, shortTitle: String, publisher: String): ByteArray {
        val hdrSize = 0x2020
        val certSize = 0xA00
        val tikSize = 0x350
        val tmdBodyOffset = 0x140
        val tmdSize = tmdBodyOffset + 0x9C4 + 0x30
        val content = buildSyntheticNcch(titleId, shortTitle, publisher, payloadKb = 24)
        fun align64(v: Int): Int = (v + 63) and -64
        val certOff = align64(hdrSize)
        val tikOff = align64(certOff + certSize)
        val tmdOff = align64(tikOff + tikSize)
        val contentOff = align64(tmdOff + tmdSize)
        val cia = ByteArray(contentOff + content.size)

        val cbb = ByteBuffer.wrap(cia).order(ByteOrder.LITTLE_ENDIAN)
        cbb.putInt(0x00, hdrSize)
        cbb.putInt(0x08, certSize)
        cbb.putInt(0x0C, tikSize)
        cbb.putInt(0x10, tmdSize)
        cbb.putInt(0x14, 0)
        cbb.putLong(0x18, content.size.toLong())

        val tbb = ByteBuffer.wrap(cia).order(ByteOrder.BIG_ENDIAN)
        tbb.putInt(tmdOff, 0x00010001) // RSA_2048_SHA256 -> sigSize = 0x140
        val bodyOff = tmdOff + tmdBodyOffset
        tbb.putLong(bodyOff + 0x4C, titleId)
        tbb.putInt(bodyOff + 0x54, 0)
        tbb.putShort(bodyOff + 0x9C, version.toShort())
        tbb.putShort(bodyOff + 0x9E, 1.toShort())
        val recOff = bodyOff + 0x9C4
        tbb.putLong(recOff + 8, content.size.toLong()) // cTypeFlags = 0x0000 (decrypted)

        content.copyInto(cia, contentOff)
        return cia
    }

    @Test
    fun verifyAll18ExtensionsAndTitleIdGrouping() {
        val all18 = listOf(
            "bcci", "bcxi", "bcia",
            "zcci", "zcxi", "zcia", "z3dsx", "z3ds",
            "3ds", "cci", "cxi", "cia", "cfa", "app", "3dsx", "elf", "axf", "artic"
        )
        for (ext in all18) {
            assertTrue("Expected .$ext to be recognized as 3DS format", StormNczEngine.is3dsExtension(ext))
        }
        assertFalse(StormNczEngine.is3dsExtension("nsp"))

        val base = StormNczEngine.normalizeBaseTitleId("00040000000F7500")
        val upd = StormNczEngine.normalizeBaseTitleId("0004000E000F7500")
        val dlc = StormNczEngine.normalizeBaseTitleId("0004008C000F7500")
        assertEquals("00040000000F7500", base)
        assertEquals(base, upd)
        assertEquals(base, dlc)
    }

    @Test
    fun verifyBcciBcxiBciaBundlesCxiCciAndDiagnosticFormats() {
        val tempRoot = Files.createTempDirectory("storm_3ds_bundle_test").toFile()
        try {
            val outDir = File(tempRoot, "out").apply { mkdirs() }
            val gameFile = File(tempRoot, "TestGame [00040000000F7500].3ds").apply {
                writeBytes(buildSyntheticNcsd(0x00040000000F7500L, "Xenoblade Test", "Nintendo"))
            }
            val updFile = File(tempRoot, "TestGame [0004000E000F7500][UPD].cia").apply {
                writeBytes(buildSyntheticDecryptedCia(0x0004000E000F7500L, 1040, "Xenoblade Upd", "Nintendo"))
            }
            val dlcFile = File(tempRoot, "TestGame [0004008C000F7500][DLC].cia").apply {
                writeBytes(buildSyntheticDecryptedCia(0x0004008C000F7500L, 0, "Xenoblade DLC", "Nintendo"))
            }

            // 1. Group queue items by TitleID
            val groups = StormNczEngine.groupFilesByGameFamily(listOf(dlcFile, updFile, gameFile))
            assertEquals("Base + Update + DLC should group into 1 3DS family", 1, groups.size)
            assertEquals("00040000000F7500", groups[0].baseTitleId)

            // 2. Build BCCI (mode 13)
            val bcciRes = StormNczEngine.buildSmartMultiContentPackage(
                inputFiles = listOf(gameFile, updFile, dlcFile),
                outputDir = outDir,
                targetFormatMode = 13,
                preBuildCustomization = StormNczEngine.PreBuildCustomization(enabled = true, customOutputFileName = "TestBundle"),
                zstdLevel = 3
            ) {}
            assertTrue("BCCI build failed: ${bcciRes.message}", bcciRes.success)
            val bcciFile = File(bcciRes.outputPath)
            assertEquals("bcci", bcciFile.extension.lowercase())
            assertTrue(StormNczEngine.is3dsBundleFile(bcciFile))

            val bcciInfo = StormNczEngine.read3dsTarBundle(bcciFile)
            assertNotNull(bcciInfo)
            assertEquals("metadata", bcciInfo!!.entries.first().name)
            assertTrue(bcciInfo.metadata.containsKey("bundler"))
            assertTrue(bcciInfo.metadata.containsKey("date"))
            assertTrue(bcciInfo.metadata.containsKey("mainfile"))
            assertTrue(bcciInfo.metadata.containsKey("titleinfo"))
            assertNull("TAR metadata must NOT duplicate smdh", bcciInfo.metadata["smdh"])
            assertEquals(4, bcciInfo.entries.size) // metadata + base.zcci + upd.zcia + dlc.zcia

            val (bcciValid, bcciMsg) = StormNczEngine.verify3dsTarBundle(bcciFile)
            assertTrue("BCCI TAR verify failed: $bcciMsg", bcciValid)
            val bcciIntegrity = StormNczEngine.verifyContainerIntegrity(bcciFile)
            assertTrue("BCCI verifyContainerIntegrity failed", bcciIntegrity.valid)
            val bcciMeta = StormNczEngine.readContainerMetadata(bcciFile)
            assertEquals("00040000000F7500", bcciMeta.titleId)

            // Extract BCCI and confirm 3 inner files exist
            val extractDir = File(tempRoot, "extracted_bcci")
            val extracted = StormNczEngine.extract3dsTarBundle(bcciFile, extractDir)
            assertEquals(3, extracted.size)

            // 3. Build BCXI (mode 14) -> stripped Partition 0 (.zcxi) + .zcia updates/DLC -> strictly smaller than BCCI
            val bcxiRes = StormNczEngine.buildSmartMultiContentPackage(
                inputFiles = listOf(gameFile, updFile, dlcFile),
                outputDir = outDir,
                targetFormatMode = 14,
                preBuildCustomization = StormNczEngine.PreBuildCustomization(enabled = true, customOutputFileName = "TestBundle"),
                zstdLevel = 3
            ) {}
            assertTrue("BCXI build failed: ${bcxiRes.message}", bcxiRes.success)
            val bcxiFile = File(bcxiRes.outputPath)
            assertEquals("bcxi", bcxiFile.extension.lowercase())
            val bcxiInfo = StormNczEngine.read3dsTarBundle(bcxiFile)!!
            assertTrue(bcxiInfo.mainEntry!!.name.endsWith(".zcxi"))
            assertTrue("BCXI verifyContainerIntegrity failed", StormNczEngine.verifyContainerIntegrity(bcxiFile).valid)

            // 4. Build BCIA (mode 15) from CIAs
            val bciaRes = StormNczEngine.buildSmartMultiContentPackage(
                inputFiles = listOf(updFile, dlcFile),
                outputDir = outDir,
                targetFormatMode = 15,
                preBuildCustomization = StormNczEngine.PreBuildCustomization(enabled = true, customOutputFileName = "PatchesBundle"),
                zstdLevel = 3
            ) {}
            assertTrue("BCIA build failed: ${bciaRes.message}", bciaRes.success)
            val bciaFile = File(bciaRes.outputPath)
            assertEquals("bcia", bciaFile.extension.lowercase())
            assertTrue(StormNczEngine.verifyContainerIntegrity(bciaFile).valid)

            // 5. Conversions: 3DS -> CXI (16), CXI -> CCI (4), BCCI -> CCI (4), 3DS -> ZCXI (10)
            val cxiRes = StormNczEngine.processAndConvert3dsContainer(gameFile, outDir, 16) {}
            assertTrue("3DS->CXI failed: ${cxiRes.message}", cxiRes.success)
            val cxiFile = File(cxiRes.outputPath)
            assertEquals("cxi", cxiFile.extension.lowercase())

            val cciFromCxi = StormNczEngine.processAndConvert3dsContainer(cxiFile, outDir, 4, StormNczEngine.PreBuildCustomization(true, "WrappedCci")) {}
            assertTrue("CXI->CCI failed: ${cciFromCxi.message}", cciFromCxi.success)
            assertEquals("cci", File(cciFromCxi.outputPath).extension.lowercase())

            val cciFromBcci = StormNczEngine.processAndConvert3dsContainer(bcciFile, outDir, 4, StormNczEngine.PreBuildCustomization(true, "ExtractedCci")) {}
            assertTrue("BCCI->CCI failed: ${cciFromBcci.message}", cciFromBcci.success)
            assertEquals("cci", File(cciFromBcci.outputPath).extension.lowercase())

            // 6. ELF / AXF / ARTIC / APP / CFA / Z3DS inspection & integrity verification
            val elfFile = File(tempRoot, "boot.elf").apply { writeBytes(byteArrayOf(0x7F, 'E'.code.toByte(), 'L'.code.toByte(), 'F'.code.toByte(), 1, 1, 1, 0)) }
            val axfFile = File(tempRoot, "debug.axf").apply { writeBytes(ByteArray(52).also { b -> b[0] = 0x7F; b[1] = 'E'.code.toByte(); b[2] = 'L'.code.toByte(); b[3] = 'F'.code.toByte() }) }
            val articFile = File(tempRoot, "stream.artic").apply { writeText("articbase://192.168.1.50") }
            val appFile = File(tempRoot, "content.app").apply { writeBytes(buildSyntheticNcch(0x000400000F700200L, "AppTest", "Nintendo")) }
            val cfaFile = File(tempRoot, "manual.cfa").apply { writeBytes(buildSyntheticNcch(0x0004008C0F700200L, "CfaTest", "Nintendo")) }
            val genericZ3ds = File(tempRoot, "generic.z3ds").apply {
                val innerZcci = extracted.first { it.extension.equals("zcci", true) }
                innerZcci.copyTo(this, overwrite = true)
            }
            assertTrue(StormNczEngine.verifyContainerIntegrity(elfFile).valid)
            assertTrue(StormNczEngine.verifyContainerIntegrity(axfFile).valid)
            assertTrue(StormNczEngine.verifyContainerIntegrity(articFile).valid)
            assertTrue(StormNczEngine.inspectContainer(articFile).first().name.contains("articbase://"))
            assertTrue(StormNczEngine.verifyContainerIntegrity(appFile).valid)
            assertTrue(StormNczEngine.verifyContainerIntegrity(cfaFile).valid)
            assertTrue(StormNczEngine.verifyContainerIntegrity(genericZ3ds).valid)
        } finally {
            tempRoot.deleteRecursively()
        }
    }
}
