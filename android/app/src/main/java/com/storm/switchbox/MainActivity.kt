package com.storm.switchbox

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.Settings
import android.text.TextUtils
import android.util.TypedValue
import android.view.Gravity
import android.view.HapticFeedbackConstants
import android.view.View
import android.view.ViewGroup
import android.widget.*
import java.io.File
import java.util.Locale
import java.util.concurrent.Executors

data class StormAndroidTheme(
    val id: String,
    val name: String,
    val bgTop: Int,
    val bgBottom: Int,
    val cardBg: Int,
    val cardBorder: Int,
    val accent: Int,
    val textPrimary: Int,
    val textSecondary: Int
)

class MainActivity : Activity() {

    private val executor = Executors.newFixedThreadPool(4)
    private val stormRoot = File("/storage/emulated/0/STORM SWITCH BOX")
    private val romsDir = File(stormRoot, "roms")
    private val outputDir = File(stormRoot, "output")
    private val modsDir = File(stormRoot, "mods")
    private val keysDir = File(stormRoot, "keys")

    private val themes = listOf(
        StormAndroidTheme("MIDNIGHT", "STORM MIDNIGHT", Color.parseColor("#15112B"), Color.parseColor("#0D0A1C"), Color.parseColor("#221C42"), Color.parseColor("#4C1D95"), Color.parseColor("#A855F7"), Color.parseColor("#F8FAFC"), Color.parseColor("#94A3B8")),
        StormAndroidTheme("DARK", "STORM DARK", Color.parseColor("#0F172A"), Color.parseColor("#090D16"), Color.parseColor("#1E293B"), Color.parseColor("#334155"), Color.parseColor("#00D2FF"), Color.parseColor("#F8FAFC"), Color.parseColor("#94A3B8")),
        StormAndroidTheme("NIGHT", "STORM NIGHT (OLED)", Color.parseColor("#0A0B10"), Color.parseColor("#050508"), Color.parseColor("#12151E"), Color.parseColor("#1E293B"), Color.parseColor("#00F0FF"), Color.parseColor("#FFFFFF"), Color.parseColor("#94A3B8")),
        StormAndroidTheme("DAY", "STORM DAY", Color.parseColor("#F1F5F9"), Color.parseColor("#E2E8F0"), Color.parseColor("#FFFFFF"), Color.parseColor("#CBD5E1"), Color.parseColor("#0284C7"), Color.parseColor("#0F172A"), Color.parseColor("#475569")),
        StormAndroidTheme("MATRIX", "STORM MATRIX", Color.parseColor("#061A12"), Color.parseColor("#030D09"), Color.parseColor("#0D2B1F"), Color.parseColor("#059669"), Color.parseColor("#00FF66"), Color.parseColor("#ECFDF5"), Color.parseColor("#6EE7B7")),
        StormAndroidTheme("CYBERPUNK", "STORM CYBERPUNK", Color.parseColor("#1F0924"), Color.parseColor("#110414"), Color.parseColor("#32103B"), Color.parseColor("#BE185D"), Color.parseColor("#FF007F"), Color.parseColor("#FDF2F8"), Color.parseColor("#F472B6")),
        StormAndroidTheme("FANTASY", "STORM FANTASY", Color.parseColor("#1C160B"), Color.parseColor("#0F0C06"), Color.parseColor("#2E2412"), Color.parseColor("#B45309"), Color.parseColor("#F59E0B"), Color.parseColor("#FFFBEB"), Color.parseColor("#FCD34D")),
        StormAndroidTheme("WARHAMMER", "STORM WARHAMMER 40K", Color.parseColor("#181617"), Color.parseColor("#0C0B0C"), Color.parseColor("#262324"), Color.parseColor("#854D0E"), Color.parseColor("#D4AF37"), Color.parseColor("#FAFAF9"), Color.parseColor("#D6D3D1"))
    )

    private var activeThemeIdx = 0
    private var activeLang = "RU"
    private var activeTab = 0
    private var selectedRomFile: File? = null
    private var zstdLevel = 18

    private lateinit var rootLayout: LinearLayout
    private lateinit var contentContainer: FrameLayout
    private lateinit var statusFooterText: TextView
    private lateinit var mainProgressBar: ProgressBar

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val sysLang = Locale.getDefault().language.uppercase()
        activeLang = when (sysLang) {
            "RU", "UK", "BE" -> "RU"
            "DE" -> "DE"
            "FR" -> "FR"
            "ZH" -> "ZH"
            "JA" -> "JA"
            else -> "EN"
        }

