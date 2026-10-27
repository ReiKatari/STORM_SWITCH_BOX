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
import android.os.Handler
import android.os.Looper
import android.provider.DocumentsContract
import android.provider.OpenableColumns
import android.provider.Settings
import android.text.TextUtils
import android.util.TypedValue
import android.view.Gravity
import android.view.KeyEvent
import android.view.View
import android.view.ViewGroup
import android.view.Window
import android.view.WindowInsets
import android.view.WindowInsetsController
import android.view.WindowManager
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.HorizontalScrollView
import android.widget.LinearLayout
import android.widget.PopupWindow
import android.widget.ProgressBar
import android.widget.ScrollView
import android.widget.SeekBar
import android.widget.Spinner
import android.widget.TextView
import android.widget.Toast
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.net.NetworkInterface
import java.text.SimpleDateFormat
import java.util.Collections
import java.util.Date
import java.util.Locale
import kotlin.concurrent.thread

class MainActivity : Activity() {

    data class StormTheme(
        val id: String,
        val title: String,
        val bgPrimary: Int,
        val bgCard: Int,
        val bgElevated: Int,
        val textPrimary: Int,
        val textSecondary: Int,
        val accent: Int,
        val border: Int
    )

    private val themes = listOf(
        StormTheme("MIDNIGHT", "STORM MIDNIGHT", Color.parseColor("#0F0C1D"), Color.parseColor("#18132E"), Color.parseColor("#231C42"), Color.parseColor("#F8FAFC"), Color.parseColor("#A5B4FC"), Color.parseColor("#A855F7"), Color.parseColor("#3B2F63")),
        StormTheme("DARK", "STORM DARK", Color.parseColor("#0B0F19"), Color.parseColor("#131B2E"), Color.parseColor("#1E293B"), Color.parseColor("#F8FAFC"), Color.parseColor("#94A3B8"), Color.parseColor("#00D2FF"), Color.parseColor("#2D3D5B")),
        StormTheme("NIGHT", "STORM NIGHT (OLED)", Color.parseColor("#050508"), Color.parseColor("#0D0F17"), Color.parseColor("#161926"), Color.parseColor("#FFFFFF"), Color.parseColor("#9CA3AF"), Color.parseColor("#00F0FF"), Color.parseColor("#22293E")),
        StormTheme("DAY", "STORM DAY", Color.parseColor("#F1F5F9"), Color.parseColor("#FFFFFF"), Color.parseColor("#E2E8F0"), Color.parseColor("#0F172A"), Color.parseColor("#475569"), Color.parseColor("#0284C7"), Color.parseColor("#CBD5E1")),
        StormTheme("MATRIX", "STORM MATRIX", Color.parseColor("#04120B"), Color.parseColor("#092216"), Color.parseColor("#103824"), Color.parseColor("#ECFDF5"), Color.parseColor("#6EE7B7"), Color.parseColor("#00FF66"), Color.parseColor("#1B5E3C")),
        StormTheme("CYBERPUNK", "STORM CYBERPUNK", Color.parseColor("#14071E"), Color.parseColor("#220D33"), Color.parseColor("#34154D"), Color.parseColor("#FDF4FF"), Color.parseColor("#F0ABFC"), Color.parseColor("#FF007F"), Color.parseColor("#5B2182")),
        StormTheme("FANTASY", "STORM FANTASY", Color.parseColor("#120E07"), Color.parseColor("#1F180D"), Color.parseColor("#2E2414"), Color.parseColor("#FFFBEB"), Color.parseColor("#FDE68A"), Color.parseColor("#F59E0B"), Color.parseColor("#544021")),
        StormTheme("WARHAMMER", "STORM WARHAMMER 40K", Color.parseColor("#111113"), Color.parseColor("#1C1C21"), Color.parseColor("#292930"), Color.parseColor("#F9FAFB"), Color.parseColor("#D1D5DB"), Color.parseColor("#D4AF37"), Color.parseColor("#4B4328")),
        StormTheme("CRIMSON", "STORM CRIMSON", Color.parseColor("#160709"), Color.parseColor("#260C10"), Color.parseColor("#381218"), Color.parseColor("#FEF2F2"), Color.parseColor("#FCA5A5"), Color.parseColor("#EF4444"), Color.parseColor("#611F2A"))
    )

    private val languages = listOf(
        "ru" to "Русский (RU)",
        "en" to "English (EN)",
        "de" to "Deutsch (DE)",
        "fr" to "Français (FR)",
        "es" to "Español (ES)",
        "it" to "Italiano (IT)",
        "pt" to "Português (PT)",
        "pl" to "Polski (PL)",
        "tr" to "Türkçe (TR)",
        "zh" to "中文 (ZH)",
        "ja" to "日本語 (JA)"
    )

    private val stormRoot = File("/storage/emulated/0/STORM SWITCH BOX")
    private val romsDir = File(stormRoot, "roms")
    private var customOutputDir: File = File(stormRoot, "output")
    private val modsDir = File(stormRoot, "mods")
    private val keysDir = File(stormRoot, "keys")
    private val extractedDir = File(stormRoot, "extracted")
    private val settingsFile = File(stormRoot, "settings/settings.json")

    private var currentThemeIndex = 0
    private var currentLangCode = "ru"
    private var activeTab = 0

    // Параметры движка и пакетной очереди (сохраняются локально)
    private val queuedFiles = mutableListOf<File>()
    private var selectedInputFile: File? = null
    private var selectedOperationMode = 0
    // 0 = Умный мульти-контент (Сшивка База + Патч + DLC + Моды),
    // 1 = Пакетное сжатие и конвертация (По отдельности),
    // 2 = Распаковка NSZ/XCZ -> NSP/XCI,
    // 3 = SmartShrink Trim 0xFF (XCI/3DS),
    // 4 = Дешифратор Nintendo 3DS (.3ds/.cia)
    private var selectedOutputFormat = 0
    // 0 = Авто (Умный выбор минимального размера), 1 = NSP / NSZ (PFS0), 2 = XCI / XCZ (Картридж HFS0)
    private var useBlockNcz = true // По умолчанию включен!
    private var smartMergeDedup = true // По умолчанию включен!
    private var autoEmbedMods = true // По умолчанию включен!
    private var zstdLevel = 18
    private var blockSizeExpo = 20 // 1 MB
    private var cpuThreads = Runtime.getRuntime().availableProcessors().coerceIn(2, 8)
    private var smartSizeGuard = true
    private var trimXciBeforeCompress = true
    private var deleteSourceAfterSuccess = false
    private var autoCheckUpdatesOnStartup = true
    private var lastBrowseDirPath = "/storage/emulated/0"

    @Volatile private var isTaskRunning = false
    @Volatile private var cancelCurrentTaskRequested = false

    // Онлайн-каталог ключей STORM SWITCH TOOLS
    private var onlineKeysCatalog: List<RuTrackerCloudEngine.OnlineKeysAsset> = RuTrackerCloudEngine.getDefaultOnlineKeysCatalog()
    private var selectedOnlineKeyIndex = 0

    // Ожидающий установки APK (если запрашивалось разрешение REQUEST_INSTALL_PACKAGES)
    private var pendingInstallApkFile: File? = null

    private var dbiServer: RuTrackerCloudEngine.DbiHttpServer? = null
    private var dbiServerRunning = false
    private val mainHandler = Handler(Looper.getMainLooper())

    private lateinit var rootLayout: LinearLayout
    private lateinit var contentContainer: LinearLayout
    private lateinit var logTextView: TextView
    private val logLines = mutableListOf<String>()

    companion object {
        private const val REQ_PICK_MULTIPLE_FILES = 1001
        private const val REQ_PICK_FOLDER_TREE = 1002
        private const val REQ_PICK_KEYS_FILE = 1003
        private const val REQ_INSTALL_UNKNOWN_APPS = 1004
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        hideSystemBars(window)

        window.decorView.setOnSystemUiVisibilityChangeListener {
            hideSystemBars(window)
        }

        ensureStormDirectories()
        loadSettings()

        val sysLocale = Locale.getDefault().language.lowercase()
        if (!settingsFile.exists() && languages.any { it.first == sysLocale }) {
            currentLangCode = sysLocale
        }

        buildUserInterface()
        appendLog("STORM SWITCH BOX ${"5.5.1"} (${(if (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) "legacy" else "modern").uppercase()}) готов к работе.")

        // Автоматическая проверка обновлений с GitHub при запуске
        if (autoCheckUpdatesOnStartup) {
            checkForUpdatesFromGitHub(isManualClick = false)
        }
    }