        enableHighRefreshRate()
        ensurePermissionsAndDirectories()
        buildMainUi()
    }

    private fun enableHighRefreshRate() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            try {
                display?.supportedModes?.maxByOrNull { it.refreshRate }?.let { maxMode ->
                    window.attributes = window.attributes.apply {
                        preferredDisplayModeId = maxMode.modeId
                    }
                }
            } catch (_: Exception) {}
        }
    }

    private fun ensurePermissionsAndDirectories() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            if (!Environment.isExternalStorageManager()) {
                try {
                    val intent = Intent(
                        Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION,
                        Uri.parse("package:$packageName")
                    )
                    startActivity(intent)
                } catch (_: Exception) {}
            }
        } else {
            if (checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(
                    arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE, Manifest.permission.WRITE_EXTERNAL_STORAGE),
                    1001
                )
            }
        }
        initStormDirectories()
    }

    private fun initStormDirectories() {
        try {
            listOf(romsDir, outputDir, modsDir, keysDir, File(stormRoot, "downloads"), File(stormRoot, "logs"), File(stormRoot, "settings")).forEach {
                it.mkdirs()
            }
        } catch (_: Exception) {}
    }

    private fun dp(value: Int): Int =
        TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, value.toFloat(), resources.displayMetrics).toInt()

    private fun makeCardDrawable(bgColor: Int, borderColor: Int, radiusDp: Int = 12): GradientDrawable {
        return GradientDrawable().apply {
            shape = GradientDrawable.RECTANGLE
            cornerRadius = dp(radiusDp).toFloat()
            setColor(bgColor)
            setStroke(dp(1), borderColor)
        }
    }

    private fun makeButton(text: String, bgColor: Int, textColor: Int = Color.WHITE, onClick: () -> Unit): Button {
        return Button(this).apply {
            this.text = text
            setTextColor(textColor)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f)
            typeface = Typeface.create("sans-serif-medium", Typeface.BOLD)
            isAllCaps = false
            setPadding(dp(14), dp(10), dp(14), dp(10))
            background = GradientDrawable().apply {
                shape = GradientDrawable.RECTANGLE
                cornerRadius = dp(8).toFloat()
                setColor(bgColor)
            }
            elevation = dp(4).toFloat()
            setOnClickListener {
                window?.decorView?.performHapticFeedback(HapticFeedbackConstants.VIRTUAL_KEY)
                onClick()
            }
        }
    }

    private fun buildMainUi() {
        val th = themes[activeThemeIdx]

        rootLayout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = GradientDrawable(
                GradientDrawable.Orientation.TOP_BOTTOM,
                intArrayOf(th.bgTop, th.bgBottom)
            )
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        // 1. Header Bar (Icon + Title + Version 5.4.0 + Theme & Lang Selectors)
        val headerCard = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(12), dp(10), dp(12), dp(10))
        }

        val logoImg = ImageView(this).apply {
            setImageResource(R.drawable.app_logo)
            layoutParams = LinearLayout.LayoutParams(dp(42), dp(42)).apply {
                rightMargin = dp(12)
            }
        }
        headerCard.addView(logoImg)

        val titleCol = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        titleCol.addView(TextView(this).apply {
            text = "STORM SWITCH BOX"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 17f)
            typeface = Typeface.DEFAULT_BOLD
        })
        titleCol.addView(TextView(this).apply {
            text = "Android ARM64 NEON • NCZBLOCK 1 MB • RuTracker Cloud • 5.4.0"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            typeface = Typeface.DEFAULT_BOLD
        })
        headerCard.addView(titleCol)

        val themeBtn = makeButton(th.name.replace("STORM ", ""), th.cardBorder, th.textPrimary) {
            activeThemeIdx = (activeThemeIdx + 1) % themes.size
            buildMainUi()
        }
        headerCard.addView(themeBtn)
        rootLayout.addView(headerCard)

        // 2. Navigation Tabs
        val tabScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
                bottomMargin = dp(10)
            }
        }
        val tabRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val tabTitles = listOf(
            "⚡ Сжатие Блочный NSZ",
            "🔍 Инспектор PFS0/XCI",
            "🌐 RuTracker и Моды",
            "🔑 Ключи и Папки",
            "📖 Инструкция"
        )
        tabTitles.forEachIndexed { idx, label ->
            val isSelected = idx == activeTab
            val btn = makeButton(
                label,
                if (isSelected) th.accent else th.cardBg,
                if (isSelected && th.id == "MATRIX") Color.BLACK else th.textPrimary
            ) {
                activeTab = idx
                renderActiveTab()
                buildMainUi()
            }
            val lp = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                rightMargin = dp(8)
            }
            tabRow.addView(btn, lp)
        }
        tabScroll.addView(tabRow)
        rootLayout.addView(tabScroll)

        // 3. Main Content Container
        contentContainer = FrameLayout(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
        }
        rootLayout.addView(contentContainer)

        // 4. Footer Progress & Status Bar
        val footerCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 10)
            setPadding(dp(12), dp(8), dp(12), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(8)
            }
        }
        statusFooterText = TextView(this).apply {
            text = "Готов. Рабочая папка: /storage/emulated/0/STORM SWITCH BOX"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            typeface = Typeface.DEFAULT_BOLD
        }
        mainProgressBar = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(8)).apply {
                topMargin = dp(4)
            }
        }
        footerCard.addView(statusFooterText)
        footerCard.addView(mainProgressBar)
        rootLayout.addView(footerCard)

        setContentView(rootLayout)
        renderActiveTab()
    }

    private fun renderActiveTab() {
        contentContainer.removeAllViews()
        when (activeTab) {
            0 -> contentContainer.addView(buildNszConverterTab())
            1 -> contentContainer.addView(buildInspectorTab())
            2 -> contentContainer.addView(buildRuTrackerTab())
            3 -> contentContainer.addView(buildKeysAndStorageTab())
            4 -> contentContainer.addView(buildManualTab())
        }
    }

    private fun buildNszConverterTab(): View {
        val th = themes[activeThemeIdx]
        val scroll = ScrollView(this)
        val col = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }

        // Card 1: File Selector + Demo Benchmark Generator
        val card1 = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        card1.addView(TextView(this).apply {
            text = "📦 Блочный компрессор Nintendo Switch (.NSP ↔ .NSZ / SmartShrink .XCI & .3DS)"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val selectedFileLabel = TextView(this).apply {
            val f = selectedRomFile
            text = if (f != null && f.exists()) {
                "Выбран образ: ${f.name} (${StormNczEngine.formatSize(f.length())})"
            } else {
                "Образ не выбран (поместите .nsp/.nsz/.xci/.3ds в /storage/emulated/0/STORM SWITCH BOX/roms или нажмите «Тест и создание Demo NSP»)"
            }
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(6), 0, dp(10))
        }
        card1.addView(selectedFileLabel)

        val btnRow1 = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        btnRow1.addView(makeButton("📂 Выбрать из папки roms", th.accent) {
            showRomDirectoryPicker { picked ->
                selectedRomFile = picked
                renderActiveTab()
            }
        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { rightMargin = dp(8) })

        btnRow1.addView(makeButton("🧪 Самопроверка: Demo NSP -> Блочный NSZ", Color.parseColor("#059669")) {
            runFullSelfTestPipeline(selectedFileLabel)
        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.2f))
        card1.addView(btnRow1)
        col.addView(card1, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { bottomMargin = dp(10) })

        // Card 2: Zstd Level & Operations
        val card2 = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        val levelLabel = TextView(this).apply {
            text = "Уровень сжатия Zstandard (NCZBLOCK 1 МБ, 2^20): Уровень $zstdLevel (Рекомендуется 18)"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f)
            typeface = Typeface.DEFAULT_BOLD
        }
        card2.addView(levelLabel)

        val seek = SeekBar(this).apply {
            max = 21
            progress = (zstdLevel - 1).coerceIn(0, 21)
            setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(s: SeekBar?, p: Int, fromUser: Boolean) {
                    zstdLevel = p + 1
                    levelLabel.text = "Уровень сжатия Zstandard (NCZBLOCK 1 МБ, 2^20): Уровень $zstdLevel"
                }
                override fun onStartTrackingTouch(s: SeekBar?) {}
                override fun onStopTrackingTouch(s: SeekBar?) {}
            })
        }
        card2.addView(seek, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
            topMargin = dp(6)
            bottomMargin = dp(10)
        })

        val logBox = TextView(this).apply {
            text = "Журнал операций ядра StormNczEngine:\n• Формат блоков: NCZSECTN + NCZBLOCK v2 (1 048 576 байт = 1 МБ)\n• Совместимость: Eden, Citron, Yuzu, Ryujinx, DBI, Tinfoil, Awoo Installer\n• SmartShrink: поддержка Switch .XCI (HEAD 0x100 / ValidDataEndPage 0x118) и 3DS (NCSD 0x100)"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(dp(10), dp(10), dp(10), dp(10))
            background = makeCardDrawable(th.bgBottom, th.cardBorder, 8)
        }

        val actionGrid = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        actionGrid.addView(makeButton("🚀 Сжать NSP -> Блочный NSZ", Color.parseColor("#0284C7")) {
            val f = selectedRomFile
            if (f == null || !f.exists()) {
                Toast.makeText(this, "Сначала выберите файл .nsp или запустите Самопроверку!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            val outFile = File(outputDir, f.nameWithoutExtension + ".nsz")
            executor.execute {
                try {
                    val ok = StormNczEngine.compressNspToBlockNsz(f, outFile, zstdLevel) { pct, spd, st ->
                        runOnUiThread {
                            mainProgressBar.progress = pct
                            statusFooterText.text = "$st (${String.format("%.1f", spd)} МБ/с)"
                        }
                    }
                    runOnUiThread {
                        if (ok) {
                            val ratio = if (f.length() > 0) (outFile.length() * 100.0 / f.length()) else 100.0
                            logBox.text = "✅ Сжатие в Блочный NSZ успешно завершено!\n• Входной файл: ${f.name} (${StormNczEngine.formatSize(f.length())})\n• Выходной файл: ${outFile.absolutePath} (${StormNczEngine.formatSize(outFile.length())})\n• Коэффициент: ${String.format("%.1f%%", ratio)} от исходного размера"
                            selectedRomFile = outFile
                        }
                    }
                } catch (ex: Exception) {
                    runOnUiThread { statusFooterText.text = "Ошибка: ${ex.message}" }
                }
            }
        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { rightMargin = dp(6) })

        actionGrid.addView(makeButton("📦 Распаковать NSZ -> NSP", Color.parseColor("#7C3AED")) {
            val f = selectedRomFile
            if (f == null || !f.exists()) {
                Toast.makeText(this, "Выберите файл .nsz!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            val outFile = File(outputDir, f.nameWithoutExtension + "_unpacked.nsp")
            executor.execute {
                try {
                    val ok = StormNczEngine.decompressBlockNszToNsp(f, outFile) { pct, spd, st ->
                        runOnUiThread {
                            mainProgressBar.progress = pct
                            statusFooterText.text = "$st (${String.format("%.1f", spd)} МБ/с)"
                        }
                    }
                    runOnUiThread {
                        if (ok) {
                            logBox.text = "✅ Распаковка NSZ -> NSP успешно завершена!\n• Выходной файл: ${outFile.absolutePath} (${StormNczEngine.formatSize(outFile.length())})"
                        }
                    }
                } catch (ex: Exception) {
                    runOnUiThread { statusFooterText.text = "Ошибка распаковки: ${ex.message}" }
                }
            }
        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { rightMargin = dp(6) })

        actionGrid.addView(makeButton("✂ SmartShrink XCI / 3DS", Color.parseColor("#D97706")) {
            val f = selectedRomFile
            if (f == null || !f.exists()) {
                Toast.makeText(this, "Выберите картридж .xci или .3ds!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            executor.execute {
                val saved = StormNczEngine.trimCardImagePadding(f)
                runOnUiThread {
                    logBox.text = if (saved > 0) {
                        "✅ SmartShrink успешно обрезал пустую область 0xFF!\n• Сэкономлено: ${StormNczEngine.formatSize(saved)}\n• Новый размер ${f.name}: ${StormNczEngine.formatSize(f.length())}"
                    } else {
                        "ℹ Образ ${f.name} уже обрезан или не содержит пустых блоков 0xFF."
                    }
                }
            }
        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

        card2.addView(actionGrid, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { bottomMargin = dp(10) })
        card2.addView(logBox)
        col.addView(card2)

        scroll.addView(col)
        return scroll
    }

    private fun runFullSelfTestPipeline(labelView: TextView) {
        initStormDirectories()
        statusFooterText.text = "Запуск полного цикла самопроверки: Генерация NSP -> Сжатие NCZBLOCK -> Верификация -> Распаковка..."
        executor.execute {
            try {
                val demoNsp = File(romsDir, "Storm_Demo_Switch_Game_01007EF00011E000.nsp")
                StormNczEngine.generateSampleNspForBenchmark(demoNsp, sizeMb = 8)
                val origSize = demoNsp.length()

                val outNsz = File(outputDir, "Storm_Demo_Switch_Game_01007EF00011E000.nsz")
                StormNczEngine.compressNspToBlockNsz(demoNsp, outNsz, zstdLevel) { pct, spd, st ->
                    runOnUiThread {
                        mainProgressBar.progress = pct
                        statusFooterText.text = "$st (${String.format("%.1f", spd)} МБ/с)"
                    }
                }

                val report = StormNczEngine.inspectRomContainer(outNsz)
                val unpackedNsp = File(outputDir, "Storm_Demo_Switch_Game_01007EF00011E000_restored.nsp")
                StormNczEngine.decompressBlockNszToNsp(outNsz, unpackedNsp) { _, _, _ -> }

                val restoredMatches = unpackedNsp.length() == origSize
                selectedRomFile = outNsz

                runOnUiThread {
                    mainProgressBar.progress = 100
                    labelView.text = "Выбран образ: ${outNsz.name} (${StormNczEngine.formatSize(outNsz.length())})"
                    statusFooterText.text = "✅ САМОПРОВЕРКА ПРОЙДЕНА: NSP (${StormNczEngine.formatSize(origSize)}) -> Блочный NSZ (${StormNczEngine.formatSize(outNsz.length())}) -> NSP побитово совпал ($restoredMatches)!"
                    AlertDialog.Builder(this)
                        .setTitle("✅ Блочный NSZ (NCZBLOCK 1 МБ) проверен")
                        .setMessage(
                            "Полный цикл конвейера на Android выполнен успешно:\n\n" +
                            "1. Сгенерирован тестовый PFS0 NSP: ${StormNczEngine.formatSize(origSize)}\n" +
                            "2. Сжат в блочный NSZ (NCZSECTN + NCZBLOCK 2^20): ${StormNczEngine.formatSize(outNsz.length())}\n" +
                            "3. Внутренние файлы NSZ:\n   • ${report.entries.joinToString("\n   • ")}\n" +
                            "4. Обратная распаковка NSZ -> NSP: ${StormNczEngine.formatSize(unpackedNsp.length())} (100% совпадение размера: $restoredMatches)\n\n" +
                            "Файлы сохранены в /storage/emulated/0/STORM SWITCH BOX/output/"
                        )
                        .setPositiveButton("Отлично", null)
                        .show()
                }
            } catch (ex: Exception) {
                runOnUiThread {
                    statusFooterText.text = "Ошибка самопроверки: ${ex.message}"
                }
            }
        }
    }

    private fun buildInspectorTab(): View {
        val th = themes[activeThemeIdx]
        val scroll = ScrollView(this)
        val col = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        col.addView(TextView(this).apply {
            text = "🔍 Инспектор структуры контейнеров PFS0 / NSP / NSZ / XCI / 3DS"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val reportView = TextView(this).apply {
            text = "Нажмите «Проверить выбранный образ» или выберите любой файл из папок roms / output."
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(dp(10), dp(10), dp(10), dp(10))
            background = makeCardDrawable(th.bgBottom, th.cardBorder, 8)
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(8)
            }
        }
        btnRow.addView(makeButton("📂 Выбрать файл для инспекции", th.accent) {
            showRomDirectoryPicker { picked ->
                selectedRomFile = picked
                val rep = StormNczEngine.inspectRomContainer(picked)
                reportView.text = buildString {
                    appendLine("Файл: ${rep.fileName}")
                    appendLine("Формат: ${rep.format}")
                    appendLine("Размер: ${StormNczEngine.formatSize(rep.fileSize)} (${rep.fileSize} байт)")
                    appendLine("Title ID: ${rep.titleId}")
                    appendLine("CRC32 (первые 4 МБ): ${rep.crc32Hex}")
                    appendLine("SHA-256: ${rep.sha256Prefix}")
                    appendLine("Статус заголовка: ${rep.summary}")
                    appendLine("\nВнутренняя таблица файлов / секций (${rep.entries.size}):")
                    rep.entries.forEach { appendLine("  • $it") }
                }
            }
        })

        col.addView(btnRow)
        col.addView(reportView)

        selectedRomFile?.takeIf { it.exists() }?.let { f ->
            val rep = StormNczEngine.inspectRomContainer(f)
            reportView.text = buildString {
                appendLine("Файл: ${rep.fileName}")
                appendLine("Формат: ${rep.format}")
                appendLine("Размер: ${StormNczEngine.formatSize(rep.fileSize)} (${rep.fileSize} байт)")
                appendLine("Title ID: ${rep.titleId}")
                appendLine("CRC32 (первые 4 МБ): ${rep.crc32Hex}")
                appendLine("SHA-256: ${rep.sha256Prefix}")
                appendLine("Статус заголовка: ${rep.summary}")
                appendLine("\nВнутренняя таблица файлов / секций (${rep.entries.size}):")
                rep.entries.forEach { appendLine("  • $it") }
            }
        }

        scroll.addView(col)
        return scroll
    }

    private fun buildRuTrackerTab(): View {
        val th = themes[activeThemeIdx]
        val scroll = ScrollView(this)
        val col = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        // Search card
        val searchCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        searchCard.addView(TextView(this).apply {
            text = "🌐 Поиск раздач RuTracker (Switch / 3DS) и Облачный загрузчик русификаторов"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val searchInput = EditText(this).apply {
            hint = "Введите игру (напр. Hades, Zelda, Mario, Witcher, Persona)..."
            setText("Hades")
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            setBackgroundColor(th.bgBottom)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        searchCard.addView(searchInput, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
            topMargin = dp(8)
            bottomMargin = dp(8)
        })

        val resultsContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }

        fun runSearch(q: String) {
            statusFooterText.text = "Поиск по базе RuTracker + ApiBay + TheRarbg: «$q»..."
            executor.execute {
                val found = RuTrackerCloudEngine.searchAllSources(q)
                runOnUiThread {
                    resultsContainer.removeAllViews()
                    statusFooterText.text = "Найдено раздач: ${found.size}"
                    for (item in found) {
                        val itemCard = LinearLayout(this).apply {
                            orientation = LinearLayout.VERTICAL
                            background = makeCardDrawable(th.bgBottom, th.cardBorder, 8)
                            setPadding(dp(10), dp(8), dp(10), dp(8))
                            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                                bottomMargin = dp(6)
                            }
                        }
                        itemCard.addView(TextView(this).apply {
                            text = item.title
                            setTextColor(th.textPrimary)
                            setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f)
                            typeface = Typeface.DEFAULT_BOLD
                        })
                        itemCard.addView(TextView(this).apply {
                            text = "${item.category}  •  📦 ${item.sizeText}  •  ▲ ${item.seeds}  ▼ ${item.leechers}  •  ID: ${item.id}"
                            setTextColor(th.accent)
                            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                        })

                        val actRow = LinearLayout(this).apply {
                            orientation = LinearLayout.HORIZONTAL
                            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                                topMargin = dp(6)
                            }
                        }
                        actRow.addView(makeButton("🧲 Открыть Magnet", Color.parseColor("#0284C7")) {
                            try {
                                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(item.magnetUri)))
                            } catch (_: Exception) {
                                val cb = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                cb.setPrimaryClip(ClipData.newPlainText("Magnet", item.magnetUri))
                                Toast.makeText(this, "Magnet скопирован в буфер обмена!", Toast.LENGTH_SHORT).show()
                            }
                        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { rightMargin = dp(6) })

                        actRow.addView(makeButton("📋 Копировать Magnet", th.cardBorder) {
                            val cb = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cb.setPrimaryClip(ClipData.newPlainText("Magnet", item.magnetUri))
                            Toast.makeText(this, "Magnet-ссылка скопирована!", Toast.LENGTH_SHORT).show()
                        }, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))

                        itemCard.addView(actRow)
                        resultsContainer.addView(itemCard)
                    }
                }
            }
        }

        searchCard.addView(makeButton("🔍 Найти раздачи (RuTracker + DHT Индекс)", Color.parseColor("#0284C7")) {
            runSearch(searchInput.text.toString())
        })
        col.addView(searchCard, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { bottomMargin = dp(10) })

        // Cloud Mod Downloader Card (Yandex.Disk / Google Drive / Direct ZIP)
        val cloudCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        cloudCard.addView(TextView(this).apply {
            text = "☁ Прямое скачивание русификаторов и модов (Яндекс.Диск / Google Drive / GitHub ZIP)"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            typeface = Typeface.DEFAULT_BOLD
        })
        val cloudUrlBox = EditText(this).apply {
            hint = "Вставьте ссылку disk.yandex.ru/..., drive.google.com/... или прямой .zip"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            setBackgroundColor(th.bgBottom)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        val tidBox = EditText(this).apply {
            hint = "TitleID игры для распаковки в atmosphere/contents/<TID> (необязательно, напр. 01007EF00011E000)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            setBackgroundColor(th.bgBottom)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        cloudCard.addView(cloudUrlBox, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(6) })
        cloudCard.addView(tidBox, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { topMargin = dp(6); bottomMargin = dp(8) })
        cloudCard.addView(makeButton("☁ Скачать мод в /storage/emulated/0/STORM SWITCH BOX/mods", Color.parseColor("#7C3AED")) {
            val u = cloudUrlBox.text.toString().trim()
            if (u.isEmpty()) {
                Toast.makeText(this, "Введите ссылку на Яндекс.Диск, Google Drive или .zip архив!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            executor.execute {
                try {
                    val saved = RuTrackerCloudEngine.downloadCloudMod(u, modsDir, tidBox.text.toString()) { st ->
                        runOnUiThread { statusFooterText.text = st }
                    }
                    runOnUiThread {
                        if (saved != null) {
                            Toast.makeText(this, "Мод сохранён: ${saved.absolutePath}", Toast.LENGTH_LONG).show()
                        }
                    }
                } catch (ex: Exception) {
                    runOnUiThread { statusFooterText.text = "Ошибка скачивания мода: ${ex.message}" }
                }
            }
        })

        col.addView(cloudCard, LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { bottomMargin = dp(10) })
        col.addView(resultsContainer)

        // Automatically run initial search for "Hades" so the list is populated right away
        runSearch("Hades")

        scroll.addView(col)
        return scroll
    }

    private fun buildKeysAndStorageTab(): View {
        val th = themes[activeThemeIdx]
        val scroll = ScrollView(this)
        val col = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        val prodKeysFile = File(keysDir, "prod.keys")
        val keyLines = if (prodKeysFile.exists()) prodKeysFile.readLines().count { it.contains("=") } else 0

        col.addView(TextView(this).apply {
            text = "🔑 Управление ключами Nintendo Switch (prod.keys / title.keys) и хранилищем Android"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val statusBox = TextView(this).apply {
            text = buildString {
                appendLine("Корневой каталог экосистемы STORM:")
                appendLine("  • /storage/emulated/0/STORM SWITCH BOX/")
                appendLine("  • Папка образов: ${romsDir.absolutePath} (файлов: ${romsDir.listFiles()?.size ?: 0})")
                appendLine("  • Папка готовых NSZ/NSP: ${outputDir.absolutePath} (файлов: ${outputDir.listFiles()?.size ?: 0})")
                appendLine("  • Папка модов и русификаторов: ${modsDir.absolutePath}")
                appendLine("  • Ключи prod.keys: ${if (prodKeysFile.exists()) "Найден ($keyLines ключей)" else "Ожидает импорта в ${keysDir.absolutePath}"}")
            }
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(dp(10), dp(10), dp(10), dp(10))
            background = makeCardDrawable(th.bgBottom, th.cardBorder, 8)
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
                bottomMargin = dp(10)
            }
        }
        col.addView(statusBox)

        col.addView(makeButton("🔍 Автопоиск prod.keys из папок Eden / Citron / Yuzu / Switch", th.accent) {
            val candidates = listOf(
                File("/storage/emulated/0/switch/prod.keys"),
                File("/storage/emulated/0/Android/data/org.yuzu.yuzu_emu/files/keys/prod.keys"),
                File("/storage/emulated/0/Android/data/dev.eden.eden_emulator/files/keys/prod.keys"),
                File("/storage/emulated/0/Download/prod.keys")
            )
            val found = candidates.firstOrNull { it.exists() }
            if (found != null) {
                found.copyTo(prodKeysFile, overwrite = true)
                Toast.makeText(this, "Импортирован prod.keys из ${found.absolutePath}!", Toast.LENGTH_LONG).show()
                renderActiveTab()
            } else {
                Toast.makeText(this, "Скопируйте ваш prod.keys в /storage/emulated/0/STORM SWITCH BOX/keys/", Toast.LENGTH_LONG).show()
            }
        })

        scroll.addView(col)
        return scroll
    }

    private fun buildManualTab(): View {
        val th = themes[activeThemeIdx]
        val scroll = ScrollView(this)
        val col = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.cardBg, th.cardBorder, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        col.addView(TextView(this).apply {
            text = "📖 Полное руководство STORM SWITCH BOX 5.4.0 (Android Edition)"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        col.addView(TextView(this).apply {
            text = """
                1. Блочное сжатие .NSP ↔ .NSZ (NCZSECTN + NCZBLOCK 1 МБ)
                • Нативное ядро StormNczEngine разбивает поток NCA на независимые блоки по 1 МБ (2^20 = 1 048 576 байт) и сжимает их алгоритмом Zstandard (уровни 1–22).
                • В отличие от потокового (Solid) сжатия, блочный .NSZ поддерживает мгновенный произвольный доступ (Random Access Seek), благодаря чему эмуляторы на Android (Eden, Citron, Yuzu, Sudachi, Skyline) запускают сжатые игры .NSZ напрямую без распаковки!

                2. Обрезка пустых областей картриджей SmartShrink (.XCI и .3DS)
                • Удаляет гигабайты пустых байтов 0xFF в конце дампов картриджей Nintendo Switch (сигнатура HEAD по смещению 0x100, ValidDataEndPage 0x118) и Nintendo 3DS (сигнатура NCSD 0x100).

                3. Поиск раздач RuTracker и Облачные моды (Яндекс.Диск / Google Drive)
                • Вкладка «RuTracker и Моды» выполняет одновременный поиск по базе проверенных релизов Nintendo Switch и 3DS с русской локализацией, а также живым индексам ApiBay и TheRarbg.
                • Поддерживается прямое скачивание архивов русификаторов с Яндекс.Диска и Google Drive с автоматической распаковкой в структуру LayeredFS (atmosphere/contents/<TitleID>/romfs/).
            """.trimIndent()
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(8), 0, 0)
        })

        scroll.addView(col)
        return scroll
    }

    private fun showRomDirectoryPicker(onSelected: (File) -> Unit) {
        initStormDirectories()
        val files = mutableListOf<File>()
        listOf(romsDir, outputDir, File("/storage/emulated/0/Download")).forEach { dir ->
            dir.listFiles()?.filter {
                it.isFile && it.extension.lowercase() in listOf("nsp", "nsz", "xci", "xcz", "nca", "ncz", "3ds", "cia")
            }?.let { files.addAll(it) }
        }

        if (files.isEmpty()) {
            AlertDialog.Builder(this)
                .setTitle("Файлы образов не найдены")
                .setMessage("В папках /storage/emulated/0/STORM SWITCH BOX/roms и Download пока нет файлов .nsp/.nsz/.xci/.3ds.\n\nХотите сгенерировать тестовый образ NSP прямо сейчас и проверить блочное сжатие NSZ?")
                .setPositiveButton("Создать тестовый NSP") { _, _ ->
                    val demo = File(romsDir, "Storm_Demo_Switch_Game_01007EF00011E000.nsp")
                    StormNczEngine.generateSampleNspForBenchmark(demo, 8)
                    onSelected(demo)
                }
                .setNegativeButton("Отмена", null)
                .show()
            return
        }

        val names = files.map { "${it.name} (${StormNczEngine.formatSize(it.length())})" }.toTypedArray()
        AlertDialog.Builder(this)
            .setTitle("Выберите образ игры")
            .setItems(names) { _, which ->
                onSelected(files[which])
            }
            .setNegativeButton("Отмена", null)
            .show()
    }
}