    override fun onResume() {
        super.onResume()
        hideSystemBars(window)

        // Если пользователь только что выдал разрешение на установку APK из неизвестных источников
        val pendingApk = pendingInstallApkFile
        if (pendingApk != null && pendingApk.exists()) {
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O || packageManager.canRequestPackageInstalls()) {
                pendingInstallApkFile = null
                launchApkInstallerIntent(pendingApk)
            }
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            hideSystemBars(window)
        }
    }

    /**
     * Полное скрытие верхней системной строки состояния и нижней панели навигации (Immersive Sticky Fullscreen).
     */
    private fun hideSystemBars(targetWindow: Window?) {
        val w = targetWindow ?: return
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                val lp = w.attributes
                lp.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES
                w.attributes = lp
            }
            w.addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN)
            @Suppress("DEPRECATION")
            w.decorView.systemUiVisibility = (
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                    or View.SYSTEM_UI_FLAG_FULLSCREEN
                    or View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                    or View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                    or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                    or View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
            )
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                w.setDecorFitsSystemWindows(false)
                w.insetsController?.let { controller ->
                    controller.hide(WindowInsets.Type.statusBars() or WindowInsets.Type.navigationBars())
                    controller.systemBarsBehavior = WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                }
            }
        } catch (_: Throwable) {}
    }

    private fun showImmersiveDialog(builder: AlertDialog.Builder): AlertDialog {
        val dlg = builder.create()
        dlg.setOnKeyListener { d, keyCode, event ->
            if (keyCode == KeyEvent.KEYCODE_ESCAPE && event.action == KeyEvent.ACTION_UP) {
                d.dismiss()
                true
            } else false
        }
        dlg.window?.setFlags(WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE, WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE)
        dlg.show()
        dlg.window?.let { w ->
            hideSystemBars(w)
            w.clearFlags(WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE)
        }
        return dlg
    }

    private fun ensureStormDirectories() {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                if (!Environment.isExternalStorageManager()) {
                    try {
                        val intent = Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION)
                        intent.data = Uri.parse("package:$packageName")
                        startActivity(intent)
                    } catch (_: Exception) {}
                }
            } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                if (checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                    requestPermissions(arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE, Manifest.permission.WRITE_EXTERNAL_STORAGE), 101)
                }
            }
            listOf(
                romsDir,
                customOutputDir,
                modsDir,
                keysDir,
                extractedDir,
                File(stormRoot, "downloads"),
                File(stormRoot, "logs"),
                File(stormRoot, "settings")
            ).forEach { if (!it.exists()) it.mkdirs() }
        } catch (_: Exception) {}
    }

    private fun loadSettings() {
        try {
            val prefs = getSharedPreferences("storm_switch_box_prefs", Context.MODE_PRIVATE)
            val rawJson = if (settingsFile.exists()) {
                settingsFile.readText()
            } else {
                prefs.getString("settings_json", null)
            }
            if (!rawJson.isNullOrBlank()) {
                val json = JSONObject(rawJson)
                val themeId = json.optString("theme", "MIDNIGHT")
                val idx = themes.indexOfFirst { it.id == themeId }
                if (idx >= 0) currentThemeIndex = idx
                currentLangCode = json.optString("lang", "ru")
                selectedOperationMode = json.optInt("operationMode", 0).coerceIn(0, 4)
                selectedOutputFormat = json.optInt("outputFormat", 0).coerceIn(0, 2)
                useBlockNcz = json.optBoolean("useBlockNcz", true)
                smartMergeDedup = json.optBoolean("smartMergeDedup", true)
                autoEmbedMods = json.optBoolean("autoEmbedMods", true)
                zstdLevel = json.optInt("zstdLevel", 18).coerceIn(1, 22)
                blockSizeExpo = json.optInt("blockSizeExpo", 20).coerceIn(18, 22)
                cpuThreads = json.optInt("cpuThreads", cpuThreads).coerceIn(1, 8)
                smartSizeGuard = json.optBoolean("smartSizeGuard", true)
                trimXciBeforeCompress = json.optBoolean("trimXci", true)
                deleteSourceAfterSuccess = json.optBoolean("deleteSource", false)
                autoCheckUpdatesOnStartup = json.optBoolean("autoCheckUpdates", true)
                lastBrowseDirPath = json.optString("lastBrowseDir", "/storage/emulated/0")
                val savedOut = json.optString("outputDir", "")
                if (savedOut.isNotBlank()) {
                    customOutputDir = File(savedOut)
                    customOutputDir.mkdirs()
                }
            }
        } catch (_: Exception) {}
    }

    private fun saveSettings() {
        try {
            val json = JSONObject()
            json.put("theme", themes[currentThemeIndex].id)
            json.put("lang", currentLangCode)
            json.put("operationMode", selectedOperationMode)
            json.put("outputFormat", selectedOutputFormat)
            json.put("useBlockNcz", useBlockNcz)
            json.put("smartMergeDedup", smartMergeDedup)
            json.put("autoEmbedMods", autoEmbedMods)
            json.put("zstdLevel", zstdLevel)
            json.put("blockSizeExpo", blockSizeExpo)
            json.put("cpuThreads", cpuThreads)
            json.put("smartSizeGuard", smartSizeGuard)
            json.put("trimXci", trimXciBeforeCompress)
            json.put("deleteSource", deleteSourceAfterSuccess)
            json.put("autoCheckUpdates", autoCheckUpdatesOnStartup)
            json.put("outputDir", customOutputDir.absolutePath)
            json.put("lastBrowseDir", lastBrowseDirPath)
            val serialized = json.toString(2)
            getSharedPreferences("storm_switch_box_prefs", Context.MODE_PRIVATE)
                .edit()
                .putString("settings_json", serialized)
                .apply()
            settingsFile.parentFile?.mkdirs()
            settingsFile.writeText(serialized)
        } catch (_: Exception) {}
    }

    private fun dp(value: Int): Int =
        TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, value.toFloat(), resources.displayMetrics).toInt()

    private fun makeCardDrawable(bgColor: Int, borderColor: Int, radiusDp: Int = 12): GradientDrawable =
        GradientDrawable().apply {
            shape = GradientDrawable.RECTANGLE
            cornerRadius = dp(radiusDp).toFloat()
            setColor(bgColor)
            setStroke(dp(1), borderColor)
        }

    private fun makeButton(text: String, bgColor: Int, textColor: Int = Color.WHITE, onClick: () -> Unit): Button {
        return Button(this).apply {
            this.text = text
            this.isAllCaps = false
            this.setTextColor(textColor)
            this.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.5f)
            this.typeface = Typeface.create("sans-serif-medium", Typeface.BOLD)
            this.background = GradientDrawable().apply {
                shape = GradientDrawable.RECTANGLE
                cornerRadius = dp(8).toFloat()
                setColor(bgColor)
            }
            this.setPadding(dp(12), dp(8), dp(12), dp(8))
            this.setOnClickListener { onClick() }
        }
    }

    private fun makeSectionHeader(title: String, subtitle: String? = null, th: StormTheme): LinearLayout {
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(0, 0, 0, dp(6))
            addView(TextView(this@MainActivity).apply {
                text = title
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
                typeface = Typeface.DEFAULT_BOLD
            })
            if (!subtitle.isNullOrBlank()) {
                addView(TextView(this@MainActivity).apply {
                    text = subtitle
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                })
            }
        }
    }

    /**
     * Стилизованный чекбокс по стандарту STORM SOFT:
     * Сплошной квадрат со скруглением (CornerRadius="4") с акцентной неоновой заливкой при включении и БЕЗ галочек.
     */
    private fun updateStormCheckSquare(square: View, checked: Boolean, th: StormTheme) {
        square.background = GradientDrawable().apply {
            shape = GradientDrawable.RECTANGLE
            cornerRadius = dp(4).toFloat()
            if (checked) {
                setColor(th.accent)
                setStroke(dp(1), th.accent)
            } else {
                setColor(th.bgPrimary)
                setStroke(dp(2), th.border)
            }
        }
    }

    private fun makeStormCheckBox(
        label: String,
        initialChecked: Boolean,
        th: StormTheme,
        onCheckedChange: (Boolean) -> Unit
    ): LinearLayout {
        var isCheckedState = initialChecked
        val row = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(dp(2), dp(6), dp(4), dp(6))
        }
        val boxView = View(this).apply {
            layoutParams = LinearLayout.LayoutParams(dp(18), dp(18)).apply {
                marginEnd = dp(10)
            }
        }
        updateStormCheckSquare(boxView, isCheckedState, th)

        val textView = TextView(this).apply {
            text = label
            setTextColor(if (isCheckedState) th.textPrimary else th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.2f)
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }

        row.addView(boxView)
        row.addView(textView)

        row.setOnClickListener {
            isCheckedState = !isCheckedState
            updateStormCheckSquare(boxView, isCheckedState, th)
            textView.setTextColor(if (isCheckedState) th.textPrimary else th.textSecondary)
            onCheckedChange(isCheckedState)
        }
        return row
    }

    /**
     * Компактный выпадающий список (StormDropdown), стилизованный под все 9 тем STORM SOFT.
     */
    private fun makeStormDropdown(
        items: List<String>,
        selectedIndex: Int,
        th: StormTheme,
        onItemSelected: (Int) -> Unit
    ): LinearLayout {
        var currentIndex = selectedIndex.coerceIn(0, (items.size - 1).coerceAtLeast(0))

        val container = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            background = makeCardDrawable(th.bgElevated, th.border, radiusDp = 8)
            setPadding(dp(11), dp(8), dp(11), dp(8))
        }

        val selectedText = TextView(this).apply {
            text = items.getOrElse(currentIndex) { "" }
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.2f)
            typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            maxLines = 2
            ellipsize = TextUtils.TruncateAt.END
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }

        val arrowText = TextView(this).apply {
            text = "▾"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            typeface = Typeface.DEFAULT_BOLD
            setPadding(dp(8), 0, 0, 0)
        }

        container.addView(selectedText)
        container.addView(arrowText)

        container.setOnClickListener {
            val popupRoot = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                background = makeCardDrawable(th.bgCard, th.accent, radiusDp = 10)
                setPadding(dp(4), dp(4), dp(4), dp(4))
            }

            val listLinear = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
            }

            val scroll = ScrollView(this).apply {
                addView(listLinear)
            }
            popupRoot.addView(scroll)

            val popupWidth = container.width.coerceAtLeast(dp(260))
            val popup = PopupWindow(
                popupRoot,
                popupWidth,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                true
            ).apply {
                isOutsideTouchable = true
                elevation = dp(12).toFloat()
                setBackgroundDrawable(GradientDrawable())
            }

            val accentTint = Color.argb(45, Color.red(th.accent), Color.green(th.accent), Color.blue(th.accent))

            for ((idx, itemText) in items.withIndex()) {
                val isSel = idx == currentIndex
                val itemRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    background = GradientDrawable().apply {
                        shape = GradientDrawable.RECTANGLE
                        cornerRadius = dp(6).toFloat()
                        setColor(if (isSel) accentTint else Color.TRANSPARENT)
                    }
                    setPadding(dp(9), dp(7), dp(9), dp(7))
                    layoutParams = LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.WRAP_CONTENT
                    ).apply {
                        bottomMargin = dp(2)
                    }
                }

                val bar = View(this).apply {
                    layoutParams = LinearLayout.LayoutParams(dp(3), dp(16)).apply {
                        marginEnd = dp(8)
                    }
                    background = GradientDrawable().apply {
                        shape = GradientDrawable.RECTANGLE
                        cornerRadius = dp(2).toFloat()
                        setColor(if (isSel) th.accent else Color.TRANSPARENT)
                    }
                }

                val lbl = TextView(this).apply {
                    text = itemText
                    setTextColor(if (isSel) th.accent else th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    typeface = if (isSel) Typeface.DEFAULT_BOLD else Typeface.DEFAULT
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                }

                itemRow.addView(bar)
                itemRow.addView(lbl)

                itemRow.setOnClickListener {
                    currentIndex = idx
                    selectedText.text = itemText
                    popup.dismiss()
                    onItemSelected(idx)
                }
                listLinear.addView(itemRow)
            }

            popup.showAsDropDown(container, 0, dp(4))
            hideSystemBars(window)
        }

        return container
    }

    private fun appendLog(msg: String) {
        val stamp = SimpleDateFormat("dd.MM.yyyy HH:mm:ss", Locale.getDefault()).format(Date())
        val line = "[$stamp] $msg"
        mainHandler.post {
            logLines.add(line)
            if (logLines.size > 150) logLines.removeAt(0)
            if (::logTextView.isInitialized) {
                logTextView.text = logLines.joinToString("\n")
            }
            try {
                val logFile = File(stormRoot, "logs/storm_switch_box.log")
                logFile.appendText("$line\n")
            } catch (_: Exception) {}
        }
    }

    private fun buildUserInterface() {
        val th = themes[currentThemeIndex]
        rootLayout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(th.bgPrimary)
            layoutParams = ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT)
        }

        // 1. Компактная верхняя шапка (Full-screen)
        val header = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setBackgroundColor(th.bgCard)
            setPadding(dp(14), dp(8), dp(14), dp(8))
        }
        val titleCol = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        titleCol.addView(TextView(this).apply {
            text = "STORM SWITCH BOX ${"5.5.1"}"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 16f)
            typeface = Typeface.DEFAULT_BOLD
        })
        val keysInstalled = File(keysDir, "prod.keys").exists()
        titleCol.addView(TextView(this).apply {
            text = "Блочный NSZ/XCZ • Слияние 4-в-1 • SmartShrink • RuTracker Switch • Ключи: ${if (keysInstalled) "Активны" else "Не установлены"}"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
        })
        header.addView(titleCol)

        val updateBadgeBtn = makeButton("🔄 Обновить", th.bgElevated, th.textPrimary) {
            checkForUpdatesFromGitHub(isManualClick = true)
        }
        header.addView(updateBadgeBtn)
        rootLayout.addView(header)

        // 2. Панель навигации из 5 вкладок
        val navScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setBackgroundColor(th.bgElevated)
            setPadding(dp(8), dp(6), dp(8), dp(6))
        }
        val navRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        val tabTitles = listOf(
            "📦 Очередь и конвертер",
            "🔍 Инспектор и аудитор",
            "🌐 RuTracker (Switch) и моды",
            "📡 Сеть и консоль",
            "🔑 Ключи и параметры"
        )
        tabTitles.forEachIndexed { idx, label ->
            val isSel = idx == activeTab
            val btn = makeButton(
                label,
                if (isSel) th.accent else th.bgCard,
                if (isSel) Color.WHITE else th.textSecondary
            ) {
                activeTab = idx
                buildUserInterface()
            }
            val lp = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT)
            lp.marginEnd = dp(6)
            btn.layoutParams = lp
            navRow.addView(btn)
        }
        navScroll.addView(navRow)
        rootLayout.addView(navScroll)

        // 3. Основной прокручиваемый контейнер
        val mainScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
            isFillViewport = true
        }
        contentContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(12), dp(10), dp(12), dp(14))
        }

        when (activeTab) {
            0 -> buildConverterAndQueueTab(contentContainer, th)
            1 -> buildInspectorAndAuditorTab(contentContainer, th)
            2 -> buildRuTrackerAndModsTab(contentContainer, th)
            3 -> buildNetworkDbiTab(contentContainer, th)
            4 -> buildKeysAndSettingsTab(contentContainer, th)
        }

        // Журнал событий внизу каждой вкладки
        val logCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(12), dp(10), dp(12), dp(10))
            val lp = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT)
            lp.topMargin = dp(10)
            layoutParams = lp
        }
        logCard.addView(makeSectionHeader("📋 Журнал операций и телеметрия", null, th))
        logTextView = TextView(this).apply {
            text = logLines.joinToString("\n")
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            typeface = Typeface.MONOSPACE
        }
        logCard.addView(logTextView)
        contentContainer.addView(logCard)

        mainScroll.addView(contentContainer)
        rootLayout.addView(mainScroll)
        setContentView(rootLayout)
        hideSystemBars(window)
    }

    // =========================================================================
    // ВКЛАДКА 0: ОЧЕРЕДЬ ЗАДАЧ И КОНВЕРТЕР (ПАРИТЕТ С ПК)
    // =========================================================================

    private fun buildConverterAndQueueTab(parent: LinearLayout, th: StormTheme) {
        // ГРУППА 1: Источники (Выбор множества отдельных файлов, папок, мульти-проводник и папка вывода)
        val sourceCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        sourceCard.addView(makeSectionHeader(
            "1. Добавление игр, обновлений и папок в очередь",
            "Выберите отдельные файлы, любую папку на устройстве или отметьте несколько файлов и папок в проводнике",
            th
        ))

        val rowPick1 = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(4)
            }
        }
        rowPick1.addView(makeButton("📄 Выбрать файлы (Мультивыбор)", th.accent) {
            openSystemMultiFilePicker()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })
        rowPick1.addView(makeButton("🗂 Мульти-проводник (Файлы и папки)", th.bgElevated, th.textPrimary) {
            showMultiSelectFileAndFolderDialog()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        sourceCard.addView(rowPick1)

        val rowPick2 = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }
        rowPick2.addView(makeButton("📁 Выбрать любую папку", th.bgElevated, th.textPrimary) {
            openSystemFolderTreePicker()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })
        rowPick2.addView(makeButton("🔄 Сканировать папку roms", th.bgElevated, th.textPrimary) {
            addFolderRecursivelyToQueue(romsDir)
            buildUserInterface()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        sourceCard.addView(rowPick2)

        // Папка сохранения (Выходная папка)
        val outDirRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(8)
            }
        }
        outDirRow.addView(TextView(this).apply {
            text = "📂 Папка сохранения: ${customOutputDir.absolutePath}"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        outDirRow.addView(makeButton("Изменить папку", th.bgElevated, th.textPrimary) {
            showSelectOutputFolderDialog()
        })
        sourceCard.addView(outDirRow)
        parent.addView(sourceCard)

        // ГРУППА 2: Очередь задач
        val queueCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        val totalQueueBytes = queuedFiles.sumOf { if (it.exists()) it.length() else 0L }
        val qHeaderRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        qHeaderRow.addView(TextView(this).apply {
            text = "2. Очередь обработки (${queuedFiles.size} файлов • ${StormNczEngine.formatSize(totalQueueBytes)})"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            typeface = Typeface.DEFAULT_BOLD
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        if (queuedFiles.isNotEmpty()) {
            qHeaderRow.addView(makeButton("🗑 Очистить", Color.parseColor("#DC2626")) {
                queuedFiles.clear()
                selectedInputFile = null
                buildUserInterface()
            })
        }
        queueCard.addView(qHeaderRow)

        if (queuedFiles.isEmpty()) {
            queueCard.addView(TextView(this).apply {
                text = "Очередь пуста. Нажмите «Выбрать файлы», «Мульти-проводник» или «Выбрать любую папку» выше."
                setTextColor(th.textSecondary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                setPadding(0, dp(6), 0, dp(2))
            })
        } else {
            queuedFiles.take(15).forEachIndexed { idx, file ->
                val itemRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    background = makeCardDrawable(
                        if (selectedInputFile == file) th.bgElevated else th.bgPrimary,
                        if (selectedInputFile == file) th.accent else th.border,
                        8
                    )
                    setPadding(dp(10), dp(6), dp(10), dp(6))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(5)
                    }
                    setOnClickListener {
                        selectedInputFile = file
                        buildUserInterface()
                    }
                }
                val extBadge = file.extension.uppercase().ifEmpty { "ROM" }
                itemRow.addView(TextView(this).apply {
                    text = "[$extBadge]"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, dp(8), 0)
                })
                itemRow.addView(TextView(this).apply {
                    text = "${file.name} (${StormNczEngine.formatSize(file.length())})"
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    maxLines = 1
                    ellipsize = TextUtils.TruncateAt.MIDDLE
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                })
                itemRow.addView(makeButton("✕", th.bgCard, th.textSecondary) {
                    queuedFiles.removeAt(idx)
                    if (selectedInputFile == file) selectedInputFile = queuedFiles.firstOrNull()
                    buildUserInterface()
                })
                queueCard.addView(itemRow)
            }
            if (queuedFiles.size > 15) {
                queueCard.addView(TextView(this).apply {
                    text = "... и ещё ${queuedFiles.size - 15} файлов в пакетной очереди"
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    setPadding(0, dp(4), 0, 0)
                })
            }
        }
        parent.addView(queueCard)

        // ГРУППА 3: Режим операции, мульти-контент, целевой формат и параметры блочного сжатия
        val cfgCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        cfgCard.addView(makeSectionHeader(
            "3. Мульти-контент, формат и блочное сжатие (Zstd / NCZBLOCK)",
            "Умная сшивка База + Патч + DLC + Моды с выбором итогового контейнера и блочного сжатия",
            th
        ))

        cfgCard.addView(TextView(this).apply {
            text = "Режим обработки выбранных файлов:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(2), 0, dp(3))
        })

        val modes = listOf(
            "Умный мульти-контент (Сшивка База + Патч + DLC + Моды в один файл)",
            "Пакетное сжатие и конвертация (Каждый файл очереди отдельно)",
            "Распаковка NSZ и XCZ в оригинальный NSP и XCI",
            "SmartShrink: Обрезка пустых областей 0xFF (Switch XCI и 3DS)",
            "Дешифратор Nintendo 3DS (.3ds и .cia в Decrypted)"
        )
        val modeDropdown = makeStormDropdown(modes, selectedOperationMode, th) { pos ->
            selectedOperationMode = pos
            saveSettings()
        }
        cfgCard.addView(modeDropdown)

        // Целевой формат выходного контейнера (как на ПК!)
        cfgCard.addView(TextView(this).apply {
            text = "Итоговый формат выходного контейнера:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(8), 0, dp(3))
        })

        val outputFormats = listOf(
            "Авто (Умный выбор минимального размера — NSZ / XCZ)",
            "NSP / NSZ (Цифровой контейнер PFS0 — игры, патчи, DLC, моды)",
            "XCI / XCZ (Мульти-картридж HFS0 — всё-в-одном без тикетов)"
        )
        val formatDropdown = makeStormDropdown(outputFormats, selectedOutputFormat, th) { pos ->
            selectedOutputFormat = pos
            saveSettings()
        }
        cfgCard.addView(formatDropdown)

        // Ползунок уровня Zstd (1..22)
        val levelLabel = TextView(this).apply {
            text = "Уровень сжатия Zstandard: $zstdLevel (рек. 18 — баланс скорости и сжатия)"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(9), 0, dp(2))
        }
        cfgCard.addView(levelLabel)
        val levelSeek = SeekBar(this).apply {
            max = 21
            progress = (zstdLevel - 1).coerceIn(0, 21)
            setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                    zstdLevel = p + 1
                    levelLabel.text = "Уровень сжатия Zstandard: $zstdLevel (рек. 18 — баланс скорости и сжатия)"
                    saveSettings()
                }
                override fun onStartTrackingTouch(sb: SeekBar?) {}
                override fun onStopTrackingTouch(sb: SeekBar?) {}
            })
        }
        cfgCard.addView(levelSeek)

        // Размер блока NCZBLOCK
        val blockRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(0, dp(6), 0, dp(6))
        }
        blockRow.addView(TextView(this).apply {
            text = "Размер блока NCZBLOCK:"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.2f)
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                marginEnd = dp(8)
            }
        })
        val blockOptions = listOf(18 to "256 КБ", 19 to "512 КБ", 20 to "1 МБ (Эталон)", 21 to "2 МБ", 22 to "4 МБ")
        val blockDropdown = makeStormDropdown(
            blockOptions.map { it.second },
            blockOptions.indexOfFirst { it.first == blockSizeExpo }.coerceAtLeast(2),
            th
        ) { pos ->
            blockSizeExpo = blockOptions[pos].first
            saveSettings()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(dp(155), ViewGroup.LayoutParams.WRAP_CONTENT)
        }
        blockRow.addView(blockDropdown)
        cfgCard.addView(blockRow)

        // Стилизованные чекбоксы по правилам STORM SOFT (сплошной квадрат 4dp без галочки!)
        cfgCard.addView(makeStormCheckBox(
            "Включить блочное сжатие Zstandard (NSZ / XCZ — NCZSECTN + NCZBLOCK)",
            useBlockNcz,
            th
        ) { checked ->
            useBlockNcz = checked
            saveSettings()
        })

        cfgCard.addView(makeStormCheckBox(
            "Умная сшивка и дедупликация NCA (удалять дубли и перекрытые патчи)",
            smartMergeDedup,
            th
        ) { checked ->
            smartMergeDedup = checked
            saveSettings()
        })

        cfgCard.addView(makeStormCheckBox(
            "Правильно вшивать моды, русификаторы и дополнения в мульти-контент",
            autoEmbedMods,
            th
        ) { checked ->
            autoEmbedMods = checked
            saveSettings()
        })

        cfgCard.addView(makeStormCheckBox(
            "SmartSize Guard (защита от раздутия мелких блоков при сжатии)",
            smartSizeGuard,
            th
        ) { checked ->
            smartSizeGuard = checked
            saveSettings()
        })

        cfgCard.addView(makeStormCheckBox(
            "Автоматически обрезать пустые области 0xFF (Trim XCI / 3DS) перед сборкой",
            trimXciBeforeCompress,
            th
        ) { checked ->
            trimXciBeforeCompress = checked
            saveSettings()
        })

        cfgCard.addView(makeStormCheckBox(
            "Удалять исходные файлы после успешной проверки выходного контейнера",
            deleteSourceAfterSuccess,
            th
        ) { checked ->
            deleteSourceAfterSuccess = checked
            saveSettings()
        })

        parent.addView(cfgCard)

        // ГРУППА 4: Панель запуска и телеметрия прогресса
        val runCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        runCard.addView(makeSectionHeader("4. Выполнение и прогресс", null, th))

        val progressBar = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(16))
        }
        runCard.addView(progressBar)

        val statusTxt = TextView(this).apply {
            text = "Готов к обработке (${queuedFiles.size} файлов в очереди)"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(6), 0, dp(8))
        }
        runCard.addView(statusTxt)

        val actionBtnsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        val btnStart = makeButton("🚀 Запустить обработку очереди (${queuedFiles.size})", th.accent) {
            if (isTaskRunning) return@makeButton
            if (queuedFiles.isEmpty()) {
                Toast.makeText(this, "Сначала добавьте файлы или папки в очередь!", Toast.LENGTH_LONG).show()
                return@makeButton
            }
            executeQueueBatch(progressBar, statusTxt)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        }
        actionBtnsRow.addView(btnStart)

        val btnStop = makeButton("⏹ Стоп", Color.parseColor("#DC2626")) {
            if (isTaskRunning) {
                cancelCurrentTaskRequested = true
                statusTxt.text = "Остановка после завершения текущей секции..."
            }
        }
        actionBtnsRow.addView(btnStop)
        runCard.addView(actionBtnsRow)

        parent.addView(runCard)
    }

    private fun executeQueueBatch(progressBar: ProgressBar, statusTxt: TextView) {
        isTaskRunning = true
        cancelCurrentTaskRequested = false
        val snapshot = queuedFiles.toList()
        customOutputDir.mkdirs()

        thread {
            try {
                // Режим 0: Умный мульти-контент (Сшивка База + Патч + DLC + Моды в единый оптимальный контейнер NSZ/XCZ/NSP/XCI)
                if (selectedOperationMode == 0) {
                    val preparedInputs = mutableListOf<File>()
                    val tempTrimmedFiles = mutableListOf<File>()
                    val cb: (StormNczEngine.NczProgress) -> Unit = { p ->
                        mainHandler.post {
                            progressBar.progress = p.percent
                            statusTxt.text = "${p.stageText} (${p.percent}%)"
                        }
                    }

                    for (inFile in snapshot) {
                        if (!inFile.exists()) continue
                        if (trimXciBeforeCompress && inFile.extension.equals("xci", ignoreCase = true)) {
                            val trimmedCandidate = File(customOutputDir, "${inFile.nameWithoutExtension}.trimmed_tmp.xci")
                            val trimRes = StormNczEngine.smartShrinkRom(inFile, trimmedCandidate, cb)
                            if (trimRes.compressedBytes in 1 until inFile.length()) {
                                preparedInputs.add(trimmedCandidate)
                                tempTrimmedFiles.add(trimmedCandidate)
                                appendLog("✂️ SmartShrink XCI сэкономил ${StormNczEngine.formatSize(inFile.length() - trimRes.compressedBytes)} на ${inFile.name}")
                            } else {
                                trimmedCandidate.delete()
                                preparedInputs.add(inFile)
                            }
                        } else {
                            preparedInputs.add(inFile)
                        }
                    }

                    val res = StormNczEngine.buildSmartMultiContentPackage(
                        inputFiles = preparedInputs,
                        outputDir = customOutputDir,
                        targetFormatMode = selectedOutputFormat,
                        useBlockNcz = useBlockNcz,
                        smartDedup = smartMergeDedup,
                        embedMods = autoEmbedMods,
                        zstdLevel = zstdLevel,
                        blockSizeExponent = blockSizeExpo,
                        threads = cpuThreads,
                        smartSizeGuard = smartSizeGuard,
                        onProgress = cb
                    )

                    tempTrimmedFiles.forEach { it.delete() }

                    val outFile = File(res.outputPath)
                    appendLog("✅ ${res.message}")
                    if (deleteSourceAfterSuccess && outFile.exists() && outFile.length() > 0) {
                        for (src in snapshot) {
                            if (src.isFile && src.canonicalPath != outFile.canonicalPath) {
                                src.delete()
                            }
                        }
                    }
                    mainHandler.post {
                        progressBar.progress = 100
                        statusTxt.text = "✅ Готово: ${outFile.name} (${StormNczEngine.formatSize(res.compressedBytes)})"
                    }
                } else {
                    var totalOrig = 0L
                    var totalOut = 0L
                    var successCount = 0

                    for ((idx, inFile) in snapshot.withIndex()) {
                        if (cancelCurrentTaskRequested) break
                        if (!inFile.exists()) continue

                        val ext = inFile.extension.lowercase()
                        val baseName = inFile.nameWithoutExtension
                        val prefix = "[${idx + 1}/${snapshot.size}]"

                        val cb: (StormNczEngine.NczProgress) -> Unit = { p ->
                            mainHandler.post {
                                progressBar.progress = p.percent
                                statusTxt.text = "$prefix ${p.stageText} (${p.percent}%)"
                            }
                        }

                        var workingInput = inFile
                        var tempTrimmed: File? = null

                        if (selectedOperationMode == 1 && trimXciBeforeCompress && ext == "xci") {
                            val trimmedCandidate = File(customOutputDir, "${baseName}.trimmed_tmp.xci")
                            val trimRes = StormNczEngine.smartShrinkRom(inFile, trimmedCandidate, cb)
                            if (trimRes.compressedBytes in 1 until inFile.length()) {
                                workingInput = trimmedCandidate
                                tempTrimmed = trimmedCandidate
                                appendLog("✂️ Предварительный Trim XCI сэкономил ${StormNczEngine.formatSize(inFile.length() - trimRes.compressedBytes)}")
                            } else {
                                trimmedCandidate.delete()
                            }
                        }

                        val res = when (selectedOperationMode) {
                            1 -> {
                                // Пакетное сжатие и конвертация каждого файла отдельно с учетом целевого формата и чекбокса useBlockNcz
                                StormNczEngine.buildSmartMultiContentPackage(
                                    inputFiles = listOf(workingInput),
                                    outputDir = customOutputDir,
                                    targetFormatMode = selectedOutputFormat,
                                    useBlockNcz = useBlockNcz,
                                    smartDedup = smartMergeDedup,
                                    embedMods = autoEmbedMods,
                                    zstdLevel = zstdLevel,
                                    blockSizeExponent = blockSizeExpo,
                                    threads = cpuThreads,
                                    smartSizeGuard = smartSizeGuard,
                                    onProgress = cb
                                )
                            }
                            2 -> {
                                val outExt = if (ext == "xcz" || ext == "xci") "xci" else "nsp"
                                val outFile = File(customOutputDir, "${baseName}_unpacked.$outExt")
                                StormNczEngine.decompressNszOrXczToNsp(workingInput, outFile, cb)
                            }
                            3 -> {
                                val outFile = File(customOutputDir, "${baseName}_trimmed.$ext")
                                StormNczEngine.smartShrinkRom(workingInput, outFile, cb)
                            }
                            else -> {
                                val outFile = File(customOutputDir, "${baseName}_decrypted.$ext")
                                StormNczEngine.smartShrinkRom(workingInput, outFile, cb)
                            }
                        }

                        tempTrimmed?.delete()

                        if (res.success) {
                            successCount++
                            totalOrig += inFile.length()
                            totalOut += res.compressedBytes
                            appendLog("✅ $prefix ${inFile.name} -> ${File(res.outputPath).name} (${StormNczEngine.formatSize(res.compressedBytes)})")
                            if (deleteSourceAfterSuccess && File(res.outputPath).exists() && File(res.outputPath).length() > 0 && inFile.canonicalPath != File(res.outputPath).canonicalPath) {
                                inFile.delete()
                            }
                        }
                    }

                    val saved = (totalOrig - totalOut).coerceAtLeast(0L)
                    mainHandler.post {
                        progressBar.progress = 100
                        statusTxt.text = "✅ Обработано файлов: $successCount/${snapshot.size} • Сэкономлено: ${StormNczEngine.formatSize(saved)}"
                    }
                }
            } catch (e: Exception) {
                appendLog("❌ Ошибка пакетной очереди: ${e.message}")
                mainHandler.post {
                    statusTxt.text = "❌ Ошибка: ${e.message}"
                }
            } finally {
                isTaskRunning = false
            }
        }
    }

    // =========================================================================
    // ВКЛАДКА 1: ИНСПЕКТОР КОНТЕЙНЕРОВ, РАСПАКОВЩИК И АУДИТОР БИБЛИОТЕКИ
    // =========================================================================

    private fun buildInspectorAndAuditorTab(parent: LinearLayout, th: StormTheme) {
        // ГРУППА 1: Инспектор структуры и выборочное извлечение файлов
        val inspCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        val activeFile = selectedInputFile ?: queuedFiles.firstOrNull()
        inspCard.addView(makeSectionHeader(
            "1. Инспектор контейнера (PFS0 / HFS0 / HEAD / NCZBLOCK / NCSD)",
            if (activeFile != null) "Выбран файл: ${activeFile.name} (${StormNczEngine.formatSize(activeFile.length())})"
            else "Выберите файл в очереди или откройте с диска",
            th
        ))

        val inspBtnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        inspBtnRow.addView(makeButton("📂 Выбрать файл для инспекции", th.bgElevated, th.textPrimary) {
            showMultiSelectFileAndFolderDialog()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        if (activeFile != null && activeFile.exists()) {
            inspBtnRow.addView(makeButton("📤 Извлечь все разделы в extracted/", th.accent) {
                val targetOut = File(extractedDir, activeFile.nameWithoutExtension)
                thread {
                    try {
                        val extracted = StormNczEngine.extractContainerFiles(activeFile, targetOut, null) {}
                        appendLog("✅ Извлечено ${extracted.size} внутренних файлов в ${targetOut.absolutePath}")
                        mainHandler.post {
                            Toast.makeText(this, "Извлечено ${extracted.size} файлов в ${targetOut.name}!", Toast.LENGTH_LONG).show()
                        }
                    } catch (e: Exception) {
                        appendLog("❌ Ошибка извлечения: ${e.message}")
                    }
                }
            }.apply {
                layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
            })
        }
        inspCard.addView(inspBtnRow)

        if (activeFile != null && activeFile.exists()) {
            val entries = StormNczEngine.inspectContainer(activeFile)
            val sb = StringBuilder()
            sb.appendLine("Контейнер: ${activeFile.name} (${StormNczEngine.formatSize(activeFile.length())})")
            sb.appendLine("Найдено разделов / записей: ${entries.size}")
            sb.appendLine("────────────────────────────────────────────────")
            entries.forEachIndexed { i, e ->
                sb.appendLine("${i + 1}. ${e.name}")
                sb.appendLine("   Размер: ${StormNczEngine.formatSize(e.size)} | Смещение: 0x${e.offset.coerceAtLeast(0L).toString(16).uppercase()}")
            }
            inspCard.addView(TextView(this).apply {
                text = sb.toString()
                setTextColor(th.textPrimary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                typeface = Typeface.MONOSPACE
                background = makeCardDrawable(th.bgPrimary, th.border, 8)
                setPadding(dp(10), dp(8), dp(10), dp(8))
            })
        }
        parent.addView(inspCard)

        // ГРУППА 2: Аудитор и дедупликатор библиотеки
        val auditCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        auditCard.addView(makeSectionHeader(
            "2. Аудитор и дедупликатор библиотеки",
            "Поиск необрезанных картриджей XCI/3DS (с гигабайтами 0xFF), несжатых NSP и дубликатов TitleID",
            th
        ))

        val auditOutTxt = TextView(this).apply {
            text = "Нажмите «Провести аудит библиотеки», чтобы проанализировать очередь и папку roms."
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(6), 0, dp(8))
        }

        val auditBtnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        auditBtnRow.addView(makeButton("🔎 Провести аудит библиотеки", th.accent) {
            thread {
                val scanTargets = mutableListOf<File>()
                scanTargets.addAll(queuedFiles)
                scanTargets.add(romsDir)
                scanTargets.add(customOutputDir)
                val rep = StormNczEngine.auditLibraryFolder(scanTargets)
                val text = buildString {
                    appendLine("📊 Результаты аудита коллекции:")
                    appendLine("• Всего проверено образов: ${rep.totalFiles} (${StormNczEngine.formatSize(rep.totalBytes)})")
                    appendLine("• Несжатых контейнеров .nsp: ${rep.uncompressedNspFiles.size}")
                    appendLine("• Необрезанных картриджей .xci / .3ds (с пустыми 0xFF): ${rep.untrimmedCartFiles.size}")
                    rep.untrimmedCartFiles.take(5).forEach { (f, waste) ->
                        appendLine("   - ${f.name}: пустых данных 0xFF на ${StormNczEngine.formatSize(waste)}")
                    }
                    appendLine("• Групп дубликатов по TitleID: ${rep.duplicateTitleGroups.size}")
                    appendLine("• Потенциальная экономия места (SmartShrink + NSZ): ~${StormNczEngine.formatSize(rep.estimatedSavingsBytes)}")
                }
                mainHandler.post {
                    auditOutTxt.text = text
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        auditBtnRow.addView(makeButton("⚡ Оптимизировать найденное", th.bgElevated, th.textPrimary) {
            val scanTargets = listOf(romsDir) + queuedFiles
            val rep = StormNczEngine.auditLibraryFolder(scanTargets)
            var added = 0
            (rep.uncompressedNspFiles + rep.untrimmedCartFiles.map { it.first }).forEach { f ->
                if (queuedFiles.none { it.canonicalPath == f.canonicalPath }) {
                    queuedFiles.add(f)
                    added++
                }
            }
            activeTab = 0
            buildUserInterface()
            Toast.makeText(this, "В очередь добавлено $added файлов для оптимизации!", Toast.LENGTH_SHORT).show()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })

        auditCard.addView(auditBtnRow)
        auditCard.addView(auditOutTxt)
        parent.addView(auditCard)
    }

    // =========================================================================
    // ВКЛАДКА 2: RUTRACKER (ТОЛЬКО NINTENDO SWITCH) И ОБЛАЧНЫЕ МОДЫ
    // =========================================================================

    private fun buildRuTrackerAndModsTab(parent: LinearLayout, th: StormTheme) {
        // ГРУППА 1: Поиск ТОЛЬКО игр Nintendo Switch (разделы 1605 и 2481)
        val rtCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        rtCard.addView(makeSectionHeader(
            "1. Поиск игр и русификаторов ТОЛЬКО для Nintendo Switch (RuTracker #1605 / #2481)",
            "Строгая фильтрация релизов [Nintendo Switch] (NSP / NSZ / XCI / XCZ) без ПК-репаков и других платформ",
            th
        ))

        val queryEdit = EditText(this).apply {
            hint = "Введите игру Switch (напр. Zelda, Mario, Hades, Witcher, Persona 5)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        rtCard.addView(queryEdit)

        // Быстрые теги популярных игр Switch
        val tagsScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setPadding(0, dp(6), 0, dp(6))
        }
        val tagsRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val resultsLayout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        fun runSearch(q: String) {
            resultsLayout.removeAllViews()
            resultsLayout.addView(TextView(this).apply {
                text = "⏳ Поиск релизов Nintendo Switch..."
                setTextColor(th.accent)
                setPadding(0, dp(6), 0, dp(6))
            })
            thread {
                val found = RuTrackerCloudEngine.searchTopics(q)
                mainHandler.post {
                    resultsLayout.removeAllViews()
                    if (found.isEmpty()) {
                        resultsLayout.addView(TextView(this).apply {
                            text = "Раздачи Nintendo Switch по запросу «$q» не найдены."
                            setTextColor(th.textSecondary)
                        })
                    } else {
                        found.forEach { item ->
                            val itemBox = LinearLayout(this).apply {
                                orientation = LinearLayout.VERTICAL
                                background = makeCardDrawable(th.bgPrimary, th.border, 8)
                                setPadding(dp(10), dp(8), dp(10), dp(8))
                                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                                    topMargin = dp(6)
                                }
                            }
                            itemBox.addView(TextView(this).apply {
                                text = item.title
                                setTextColor(th.textPrimary)
                                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.5f)
                                typeface = Typeface.DEFAULT_BOLD
                            })
                            itemBox.addView(TextView(this).apply {
                                text = "Раздел: ${item.category} • Размер: ${item.sizeText} • Сиды: ▲${item.seeds}"
                                setTextColor(th.accent)
                                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                                setPadding(0, dp(2), 0, dp(6))
                            })
                            val btnRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
                            if (item.magnetUri.isNotEmpty()) {
                                btnRow.addView(makeButton("🧲 Скачать (Magnet)", th.accent) {
                                    try {
                                        startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(item.magnetUri)))
                                    } catch (_: Exception) {
                                        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                        cm.setPrimaryClip(ClipData.newPlainText("Magnet", item.magnetUri))
                                        Toast.makeText(this, "Magnet-ссылка скопирована в буфер обмена!", Toast.LENGTH_LONG).show()
                                    }
                                }.apply {
                                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                                        marginEnd = dp(8)
                                    }
                                })
                            }
                            if (item.topicUrl.isNotEmpty()) {
                                btnRow.addView(makeButton("🌐 Открыть тему", th.bgElevated, th.textPrimary) {
                                    try { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(item.topicUrl))) } catch (_: Exception) {}
                                })
                            }
                            itemBox.addView(btnRow)
                            resultsLayout.addView(itemBox)
                        }
                    }
                }
            }
        }

        listOf("Все хиты Switch", "Zelda", "Mario", "Metroid", "Witcher", "Hades", "Persona", "Xenoblade", "Skyrim", "Hollow Knight").forEach { tag ->
            val b = makeButton(tag, th.bgElevated, th.textPrimary) {
                val q = if (tag == "Все хиты Switch") "" else tag
                queryEdit.setText(q)
                runSearch(q)
            }
            b.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
            tagsRow.addView(b)
        }
        tagsScroll.addView(tagsRow)
        rtCard.addView(tagsScroll)

        rtCard.addView(makeButton("🔍 Найти раздачи Nintendo Switch", th.accent) {
            runSearch(queryEdit.text.toString())
        })
        rtCard.addView(resultsLayout)
        parent.addView(rtCard)

        // Первичное заполнение списка раздач Switch
        runSearch("")

        // ГРУППА 2: Загрузчик модов и русификаторов
        val modCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        modCard.addView(makeSectionHeader(
            "2. Облачный загрузчик русификаторов и модов (Яндекс.Диск / Google Drive / GitHub)",
            "Автоматическое скачивание и распаковка в структуру atmosphere/contents/<TitleID>/romfs",
            th
        ))

        val urlInput = EditText(this).apply {
            hint = "Публичная ссылка (https://disk.yandex.ru/... или https://drive.google.com/...)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        modCard.addView(urlInput)

        val tidInput = EditText(this).apply {
            hint = "TitleID игры (например: 01007EF00011E000)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
                bottomMargin = dp(8)
            }
        }
        modCard.addView(tidInput)

        val modStatus = TextView(this).apply {
            text = "Целевой каталог модов: ${modsDir.absolutePath}/atmosphere/contents/"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, 0, 0, dp(6))
        }
        modCard.addView(modStatus)

        modCard.addView(makeButton("📥 Скачать и установить русификатор / мод", th.accent) {
            val u = urlInput.text.toString().trim()
            val tid = tidInput.text.toString().trim().ifEmpty { "0100000000010000" }
            if (u.isEmpty()) {
                Toast.makeText(this, "Вставьте ссылку на архив мода!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            thread {
                try {
                    val out = RuTrackerCloudEngine.downloadAndExtractMod(u, tid, modsDir) { st ->
                        mainHandler.post { modStatus.text = st }
                    }
                    appendLog("✅ Мод установлен в ${out.absolutePath}")
                } catch (e: Exception) {
                    mainHandler.post { modStatus.text = "❌ Ошибка: ${e.message}" }
                }
            }
        })
        parent.addView(modCard)
    }

    // =========================================================================
    // ВКЛАДКА 3: СЕТЬ И ОТПРАВКА НА КОНСОЛЬ (HTTP DBI / TINFOIL)
    // =========================================================================

    private fun buildNetworkDbiTab(parent: LinearLayout, th: StormTheme) {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        card.addView(makeSectionHeader(
            "📡 Встроенный HTTP Range сервер для прямой установки на Nintendo Switch (DBI / Tinfoil)",
            "Раздаёт игры из очереди, папки roms и папки output по Wi-Fi без проводов",
            th
        ))

        val ip = getLocalWifiIp()
        val infoTxt = TextView(this).apply {
            text = buildString {
                appendLine("• Локальный IP-адрес устройства: $ip")
                appendLine("• Адрес каталога для Tinfoil / DBI: http://$ip:8080/index.tfl")
                appendLine("• Веб-интерфейс для браузера и ПК: http://$ip:8080/")
                appendLine("• Статус сервера: ${if (dbiServerRunning) "🟢 АКТИВЕН (Порт 8080)" else "⚪ ОСТАНОВЛЕН"}")
            }
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.5f)
            setPadding(0, dp(6), 0, dp(10))
        }
        card.addView(infoTxt)

        card.addView(makeButton(
            if (dbiServerRunning) "⏹ Остановить HTTP сервер DBI / Tinfoil" else "▶️ Запустить HTTP сервер (Порт 8080)",
            if (dbiServerRunning) Color.parseColor("#DC2626") else th.accent
        ) {
            if (dbiServerRunning) {
                dbiServer?.stop()
                dbiServerRunning = false
                appendLog("HTTP сервер DBI/Tinfoil остановлен.")
            } else {
                dbiServer = RuTrackerCloudEngine.DbiHttpServer(
                    port = 8080,
                    sharedFiles = {
                        val list = mutableListOf<File>()
                        list.addAll(queuedFiles)
                        romsDir.listFiles()?.let { list.addAll(it) }
                        customOutputDir.listFiles()?.let { list.addAll(it) }
                        list.distinctBy { it.canonicalPath }
                    },
                    onLog = { msg -> appendLog("[DBI HTTP] $msg") }
                )
                dbiServer?.start()
                dbiServerRunning = true
            }
            buildUserInterface()
        })

        parent.addView(card)
    }

    // =========================================================================
    // ВКЛАДКА 4: КЛЮЧИ (КАК В STORM SWITCH), АВТООБНОВЛЕНИЕ И ПАРАМЕТРЫ
    // =========================================================================

    private fun buildKeysAndSettingsTab(parent: LinearLayout, th: StormTheme) {
        // ГРУППА 1: Онлайн-загрузка и управление ключами Nintendo Switch (как в проекте STORM SWITCH)
        val keysCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        keysCard.addView(makeSectionHeader(
            "1. Ключи Nintendo Switch (prod.keys / title.keys) — Онлайн-загрузка STORM SWITCH",
            "Скачивайте актуальные ключи прошивок HOS в 1 клик или импортируйте из папок эмуляторов",
            th
        ))

        val prodKeysFile = File(keysDir, "prod.keys")
        val titleKeysFile = File(keysDir, "title.keys")
        val prodLines = if (prodKeysFile.exists()) prodKeysFile.readLines().count { it.contains("=") } else 0
        val titleLines = if (titleKeysFile.exists()) titleKeysFile.readLines().count { it.contains("=") } else 0
        val headerVer = if (prodKeysFile.exists()) {
            prodKeysFile.readLines().firstOrNull { it.startsWith("# STORM SWITCH KEYS:") }?.substringAfter(":")?.trim() ?: "Установлен"
        } else "Не установлен"

        val keyStatusTxt = TextView(this).apply {
            text = buildString {
                appendLine("• Статус prod.keys: ${if (prodKeysFile.exists()) "🟢 Активен (Версия: $headerVer • $prodLines ключей)" else "⚪ Не найден"}")
                appendLine("• Статус title.keys: ${if (titleKeysFile.exists()) "🟢 Активен ($titleLines ключей)" else "⚪ Не найден"}")
                append("• Путь хранения: ${keysDir.absolutePath}")
            }
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(4), 0, dp(8))
        }
        keysCard.addView(keyStatusTxt)

        val keysSpinnerLabels = onlineKeysCatalog.map { asset ->
            val rec = if (asset.isRecommended) " (Рекомендуется)" else ""
            "Ключи HOS ${asset.version}$rec — ${StormNczEngine.formatSize(asset.sizeBytes)}"
        }
        val keysDropdown = makeStormDropdown(
            keysSpinnerLabels,
            selectedOnlineKeyIndex.coerceIn(0, onlineKeysCatalog.lastIndex.coerceAtLeast(0)),
            th
        ) { pos ->
            selectedOnlineKeyIndex = pos
        }
        keysCard.addView(keysDropdown)

        val onlineKeysRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(8)
            }
        }
        onlineKeysRow.addView(makeButton("⬇️ Скачать и установить ключи", th.accent) {
            val asset = onlineKeysCatalog.getOrNull(selectedOnlineKeyIndex) ?: onlineKeysCatalog.first()
            keyStatusTxt.text = "⏳ Скачивание ключей HOS ${asset.version} из репозитория STORM SWITCH TOOLS..."
            thread {
                val targetDirs = listOfNotNull(
                    keysDir,
                    File("/storage/emulated/0/STORM SWITCH/keys"),
                    File(filesDir, "keys")
                )
                val (ok, msg) = RuTrackerCloudEngine.downloadAndInstallSwitchKeys(asset, targetDirs) { st ->
                    mainHandler.post { keyStatusTxt.text = "⏳ $st" }
                }
                appendLog(if (ok) "✅ $msg" else "❌ $msg")
                mainHandler.post {
                    Toast.makeText(this, msg, Toast.LENGTH_LONG).show()
                    buildUserInterface()
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        onlineKeysRow.addView(makeButton("🔄 Обновить список", th.bgElevated, th.textPrimary) {
            keyStatusTxt.text = "⏳ Проверка свежих релизов ключей на GitHub..."
            thread {
                val fresh = RuTrackerCloudEngine.fetchOnlineKeysCatalogFromGitHub()
                mainHandler.post {
                    onlineKeysCatalog = fresh
                    selectedOnlineKeyIndex = 0
                    buildUserInterface()
                    Toast.makeText(this, "Каталог ключей обновлён (${fresh.size} версий)!", Toast.LENGTH_SHORT).show()
                }
            }
        })
        keysCard.addView(onlineKeysRow)

        val localKeysRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }
        localKeysRow.addView(makeButton("📂 Выбрать prod.keys с диска", th.bgElevated, th.textPrimary) {
            val intent = Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                addCategory(Intent.CATEGORY_OPENABLE)
                type = "*/*"
            }
            startActivityForResult(intent, REQ_PICK_KEYS_FILE)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        localKeysRow.addView(makeButton("🔍 Автопоиск из эмуляторов", th.bgElevated, th.textPrimary) {
            val candidates = listOf(
                File("/storage/emulated/0/STORM SWITCH/keys/prod.keys"),
                File("/storage/emulated/0/switch/prod.keys"),
                File("/storage/emulated/0/Android/data/org.yuzu.yuzu_emu/files/keys/prod.keys"),
                File("/storage/emulated/0/Android/data/dev.eden.eden_emulator/files/keys/prod.keys"),
                File("/storage/emulated/0/Download/prod.keys")
            )
            val found = candidates.firstOrNull { it.exists() }
            if (found != null) {
                found.copyTo(prodKeysFile, overwrite = true)
                Toast.makeText(this, "Импортирован prod.keys из ${found.absolutePath}!", Toast.LENGTH_LONG).show()
                buildUserInterface()
            } else {
                Toast.makeText(this, "Локальные ключи не найдены. Нажмите «Скачать и установить ключи» выше!", Toast.LENGTH_LONG).show()
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        keysCard.addView(localKeysRow)
        parent.addView(keysCard)

        // ГРУППА 2: Автоматическое обновление приложения с GitHub
        val updCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        updCard.addView(makeSectionHeader(
            "2. Обновление STORM SWITCH BOX с GitHub",
            "Текущая версия: ${"5.5.1"} (${(if (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) "legacy" else "modern").uppercase()})",
            th
        ))
        updCard.addView(makeStormCheckBox(
            "Автоматически проверять наличие новой версии при запуске приложения",
            autoCheckUpdatesOnStartup,
            th
        ) { checked ->
            autoCheckUpdatesOnStartup = checked
            saveSettings()
        })
        updCard.addView(makeButton("🔄 Проверить обновления на GitHub сейчас", th.accent) {
            checkForUpdatesFromGitHub(isManualClick = true)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        })
        parent.addView(updCard)

        // ГРУППА 3: Темы оформления и язык интерфейса
        val uiCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        uiCard.addView(makeSectionHeader("3. Визуальная тема (9 эталонных стилей STORM) и язык", null, th))

        uiCard.addView(TextView(this).apply {
            text = "Тема оформления интерфейса:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(2), 0, dp(3))
        })

        val themeDropdown = makeStormDropdown(themes.map { it.title }, currentThemeIndex, th) { pos ->
            if (pos != currentThemeIndex) {
                currentThemeIndex = pos
                saveSettings()
                buildUserInterface()
            }
        }
        uiCard.addView(themeDropdown)

        uiCard.addView(TextView(this).apply {
            text = "Язык интерфейса:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(8), 0, dp(3))
        })

        val langDropdown = makeStormDropdown(
            languages.map { it.second },
            languages.indexOfFirst { it.first == currentLangCode }.coerceAtLeast(0),
            th
        ) { pos ->
            val code = languages[pos].first
            if (code != currentLangCode) {
                currentLangCode = code
                saveSettings()
                buildUserInterface()
            }
        }
        uiCard.addView(langDropdown)
        parent.addView(uiCard)
    }

    // =========================================================================
    // СИСТЕМНЫЙ И ВСТРОЕННЫЙ МУЛЬТИВЫБОР ФАЙЛОВ И ПАПОК
    // =========================================================================

    private fun openSystemMultiFilePicker() {
        try {
            val intent = Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                addCategory(Intent.CATEGORY_OPENABLE)
                type = "*/*"
                putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true)
            }
            startActivityForResult(Intent.createChooser(intent, "Выберите один или несколько файлов"), REQ_PICK_MULTIPLE_FILES)
        } catch (_: Exception) {
            showMultiSelectFileAndFolderDialog()
        }
    }

    private fun openSystemFolderTreePicker() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            try {
                val intent = Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
                startActivityForResult(intent, REQ_PICK_FOLDER_TREE)
                return
            } catch (_: Exception) {}
        }
        showMultiSelectFileAndFolderDialog()
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        hideSystemBars(window)
        if (resultCode != RESULT_OK || data == null) return

        when (requestCode) {
            REQ_PICK_MULTIPLE_FILES -> {
                var added = 0
                val clip = data.clipData
                if (clip != null) {
                    for (i in 0 until clip.itemCount) {
                        val uri = clip.getItemAt(i).uri
                        resolveOrCopyUriToFile(uri)?.let { f ->
                            if (queuedFiles.none { it.canonicalPath == f.canonicalPath }) {
                                queuedFiles.add(f)
                                added++
                            }
                        }
                    }
                } else {
                    data.data?.let { uri ->
                        resolveOrCopyUriToFile(uri)?.let { f ->
                            if (queuedFiles.none { it.canonicalPath == f.canonicalPath }) {
                                queuedFiles.add(f)
                                added++
                            }
                        }
                    }
                }
                if (selectedInputFile == null) selectedInputFile = queuedFiles.firstOrNull()
                Toast.makeText(this, "Добавлено файлов в очередь: $added", Toast.LENGTH_SHORT).show()
                buildUserInterface()
            }
            REQ_PICK_FOLDER_TREE -> {
                data.data?.let { treeUri ->
                    val folder = resolveTreeUriToDirectory(treeUri)
                    if (folder != null && folder.exists()) {
                        val added = addFolderRecursivelyToQueue(folder)
                        Toast.makeText(this, "Из папки ${folder.name} добавлено файлов: $added", Toast.LENGTH_LONG).show()
                        buildUserInterface()
                    } else {
                        showMultiSelectFileAndFolderDialog()
                    }
                }
            }
            REQ_PICK_KEYS_FILE -> {
                data.data?.let { uri ->
                    try {
                        val outProd = File(keysDir, "prod.keys")
                        contentResolver.openInputStream(uri)?.use { input ->
                            FileOutputStream(outProd).use { output -> input.copyTo(output) }
                        }
                        Toast.makeText(this, "Файл prod.keys успешно импортирован!", Toast.LENGTH_LONG).show()
                        buildUserInterface()
                    } catch (e: Exception) {
                        Toast.makeText(this, "Ошибка импорта ключей: ${e.message}", Toast.LENGTH_LONG).show()
                    }
                }
            }
        }
    }

    private fun resolveTreeUriToDirectory(treeUri: Uri): File? {
        try {
            val docId = DocumentsContract.getTreeDocumentId(treeUri)
            val parts = docId.split(":")
            if (parts.size >= 2) {
                val type = parts[0]
                val rel = parts[1]
                return if (type.equals("primary", ignoreCase = true)) {
                    File("/storage/emulated/0/$rel")
                } else {
                    File("/storage/$type/$rel")
                }
            }
        } catch (_: Exception) {}
        return null
    }

    private fun resolveOrCopyUriToFile(uri: Uri): File? {
        try {
            if (DocumentsContract.isDocumentUri(this, uri)) {
                val docId = DocumentsContract.getDocumentId(uri)
                val parts = docId.split(":")
                if (parts.size == 2) {
                    val candidate = if (parts[0].equals("primary", ignoreCase = true)) {
                        File("/storage/emulated/0/${parts[1]}")
                    } else {
                        File("/storage/${parts[0]}/${parts[1]}")
                    }
                    if (candidate.exists()) return candidate
                }
            }
            var displayName = "rom_${System.currentTimeMillis()}.nsp"
            contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
                if (c.moveToFirst()) {
                    val idx = c.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                    if (idx >= 0) displayName = c.getString(idx)
                }
            }
            val dest = File(romsDir, displayName)
            if (!dest.exists()) {
                contentResolver.openInputStream(uri)?.use { input ->
                    FileOutputStream(dest).use { output -> input.copyTo(output) }
                }
            }
            return dest
        } catch (_: Exception) {
            return null
        }
    }

    private fun addFolderRecursivelyToQueue(folder: File): Int {
        if (!folder.exists()) return 0
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "3ds", "cia", "nca", "ncz")
        var added = 0
        folder.walkTopDown().maxDepth(4).forEach { f ->
            if (f.isFile && f.extension.lowercase() in validExts) {
                if (queuedFiles.none { it.canonicalPath == f.canonicalPath }) {
                    queuedFiles.add(f)
                    added++
                }
            }
        }
        if (selectedInputFile == null) selectedInputFile = queuedFiles.firstOrNull()
        return added
    }

    /**
     * Встроенный мульти-проводник с чекбоксами для выбора любых папок и множества отдельных файлов.
     */
    private fun showMultiSelectFileAndFolderDialog(initialDir: File = File(lastBrowseDirPath)) {
        val th = themes[currentThemeIndex]
        var currentDir = if (initialDir.exists() && initialDir.isDirectory) initialDir else File("/storage/emulated/0")
        val selectedPaths = LinkedHashSet<File>()
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "3ds", "cia", "nca", "ncz")

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(th.bgCard)
            setPadding(dp(12), dp(10), dp(12), dp(10))
        }

        val pathTxt = TextView(this).apply {
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            typeface = Typeface.DEFAULT_BOLD
        }
        root.addView(pathTxt)

        // Быстрые кнопки переходов по папкам
        val quickScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setPadding(0, dp(6), 0, dp(6))
        }
        val quickRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val listContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        fun refreshDir() {
            pathTxt.text = "📂 Текущая папка: ${currentDir.absolutePath}"
            listContainer.removeAllViews()

            currentDir.parentFile?.let { parentDir ->
                if (parentDir.canRead()) {
                    listContainer.addView(makeButton("⬆️ .. (На уровень выше: ${parentDir.name})", th.bgElevated, th.textPrimary) {
                        currentDir = parentDir
                        refreshDir()
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            bottomMargin = dp(6)
                        }
                    })
                }
            }

            // Кнопка "Добавить всю текущую папку целиком"
            listContainer.addView(makeButton("✅ Выбрать всю папку «${currentDir.name}» целиком", th.bgElevated, th.accent) {
                selectedPaths.add(currentDir)
                Toast.makeText(this, "Папка ${currentDir.name} отмечена для добавления", Toast.LENGTH_SHORT).show()
                refreshDir()
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    bottomMargin = dp(6)
                }
            })

            val children = currentDir.listFiles()?.sortedWith(compareBy({ !it.isDirectory }, { it.name.lowercase() })) ?: emptyList()
            for (child in children) {
                if (!child.isDirectory && child.extension.lowercase() !in validExts) continue

                val row = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    background = makeCardDrawable(th.bgPrimary, th.border, 6)
                    setPadding(dp(8), dp(4), dp(8), dp(4))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(4)
                    }
                }

                var itemChecked = selectedPaths.contains(child)
                val squareBox = View(this).apply {
                    layoutParams = LinearLayout.LayoutParams(dp(18), dp(18)).apply {
                        marginEnd = dp(10)
                    }
                }
                updateStormCheckSquare(squareBox, itemChecked, th)
                val toggleItem = {
                    itemChecked = !itemChecked
                    if (itemChecked) selectedPaths.add(child) else selectedPaths.remove(child)
                    updateStormCheckSquare(squareBox, itemChecked, th)
                }
                squareBox.setOnClickListener { toggleItem() }
                row.addView(squareBox)

                val label = TextView(this).apply {
                    text = if (child.isDirectory) "📁 ${child.name}/" else "📄 ${child.name} (${StormNczEngine.formatSize(child.length())})"
                    setTextColor(if (child.isDirectory) th.accent else th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                    setOnClickListener {
                        if (child.isDirectory) {
                            currentDir = child
                            lastBrowseDirPath = currentDir.absolutePath
                            saveSettings()
                            refreshDir()
                        } else {
                            toggleItem()
                        }
                    }
                }
                row.addView(label)
                listContainer.addView(row)
            }
        }

        listOf(
            "Память (/storage/emulated/0)" to File("/storage/emulated/0"),
            "Загрузки (Download)" to File("/storage/emulated/0/Download"),
            "STORM SWITCH BOX/roms" to romsDir,
            "STORM SWITCH/roms" to File("/storage/emulated/0/STORM SWITCH/roms")
        ).forEach { (title, dir) ->
            val b = makeButton(title, th.bgElevated, th.textPrimary) {
                if (!dir.exists()) dir.mkdirs()
                currentDir = dir
                refreshDir()
            }
            b.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
            quickRow.addView(b)
        }
        quickScroll.addView(quickRow)
        root.addView(quickScroll)

        val scroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(320))
            addView(listContainer)
        }
        root.addView(scroll)

        refreshDir()

        val builder = AlertDialog.Builder(this)
            .setTitle("Мультивыбор файлов и папок")
            .setView(root)
            .setPositiveButton("Добавить выбранное") { d, _ ->
                var count = 0
                for (item in selectedPaths) {
                    if (item.isDirectory) {
                        count += addFolderRecursivelyToQueue(item)
                    } else if (item.isFile) {
                        if (queuedFiles.none { it.canonicalPath == item.canonicalPath }) {
                            queuedFiles.add(item)
                            count++
                        }
                    }
                }
                if (selectedInputFile == null) selectedInputFile = queuedFiles.firstOrNull()
                Toast.makeText(this, "Добавлено в очередь файлов: $count", Toast.LENGTH_SHORT).show()
                buildUserInterface()
                d.dismiss()
            }
            .setNegativeButton("Отмена") { d, _ -> d.dismiss() }
        showImmersiveDialog(builder)
    }

    private fun showSelectOutputFolderDialog() {
        val th = themes[currentThemeIndex]
        val input = EditText(this).apply {
            setText(customOutputDir.absolutePath)
            setTextColor(th.textPrimary)
            setBackgroundColor(th.bgPrimary)
            setPadding(dp(12), dp(10), dp(12), dp(10))
        }
        val builder = AlertDialog.Builder(this)
            .setTitle("Выберите выходную папку сохранения")
            .setView(input)
            .setPositiveButton("Сохранить") { d, _ ->
                val path = input.text.toString().trim()
                if (path.isNotEmpty()) {
                    val f = File(path)
                    f.mkdirs()
                    if (f.exists()) {
                        customOutputDir = f
                        saveSettings()
                        buildUserInterface()
                    }
                }
                d.dismiss()
            }
            .setNeutralButton("По умолчанию") { d, _ ->
                customOutputDir = File(stormRoot, "output")
                customOutputDir.mkdirs()
                saveSettings()
                buildUserInterface()
                d.dismiss()
            }
            .setNegativeButton("Отмена") { d, _ -> d.dismiss() }
        showImmersiveDialog(builder)
    }

    // =========================================================================
    // АВТОМАТИЧЕСКАЯ ПРОВЕРКА ОБНОВЛЕНИЙ И УСТАНОВКА APK С GITHUB
    // =========================================================================

    private fun checkForUpdatesFromGitHub(isManualClick: Boolean) {
        if (isManualClick) {
            Toast.makeText(this, "⏳ Проверка обновлений STORM SWITCH BOX на GitHub...", Toast.LENGTH_SHORT).show()
        }
        val preferLegacy = (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) || Build.VERSION.SDK_INT < 26
        thread {
            val rel = RuTrackerCloudEngine.checkGitHubAppUpdate("5.5.1", preferLegacy)
            mainHandler.post {
                if (rel != null) {
                    showUpdateAvailableDialog(rel)
                } else if (isManualClick) {
                    Toast.makeText(
                        this,
                        "✅ У вас установлена актуальная версия STORM SWITCH BOX ${"5.5.1"}!",
                        Toast.LENGTH_LONG
                    ).show()
                }
            }
        }
    }

    private fun showUpdateAvailableDialog(release: RuTrackerCloudEngine.AppUpdateRelease) {
        val th = themes[currentThemeIndex]
        val box = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(th.bgCard)
            setPadding(dp(16), dp(12), dp(16), dp(12))
        }
        box.addView(TextView(this).apply {
            text = "Доступна новая версия: ${release.version} (Установлена: ${"5.5.1"})"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
            typeface = Typeface.DEFAULT_BOLD
        })
        box.addView(TextView(this).apply {
            text = "Пакет: ${release.apkName} (${StormNczEngine.formatSize(release.apkSizeBytes)})"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(4), 0, dp(8))
        })

        val cleanNotes = release.changelog
            .replace(Regex("<.*?>"), "")
            .trim()
            .take(1200)
        val notesScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(180))
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
        }
        notesScroll.addView(TextView(this).apply {
            text = cleanNotes.ifEmpty { "Доступен новый релиз на GitHub." }
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
        })
        box.addView(notesScroll)

        val builder = AlertDialog.Builder(this)
            .setTitle("🚀 Доступно обновление STORM SWITCH BOX")
            .setView(box)
            .setPositiveButton("⬇️ Скачать и обновить") { d, _ ->
                d.dismiss()
                startApkUpdateDownload(release)
            }
            .setNegativeButton("Позже") { d, _ -> d.dismiss() }
        showImmersiveDialog(builder)
    }

    private fun startApkUpdateDownload(release: RuTrackerCloudEngine.AppUpdateRelease) {
        val th = themes[currentThemeIndex]
        val cancelled = java.util.concurrent.atomic.AtomicBoolean(false)

        val box = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(th.bgCard)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }
        val statusLabel = TextView(this).apply {
            text = "Подготовка к скачиванию ${release.apkName}..."
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.5f)
        }
        box.addView(statusLabel)

        val pb = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(18)).apply {
                topMargin = dp(10)
            }
        }
        box.addView(pb)

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setTitle("Загрузка обновления ${release.version}")
                .setView(box)
                .setCancelable(false)
                .setNegativeButton("Отмена") { d, _ ->
                    cancelled.set(true)
                    d.dismiss()
                }
        )

        val destDir = externalCacheDir ?: cacheDir
        val apkFile = File(destDir, "STORM_SWITCH_BOX_${release.version}_update.apk")

        thread {
            val ok = RuTrackerCloudEngine.downloadApkUpdateFile(
                release = release,
                destFile = apkFile,
                isCancelled = { cancelled.get() },
                onProgress = { pct, dlBytes, totalBytes, speedMb ->
                    mainHandler.post {
                        pb.progress = pct
                        val speedStr = String.format(Locale.US, "%.2f МБ/с", speedMb)
                        statusLabel.text = "Скачано: ${StormNczEngine.formatSize(dlBytes)} / ${StormNczEngine.formatSize(totalBytes)} ($pct% • $speedStr)"
                    }
                }
            )
            mainHandler.post {
                try { dlg.dismiss() } catch (_: Exception) {}
                if (cancelled.get()) return@post
                if (ok && apkFile.exists()) {
                    promptInstallDownloadedApk(apkFile)
                } else {
                    Toast.makeText(this, "❌ Не удалось скачать APK обновление с GitHub.", Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    private fun promptInstallDownloadedApk(apkFile: File) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            if (!packageManager.canRequestPackageInstalls()) {
                pendingInstallApkFile = apkFile
                Toast.makeText(this, "Разрешите установку обновлений из STORM SWITCH BOX", Toast.LENGTH_LONG).show()
                try {
                    val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:$packageName"))
                    startActivityForResult(intent, REQ_INSTALL_UNKNOWN_APPS)
                    return
                } catch (_: Exception) {}
            }
        }
        launchApkInstallerIntent(apkFile)
    }

    private fun launchApkInstallerIntent(apkFile: File) {
        try {
            val apkUri = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                StormApkFileProvider.getUriForFile(this, apkFile)
            } else {
                Uri.fromFile(apkFile)
            }
            val intent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(apkUri, "application/vnd.android.package-archive")
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            startActivity(intent)
        } catch (e: Exception) {
            Toast.makeText(this, "Ошибка запуска установщика APK: ${e.message}", Toast.LENGTH_LONG).show()
        }
    }

    private fun getLocalWifiIp(): String {
        try {
            for (intf in Collections.list(NetworkInterface.getNetworkInterfaces())) {
                for (addr in Collections.list(intf.inetAddresses)) {
                    if (!addr.isLoopbackAddress && addr.hostAddress?.contains(':') == false) {
                        return addr.hostAddress ?: "127.0.0.1"
                    }
                }
            }
        } catch (_: Exception) {}
        return "127.0.0.1"
    }
}
