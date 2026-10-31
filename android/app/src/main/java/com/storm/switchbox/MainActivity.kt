package com.storm.switchbox

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
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
import android.widget.FrameLayout
import android.widget.HorizontalScrollView
import android.widget.ImageView
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
    private var customSwitch2OutputDir: File = File(stormRoot, "roms_switch2")
    private var custom3dsOutputDir: File = File(stormRoot, "roms_3ds")
    private val modsDir = File(stormRoot, "mods")
    private val keysDir = File(stormRoot, "keys")
    private val extractedDir = File(stormRoot, "extracted")
    private val settingsFile = File(stormRoot, "settings/settings.json")

    private var currentThemeIndex = 0
    private fun detectDefaultLanguage(): String {
        val sysLang = java.util.Locale.getDefault().language.lowercase()
        return when (sysLang) {
            "ru", "uk", "be", "kk" -> "ru"
            "en", "de", "fr", "es", "it", "pt", "pl", "tr", "zh", "ja" -> sysLang
            else -> "en"
        }
    }

    private var currentLangCode = detectDefaultLanguage()
    private var activeTab = 0
    private var pendingRuTrackerSearchQuery: String = ""
    private var navScrollX = 0

    // Параметры движка и пакетной очереди (сохраняются локально)
    private val queuedFiles = mutableListOf<File>()
    private var selectedInputFile: File? = null
    private val expandedQueueGroups = mutableSetOf<String>()
    private var selectedOperationMode = 0
    // 0 = Умный мульти-контент (Сшивка База + Патч + DLC + Моды),
    // 1 = Пакетное сжатие и конвертация (По отдельности),
    // 2 = Распаковка NSZ/XCZ -> NSP/XCI,
    // 3 = SmartShrink Trim 0xFF (XCI/3DS),
    // 4 = Дешифратор Nintendo 3DS (.3ds/.cia)
    private var selectedCompressionMethod = 0
    // 0 = Умный адаптивный способ сжатия (авто-подбор параметров блоков, пропуск уже сжатых .ncz и несжимаемых данных),
    // 1 = Эталонный блочный NCZBLOCK (фиксированный блок для всех NCA),
    // 2 = Максимальное сжатие Zstandard Ultra (приоритет минимального веса),
    // 3 = Быстрая прямая сшивка без пережатия NCA
    private var selectedOutputFormat = 0
    // Конечный формат задаёт пользователь: 0 = NSZ, 1 = XCZ, 2 = NSP, 3 = XCI
    private var useBlockNcz = true // По умолчанию включен!
    private var smartMergeDedup = true // По умолчанию включен!
    private var autoEmbedMods = true // По умолчанию включен!
    private var zstdLevel = 18
    private var blockSizeExpo = 20 // 1 MB
    private var cpuThreads = Runtime.getRuntime().availableProcessors().coerceIn(2, 8)
    private var smartSizeGuard = true
    private var trimXciBeforeCompress = true
    private var deleteSourceAfterSuccess = false
    private var deleteRuTrackerSourceAfterSuccess = true
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

    // Предварительное переименование внутри (control.nacp / SMDH), смена иконки 256x256 и имя итогового файла (как на ПК)
    private var preBuildCustomizeEnabled = false
    private var customInternalGameTitle = ""
    private var customGamePublisher = ""
    private var customDisplayVer = ""
    private var customOutputFileName = ""
    private var customIconImagePath = ""
    private var selectedIconBadgeIndex = 0
    private val iconBadgeOptions = listOf("Без бейджа", "RUS", "RUS + MOD", "60 FPS", "MULTI", "4K MOD")
    private var onCustomIconPickedCallback: ((String) -> Unit)? = null

    // Параметры 6.0.1: Распаковка/Упаковка RomFS+ExeFS, Генератор Forwarder (.nro -> .nsp), 3DS FBI QR и FTP
    private var extractRomfsEnabled = true
    private var extractExefsEnabled = true
    private var extractControlEnabled = true
    private var extractRawPfs0Enabled = true
    private var fwdAppTitle = "DBI Installer"
    private var fwdAppAuthor = "Nintendo"
    private var fwdAppVersion = "715.0"
    private var fwdTitleId = "0100F0001DB10000"
    private var fwdSdmcNroPath = "sdmc:/switch/DBI/DBI.nro"
    private var fwdRomArgPath = ""
    private var fwdLocalNroPath = ""
    private var fwdCustomIconPath = ""
    private var ftpConsoleIp = "192.168.1.50"
    private var ftpConsolePort = 5000
    private var ftpRemoteDir = "/switch/"
    private var selectedQrFileIndex = 0
    private var selectedFtpFileIndex = 0

    companion object {
        private const val REQ_PICK_MULTIPLE_FILES = 1001
        private const val REQ_PICK_FOLDER_TREE = 1002
        private const val REQ_PICK_KEYS_FILE = 1003
        private const val REQ_INSTALL_UNKNOWN_APPS = 1004
        private const val REQ_PICK_OUTPUT_FOLDER_TREE = 1005
        private const val REQ_PICK_CUSTOM_ICON = 1006
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        hideSystemBars(window)

        window.decorView.setOnSystemUiVisibilityChangeListener {
            hideSystemBars(window)
        }

        StormNczEngine.initAppContext(applicationContext)
        RuTrackerCloudEngine.appContext = applicationContext
        ensureStormDirectories()
        loadSettings()

        if (!settingsFile.exists()) {
            currentLangCode = detectDefaultLanguage()
        }

        buildUserInterface()
        appendLog("STORM SWITCH BOX ${"6.7.1"} (${(if (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) "legacy" else "modern").uppercase()}) готов к работе.")

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

    /**
     * Оборачивает кастомную карточку диалога в прозрачный контейнер с отступами,
     * чтобы прямоугольное окно Android AlertDialog никогда не обрезало скруглённую рамку на углах.
     */
    private fun makeUnclippedDialogContainer(cardView: View): FrameLayout {
        return FrameLayout(this).apply {
            setBackgroundColor(Color.TRANSPARENT)
            clipChildren = false
            clipToPadding = false
            setPadding(dp(8), dp(8), dp(8), dp(8))
            addView(
                cardView,
                FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT
                )
            )
        }
    }

    private fun showImmersiveDialog(builder: AlertDialog.Builder): AlertDialog {
        val dlg = builder.create()
        dlg.setOnKeyListener { d, keyCode, event ->
            if (keyCode == KeyEvent.KEYCODE_ESCAPE && event.action == KeyEvent.ACTION_UP) {
                d.dismiss()
                true
            } else false
        }
        dlg.window?.setBackgroundDrawable(android.graphics.drawable.ColorDrawable(Color.TRANSPARENT))
        dlg.window?.setFlags(WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE, WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE)
        dlg.show()
        dlg.window?.let { w ->
            w.setBackgroundDrawable(android.graphics.drawable.ColorDrawable(Color.TRANSPARENT))
            w.decorView?.setPadding(0, 0, 0, 0)
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
                val savedLang = json.optString("lang", "")
                if (savedLang.isNotBlank() && languages.any { it.first == savedLang }) {
                    currentLangCode = savedLang
                }
                selectedOperationMode = json.optInt("operationMode", 0).coerceIn(0, 4)
                selectedCompressionMethod = json.optInt("compressionMethod", 0).coerceIn(0, 3)
                selectedOutputFormat = json.optInt("outputFormat", 0).coerceIn(0, 6)
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
                val savedOutSwitch2 = json.optString("outputDirSwitch2", "")
                if (savedOutSwitch2.isNotBlank()) {
                    customSwitch2OutputDir = File(savedOutSwitch2)
                    customSwitch2OutputDir.mkdirs()
                }
                val savedOut3ds = json.optString("outputDir3ds", "")
                if (savedOut3ds.isNotBlank()) {
                    custom3dsOutputDir = File(savedOut3ds)
                    custom3dsOutputDir.mkdirs()
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
            json.put("compressionMethod", selectedCompressionMethod)
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
            json.put("deleteRuTrackerSource", deleteRuTrackerSourceAfterSuccess)
            json.put("autoCheckUpdates", autoCheckUpdatesOnStartup)
            json.put("outputDir", customOutputDir.absolutePath)
            json.put("outputDirSwitch2", customSwitch2OutputDir.absolutePath)
            json.put("outputDir3ds", custom3dsOutputDir.absolutePath)
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
            this.gravity = Gravity.CENTER
            this.includeFontPadding = true
            this.minHeight = dp(48)
            this.minimumHeight = dp(48)
            this.setLineSpacing(0f, 1.05f)
            this.setTextColor(textColor)
            this.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            this.typeface = Typeface.create("sans-serif-medium", Typeface.BOLD)
            this.background = GradientDrawable().apply {
                shape = GradientDrawable.RECTANGLE
                cornerRadius = dp(8).toFloat()
                setColor(bgColor)
            }
            this.setPadding(dp(12), dp(9), dp(12), dp(9))
            this.setOnClickListener { onClick() }
        }
    }

    /**
     * Создаёт горизонтальный ряд кнопок единой высоты без обрезания текста снизу:
     * Сначала измеряет полную высоту каждой кнопки с учётом переноса строк (WRAP_CONTENT),
     * а затем выравнивает все кнопки ряда по максимальной высоте (не менее 48dp).
     */
    private fun makeUniformButtonRow(bottomMarginDp: Int = 6): LinearLayout {
        return object : LinearLayout(this@MainActivity) {
            override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
                for (i in 0 until childCount) {
                    val ch = getChildAt(i)
                    if (ch != null && ch.visibility != View.GONE) {
                        ch.layoutParams?.height = ViewGroup.LayoutParams.WRAP_CONTENT
                    }
                }
                super.onMeasure(widthMeasureSpec, MeasureSpec.makeMeasureSpec(0, MeasureSpec.UNSPECIFIED))
                var maxH = dp(48)
                for (i in 0 until childCount) {
                    val ch = getChildAt(i)
                    if (ch != null && ch.visibility != View.GONE) {
                        if (ch.measuredHeight > maxH) maxH = ch.measuredHeight
                    }
                }
                for (i in 0 until childCount) {
                    val ch = getChildAt(i)
                    if (ch != null && ch.visibility != View.GONE) {
                        ch.layoutParams?.height = maxH
                    }
                }
                super.onMeasure(
                    widthMeasureSpec,
                    MeasureSpec.makeMeasureSpec(maxH + paddingTop + paddingBottom, MeasureSpec.EXACTLY)
                )
            }
        }.apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            isBaselineAligned = false
            layoutParams = LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT
            ).apply {
                if (bottomMarginDp > 0) bottomMargin = dp(bottomMarginDp)
            }
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

        val applyStateProgrammatically: (Boolean) -> Unit = { newState ->
            isCheckedState = newState
            updateStormCheckSquare(boxView, isCheckedState, th)
            textView.setTextColor(if (isCheckedState) th.textPrimary else th.textSecondary)
        }
        row.tag = applyStateProgrammatically

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
            val popupCard = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                background = makeCardDrawable(th.bgCard, th.accent, radiusDp = 10)
                setPadding(dp(6), dp(6), dp(6), dp(6))
                clipChildren = true
                clipToPadding = true
            }

            val listLinear = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
            }

            val scroll = ScrollView(this).apply {
                addView(listLinear)
            }
            popupCard.addView(scroll)

            val popupOuter = FrameLayout(this).apply {
                setBackgroundColor(Color.TRANSPARENT)
                clipChildren = false
                clipToPadding = false
                setPadding(dp(4), dp(4), dp(4), dp(4))
                addView(popupCard, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT))
            }

            val itemH = dp(38)
            val maxPopupH = (items.size * itemH + dp(16)).coerceAtMost(dp(240))
            val popupWidth = (container.width + dp(8)).coerceAtLeast(dp(260))

            val displayMetrics = resources.displayMetrics
            val screenH = displayMetrics.heightPixels
            val location = IntArray(2)
            container.getLocationOnScreen(location)
            val containerY = location[1]
            val containerH = container.height

            val spaceBelow = screenH - (containerY + containerH)
            val spaceAbove = containerY
            val openUpwards = spaceBelow < maxPopupH && spaceAbove > spaceBelow

            val popup = PopupWindow(
                popupOuter,
                popupWidth,
                maxPopupH,
                true
            ).apply {
                isOutsideTouchable = true
                elevation = dp(12).toFloat()
                setBackgroundDrawable(android.graphics.drawable.ColorDrawable(Color.TRANSPARENT))
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

            if (openUpwards) {
                popup.showAsDropDown(container, 0, -(maxPopupH + containerH + dp(4)))
            } else {
                popup.showAsDropDown(container, 0, dp(4))
            }
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
            text = "STORM SWITCH BOX ${"6.7.1"}"
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

        // 2. Панель навигации из 5 вкладок (с сохранением позиции прокрутки и авто-фокусом на выбранной вкладке)
        val navScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setBackgroundColor(th.bgElevated)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                setOnScrollChangeListener { _, scrollX, _, _, _ ->
                    navScrollX = scrollX
                }
            }
        }
        val navRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        val tabTitles = listOf(
            "📦 Очередь и конвертер",
            "🔍 Инспектор и RomFS",
            "🌐 RuTracker и Форвардер",
            "📡 Сеть, 3DS QR и FTP",
            "📚 База TitleDB и Ошибки",
            "🔑 Ключи и параметры",
            "📖 Инструкция"
        )
        tabTitles.forEachIndexed { idx, label ->
            val isSel = idx == activeTab
            val btn = makeButton(
                label,
                if (isSel) th.accent else th.bgCard,
                if (isSel) Color.WHITE else th.textSecondary
            ) {
                navScrollX = navScroll.scrollX
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
        navScroll.post {
            val activeBtn = navRow.getChildAt(activeTab)
            if (activeBtn != null && navScroll.width > 0) {
                val btnCenter = (activeBtn.left + activeBtn.right) / 2
                val targetX = (btnCenter - navScroll.width / 2).coerceAtLeast(0)
                navScroll.scrollTo(targetX, 0)
                navScrollX = navScroll.scrollX
            } else if (navScrollX > 0) {
                navScroll.scrollTo(navScrollX, 0)
            }
        }

        // 3. Основной прокручиваемый контейнер
        val mainScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f)
            isFillViewport = true
        }
        val bottomPadding = if (activeTab == 0 || activeTab == 5) dp(110) else dp(32)
        contentContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(12), dp(10), dp(12), bottomPadding)
        }

        when (activeTab) {
            0 -> buildConverterAndQueueTab(contentContainer, th)
            1 -> buildInspectorAndAuditorTab(contentContainer, th)
            2 -> buildRuTrackerAndModsTab(contentContainer, th)
            3 -> buildNetworkDbiTab(contentContainer, th)
            4 -> buildTitleDbAndErrorCodesTab(contentContainer, th)
            5 -> buildKeysAndSettingsTab(contentContainer, th)
            6 -> buildInstructionsTab(contentContainer, th)
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
        translateUiTree(window.decorView, currentLangCode)
        hideSystemBars(window)
    }

    // =========================================================================
    // ВКЛАДКА 0: ОЧЕРЕДЬ ЗАДАЧ И КОНВЕРТЕР (ПАРИТЕТ С ПК)
    // =========================================================================

    private fun getEffectiveOutputDirForFile(file: File): File {
        val name = file.name
        val path = file.absolutePath.replace('\\', '/')
        // Switch 2: TitleID starting with 04, e.g. [04...] or in dump dir
        val isSwitch2 = name.contains(Regex("""\[04[0-9a-fA-F]{14}\]""")) ||
                path.contains("/roms_switch2/", ignoreCase = true) ||
                file.nameWithoutExtension.startsWith("04")
        if (isSwitch2) {
            customSwitch2OutputDir.mkdirs()
            return customSwitch2OutputDir
        }

        // 3DS: .3ds, .cia, .cxi, or roms_3ds
        val ext = file.extension.lowercase()
        val is3ds = ext in listOf("3ds", "cia", "cxi", "cci") || path.contains("/roms_3ds/", ignoreCase = true)
        if (is3ds) {
            custom3dsOutputDir.mkdirs()
            return custom3dsOutputDir
        }

        customOutputDir.mkdirs()
        return customOutputDir
    }

    private fun buildOutputFoldersCard(th: StormTheme): LinearLayout {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }

        card.addView(makeSectionHeader(
            "📁 Выходные папки сохранения по платформам",
            "Настройте персональные целевые директории для Nintendo Switch, Nintendo Switch 2 и Nintendo 3DS",
            th
        ))

        val platforms = listOf(
            Triple(0, "🔴 Nintendo Switch", customOutputDir),
            Triple(1, "🟣 Nintendo Switch 2", customSwitch2OutputDir),
            Triple(2, "🔵 Nintendo 3DS", custom3dsOutputDir)
        )

        for ((mode, name, dir) in platforms) {
            val row = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                background = makeCardDrawable(th.bgElevated, th.border, radiusDp = 8)
                setPadding(dp(10), dp(8), dp(10), dp(8))
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    topMargin = dp(6)
                }
            }

            val topTitle = TextView(this).apply {
                text = name
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.5f)
                typeface = Typeface.DEFAULT_BOLD
            }
            row.addView(topTitle)

            val pathTxt = TextView(this).apply {
                text = dir.absolutePath
                setTextColor(th.textPrimary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                maxLines = 2
                ellipsize = TextUtils.TruncateAt.MIDDLE
                setPadding(0, dp(2), 0, dp(6))
            }
            row.addView(pathTxt)

            val btnRow = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
            }

            btnRow.addView(makeButton("📂 Выбрать", th.accent) {
                showSelectOutputFolderDialog(mode)
            }.apply {
                layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                    marginEnd = dp(6)
                }
            })

            btnRow.addView(makeButton("🔄 Сброс", th.bgCard, th.textSecondary) {
                when (mode) {
                    1 -> { customSwitch2OutputDir = File(stormRoot, "roms_switch2"); customSwitch2OutputDir.mkdirs() }
                    2 -> { custom3dsOutputDir = File(stormRoot, "roms_3ds"); custom3dsOutputDir.mkdirs() }
                    else -> { customOutputDir = File(stormRoot, "output"); customOutputDir.mkdirs() }
                }
                saveSettings()
                buildUserInterface()
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT)
            })

            row.addView(btnRow)
            card.addView(row)
        }

        return card
    }

    private val TRANSLATION_MAP = mapOf(
        "Очередь задач" to mapOf(
            "en" to "Task Queue", "de" to "Aufgaben-Warteschlange", "fr" to "File de tâches",
            "es" to "Cola de tareas", "it" to "Coda delle attività", "pt" to "Fila de tarefas",
            "pl" to "Kolejka zadań", "tr" to "Görev Kuyruğu", "zh" to "任务队列", "ja" to "タスクキュー"
        ),
        "СБОРКА" to mapOf(
            "en" to "BUILD", "de" to "ERSTELLEN", "fr" to "ASSEMBLAGE",
            "es" to "COMPILAR", "it" to "ASSEMBLAGGIO", "pt" to "COMPILAÇÃO",
            "pl" to "KOMPILACJA", "tr" to "DERLEME", "zh" to "构建", "ja" to "ビルド"
        ),
        "ИНСПЕКТОР" to mapOf(
            "en" to "INSPECTOR", "de" to "INSPEKTOR", "fr" to "INSPECTEUR",
            "es" to "INSPECTOR", "it" to "ISPETTORE", "pt" to "INSPETOR",
            "pl" to "INSPEKTOR", "tr" to "DENETÇİ", "zh" to "检测器", "ja" to "インスペクター"
        ),
        "СЕТЬ / DBI" to mapOf(
            "en" to "NETWORK / DBI", "de" to "NETZWERK / DBI", "fr" to "RÉSEAU / DBI",
            "es" to "RED / DBI", "it" to "RETE / DBI", "pt" to "REDE / DBI",
            "pl" to "SIEĆ / DBI", "tr" to "AĞ / DBI", "zh" to "网络 / DBI", "ja" to "ネットワーク / DBI"
        ),
        "НАСТРОЙКИ" to mapOf(
            "en" to "SETTINGS", "de" to "EINSTELLUNGEN", "fr" to "PARAMÈTRES",
            "es" to "AJUSTES", "it" to "IMPOSTAZIONI", "pt" to "CONFIGURAÇÕES",
            "pl" to "USTAWIENIA", "tr" to "AYARLAR", "zh" to "设置", "ja" to "設定"
        ),
        "СПРАВКА" to mapOf(
            "en" to "HELP", "de" to "HILFE", "fr" to "AIDE",
            "es" to "AYUDA", "it" to "GUIDA", "pt" to "AJUDA",
            "pl" to "POMOC", "tr" to "YARDIM", "zh" to "帮助", "ja" to "ヘルプ"
        ),
        "🔄 Обновить" to mapOf(
            "en" to "🔄 Update", "de" to "🔄 Aktualisieren", "fr" to "🔄 Mettre à jour",
            "es" to "🔄 Actualizar", "it" to "🔄 Aggiorna", "pt" to "🔄 Atualizar",
            "pl" to "🔄 Aktualizuj", "tr" to "🔄 Güncelle", "zh" to "🔄 更新", "ja" to "🔄 更新"
        ),
        "Изменить папку" to mapOf(
            "en" to "Change folder", "de" to "Ordner ändern", "fr" to "Changer de dossier",
            "es" to "Cambiar carpeta", "it" to "Cambia cartella", "pt" to "Mudar pasta",
            "pl" to "Zmień folder", "tr" to "Klasörü değiştir", "zh" to "更改文件夹", "ja" to "フォルダを変更"
        ),
        "📂 Выбрать" to mapOf(
            "en" to "📂 Select", "de" to "📂 Auswählen", "fr" to "📂 Sélectionner",
            "es" to "📂 Seleccionar", "it" to "📂 Seleziona", "pt" to "📂 Selecionar",
            "pl" to "📂 Wybierz", "tr" to "📂 Seç", "zh" to "📂 选择", "ja" to "📂 選択"
        ),
        "🔄 Сброс" to mapOf(
            "en" to "🔄 Reset", "de" to "🔄 Zurücksetzen", "fr" to "🔄 Réinitialiser",
            "es" to "🔄 Restablecer", "it" to "🔄 Ripristina", "pt" to "🔄 Redefinir",
            "pl" to "🔄 Reset", "tr" to "🔄 Sıfırla", "zh" to "🔄 重置", "ja" to "🔄 リセット"
        ),
        "Отмена" to mapOf(
            "en" to "Cancel", "de" to "Abbrechen", "fr" to "Annuler",
            "es" to "Cancelar", "it" to "Annulla", "pt" to "Cancelar",
            "pl" to "Anuluj", "tr" to "İptal", "zh" to "取消", "ja" to "キャンセル"
        ),
        "По умолчанию" to mapOf(
            "en" to "Default", "de" to "Standard", "fr" to "Par défaut",
            "es" to "Por defecto", "it" to "Predefinito", "pt" to "Padrão",
            "pl" to "Domyślne", "tr" to "Varsayılan", "zh" to "默认", "ja" to "デフォルト"
        ),
        "✅ Выбрать эту папку" to mapOf(
            "en" to "✅ Select this folder", "de" to "✅ Diesen Ordner auswählen", "fr" to "✅ Choisir ce dossier",
            "es" to "✅ Seleccionar esta carpeta", "it" to "✅ Seleziona questa cartella", "pt" to "✅ Selecionar esta pasta",
            "pl" to "✅ Wybierz ten folder", "tr" to "✅ Bu klasörü seç", "zh" to "✅ 选择此文件夹", "ja" to "✅ このフォルダを選択"
        ),
        "Тема оформления:" to mapOf(
            "en" to "Interface theme:", "de" to "Oberflächendesign:", "fr" to "Thème d'interface:",
            "es" to "Tema de interfaz:", "it" to "Tema interfaccia:", "pt" to "Tema da interface:",
            "pl" to "Motyw interfejsu:", "tr" to "Arayüz teması:", "zh" to "界面主题:", "ja" to "インターフェーステーマ:"
        ),
        "Язык интерфейса:" to mapOf(
            "en" to "Interface language:", "de" to "Oberflächensprache:", "fr" to "Langue de l'interface:",
            "es" to "Idioma de la interfaz:", "it" to "Lingua interfaccia:", "pt" to "Idioma da interface:",
            "pl" to "Język interfejsu:", "tr" to "Arayüz dili:", "zh" to "界面语言:", "ja" to "インターフェース言語:"
        ),
        "📁 Выходные папки сохранения по платформам" to mapOf(
            "en" to "📁 Output Folders by Platform", "de" to "📁 Ausgabeordner nach Plattform",
            "fr" to "📁 Dossiers de sortie par plateforme", "es" to "📁 Carpetas de salida por plataforma",
            "it" to "📁 Cartelle di destinazione per piattaforma", "pt" to "📁 Pastas de saída por plataforma",
            "pl" to "📁 Foldery wyjściowe wg platform", "tr" to "📁 Platforma Göre Çıktı Klasörleri",
            "zh" to "📁 按平台的输出文件夹", "ja" to "📁 プラットフォーム別出力フォルダ"
        ),
        "📋 Журнал операций и телеметрия" to mapOf(
            "en" to "📋 Operation Log and Telemetry", "de" to "📋 Betriebsprotokoll und Telemetrie",
            "fr" to "📋 Journal des opérations et télémétrie", "es" to "📋 Registro de operaciones y telemetría",
            "it" to "📋 Registro delle operazioni e telemetria", "pt" to "📋 Registro de operações e telemetria",
            "pl" to "📋 Dziennik operacji i telemetria", "tr" to "📋 İşlem Günlüğü ve Telemetri",
            "zh" to "📋 操作日志与遥测", "ja" to "📋 操作ログとテレメトリ"
        )
    )

    private fun translateText(src: String, lang: String): String {
        if (lang == "ru" || src.isBlank()) return src
        val trimmed = src.trim()
        val direct = TRANSLATION_MAP[trimmed]?.get(lang)
        if (direct != null) return if (src.startsWith(" ") || src.endsWith(" ")) src.replace(trimmed, direct) else direct
        // Search inside map
        for ((ruKey, trMap) in TRANSLATION_MAP) {
            val trans = trMap[lang]
            if (trans != null && trimmed.contains(ruKey)) {
                return src.replace(ruKey, trans)
            }
        }
        return src
    }

    private fun translateUiTree(view: View, lang: String) {
        if (lang == "ru") return
        when (view) {
            is TextView -> {
                val orig = view.text?.toString() ?: ""
                if (orig.isNotEmpty()) {
                    val tr = translateText(orig, lang)
                    if (tr != orig) view.text = tr
                }
                val hint = view.hint?.toString() ?: ""
                if (hint.isNotEmpty()) {
                    val trHint = translateText(hint, lang)
                    if (trHint != hint) view.hint = trHint
                }
            }
            is ViewGroup -> {
                for (i in 0 until view.childCount) {
                    translateUiTree(view.getChildAt(i), lang)
                }
            }
        }
    }

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

        parent.addView(sourceCard)
        parent.addView(buildOutputFoldersCard(th))

        // ГРУППА 2: Умная очередь обработки (автоматическая группировка разных игр по TitleID и семействам как на ПК)
        val queueCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        val totalQueueBytes = queuedFiles.sumOf { if (it.exists()) it.length() else 0L }
        val gameGroups = StormNczEngine.groupFilesByGameFamily(queuedFiles)

        val qHeaderRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        qHeaderRow.addView(TextView(this).apply {
            text = "2. Очередь обработки (Игр: ${gameGroups.size} • Файлов: ${queuedFiles.size} • ${StormNczEngine.formatSize(totalQueueBytes)})"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 13.5f)
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
                text = "Очередь пуста. Добавьте файлы или папки выше — программа автоматически распознает Базу, Патчи, DLC и Моды и сгруппирует их отдельно по каждой игре."
                setTextColor(th.textSecondary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                setPadding(0, dp(6), 0, dp(2))
            })
        } else {
            gameGroups.take(15).forEach { grp ->
                val isExpanded = expandedQueueGroups.contains(grp.groupKey) || gameGroups.size == 1
                val grpBox = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgPrimary, if (isExpanded) th.accent else th.border, 8)
                    setPadding(dp(10), dp(8), dp(10), dp(8))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(6)
                    }
                }
                val effectiveGroupTid = if (grp.items.size == 1 && grp.items[0].rawTitleId.isNotEmpty() && grp.items[0].roleOrder != 0) {
                    grp.items[0].rawTitleId
                } else {
                    grp.baseTitleId
                }
                val tidLabel = if (effectiveGroupTid.isNotEmpty()) " [$effectiveGroupTid]" else ""

                // Собираем полную сводку компонентов (БАЗА, ПАТЧ, DLC, UNLOCKER, МОДЫ) как в ПК-версии
                val allSubParts = mutableListOf<StormNczEngine.QueuedSubComponent>()
                val allAppliedMods = mutableListOf<String>()
                var historyRecord: StormNczEngine.TaskBuildHistoryEntry? = null
                for (qi in grp.items) {
                    val hist = StormNczEngine.getTaskBuildHistory(qi.file)
                    if (hist != null) {
                        historyRecord = hist
                        allSubParts.addAll(hist.processedParts)
                        allAppliedMods.addAll(hist.appliedMods)
                    } else if (qi.subComponents.isNotEmpty()) {
                        allSubParts.addAll(qi.subComponents)
                        allAppliedMods.addAll(qi.embeddedModNames)
                    } else {
                        allSubParts.add(
                            StormNczEngine.QueuedSubComponent(
                                roleBadge = qi.roleBadge,
                                titleId = qi.titleId.ifEmpty { grp.baseTitleId },
                                name = qi.file.name,
                                sizeBytes = if (qi.file.exists()) qi.file.length() else 0L
                            )
                        )
                    }
                }
                // Также проверяем наличие локальных модов в папке mods/atmosphere/contents/<BaseTID> (только для Базы или Мульти!)
                val allowsLocalMods = grp.items.any { it.roleBadge == "БАЗА" || it.roleBadge.startsWith("МУЛЬТИ") }
                if (allowsLocalMods && grp.baseTitleId.isNotEmpty()) {
                    val localModFolder = File(modsDir, "atmosphere/contents/${grp.baseTitleId}")
                    if (localModFolder.exists() && localModFolder.isDirectory) {
                        val modFilesCount = localModFolder.walkTopDown().maxDepth(4).count { it.isFile }
                        if (modFilesCount > 0 && allAppliedMods.none { it.contains(grp.baseTitleId) }) {
                            allAppliedMods.add("LayeredFS мод в папке atmosphere/contents/${grp.baseTitleId} ($modFilesCount ф.)")
                        }
                    }
                }

                val hdrRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    setOnClickListener {
                        if (expandedQueueGroups.contains(grp.groupKey)) {
                            expandedQueueGroups.remove(grp.groupKey)
                        } else {
                            expandedQueueGroups.add(grp.groupKey)
                        }
                        buildUserInterface()
                    }
                }
                val rolesSummaryBadges = allSubParts.map { it.roleBadge }.distinct().joinToString(" + ").ifEmpty { "БАЗА" }
                val modsCountBadge = if (allAppliedMods.isNotEmpty()) " • 🛠 Модов: ${allAppliedMods.size}" else ""
                hdrRow.addView(TextView(this).apply {
                    text = "🎮 ${grp.displayTitle}$tidLabel\n📦 Состав: [$rolesSummaryBadges]$modsCountBadge • ${StormNczEngine.formatSize(grp.totalBytes)}"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    typeface = Typeface.DEFAULT_BOLD
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                })
                hdrRow.addView(makeButton(if (isExpanded) "🔼 Скрыть состав" else "📋 Состав и моды ▼", th.bgElevated, th.accent) {
                    if (expandedQueueGroups.contains(grp.groupKey)) {
                        expandedQueueGroups.remove(grp.groupKey)
                    } else {
                        expandedQueueGroups.add(grp.groupKey)
                    }
                    buildUserInterface()
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply { marginStart = dp(6) }
                })
                grpBox.addView(hdrRow)

                grp.items.forEach { qItem ->
                    val file = qItem.file
                    val itemRow = LinearLayout(this).apply {
                        orientation = LinearLayout.HORIZONTAL
                        gravity = Gravity.CENTER_VERTICAL
                        background = makeCardDrawable(
                            if (selectedInputFile == file) th.bgElevated else th.bgCard,
                            if (selectedInputFile == file) th.accent else th.border,
                            6
                        )
                        setPadding(dp(8), dp(5), dp(8), dp(5))
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            topMargin = dp(4)
                        }
                        setOnClickListener {
                            selectedInputFile = file
                            if (!expandedQueueGroups.contains(grp.groupKey)) {
                                expandedQueueGroups.add(grp.groupKey)
                            } else {
                                expandedQueueGroups.remove(grp.groupKey)
                            }
                            buildUserInterface()
                        }
                    }
                    itemRow.addView(TextView(this).apply {
                        text = "[${qItem.roleBadge}]"
                        setTextColor(th.accent)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                        typeface = Typeface.DEFAULT_BOLD
                        setPadding(0, 0, dp(6), 0)
                    })
                    itemRow.addView(TextView(this).apply {
                        text = "${file.name} (${StormNczEngine.formatSize(file.length())})"
                        setTextColor(th.textPrimary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                        maxLines = 1
                        ellipsize = TextUtils.TruncateAt.MIDDLE
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                    })
                    itemRow.addView(makeButton("✕", th.bgPrimary, th.textSecondary) {
                        queuedFiles.removeAll { it.absolutePath == file.absolutePath }
                        if (selectedInputFile == file) selectedInputFile = queuedFiles.firstOrNull()
                        buildUserInterface()
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(dp(38), dp(36))
                    })
                    grpBox.addView(itemRow)
                }

                // Раскрывающийся детальный паспорт задачи (как на ПК: какие файлы сшивались и какие моды/анлокеры вшиты)
                if (isExpanded) {
                    val detailBox = LinearLayout(this).apply {
                        orientation = LinearLayout.VERTICAL
                        background = makeCardDrawable(th.bgCard, th.border, 6)
                        setPadding(dp(10), dp(8), dp(10), dp(8))
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            topMargin = dp(6)
                        }
                    }
                    detailBox.addView(TextView(this).apply {
                        text = "🔍 Детальный состав задачи и обработанных компонентов (ПК-паритет):"
                        setTextColor(th.accent)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.3f)
                        typeface = Typeface.DEFAULT_BOLD
                        setPadding(0, 0, 0, dp(4))
                    })

                    if (historyRecord != null && historyRecord.originalTotalBytes > historyRecord.finalSizeBytes) {
                        val saved = historyRecord.originalTotalBytes - historyRecord.finalSizeBytes
                        val pct = ((saved * 100L) / historyRecord.originalTotalBytes.coerceAtLeast(1L)).toInt()
                        detailBox.addView(TextView(this).apply {
                            text = "📊 Сводка сборки (${historyRecord.targetFormat}): исходный объём ${StormNczEngine.formatSize(historyRecord.originalTotalBytes)} -> итоговый ${StormNczEngine.formatSize(historyRecord.finalSizeBytes)} (экономия ${StormNczEngine.formatSize(saved)} / -$pct%)"
                            setTextColor(Color.parseColor("#22C55E"))
                            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                            typeface = Typeface.DEFAULT_BOLD
                            setPadding(0, 0, 0, dp(4))
                        })
                    }

                    allSubParts.forEachIndexed { idx, part ->
                        val badgeColor = when {
                            part.roleBadge.contains("БАЗА") -> th.accent
                            part.roleBadge.contains("ПАТЧ") -> Color.parseColor("#38BDF8")
                            part.roleBadge.contains("UNLOCK") -> Color.parseColor("#F59E0B")
                            part.roleBadge.contains("DLC") -> Color.parseColor("#A855F7")
                            else -> Color.parseColor("#22C55E")
                        }
                        detailBox.addView(TextView(this).apply {
                            val tidInfo = if (part.titleId.isNotEmpty()) " [TID: ${part.titleId}]" else ""
                            text = "  ${idx + 1}. [${part.roleBadge}]$tidInfo ${part.name} — ${StormNczEngine.formatSize(part.sizeBytes)}"
                            setTextColor(badgeColor)
                            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                            typeface = Typeface.MONOSPACE
                            setPadding(0, dp(1), 0, dp(1))
                        })
                    }

                    detailBox.addView(TextView(this).apply {
                        text = if (allAppliedMods.isNotEmpty()) {
                            "🛠 Добавленные моды / русификаторы (${allAppliedMods.size}):\n" +
                                allAppliedMods.joinToString("\n") { "  • ✅ $it" }
                        } else {
                            "🛠 Моды и русификаторы: не добавлялись (чистый официальный образ)"
                        }
                        setTextColor(if (allAppliedMods.isNotEmpty()) Color.parseColor("#22C55E") else th.textSecondary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                        setPadding(0, dp(4), 0, dp(4))
                    })

                    val quickActRow = LinearLayout(this).apply {
                        orientation = LinearLayout.HORIZONTAL
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            topMargin = dp(4)
                        }
                    }
                    val primaryFile = grp.items.first().file
                    quickActRow.addView(makeButton("🌲 Проводник RomFS / HEX", th.bgElevated, th.accent) {
                        selectedInputFile = primaryFile
                        showVirtualRomfsExplorerDialog(primaryFile, th)
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
                    })
                    quickActRow.addView(makeButton("🔍 Инспектор разделов", th.bgElevated, th.textPrimary) {
                        selectedInputFile = primaryFile
                        activeTab = 1
                        buildUserInterface()
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                    })
                    detailBox.addView(quickActRow)
                    grpBox.addView(detailBox)
                }

                queueCard.addView(grpBox)
            }
            if (gameGroups.size > 10) {
                queueCard.addView(TextView(this).apply {
                    text = "... и ещё ${gameGroups.size - 10} игр в пакетной очереди"
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    setPadding(0, dp(4), 0, 0)
                })
            }
        }
        parent.addView(queueCard)

        // ГРУППА 3: Единый лаконичный блок формата и оптимизации (без дублирования настроек!)
        val cfgCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        cfgCard.addView(makeSectionHeader(
            "3. Формат файла и параметры оптимизации",
            "Выберите желаемый выходной контейнер и профиль сжатия без лишних ручных переключателей",
            th
        ))

        // 3.1 Конечный формат выходного файла (задаётся пользователем)
        cfgCard.addView(TextView(this).apply {
            text = "Конечный формат выходного файла:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(2), 0, dp(3))
        })

        val outputFormats = listOf(
            "NSZ — Сжатый цифровой контейнер Switch (Блочный NCZBLOCK, рекомендуется)",
            "XCZ — Сжатый мульти-картридж Switch (Блочный NCZBLOCK, без тикетов)",
            "NSP — Оригинальный цифровой контейнер Switch (PFS0, без сжатия NCA)",
            "XCI — Оригинальный мульти-картридж Switch (HEAD + HFS0, без сжатия NCA)",
            "CCI — Образ картриджа Nintendo 3DS / Citra / Azahar (NCSD + NoCrypto)",
            "3DS — Образ картриджа Nintendo 3DS (NCSD Decrypted + Trim 0xFF)",
            "CIA — Установочный архив Nintendo 3DS (CTR Importable Archive)"
        )
        val formatDropdown = makeStormDropdown(outputFormats, selectedOutputFormat, th) { pos ->
            selectedOutputFormat = pos
            useBlockNcz = (pos == 0 || pos == 1)
            saveSettings()
            buildUserInterface()
        }
        cfgCard.addView(formatDropdown)

        // 3.2 Профиль оптимизации и сжатия (без дублирования!)
        cfgCard.addView(TextView(this).apply {
            text = "Профиль оптимизации и сжатия:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(8), 0, dp(3))
        })

        val optimizationProfiles = listOf(
            "Умный автоматический (Авто-подбор сжатия блоков, дедупликация NCA, Trim 0xFF и моды)",
            "Максимальное сжатие (Ультра Zstandard 22, блок 2 МБ, минимальный размер)",
            "Ручная настройка параметров (Выбор уровня Zstandard и размера блока NCZBLOCK)",
            "Распаковка NSZ / XCZ в оригинальный NSP / XCI",
            "SmartShrink: Только обрезка пустых областей 0xFF (Switch XCI и 3DS)",
            "Полная распаковка игры: Извлечение RomFS + ExeFS + Control + PFS0 в папку",
            "Упаковка папки: Сборка модифицированных RomFS / ExeFS / PFS0 обратно в NSP / NSZ",
            "Создание Homebrew Forwarder (.nro → установочный .nsp для главного экрана Switch)"
        )
        val profileDropdown = makeStormDropdown(optimizationProfiles, selectedOperationMode, th) { pos ->
            selectedOperationMode = pos
            when (pos) {
                0 -> {
                    selectedCompressionMethod = 0
                    zstdLevel = 18
                    blockSizeExpo = 20
                    trimXciBeforeCompress = true
                    smartSizeGuard = true
                    autoEmbedMods = true
                }
                1 -> {
                    selectedCompressionMethod = 2
                    zstdLevel = 22
                    blockSizeExpo = 21
                    trimXciBeforeCompress = true
                    smartSizeGuard = true
                    autoEmbedMods = true
                }
                2 -> {
                    selectedCompressionMethod = 1
                }
            }
            saveSettings()
            buildUserInterface()
        }
        cfgCard.addView(profileDropdown)

        // Ручные настройки Zstandard и размера блока NCZBLOCK показываются ТОЛЬКО если выбран профиль "Ручная настройка параметров" (2)
        if (selectedOperationMode == 2) {
            val manualBox = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                background = makeCardDrawable(th.bgPrimary, th.border, 8)
                setPadding(dp(10), dp(8), dp(10), dp(8))
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    topMargin = dp(8)
                }
            }
            val levelLabel = TextView(this).apply {
                text = "Уровень сжатия Zstandard: $zstdLevel (18 — эталон скорости и сжатия)"
                setTextColor(th.textPrimary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                setPadding(0, 0, 0, dp(2))
            }
            manualBox.addView(levelLabel)
            val levelSeek = SeekBar(this).apply {
                max = 21
                progress = (zstdLevel - 1).coerceIn(0, 21)
                setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                    override fun onProgressChanged(sb: SeekBar?, p: Int, fromUser: Boolean) {
                        zstdLevel = p + 1
                        levelLabel.text = "Уровень сжатия Zstandard: $zstdLevel (18 — эталон скорости и сжатия)"
                        saveSettings()
                    }
                    override fun onStartTrackingTouch(sb: SeekBar?) {}
                    override fun onStopTrackingTouch(sb: SeekBar?) {}
                })
            }
            manualBox.addView(levelSeek)

            val blockRow = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER_VERTICAL
                setPadding(0, dp(6), 0, dp(2))
            }
            blockRow.addView(TextView(this).apply {
                text = "Размер блока NCZBLOCK:"
                setTextColor(th.textPrimary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
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
            manualBox.addView(blockRow)
            cfgCard.addView(manualBox)
        } else {
            // Информационная плашка активной автоматики
            val summaryText = when (selectedOperationMode) {
                0 -> "⚡ Автоматика: Блочный NCZ (1 МБ, Zstd 18) • Умная дедупликация NCA • Авто-вшивание модов • Trim 0xFF"
                1 -> "🔥 Ультра-режим: Блочный NCZ (2 МБ, Zstd 22) • Максимальное уменьшение размера • Дедупликация NCA"
                3 -> "📦 Режим декомпрессии: Восстановление сжатых разделов NCZ в оригинальные NCA (.nsp / .xci)"
                4 -> "✂️ SmartShrink: Хирургическая обрезка пустых областей 0xFF в образах картриджей XCI и 3DS"
                5 -> "📂 Извлечение данных игры: Полная распаковка секций RomFS, ExeFS (main/npdm), Control (NACP/иконка) и PFS0"
                6 -> "📦 Обратная упаковка: Сборка папки с модифицированными RomFS / ExeFS / PFS0 в готовый контейнер NSP / NSZ"
                else -> "🚀 Генератор Форвардеров: Сборка установочного .NSP ярлыка для экрана Switch из любого .NRO или ретро-РОМа"
            }
            cfgCard.addView(TextView(this).apply {
                text = summaryText
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                background = makeCardDrawable(th.bgPrimary, th.border, 8)
                setPadding(dp(10), dp(7), dp(10), dp(7))
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    topMargin = dp(8)
                    bottomMargin = dp(4)
                }
            })
            if (selectedOperationMode == 5) {
                val unpackBox = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgPrimary, th.border, 8)
                    setPadding(dp(10), dp(8), dp(10), dp(8))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(4)
                        bottomMargin = dp(6)
                    }
                }
                unpackBox.addView(TextView(this).apply {
                    text = "Выберите секции для извлечения из NSP / NSZ / XCI / 3DS:"
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
                    typeface = Typeface.DEFAULT_BOLD
                })
                unpackBox.addView(makeStormCheckBox("Извлечь RomFS (игровые ресурсы, тексты перевода, конфиги -> romfs/)", extractRomfsEnabled, th) { extractRomfsEnabled = it })
                unpackBox.addView(makeStormCheckBox("Извлечь ExeFS (исполняемые модули main, main.npdm, rtld, sdk -> exefs/)", extractExefsEnabled, th) { extractExefsEnabled = it })
                unpackBox.addView(makeStormCheckBox("Извлечь Control (паспорт control.nacp + обложка 256×256 JPEG -> control/)", extractControlEnabled, th) { extractControlEnabled = it })
                unpackBox.addView(makeStormCheckBox("Сохранить сырые разделы контейнера (.nca, .tik, .cert -> pfs0/)", extractRawPfs0Enabled, th) { extractRawPfs0Enabled = it })
                cfgCard.addView(unpackBox)
            } else if (selectedOperationMode == 6) {
                cfgCard.addView(makeButton("📦 Открыть мастер упаковки папки (RomFS / ExeFS / PFS0 -> NSP/NSZ)", th.accent) {
                    showPackFolderToNspDialog(th)
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(4)
                        bottomMargin = dp(6)
                    }
                })
            } else if (selectedOperationMode == 7) {
                cfgCard.addView(makeButton("🚀 Открыть генератор Homebrew Forwarder (.nro → .nsp)", th.accent) {
                    activeTab = 2
                    buildUserInterface()
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(4)
                        bottomMargin = dp(6)
                    }
                })
            }
        }

        // Только 2 понятных чекбокса без какого-либо дублирования!
        cfgCard.addView(makeStormCheckBox(
            "Авто-сшивка мульти-контента по играм (База + Патч + DLC + Моды -> один файл на каждую игру)",
            smartMergeDedup,
            th
        ) { checked ->
            smartMergeDedup = checked
            autoEmbedMods = checked
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

        // ГРУППА 3.5: Предварительное переименование внутри (control.nacp / SMDH), смена иконки/обложки (256x256) и имя итогового файла (как в ПК-версии)
        val activeMetaFile = selectedInputFile ?: queuedFiles.firstOrNull()
        val detectedMeta = if (activeMetaFile != null) StormNczEngine.readContainerMetadata(activeMetaFile) else null
        val defaultCleanTitle = detectedMeta?.internalTitle ?: (gameGroups.firstOrNull()?.displayTitle ?: "Switch Game")
        val highestQueueVer = queuedFiles.mapNotNull { StormNczEngine.extractDisplayVersionFromFileName(it.name) }.lastOrNull()
        val defaultVer = highestQueueVer ?: (detectedMeta?.displayVersion ?: "1.0.0")
        val targetExtPreview = when (selectedOutputFormat) {
            0 -> "nsz"
            1 -> "xcz"
            2 -> "nsp"
            3 -> "xci"
            4 -> "cci"
            5 -> "3ds"
            6 -> "cia"
            else -> "nsz"
        }

        val renameCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        renameCard.addView(makeSectionHeader(
            "4. Предварительное переименование внутри (NACP / SMDH), смена иконки и имя файла",
            "Как в ПК-версии: измените внутреннее имя игры в меню приставки/эмулятора (на всех 16 языках), замените обложку 256×256 JPEG, наложите бейдж и задайте точное имя выходного файла",
            th
        ))

        val editorBody = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            visibility = if (preBuildCustomizeEnabled) View.VISIBLE else View.GONE
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }

        val enableRenameCb = makeStormCheckBox(
            "Включить предварительное переименование внутри (control.nacp / SMDH), смену иконки/обложки и имени файла",
            preBuildCustomizeEnabled,
            th
        ) { checked ->
            preBuildCustomizeEnabled = checked
            if (checked) {
                if (customInternalGameTitle.isBlank()) customInternalGameTitle = defaultCleanTitle
                if (customDisplayVer.isBlank()) customDisplayVer = defaultVer
                if (customOutputFileName.isBlank()) customOutputFileName = "$defaultCleanTitle [v$defaultVer].$targetExtPreview"
            }
            buildUserInterface()
        }
        renameCard.addView(enableRenameCb)

        // Ряд превью иконки 256x256 JPEG и кнопок выбора обложки / бейджа
        val iconRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }

        val iconPreviewImg = ImageView(this).apply {
            layoutParams = LinearLayout.LayoutParams(dp(74), dp(74)).apply {
                marginEnd = dp(10)
            }
            scaleType = ImageView.ScaleType.CENTER_CROP
            background = makeCardDrawable(th.bgCard, th.accent, 10)
            setPadding(dp(2), dp(2), dp(2), dp(2))
        }

        fun refreshIconPreview() {
            val previewTitle = customInternalGameTitle.ifBlank { defaultCleanTitle }
            val badgeStr = iconBadgeOptions.getOrElse(selectedIconBadgeIndex) { "" }
            val iconPath = customIconImagePath
            kotlin.concurrent.thread {
                try {
                    val jpegBytes = StormNczEngine.generateSwitchIconJpeg256(iconPath, previewTitle, badgeStr)
                    val bmp = BitmapFactory.decodeByteArray(jpegBytes, 0, jpegBytes.size)
                    if (bmp != null) {
                        mainHandler.post { iconPreviewImg.setImageBitmap(bmp) }
                    }
                } catch (_: Throwable) {}
            }
        }
        refreshIconPreview()
        iconRow.addView(iconPreviewImg)

        val iconRightCol = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        val iconBtnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        iconBtnRow.addView(makeButton("🖼 Сменить иконку / обложку", th.accent) {
            openCustomIconImagePicker { pickedPath ->
                customIconImagePath = pickedPath
                refreshIconPreview()
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })
        if (customIconImagePath.isNotBlank()) {
            iconBtnRow.addView(makeButton("↺ Сброс", th.bgElevated, th.textSecondary) {
                customIconImagePath = ""
                refreshIconPreview()
                buildUserInterface()
            })
        }
        iconRightCol.addView(iconBtnRow)

        iconRightCol.addView(TextView(this).apply {
            text = "Бейдж на иконке 256×256 JPEG:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
            setPadding(0, 0, 0, dp(2))
        })
        iconRightCol.addView(makeStormDropdown(iconBadgeOptions, selectedIconBadgeIndex, th) { pos ->
            selectedIconBadgeIndex = pos
            refreshIconPreview()
        })
        iconRow.addView(iconRightCol)
        editorBody.addView(iconRow)

        // Поле 1: Внутреннее название игры (control.nacp / SMDH)
        editorBody.addView(TextView(this).apply {
            text = "Внутреннее название игры (control.nacp / SMDH во всех 16 языковых слотах):"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
            setPadding(0, dp(2), 0, dp(2))
        })
        val titleInput = EditText(this).apply {
            setText(customInternalGameTitle.ifBlank { defaultCleanTitle })
            hint = "Например: Animal Well [RUS]"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    customInternalGameTitle = s?.toString() ?: ""
                    refreshIconPreview()
                }
            })
        }
        editorBody.addView(titleInput)

        // Поле 2: Издатель и отображаемая версия
        val pubVerRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }
        val pubInput = EditText(this).apply {
            setText(customGamePublisher)
            hint = "Издатель / Автор..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    customGamePublisher = s?.toString() ?: ""
                }
            })
        }
        val verInput = EditText(this).apply {
            setText(customDisplayVer.ifBlank { defaultVer })
            hint = "Версия (1.0.3)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(dp(120), ViewGroup.LayoutParams.WRAP_CONTENT)
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    customDisplayVer = s?.toString() ?: ""
                }
            })
        }
        pubVerRow.addView(pubInput)
        pubVerRow.addView(verInput)
        editorBody.addView(pubVerRow)

        // Поле 3: Имя итогового файла на диске
        editorBody.addView(TextView(this).apply {
            text = "Имя итогового выходного файла (автоматически получит расширение .$targetExtPreview):"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
            setPadding(0, dp(6), 0, dp(2))
        })
        val outFileInput = EditText(this).apply {
            setText(customOutputFileName.ifBlank { "$defaultCleanTitle [v$defaultVer] [MULTI].$targetExtPreview" })
            hint = "Имя итогового файла (напр. Animal Well [RUS].$targetExtPreview)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    customOutputFileName = s?.toString() ?: ""
                }
            })
        }
        editorBody.addView(outFileInput)

        editorBody.visibility = View.GONE
        val toggleEditorBtn = makeButton("🔽 Развернуть редактор NACP, обложки 256×256 и имени файла", th.bgElevated, th.accent) {
            val nowVisible = editorBody.visibility != View.VISIBLE
            editorBody.visibility = if (nowVisible) View.VISIBLE else View.GONE
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        renameCard.addView(toggleEditorBtn)
        renameCard.addView(editorBody)

        // ГРУППА 3: Панель запуска, прогресса и отмены задачи (сразу под очередью игр!)
        val runCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        runCard.addView(makeSectionHeader("5. Выполнение, прогресс и отмена задачи", "Всегда доступное управление очередью", th))

        val progressBar = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(16))
        }
        runCard.addView(progressBar)

        val statusTxt = TextView(this).apply {
            text = "Готов к обработке (Игр в очереди: ${gameGroups.size}, файлов: ${queuedFiles.size})"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(6), 0, dp(8))
        }
        runCard.addView(statusTxt)

        val actionBtnsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        val btnStart = makeButton("🚀 Запустить очередь (Игр: ${gameGroups.size})", th.accent) {
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

        val btnStop = makeButton("⏹ Отменить задачу", Color.parseColor("#DC2626")) {
            cancelCurrentTaskRequested = true
            if (isTaskRunning) {
                statusTxt.text = "⏹ Задача отменена пользователем (остановка конвейера...)"
                Toast.makeText(this, "⏹ Задача отменена пользователем", Toast.LENGTH_SHORT).show()
            } else {
                statusTxt.text = "⏹ Активных задач нет (очередь остановлена)"
                Toast.makeText(this, "⏹ Активных выполняемых задач нет", Toast.LENGTH_SHORT).show()
            }
        }
        actionBtnsRow.addView(btnStop)
        runCard.addView(actionBtnsRow)

        // Размещаем панель запуска сразу после карточки очереди (queueCard)!
        val queueIdx = parent.indexOfChild(queueCard)
        if (queueIdx >= 0) {
            parent.addView(runCard, queueIdx + 1)
        } else {
            parent.addView(runCard)
        }
        parent.addView(renameCard)
    }

    private fun executeQueueBatch(progressBar: ProgressBar, statusTxt: TextView) {
        isTaskRunning = true
        cancelCurrentTaskRequested = false
        val snapshot = queuedFiles.toList()
        customOutputDir.mkdirs()

        thread {
            try {
                val useBlockForFormat = (selectedOutputFormat == 0 || selectedOutputFormat == 1)
                val effectiveLevel = when (selectedOperationMode) {
                    0 -> 18
                    1 -> 22
                    else -> zstdLevel
                }
                val effectiveBlockExpo = when (selectedOperationMode) {
                    0 -> 20
                    1 -> 21
                    else -> blockSizeExpo
                }

                // Если выбран профиль сжатия/сборки (0=Умный авто, 1=Макс. сжатие, 2=Ручной) и включена авто-сшивка по играм:
                // группируем очередь по играм (как на ПК!) и собираем отдельный мульти-контент для каждой игры.
                if (selectedOperationMode in 0..2 && smartMergeDedup) {
                    val gameGroups = StormNczEngine.groupFilesByGameFamily(snapshot)
                    var totalOrigAll = 0L
                    var totalOutAll = 0L
                    var builtGamesCount = 0

                    for ((gIdx, grp) in gameGroups.withIndex()) {
                        if (cancelCurrentTaskRequested) break
                        val prefix = "[Игра ${gIdx + 1}/${gameGroups.size}: ${grp.displayTitle}]"
                        val preparedInputs = mutableListOf<File>()
                        val tempTrimmedFiles = mutableListOf<File>()

                        val cb: (StormNczEngine.NczProgress) -> Unit = { p ->
                            mainHandler.post {
                                progressBar.progress = p.percent
                                statusTxt.text = "$prefix ${p.stageText} (${p.percent}%)"
                            }
                        }

                        for (qItem in grp.items) {
                            val inFile = qItem.file
                            if (!inFile.exists()) continue
                            if (inFile.extension.equals("xci", ignoreCase = true)) {
                                val targetOutDir = getEffectiveOutputDirForFile(inFile)
                                val trimmedCandidate = File(targetOutDir, "${inFile.nameWithoutExtension}.trimmed_tmp.xci")
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

                        if (preparedInputs.isEmpty()) continue

                        val preCustom = StormNczEngine.PreBuildCustomization(
                            enabled = preBuildCustomizeEnabled,
                            customInternalTitle = customInternalGameTitle,
                            customPublisher = customGamePublisher,
                            customDisplayVersion = customDisplayVer,
                            customOutputFileName = if (gameGroups.size == 1) customOutputFileName else "",
                            customIconPath = customIconImagePath,
                            iconBadgeText = if (selectedIconBadgeIndex > 0) iconBadgeOptions.getOrElse(selectedIconBadgeIndex) { "" } else ""
                        )
                        val effectiveMultiOutDir = preparedInputs.firstOrNull()?.let { getEffectiveOutputDirForFile(it) } ?: customOutputDir
                        val res = StormNczEngine.buildSmartMultiContentPackage(
                            inputFiles = preparedInputs,
                            outputDir = effectiveMultiOutDir,
                            targetFormatMode = selectedOutputFormat,
                            useBlockNcz = useBlockForFormat,
                            smartDedup = true,
                            embedMods = true,
                            zstdLevel = effectiveLevel,
                            blockSizeExponent = effectiveBlockExpo,
                            threads = cpuThreads,
                            smartSizeGuard = true,
                            preBuildCustomization = preCustom,
                            onProgress = cb
                        )

                        tempTrimmedFiles.forEach { it.delete() }

                        if (res.success) {
                            builtGamesCount++
                            totalOrigAll += res.originalBytes
                            totalOutAll += res.compressedBytes
                            val outFile = File(res.outputPath)
                            appendLog("✅ $prefix -> ${outFile.name} (${StormNczEngine.formatSize(res.compressedBytes)})")
                            if (deleteSourceAfterSuccess && outFile.exists() && outFile.length() > 0) {
                                for (qItem in grp.items) {
                                    if (qItem.file.isFile && qItem.file.canonicalPath != outFile.canonicalPath) {
                                        qItem.file.delete()
                                    }
                                }
                            }
                        }
                    }

                    val savedAll = (totalOrigAll - totalOutAll).coerceAtLeast(0L)
                    mainHandler.post {
                        progressBar.progress = 100
                        statusTxt.text = "✅ Готово! Собрано игр: $builtGamesCount/${gameGroups.size} • Экономия: ${StormNczEngine.formatSize(savedAll)}"
                    }
                } else {
                    // Пофайловая обработка (или распаковка NSZ/XCZ [3], или SmartShrink 0xFF [4])
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

                        val res = when (selectedOperationMode) {
                            3 -> {
                                val outExt = if (ext == "xcz" || ext == "xci") "xci" else "nsp"
                                val outFile = File(getEffectiveOutputDirForFile(inFile), "${baseName}_unpacked.$outExt")
                                StormNczEngine.decompressNszOrXczToNsp(inFile, outFile, cb)
                            }
                            4 -> {
                                val outFile = File(getEffectiveOutputDirForFile(inFile), "${baseName}_trimmed.$ext")
                                StormNczEngine.smartShrinkRom(inFile, outFile, cb)
                            }
                            5 -> {
                                val rep = StormNczEngine.extractGameRomfsAndExefs(
                                    inputFile = inFile,
                                    targetBaseDir = getEffectiveOutputDirForFile(inFile),
                                    extractRomfs = extractRomfsEnabled,
                                    extractExefs = extractExefsEnabled,
                                    extractControl = extractControlEnabled,
                                    extractRawPartitions = extractRawPfs0Enabled,
                                    onProgress = cb
                                )
                                StormNczEngine.CompressionResult(
                                    success = rep.success,
                                    inputPath = inFile.absolutePath,
                                    outputPath = rep.outputDir.absolutePath,
                                    originalBytes = inFile.length(),
                                    compressedBytes = rep.totalExtractedBytes,
                                    elapsedMs = 100L,
                                    compressedNcaCount = 1,
                                    totalFilesCount = rep.extractedFilesCount,
                                    blockSizeExponent = 20,
                                    zstdLevel = 18,
                                    message = "Распаковано ${rep.extractedFilesCount} файлов (RomFS + ExeFS)"
                                )
                            }
                            6 -> {
                                val targetFolder = if (inFile.isDirectory) inFile else inFile.parentFile ?: customOutputDir
                                StormNczEngine.packFolderToSwitchContainer(
                                    inputFolder = targetFolder,
                                    outputDir = customOutputDir,
                                    targetFormatMode = selectedOutputFormat,
                                    useBlockNcz = useBlockForFormat,
                                    zstdLevel = effectiveLevel,
                                    blockSizeExponent = effectiveBlockExpo,
                                    onProgress = cb
                                )
                            }
                            7 -> {
                                val fwdOut = StormNczEngine.buildHomebrewForwarderNsp(
                                    appTitle = customInternalGameTitle.ifBlank { baseName },
                                    appAuthor = customGamePublisher.ifBlank { "Nintendo" },
                                    appVersion = customDisplayVer.ifBlank { "1.0.0" },
                                    titleIdHex = fwdTitleId,
                                    sdmcNroPath = if (ext == "nro") "sdmc:/switch/${inFile.name}" else fwdSdmcNroPath,
                                    localNroFile = if (ext == "nro") inFile else null,
                                    customIconPath = customIconImagePath,
                                    outputDir = customOutputDir,
                                    onProgress = cb
                                )
                                StormNczEngine.CompressionResult(
                                    success = fwdOut.exists(),
                                    inputPath = inFile.absolutePath,
                                    outputPath = fwdOut.absolutePath,
                                    originalBytes = inFile.length(),
                                    compressedBytes = fwdOut.length(),
                                    elapsedMs = 100L,
                                    compressedNcaCount = 1,
                                    totalFilesCount = 3,
                                    blockSizeExponent = 20,
                                    zstdLevel = 18,
                                    message = "Создан Forwarder NSP: ${fwdOut.name}"
                                )
                            }
                            else -> {
                                val preCustomSingle = StormNczEngine.PreBuildCustomization(
                                    enabled = preBuildCustomizeEnabled,
                                    customInternalTitle = customInternalGameTitle,
                                    customPublisher = customGamePublisher,
                                    customDisplayVersion = customDisplayVer,
                                    customOutputFileName = if (snapshot.size == 1) customOutputFileName else "",
                                    customIconPath = customIconImagePath,
                                    iconBadgeText = if (selectedIconBadgeIndex > 0) iconBadgeOptions.getOrElse(selectedIconBadgeIndex) { "" } else ""
                                )
                                StormNczEngine.buildSmartMultiContentPackage(
                                    inputFiles = listOf(inFile),
                                    outputDir = customOutputDir,
                                    targetFormatMode = selectedOutputFormat,
                                    useBlockNcz = useBlockForFormat,
                                    smartDedup = true,
                                    embedMods = autoEmbedMods,
                                    zstdLevel = effectiveLevel,
                                    blockSizeExponent = effectiveBlockExpo,
                                    threads = cpuThreads,
                                    smartSizeGuard = true,
                                    preBuildCustomization = preCustomSingle,
                                    onProgress = cb
                                )
                            }
                        }

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
        // ГРУППА 1: Инспектор структуры, RomFS / ExeFS и выборочное извлечение файлов
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
            "1. Инспектор контейнера и проводник RomFS / ExeFS",
            if (activeFile != null) "Активный образ: ${activeFile.name} (${StormNczEngine.formatSize(activeFile.length())})"
            else "Выберите игровой образ (.nsp / .nsz / .xci / .xcz / .3ds / .cci / .cia) для инспекции и работы с RomFS",
            th
        ))

        // Ряд 1 (2 кнопки единой высоты без обрезания текста)
        val inspBtnRow1 = makeUniformButtonRow(bottomMarginDp = 6)
        inspBtnRow1.addView(makeButton("📂 Выбрать образ / файл", th.bgElevated, th.textPrimary) {
            showMultiSelectFileAndFolderDialog()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })
        inspBtnRow1.addView(makeButton("🛡️ Проверить SHA-256", th.bgElevated, th.accent) {
            val target = activeFile ?: queuedFiles.firstOrNull()
            if (target == null || !target.exists()) {
                Toast.makeText(this, "Сначала выберите файл для криптографической проверки SHA-256!", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this, "⏳ Проверка целостности и SHA-256 ${target.name}...", Toast.LENGTH_SHORT).show()
                thread {
                    val rep = StormNczEngine.verifyContainerIntegrity(target)
                    val summary = if (rep.valid) {
                        "✅ Целостность подтверждена: ${target.name} (${rep.containerType}, разделов: ${rep.totalEntries}, NCA: ${rep.verifiedNcaCount}, NCZ: ${rep.verifiedNczCount}, SHA-256: ${rep.sha256Hex.take(16)}...)"
                    } else {
                        "❌ Обнаружены ошибки структуры в ${target.name}"
                    }
                    appendLog(summary)
                    mainHandler.post {
                        Toast.makeText(this, summary, Toast.LENGTH_LONG).show()
                        buildUserInterface()
                    }
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        inspCard.addView(inspBtnRow1)

        // Ряд 2 (2 кнопки единой высоты: Проводник RomFS / HEX и Извлечение RomFS / ExeFS)
        val romfsActionRow1 = makeUniformButtonRow(bottomMarginDp = 6)
        romfsActionRow1.addView(makeButton("🌲 Проводник RomFS / HEX", th.accent) {
            val target = activeFile ?: queuedFiles.firstOrNull()
            if (target == null || !target.exists()) {
                Toast.makeText(this, "Сначала выберите файл (.nsp/.nsz/.xci/.3ds) для открытия в проводнике RomFS!", Toast.LENGTH_SHORT).show()
            } else {
                showVirtualRomfsExplorerDialog(target, th)
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        romfsActionRow1.addView(makeButton("📂 Извлечь RomFS и ExeFS", th.bgElevated, th.accent) {
            val target = activeFile ?: queuedFiles.firstOrNull()
            if (target == null || !target.exists()) {
                Toast.makeText(this, "Выберите игровой файл (.nsp/.nsz/.xci/.3ds) для извлечения RomFS и ExeFS!", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this, "⏳ Извлечение RomFS / ExeFS + Control из ${target.name}...", Toast.LENGTH_SHORT).show()
                thread {
                    try {
                        val rep = StormNczEngine.extractGameRomfsAndExefs(
                            inputFile = target,
                            targetBaseDir = extractedDir,
                            extractRomfs = true,
                            extractExefs = true,
                            extractControl = true,
                            extractRawPartitions = true
                        ) {}
                        appendLog("✅ Извлечено RomFS / ExeFS (${rep.extractedFilesCount} файлов, ${StormNczEngine.formatSize(rep.totalExtractedBytes)}) в ${rep.outputDir.absolutePath}")
                        mainHandler.post {
                            showUnpackSummaryDialog(target.name, rep, th)
                        }
                    } catch (e: Exception) {
                        appendLog("❌ Ошибка извлечения RomFS/ExeFS: ${e.message}")
                    }
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        inspCard.addView(romfsActionRow1)

        // Ряд 3 (2 кнопки единой высоты: Упаковка папки в NSP/NSZ и Извлечение всех разделов NCA)
        val romfsActionRow2 = makeUniformButtonRow(bottomMarginDp = 8)
        romfsActionRow2.addView(makeButton("📦 Упаковать папку в NSP/NSZ", th.bgElevated, th.textPrimary) {
            showPackFolderToNspDialog(th)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        romfsActionRow2.addView(makeButton("📤 Извлечь разделы NCA/NCZ", th.bgElevated, th.textPrimary) {
            val target = activeFile ?: queuedFiles.firstOrNull()
            if (target == null || !target.exists()) {
                Toast.makeText(this, "Сначала выберите образ для извлечения внутренних разделов!", Toast.LENGTH_SHORT).show()
            } else {
                val targetOut = File(extractedDir, target.nameWithoutExtension)
                thread {
                    try {
                        val extracted = StormNczEngine.extractContainerFiles(target, targetOut, null) {}
                        appendLog("✅ Извлечено ${extracted.size} внутренних файлов в ${targetOut.absolutePath}")
                        mainHandler.post {
                            Toast.makeText(this, "Извлечено ${extracted.size} файлов в ${targetOut.name}!", Toast.LENGTH_LONG).show()
                        }
                    } catch (e: Exception) {
                        appendLog("❌ Ошибка извлечения: ${e.message}")
                    }
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        inspCard.addView(romfsActionRow2)

        if (activeFile != null && activeFile.exists()) {
            val entries = StormNczEngine.inspectContainer(activeFile)
            val ncaCount = entries.count { it.name.endsWith(".nca", true) }
            val nczCount = entries.count { it.name.endsWith(".ncz", true) }
            val tikCount = entries.count { it.name.endsWith(".tik", true) }

            val summaryBox = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                background = makeCardDrawable(th.bgPrimary, th.border, 8)
                setPadding(dp(12), dp(10), dp(12), dp(10))
            }
            summaryBox.addView(TextView(this).apply {
                text = "📊 Паспорт образа: ${activeFile.name}\n" +
                    "• Размер: ${StormNczEngine.formatSize(activeFile.length())}  |  Разделов: ${entries.size} (NCA: $ncaCount, NCZ: $nczCount, Ticket: $tikCount)"
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
                typeface = Typeface.DEFAULT_BOLD
                setPadding(0, 0, 0, dp(6))
            })

            val sb = StringBuilder()
            entries.forEachIndexed { idx, e ->
                sb.appendLine("${idx + 1}. ${e.name}")
                sb.appendLine("   Размер: ${StormNczEngine.formatSize(e.size)}  •  Смещение: 0x${e.offset.coerceAtLeast(0L).toString(16).uppercase()}")
            }
            summaryBox.addView(TextView(this).apply {
                text = sb.toString().trimEnd()
                setTextColor(th.textPrimary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                typeface = Typeface.MONOSPACE
            })
            inspCard.addView(summaryBox)
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
            "Поиск необрезанных картриджей XCI/3DS (с пустыми 0xFF), несжатых NSP и дубликатов TitleID",
            th
        ))

        val auditOutTxt = TextView(this).apply {
            text = "Нажмите «Провести аудит библиотеки», чтобы проанализировать очередь и папку roms."
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
            setPadding(0, dp(6), 0, dp(8))
        }

        val auditBtnRow = makeUniformButtonRow(bottomMarginDp = 4)
        auditBtnRow.addView(makeButton("🔎 Провести аудит библиотеки", th.accent) {
            thread {
                val scanTargets = mutableListOf<File>()
                scanTargets.addAll(queuedFiles)
                scanTargets.add(romsDir)
                scanTargets.add(customOutputDir)
                scanTargets.add(customSwitch2OutputDir)
                scanTargets.add(custom3dsOutputDir)
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
    // ВКЛАДКА 2: RUTRACKER (ТОЛЬКО РАЗДЕЛ SWITCH) И АВТО-СБОРКА С МОДАМИ
    // =========================================================================

    private fun buildRuTrackerAndModsTab(parent: LinearLayout, th: StormTheme) {
        RuTrackerCloudEngine.appContext = applicationContext
        RuTrackerCloudEngine.cacheRootDir = filesDir
        RuTrackerCloudEngine.warmupSwitchForumCacheAsync()

        val rtCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        rtCard.addView(makeSectionHeader(
            "🌐 Динамический поиск игр Nintendo Switch (RuTracker #1605) с авто-загрузкой",
            "Мгновенный живой фильтр при вводе текста. Нажмите «⬇️ Скачать и собрать» или «🧲 Magnet» для примагничивания и сборки Мульти-контента, либо «🌐 Сайт» для открытия раздачи с авто-обходом проверки безопасности!",
            th
        ))

        val searchInputRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        val queryEdit = EditText(this).apply {
            hint = "Введите любую из 11 380+ игр (напр. Brotato, Бротато, Animal Well, Zelda)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12.5f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                marginEnd = dp(6)
            }
        }
        searchInputRow.addView(queryEdit)

        val clearQueryBtn = makeButton("✕", th.bgElevated, th.textSecondary) {
            queryEdit.setText("")
        }
        searchInputRow.addView(clearQueryBtn)
        rtCard.addView(searchInputRow)

        val rtDeleteSourceCheck = makeStormCheckBox(
            "Удалять исходные файлы после успешной проверки выходного контейнера",
            deleteRuTrackerSourceAfterSuccess,
            th
        ) { checked ->
            deleteRuTrackerSourceAfterSuccess = checked
            saveSettings()
        }
        rtCard.addView(rtDeleteSourceCheck)

        // Панель анимации динамического поиска
        val animSearchRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            visibility = View.GONE
            background = makeCardDrawable(th.bgPrimary, th.accent, 8)
            setPadding(dp(10), dp(6), dp(10), dp(6))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }
        val animSpinner = ProgressBar(this).apply {
            isIndeterminate = true
            layoutParams = LinearLayout.LayoutParams(dp(18), dp(18)).apply {
                marginEnd = dp(8)
            }
        }
        val animStatusText = TextView(this).apply {
            text = "🔎 Динамический поиск в разделе Nintendo Switch (#1605)..."
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            typeface = Typeface.DEFAULT_BOLD
        }
        animSearchRow.addView(animSpinner)
        animSearchRow.addView(animStatusText)
        rtCard.addView(animSearchRow)

        val tagsScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setPadding(0, dp(6), 0, dp(6))
        }
        val tagsRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val resultsLayout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        var searchSeq = 0
        var debounceRunnable: Runnable? = null
        var suppressWatcher = false

        fun renderTopicList(found: List<RuTrackerCloudEngine.TopicItem>, q: String, isLiveSearching: Boolean) {
            resultsLayout.removeAllViews()
            if (found.isEmpty() && !isLiveSearching) {
                resultsLayout.addView(TextView(this).apply {
                    text = "Раздачи Nintendo Switch по запросу «$q» не найдены. Проверьте название или введите часть имени игры."
                    setTextColor(th.textSecondary)
                    setPadding(0, dp(6), 0, dp(6))
                })
                return
            }

            val correctionHint = RuTrackerCloudEngine.explainQueryCorrection(q, found)
            if (correctionHint != null) {
                resultsLayout.addView(TextView(this).apply {
                    text = correctionHint
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
                    typeface = Typeface.DEFAULT_BOLD
                    background = makeCardDrawable(th.bgPrimary, th.accent, 8)
                    setPadding(dp(10), dp(7), dp(10), dp(7))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(6)
                    }
                })
            }

            if (q.isNotBlank() && found.isNotEmpty()) {
                resultsLayout.addView(TextView(this).apply {
                    text = "📊 Найдено совпадений в каталоге Switch (11 380+ игр и раздач): ${found.size}"
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                    setPadding(dp(2), dp(6), dp(2), dp(2))
                })
            }

            found.take(60).forEach { item ->
                val itemBox = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgPrimary, th.border, 8)
                    setPadding(dp(12), dp(10), dp(12), dp(10))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(6)
                    }
                }
                val cleanName = StormNczEngine.extractCleanGameTitle(item.title).ifBlank { item.title }
                val resolvedTid = RuTrackerCloudEngine.resolveAuthenticBaseTitleId(item.title, item.infoHash, item.topicId)
                itemBox.addView(TextView(this).apply {
                    text = cleanName
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 13.5f)
                    typeface = Typeface.DEFAULT_BOLD
                })
                itemBox.addView(TextView(this).apply {
                    text = item.title
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                    setPadding(0, dp(2), 0, dp(4))
                })
                itemBox.addView(TextView(this).apply {
                    text = "📦 Размер: ${StormNczEngine.localizeSizeText(item.sizeText)}  •  🌱 Сиды: ${if (item.seeds > 0) "▲${item.seeds}" else "нет данных"}  •  🆔 TitleID: $resolvedTid"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, 0, dp(8))
                })

                val btnRow = makeUniformButtonRow(bottomMarginDp = 0)
                btnRow.addView(makeButton("⬇️ Скачать и собрать", th.accent) {
                    showAutomatedTorrentAndMultiBuildDialog(item, th, autoStartMagnet = false)
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.35f).apply {
                        marginEnd = dp(6)
                    }
                })

                if (item.magnetUri.isNotEmpty() || item.infoHash.isNotEmpty()) {
                    btnRow.addView(makeButton("🧲 Magnet", th.bgElevated, th.accent) {
                        showAutomatedTorrentAndMultiBuildDialog(item, th, autoStartMagnet = true)
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 0.9f).apply {
                            marginEnd = dp(6)
                        }
                    })
                }

                if (item.topicUrl.isNotEmpty()) {
                    btnRow.addView(makeButton("🌐 Сайт", th.bgElevated, th.textPrimary) {
                        showRuTrackerSiteViewerDialog(item, th)
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 0.75f)
                    })
                }
                itemBox.addView(btnRow)
                resultsLayout.addView(itemBox)
            }
        }

        fun runSearch(q: String, immediateOnly: Boolean = false) {
            val mySeq = ++searchSeq
            val cleanQ = q.trim()
            val instantList = RuTrackerCloudEngine.searchTopicsInstant(cleanQ)
            val needLiveNetwork = !immediateOnly && cleanQ.isNotEmpty()

            renderTopicList(instantList, cleanQ, needLiveNetwork)

            if (!needLiveNetwork && cleanQ.isEmpty()) {
                animSearchRow.visibility = View.GONE
                return
            }
            if (immediateOnly) return

            animSearchRow.visibility = View.VISIBLE
            animStatusText.text = if (cleanQ.isEmpty()) {
                "🔎 Обновление каталога Nintendo Switch (#1605)..."
            } else {
                "🔎 Динамический поиск «$cleanQ» в разделе Switch (#1605)..."
            }

            thread {
                val found = RuTrackerCloudEngine.searchTopics(cleanQ)
                mainHandler.post {
                    if (mySeq != searchSeq) return@post
                    animSearchRow.visibility = View.GONE
                    renderTopicList(found, cleanQ, false)
                }
            }
        }

        queryEdit.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                if (suppressWatcher) return
                val q = s?.toString() ?: ""
                // 1. Мгновенная локальная фильтрация за 0 мс прямо во время набора
                runSearch(q, immediateOnly = true)
                // 2. Динамический сетевой дозапрос через 220 мс после паузы в наборе
                debounceRunnable?.let { mainHandler.removeCallbacks(it) }
                val r = Runnable { runSearch(q, immediateOnly = false) }
                debounceRunnable = r
                mainHandler.postDelayed(r, 220L)
            }
        })

        listOf("Все хиты Switch", "Brotato", "Animal Well", "Balatro", "Zelda", "Mario", "Metroid", "Witcher", "Hades", "Hollow Knight").forEach { tag ->
            val b = makeButton(tag, th.bgElevated, th.textPrimary) {
                val q = if (tag == "Все хиты Switch") "" else tag
                suppressWatcher = true
                queryEdit.setText(q)
                queryEdit.setSelection(q.length)
                suppressWatcher = false
                runSearch(q, immediateOnly = false)
            }
            b.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
            tagsRow.addView(b)
        }
        tagsScroll.addView(tagsRow)
        rtCard.addView(tagsScroll)

        rtCard.addView(makeButton("🔍 Найти раздачи в разделе Nintendo Switch", th.accent) {
            runSearch(queryEdit.text.toString(), immediateOnly = false)
        })
        rtCard.addView(resultsLayout)
        parent.addView(rtCard)

        buildHomebrewForwarderCard(parent, th) { homebrewQuery: String ->
            suppressWatcher = true
            queryEdit.setText(homebrewQuery)
            queryEdit.setSelection(homebrewQuery.length)
            suppressWatcher = false
            runSearch(homebrewQuery, immediateOnly = false)
        }

        if (pendingRuTrackerSearchQuery.isNotBlank()) {
            val initQ = pendingRuTrackerSearchQuery
            pendingRuTrackerSearchQuery = ""
            suppressWatcher = true
            queryEdit.setText(initQ)
            queryEdit.setSelection(initQ.length)
            suppressWatcher = false
            runSearch(initQ, immediateOnly = false)
        } else {
            runSearch("", immediateOnly = true)
        }
    }

    /**
     * Встроенное окно просмотра страницы раздачи «🌐 Сайт» с автоматическим прохождением
     * проверки безопасности Cloudflare через шлюз RuTracker #1605.
     */
    private fun showRuTrackerSiteViewerDialog(item: RuTrackerCloudEngine.TopicItem, th: StormTheme) {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }

        card.addView(TextView(this).apply {
            text = "🌐 Страница раздачи RuTracker (#1605)"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val bypassBadge = TextView(this).apply {
            text = "⏳ Автоматическое прохождение проверки безопасности Cloudflare..."
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(6), dp(10), dp(6))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
                bottomMargin = dp(8)
            }
        }
        card.addView(bypassBadge)

        val bodyScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(340))
        }
        val bodyContent = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        bodyScroll.addView(bodyContent)
        card.addView(bodyScroll)

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )

        btnRow.addView(makeButton("Закрыть", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        btnRow.addView(makeButton("🧲 Magnet в сборщик", th.bgElevated, th.accent) {
            dlg.dismiss()
            showAutomatedTorrentAndMultiBuildDialog(item, th, autoStartMagnet = true)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        btnRow.addView(makeButton("🚀 Скачать и собрать", th.accent) {
            dlg.dismiss()
            showAutomatedTorrentAndMultiBuildDialog(item, th, autoStartMagnet = false)
        })
        card.addView(btnRow)

        thread {
            val details = RuTrackerCloudEngine.fetchRuTrackerTopicDetailsWithBypass(item)
            mainHandler.post {
                bypassBadge.text = details.cloudflareBypassStatus
                bodyContent.removeAllViews()

                bodyContent.addView(TextView(this).apply {
                    text = details.title
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 13.5f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, 0, dp(4))
                })

                bodyContent.addView(TextView(this).apply {
                    text = "${details.forumSection}\nТема #${details.topicId} • Размер: ${details.totalSizeText} • Сиды: ${if (details.seeds > 0) "▲${details.seeds}" else "нет данных"} • Личи: ${if (details.seeds > 0) "▼${details.leechers}" else "—"}\nBTIH: ${details.infoHash}"
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                    setPadding(0, 0, 0, dp(8))
                })

                val infoBox = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgPrimary, th.border, 8)
                    setPadding(dp(10), dp(8), dp(10), dp(8))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(8)
                    }
                }
                details.descriptionLines.forEach { line ->
                    infoBox.addView(TextView(this).apply {
                        text = line
                        setTextColor(th.textPrimary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                        setPadding(0, dp(2), 0, dp(2))
                    })
                }
                bodyContent.addView(infoBox)

                bodyContent.addView(TextView(this).apply {
                    text = "📂 Файлы в составе раздачи (${details.files.size} шт.):"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, dp(2), 0, dp(4))
                })

                details.files.forEach { rf ->
                    bodyContent.addView(TextView(this).apply {
                        text = "📄 [${rf.roleBadge}] ${rf.fileName} — ${StormNczEngine.formatSize(rf.sizeBytes)}"
                        setTextColor(th.textPrimary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                        background = makeCardDrawable(th.bgPrimary, th.border, 6)
                        setPadding(dp(8), dp(6), dp(8), dp(6))
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            bottomMargin = dp(4)
                        }
                    })
                }
            }
        }
    }

    /**
     * Автоматическое модальное окно скачивания релиза с RuTracker (или по Magnet),
     * выбора файлов раздачи (БАЗА / ПАТЧ / DLC), подключения облачных русификаторов / модов
     * и поэтапной сборки готового Мульти-контента с живым логом процесса.
     */
    private fun showAutomatedTorrentAndMultiBuildDialog(
        item: RuTrackerCloudEngine.TopicItem,
        th: StormTheme,
        autoStartMagnet: Boolean = false
    ) {
        var dialogFormatMode = selectedOutputFormat
        var embedCloudModsChecked = false
        var isRunningPipeline = false
        var cancelPipeline = false

        val releaseFiles = RuTrackerCloudEngine.getReleaseFilesList(item)

        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        // Верхний заголовок окна
        card.addView(TextView(this).apply {
            text = if (autoStartMagnet) "🧲 Примагничивание и сборка Мульти-контента" else "⬇️ Загрузка раздачи RuTracker и сборка Мульти-контента"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 14.5f)
            typeface = Typeface.DEFAULT_BOLD
        })

        // Компактная информационная шапка раздачи (всегда видна над прокруткой)
        val topicMetaBanner = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(6), dp(10), dp(6))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
                bottomMargin = dp(6)
            }
        }
        topicMetaBanner.addView(TextView(this).apply {
            text = item.title
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            typeface = Typeface.DEFAULT_BOLD
            maxLines = 2
        })
        topicMetaBanner.addView(TextView(this).apply {
            val topicInfo = when {
                item.topicId.startsWith("TDB-") -> "Каталог Nintendo Switch"
                item.topicId.all { it.isDigit() } -> "Тема RuTracker: t=${item.topicId}"
                else -> "Источник: ${item.topicId}"
            }
            text = "$topicInfo • Размер: ${StormNczEngine.localizeSizeText(item.sizeText)} • Сиды: ${if (item.seeds > 0) "▲${item.seeds}" else "нет данных"} • Папка: ${customOutputDir.name}/"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.6f)
            setPadding(0, dp(2), 0, 0)
        })
        card.addView(topicMetaBanner)

        // Прокручиваемая область с 4 чёткими сгруппированными блоками
        val scroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(295)).apply {
                bottomMargin = dp(6)
            }
        }
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        scroll.addView(root)
        card.addView(scroll)

        var syncUnlockerAutoState: () -> Unit = {}

        // =========================================================================
        // ГРУППА 1: 📦 СОСТАВ РАЗДАЧИ (ФАЙЛЫ ТОРРЕНТА)
        // =========================================================================
        val filesCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        val filesSummaryTitle = TextView(this).apply {
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
            typeface = Typeface.DEFAULT_BOLD
            setPadding(0, 0, 0, dp(4))
        }
        filesCard.addView(filesSummaryTitle)

        fun updateFilesSummaryHeader() {
            val sel = releaseFiles.filter { it.selected }
            val sumBytes = sel.sumOf { it.sizeBytes }
            filesSummaryTitle.text = "1. 📦 Состав раздачи (выбрано: ${sel.size} из ${releaseFiles.size} • ${StormNczEngine.formatSize(sumBytes)})"
        }
        updateFilesSummaryHeader()

        releaseFiles.forEach { rf ->
            val cbRow = makeStormCheckBox(
                "[${rf.roleBadge}] ${rf.fileName} — ${StormNczEngine.formatSize(rf.sizeBytes)}",
                rf.selected,
                th
            ) { checked ->
                rf.selected = checked
                updateFilesSummaryHeader()
                syncUnlockerAutoState()
            }
            filesCard.addView(cbRow)
        }
        root.addView(filesCard)

        // =========================================================================
        // ГРУППА 2: 💬 СООБЩЕНИЯ С ФАЙЛАМИ В ТЕМЕ ИГРЫ (ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНЫ)
        // =========================================================================
        val discoveredDiscussionLinks = mutableListOf<RuTrackerCloudEngine.TopicDiscussionLink>()
        val discussionCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgPrimary, th.accent, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        val discHeaderRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        val discussionTitleTv = TextView(this).apply {
            text = "2. 💬 Сообщения с файлами в теме игры (по умолчанию выключены)"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.6f)
            typeface = Typeface.DEFAULT_BOLD
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        discHeaderRow.addView(discussionTitleTv)

        val discussionSubTv = TextView(this).apply {
            text = "⏳ Сканирование сообщений темы RuTracker (шапка и комментарии)..."
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.6f)
            setPadding(0, dp(2), 0, dp(4))
        }
        val discussionListLayout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }

        fun updateDiscussionSummary() {
            val selCount = discoveredDiscussionLinks.count { it.selected }
            discussionSubTv.text = "Найдено ссылок в теме: ${discoveredDiscussionLinks.size} (включено пользователем: $selCount) • По умолчанию все выключены:"
        }

        fun reloadDiscussionLinks() {
            discussionSubTv.text = "⏳ Поиск ссылок на патчи, DLC и русификаторы в сообщениях темы t=${item.topicId}..."
            discussionListLayout.removeAllViews()
            thread {
                val links = RuTrackerCloudEngine.extractAllTopicAndDiscussionLinks(item)
                mainHandler.post {
                    discoveredDiscussionLinks.clear()
                    discoveredDiscussionLinks.addAll(links)
                    discussionListLayout.removeAllViews()
                    if (links.isEmpty()) {
                        discussionSubTv.text = "В сообщениях темы внешних облачных ссылок не обнаружено (используются только файлы раздачи)."
                    } else {
                        // СТРОГО: по умолчанию ни одна внешняя ссылка не включается автоматически!
                        discoveredDiscussionLinks.forEach { it.selected = false }
                        updateDiscussionSummary()
                        discoveredDiscussionLinks.forEach { dLink ->
                            val itemBox = LinearLayout(this).apply {
                                orientation = LinearLayout.VERTICAL
                                background = makeCardDrawable(th.bgCard, th.border, 6)
                                setPadding(dp(8), dp(6), dp(8), dp(6))
                                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                                    topMargin = dp(4)
                                }
                            }
                            val cb = makeStormCheckBox(
                                "[${dLink.roleCategory}] ${dLink.title} (${dLink.provider})",
                                false,
                                th
                            ) { checked ->
                                dLink.selected = checked
                                updateDiscussionSummary()
                                syncUnlockerAutoState()
                            }
                            itemBox.addView(cb)
                            itemBox.addView(TextView(this).apply {
                                text = "📍 ${dLink.sourceLocation} • 📅 ${dLink.publishedDate} • ☁️ ${dLink.provider}"
                                setTextColor(th.textSecondary)
                                setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.2f)
                                setPadding(dp(26), 0, 0, dp(2))
                            })
                            itemBox.addView(TextView(this).apply {
                                text = "🔗 ${dLink.url}"
                                setTextColor(th.accent)
                                setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.2f)
                                typeface = Typeface.MONOSPACE
                                setPadding(dp(26), 0, 0, 0)
                            })
                            discussionListLayout.addView(itemBox)
                        }
                    }
                    syncUnlockerAutoState()
                }
            }
        }

        discHeaderRow.addView(makeButton("🔄 Обновить", th.bgElevated, th.accent) {
            reloadDiscussionLinks()
        }.apply {
            setPadding(dp(8), dp(4), dp(8), dp(4))
        })
        discussionCard.addView(discHeaderRow)
        discussionCard.addView(discussionSubTv)
        discussionCard.addView(discussionListLayout)
        root.addView(discussionCard)
        reloadDiscussionLinks()

        // =========================================================================
        // ГРУППА 3: ⚙️ ФОРМАТ КОНТЕЙНЕРА И ОПЦИИ СБОРКИ
        // =========================================================================
        val formatAndOptionsCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        formatAndOptionsCard.addView(TextView(this).apply {
            text = "3. ⚙️ Формат выходного контейнера и опции сборки"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.6f)
            typeface = Typeface.DEFAULT_BOLD
            setPadding(0, 0, 0, dp(4))
        })

        val formatNames = listOf(
            "NSZ — Сжатый цифровой контейнер Switch (Блочный NCZBLOCK)",
            "XCZ — Сжатый мульти-картридж Switch (Блочный NCZBLOCK)",
            "NSP — Оригинальный цифровой контейнер Switch (Без сжатия)",
            "XCI — Оригинальный мульти-картридж Switch (Без сжатия)",
            "CCI — Образ картриджа Nintendo 3DS (NCSD + NoCrypto)",
            "3DS — Образ картриджа Nintendo 3DS (Decrypted + Trim)",
            "CIA — Установочный архив Nintendo 3DS"
        )

        val cleanDefaultTitle = StormNczEngine.extractCleanGameTitle(item.title)
        val verMatch = Regex("\\b(v\\d+(?:\\.\\d+)+)\\b", RegexOption.IGNORE_CASE).find(item.title)
        val defaultVerStr = verMatch?.groupValues?.get(1)?.removePrefix("v")?.removePrefix("V") ?: "1.0.1"
        fun extForMode(m: Int): String = when (m) {
            0 -> "nsz"
            1 -> "xcz"
            2 -> "nsp"
            3 -> "xci"
            4 -> "cci"
            5 -> "3ds"
            6 -> "cia"
            else -> "nsz"
        }

        var dlgCustomRenameEnabled = true
        var dlgInternalTitle = cleanDefaultTitle
        var dlgPublisher = ""
        var dlgDisplayVer = defaultVerStr
        var dlgOutputFileName = "$cleanDefaultTitle [v$defaultVerStr] [MULTI].${extForMode(dialogFormatMode)}"
        var dlgCustomIconPath = customIconImagePath
        var dlgBadgeIndex = 0
        var dlgOutNameEdit: EditText? = null

        formatAndOptionsCard.addView(makeStormDropdown(formatNames, dialogFormatMode, th) { pos ->
            val oldExt = ".${extForMode(dialogFormatMode)}"
            dialogFormatMode = pos
            val newExt = ".${extForMode(dialogFormatMode)}"
            dlgOutNameEdit?.let { editRef ->
                val curTxt = editRef.text.toString().trim()
                val updated = if (curTxt.endsWith(oldExt, ignoreCase = true)) {
                    curTxt.dropLast(oldExt.length) + newExt
                } else {
                    curTxt.substringBeforeLast('.') + newExt
                }
                editRef.setText(updated)
                dlgOutputFileName = updated
            }
        })

        var embedDlcUnlockerChecked = releaseFiles.any {
            it.selected && (it.roleBadge.equals("UNLOCKER", ignoreCase = true) ||
                it.fileName.contains("unlocker", ignoreCase = true) ||
                it.fileName.contains("анлокер", ignoreCase = true))
        }
        val dlcUnlockerCheckBox = makeStormCheckBox(
            "Вшить DLC UNLOCKER (разблокировка дополнений в RomFS при наличии)",
            embedDlcUnlockerChecked,
            th
        ) { checked ->
            embedDlcUnlockerChecked = checked
        }
        formatAndOptionsCard.addView(dlcUnlockerCheckBox)

        val rtDlgDeleteSourceCheck = makeStormCheckBox(
            "Удалять временные исходные файлы после успешной проверки контейнера",
            deleteRuTrackerSourceAfterSuccess,
            th
        ) { checked ->
            deleteRuTrackerSourceAfterSuccess = checked
            saveSettings()
        }
        formatAndOptionsCard.addView(rtDlgDeleteSourceCheck)
        root.addView(formatAndOptionsCard)

        // =========================================================================
        // ГРУППА 4: 🛠 ДОПОЛНИТЕЛЬНЫЕ ИНСТРУМЕНТЫ (СВОРАЧИВАЕМЫЕ ПАНЕЛИ)
        // =========================================================================
        fun buildDynamicUrlSection(
            headerTitle: String,
            itemHintPrefix: String,
            maxRows: Int,
            addBtnLabel: String,
            editList: MutableList<EditText>
        ): Pair<LinearLayout, (String) -> Unit> {
            val secContainer = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                visibility = View.GONE
                background = makeCardDrawable(th.bgCard, th.border, 6)
                setPadding(dp(8), dp(6), dp(8), dp(6))
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    topMargin = dp(4)
                    bottomMargin = dp(4)
                }
            }
            secContainer.addView(TextView(this).apply {
                text = headerTitle
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                typeface = Typeface.DEFAULT_BOLD
                setPadding(0, 0, 0, dp(4))
            })
            val inputsLayout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
            fun addUrlRow(prefill: String = "") {
                if (editList.size >= maxRows) {
                    Toast.makeText(this, "Достигнут лимит: максимум $maxRows строк!", Toast.LENGTH_SHORT).show()
                    return
                }
                val row = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(4)
                    }
                }
                val edit = EditText(this).apply {
                    setText(prefill)
                    hint = "$itemHintPrefix #${editList.size + 1} (из $maxRows)..."
                    setTextColor(th.textPrimary)
                    setHintTextColor(th.textSecondary)
                    textSize = 11.2f
                    background = makeCardDrawable(th.bgPrimary, th.border, 6)
                    setPadding(dp(8), dp(5), dp(8), dp(5))
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                        marginEnd = dp(6)
                    }
                }
                editList.add(edit)
                row.addView(edit)
                if (editList.size > 1) {
                    row.addView(makeButton("✕", Color.parseColor("#DC2626")) {
                        editList.remove(edit)
                        inputsLayout.removeView(row)
                    })
                }
                inputsLayout.addView(row)
            }
            addUrlRow()
            secContainer.addView(inputsLayout)
            secContainer.addView(makeButton(addBtnLabel, th.bgElevated, th.accent) {
                addUrlRow()
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    topMargin = dp(6)
                }
            })
            return secContainer to ::addUrlRow
        }

        var embedCloudUpdatesChecked = false
        var embedCloudDlcsChecked = false
        val updateEditTexts = mutableListOf<EditText>()
        val dlcEditTexts = mutableListOf<EditText>()
        val modEditTexts = mutableListOf<EditText>()

        val manualLinksWrapper = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            visibility = View.GONE
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        val (updatesContainer, _) = buildDynamicUrlSection(
            headerTitle = "🔄 Внешние ссылки на Обновления / Update (до 3 строк):",
            itemHintPrefix = "Ссылка на обновление",
            maxRows = 3,
            addBtnLabel = "➕ Добавить строку обновления",
            editList = updateEditTexts
        )
        manualLinksWrapper.addView(makeStormCheckBox("Включить свои ссылки на Обновление (до 3 ссылок)", embedCloudUpdatesChecked, th) { checked ->
            embedCloudUpdatesChecked = checked
            updatesContainer.visibility = if (checked) View.VISIBLE else View.GONE
        })
        manualLinksWrapper.addView(updatesContainer)

        val (dlcsContainer, _) = buildDynamicUrlSection(
            headerTitle = "🎁 Внешние ссылки на Дополнения / DLC (до 3 строк):",
            itemHintPrefix = "Ссылка на DLC",
            maxRows = 3,
            addBtnLabel = "➕ Добавить строку DLC",
            editList = dlcEditTexts
        )
        manualLinksWrapper.addView(makeStormCheckBox("Включить свои ссылки на DLC (до 3 ссылок)", embedCloudDlcsChecked, th) { checked ->
            embedCloudDlcsChecked = checked
            dlcsContainer.visibility = if (checked) View.VISIBLE else View.GONE
        })
        manualLinksWrapper.addView(dlcsContainer)

        val (modsContainer, _) = buildDynamicUrlSection(
            headerTitle = "☁️ Внешние ссылки на Русификаторы и Моды RomFS / ExeFS (до 5 строк):",
            itemHintPrefix = "Ссылка на русификатор / мод",
            maxRows = 5,
            addBtnLabel = "➕ Добавить строку мода / русификатора",
            editList = modEditTexts
        )
        manualLinksWrapper.addView(makeStormCheckBox("Включить свои ссылки на русификатор / мод (до 5 ссылок)", embedCloudModsChecked, th) { checked ->
            embedCloudModsChecked = checked
            modsContainer.visibility = if (checked) View.VISIBLE else View.GONE
        })
        manualLinksWrapper.addView(modsContainer)

        syncUnlockerAutoState = {
            val hasUnlockerInFiles = releaseFiles.any {
                it.selected && (it.roleBadge.equals("UNLOCKER", ignoreCase = true) ||
                    it.fileName.contains("unlocker", ignoreCase = true) ||
                    it.fileName.contains("анлокер", ignoreCase = true))
            }
            val hasUnlockerInDiscussion = discoveredDiscussionLinks.any {
                it.selected && (it.roleCategory.equals("UNLOCKER", ignoreCase = true) ||
                    it.title.contains("unlocker", ignoreCase = true) ||
                    it.title.contains("анлокер", ignoreCase = true) ||
                    it.url.contains("unlocker", ignoreCase = true))
            }
            val hasUnlockerInManualDlc = embedCloudDlcsChecked && dlcEditTexts.any {
                val txt = it.text.toString()
                txt.contains("unlocker", ignoreCase = true) || txt.contains("анлокер", ignoreCase = true)
            }
            val shouldBeChecked = hasUnlockerInFiles || hasUnlockerInDiscussion || hasUnlockerInManualDlc
            embedDlcUnlockerChecked = shouldBeChecked
            @Suppress("UNCHECKED_CAST")
            (dlcUnlockerCheckBox.tag as? (Boolean) -> Unit)?.invoke(shouldBeChecked)
        }

        // Сворачиваемый блок NACP и обложки 256x256
        val dlgRenameBox = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            visibility = View.GONE
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        val dlgIconRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        val dlgIconPreview = ImageView(this).apply {
            layoutParams = LinearLayout.LayoutParams(dp(56), dp(56)).apply { marginEnd = dp(8) }
            scaleType = ImageView.ScaleType.FIT_CENTER
        }
        fun updateDlgIconPreview() {
            val badge = iconBadgeOptions.getOrElse(dlgBadgeIndex) { "" }
            val title = dlgInternalTitle.ifBlank { cleanDefaultTitle }
            val iconPath = dlgCustomIconPath
            kotlin.concurrent.thread {
                try {
                    val jpegBytes = StormNczEngine.generateSwitchIconJpeg256(iconPath, title, badge)
                    val bmp = BitmapFactory.decodeByteArray(jpegBytes, 0, jpegBytes.size)
                    if (bmp != null) {
                        mainHandler.post { dlgIconPreview.setImageBitmap(bmp) }
                    }
                } catch (_: Throwable) {}
            }
        }
        updateDlgIconPreview()
        dlgIconRow.addView(dlgIconPreview)

        val dlgIconControls = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        dlgIconControls.addView(makeButton("🖼 Сменить иконку (256×256)", th.bgElevated, th.accent) {
            openCustomIconImagePicker { path ->
                dlgCustomIconPath = path
                customIconImagePath = path
                updateDlgIconPreview()
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(4)
            }
        })
        dlgIconControls.addView(makeStormDropdown(iconBadgeOptions, dlgBadgeIndex, th) { pos ->
            dlgBadgeIndex = pos
            updateDlgIconPreview()
        })
        dlgIconRow.addView(dlgIconControls)
        dlgRenameBox.addView(dlgIconRow)

        dlgRenameBox.addView(TextView(this).apply {
            text = "Название игры в меню приставки (control.nacp):"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.6f)
        })
        val dlgTitleEdit = EditText(this).apply {
            setText(dlgInternalTitle)
            setTextColor(th.textPrimary)
            textSize = 11.3f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(5), dp(8), dp(5))
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    dlgInternalTitle = s?.toString() ?: ""
                    updateDlgIconPreview()
                }
            })
        }
        dlgRenameBox.addView(dlgTitleEdit)

        dlgRenameBox.addView(TextView(this).apply {
            text = "Имя итогового выходного файла:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.6f)
            setPadding(0, dp(4), 0, 0)
        })
        dlgOutNameEdit = EditText(this).apply {
            setText(dlgOutputFileName)
            setTextColor(th.textPrimary)
            textSize = 11.3f
            setSingleLine(true)
            background = makeCardDrawable(th.bgCard, th.border, 6)
            setPadding(dp(8), dp(5), dp(8), dp(5))
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    dlgOutputFileName = s?.toString() ?: ""
                }
            })
        }
        dlgRenameBox.addView(dlgOutNameEdit)

        val advButtonsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        advButtonsRow.addView(makeButton("➕ Свои ссылки (Патч/Мод) ▼", th.bgElevated, th.textPrimary) {
            manualLinksWrapper.visibility = if (manualLinksWrapper.visibility == View.VISIBLE) View.GONE else View.VISIBLE
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(4) }
        })
        advButtonsRow.addView(makeButton("✏️ Имя и обложка (NACP) ▼", th.bgElevated, th.textPrimary) {
            dlgRenameBox.visibility = if (dlgRenameBox.visibility == View.VISIBLE) View.GONE else View.VISIBLE
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        root.addView(advButtonsRow)
        root.addView(manualLinksWrapper)
        root.addView(dlgRenameBox)

        // =========================================================================
        // НИЖНЯЯ ЗАКРЕПЛЁННАЯ ПАНЕЛЬ (ВНЕ SCROLLVIEW — ВСЕГДА НА ВИДУ!):
        // Прогресс-бар + Кнопка «🚀 Скачать и собрать» + Кнопка «⏹ Отменить задачу» + «✖ Закрыть»
        // =========================================================================
        val processBox = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(7), dp(10), dp(7))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }

        val stageHeaderTxt = TextView(this).apply {
            text = "⏳ Готов к загрузке и сборке (внешние моды по умолчанию выключены)"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.4f)
            typeface = Typeface.DEFAULT_BOLD
        }
        processBox.addView(stageHeaderTxt)

        val progressBar = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(12)).apply {
                topMargin = dp(4)
                bottomMargin = dp(4)
            }
        }
        processBox.addView(progressBar)

        val statusTxt = TextView(this).apply {
            text = "Выходная папка: ${customOutputDir.name}/ • Нажмите «🚀 Скачать и собрать»"
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.8f)
        }
        processBox.addView(statusTxt)

        val liveStepLogTxt = TextView(this).apply {
            text = ""
            visibility = View.GONE
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.0f)
            typeface = Typeface.MONOSPACE
            maxLines = 4
            setPadding(0, dp(3), 0, 0)
        }
        processBox.addView(liveStepLogTxt)
        card.addView(processBox)

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT)
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )

        fun triggerPipelineExecution() {
            if (isRunningPipeline) return
            val activeSelection = releaseFiles.filter { it.selected }
            if (activeSelection.isEmpty()) {
                Toast.makeText(this, "Отметьте хотя бы один файл раздачи для скачивания!", Toast.LENGTH_SHORT).show()
                return
            }
            isRunningPipeline = true
            cancelPipeline = false
            liveStepLogTxt.visibility = View.VISIBLE

            val discUpdateUrls = discoveredDiscussionLinks.filter {
                it.selected && (it.roleCategory.contains("ОБНОВЛ", ignoreCase = true) || it.roleCategory.contains("UPDATE", ignoreCase = true) || it.roleCategory.contains("ПАТЧ", ignoreCase = true))
            }.map { (listOf(it.url) + it.mirrorUrls).distinct().joinToString("|MIRROR|") }
            val discDlcUrls = discoveredDiscussionLinks.filter {
                it.selected && (it.roleCategory.contains("DLC", ignoreCase = true) || it.roleCategory.contains("UNLOCK", ignoreCase = true))
            }.map { (listOf(it.url) + it.mirrorUrls).distinct().joinToString("|MIRROR|") }
            val discModUrls = discoveredDiscussionLinks.filter {
                it.selected && (it.roleCategory.contains("МОД", ignoreCase = true) || it.roleCategory.contains("РУС", ignoreCase = true) || it.roleCategory.contains("MOD", ignoreCase = true) || it.roleCategory.contains("RUS", ignoreCase = true))
            }.map { (listOf(it.url) + it.mirrorUrls).distinct().joinToString("|MIRROR|") }

            val manualUpdateUrls = if (embedCloudUpdatesChecked) {
                updateEditTexts.map { it.text.toString().trim() }.filter { it.isNotEmpty() }.take(3)
            } else {
                emptyList()
            }
            val manualDlcUrls = if (embedCloudDlcsChecked) {
                dlcEditTexts.map { it.text.toString().trim() }.filter { it.isNotEmpty() }.take(3)
            } else {
                emptyList()
            }
            val manualModUrls = if (embedCloudModsChecked) {
                modEditTexts.map { it.text.toString().trim() }.filter { it.isNotEmpty() }.take(5)
            } else {
                emptyList()
            }

            val collectedUpdateUrls = (discUpdateUrls + manualUpdateUrls).distinct().take(6)
            val collectedDlcUrls = (discDlcUrls + manualDlcUrls).distinct().take(6)
            val collectedModUrls = (discModUrls + manualModUrls).distinct().take(8)

            thread {
                try {
                    val useBlock = (dialogFormatMode == 0 || dialogFormatMode == 1)
                    val dlgPreBuild = StormNczEngine.PreBuildCustomization(
                        enabled = dlgCustomRenameEnabled,
                        customInternalTitle = dlgInternalTitle.ifBlank { cleanDefaultTitle },
                        customPublisher = dlgPublisher,
                        customDisplayVersion = dlgDisplayVer,
                        customOutputFileName = dlgOutputFileName,
                        customIconPath = dlgCustomIconPath,
                        iconBadgeText = if (dlgBadgeIndex > 0) iconBadgeOptions.getOrElse(dlgBadgeIndex) { "" } else ""
                    )
                    val res = RuTrackerCloudEngine.downloadTopicAndBuildMultiContent(
                        topic = item,
                        modUrls = collectedModUrls,
                        targetFormatMode = dialogFormatMode,
                        useBlockNcz = useBlock,
                        zstdLevel = zstdLevel,
                        blockSizeExponent = blockSizeExpo,
                        threads = cpuThreads,
                        romsDir = romsDir,
                        modsRootDir = modsDir,
                        outputDir = customOutputDir,
                        isCancelled = { cancelPipeline },
                        onProgress = { _, _ -> },
                        selectedReleaseFiles = activeSelection,
                        preBuildCustomization = dlgPreBuild,
                        deleteSourceAfterVerify = deleteRuTrackerSourceAfterSuccess,
                        updateUrls = collectedUpdateUrls,
                        dlcUrls = collectedDlcUrls,
                        embedDlcUnlocker = embedDlcUnlockerChecked,
                        onDetailedProgress = { pct, stageTitle, detailText, stepLines ->
                            mainHandler.post {
                                progressBar.progress = pct
                                stageHeaderTxt.text = "$stageTitle ($pct%)"
                                statusTxt.text = detailText
                                if (stepLines.isNotEmpty()) {
                                    liveStepLogTxt.text = stepLines.takeLast(4).joinToString("\n")
                                }
                            }
                        }
                    )
                    val outFile = File(res.outputPath)
                    if (outFile.exists() && queuedFiles.none { it.absolutePath == outFile.absolutePath }) {
                        queuedFiles.add(outFile)
                        selectedInputFile = outFile
                    }
                    appendLog("✅ Авто-сборка RuTracker завершена: ${outFile.name} (${StormNczEngine.formatSize(res.compressedBytes)})")
                    mainHandler.post {
                        progressBar.progress = 100
                        stageHeaderTxt.text = "✅ Сборка завершена (100%)"
                        statusTxt.text = "✅ Готово! Файл создан: ${outFile.name} (${StormNczEngine.formatSize(res.compressedBytes)})"
                        Toast.makeText(this, "Готово: ${outFile.name} (${StormNczEngine.formatSize(res.compressedBytes)})", Toast.LENGTH_LONG).show()
                    }
                } catch (e: Exception) {
                    appendLog("❌ Ошибка/отмена авто-сборки: ${e.message}")
                    mainHandler.post {
                        stageHeaderTxt.text = if (cancelPipeline) "⏹ Задача отменена пользователем" else "❌ Операция прервана"
                        statusTxt.text = if (cancelPipeline) "⏹ Выполнение остановлено по запросу пользователя." else "❌ Ошибка: ${e.message}"
                    }
                } finally {
                    isRunningPipeline = false
                }
            }
        }

        val startBtn = makeButton("🚀 Скачать и собрать", th.accent) {
            triggerPipelineExecution()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.15f).apply {
                marginEnd = dp(6)
            }
        }
        btnRow.addView(startBtn)

        // ВСЕГДА ВИДИМАЯ КНОПКА ОТМЕНЫ ЗАДАЧИ!
        val cancelTaskBtn = makeButton("⏹ Отменить задачу", Color.parseColor("#DC2626")) {
            cancelPipeline = true
            cancelCurrentTaskRequested = true
            stageHeaderTxt.text = "⏹ Задача отменена пользователем"
            statusTxt.text = "⏹ Остановка загрузки и сборки..."
            Toast.makeText(this, "⏹ Задача отменена", Toast.LENGTH_SHORT).show()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                marginEnd = dp(6)
            }
        }
        btnRow.addView(cancelTaskBtn)

        val closeBtn = makeButton("✖ Закрыть", th.bgElevated, th.textPrimary) {
            if (isRunningPipeline) {
                cancelPipeline = true
            }
            dlg.dismiss()
        }
        btnRow.addView(closeBtn)
        card.addView(btnRow)

        if (autoStartMagnet) {
            mainHandler.postDelayed({ triggerPipelineExecution() }, 180L)
        }
    }

// =========================================================================
    // ВКЛАДКА 5: ПОЛНАЯ ИНСТРУКЦИЯ И БАЗА ЗНАНИЙ (24 РАЗДЕЛА ПК-ВЕРСИИ + СИМУЛЯТОР)
    // =========================================================================

    data class InstructionTopicItem(
        val id: Int,
        val title: String,
        val category: String,
        val badge: String,
        val description: String,
        val proTip: String
    )

    private val instructionTopics: List<InstructionTopicItem> by lazy {
        listOf(
                InstructionTopicItem(
                    21,
                    "21. Nintendo Switch 2 (Ounce / NVN2)",
                    "Экосистемы",
                    "SWITCH 2",
                    "STORM SWITCH BOX 6.7.1 включает полную поддержку следующего поколения игровых систем Nintendo — Nintendo Switch 2 (кодовое имя «Ounce» / API «NVN2»):\n" +
                    "• Идентификация дампов: TitleID игр Switch 2 начинается с префикса «0400...» (в отличие от «0100...» у Switch 1), извлекаемый напрямую из main.npdm (заголовок META -> секция ACI0 + 0x10).\n" +
                    "• Авто-сборка из папок дампа: при перетаскивании каталога дампа Switch 2 (содержащего base/exefs/main.npdm, base/romfs, updates/<версия> и dlc/<tid>) программа автоматически объединяет базовую игру, самое свежее обновление, все DLC и моды в 1 монолитный контейнер (.nsp, .nsz, .xci, .xcz).\n" +
                    "• Авто-генерация Control NCA: создание control.nacp на 16 языках и получение официальной 256×256 обложки через Nintendo Europe Search API / Steam.\n" +
                    "• Блочное Zstandard сжатие NCZBLOCK v2: сжатие Program NCA в ультра-компактный .nsz / .xcz.\n" +
                    "• Отдельная папка вывода: сохранение в выделенную папку (по умолчанию Switch 2 / roms_switch2), настраиваемую в Параметрах с удобным проводником.",
                    "Совет: Используйте отдельную папку сохранения для Switch 2 в Настройках, чтобы держать коллекции Switch 1, Switch 2 и 3DS аккуратно разделенными!"
                ),
                InstructionTopicItem(
                    22,
                    "22. RuTracker: Чеклист ссылок обсуждения и DLC Unlocker",
                    "Сеть, Ключи и База",
                    "RUTRACKER",
                    "В версии 6.7.1 усовершенствован модуль интеграции с RuTracker (#1605):\n" +
                    "• Чеклист ссылок из темы обсуждения: все полезные ссылки (русификаторы, моды, патчи 60 FPS, читы), найденные в комментариях и шапке темы раздачи, отображаются с датами публикации сообщений.\n" +
                    "• Выборочная загрузка: можно отметить только нужные модификации чекбоксами перед стартом сборки.\n" +
                    "• DLC Unlocker: автоматическое управление анлокерами дополнений и синхронизация тикетов.",
                    "Совет: Ссылки на сторонние модификации проверяются на доступность перед скачиванием!"
                ),
            InstructionTopicItem(
                1,
                "1. Обзор приложения и архитектура STORM SWITCH BOX",
                "Основы и Сборка",
                "ОБЗОР",
                "STORM SWITCH BOX — единая кроссплатформенная рабочая станция для Nintendo Switch и Nintendo 3DS.\n" +
                    "• Объединяет 5 ключевых модулей: Умный сборщик Мульти-контента (4-в-1), Блочный компрессор Zstandard (NCZSECTN + NCZBLOCK), Инспектор и Аудитор коллекций, Поисковик и загрузчик RuTracker (#1605) с облачными модами и Локальный HTTP сервер DBI / Tinfoil.\n" +
                    "• Все операции выполняются потоково (Streaming I/O) без распаковки многогигабайтных образов в оперативную память.",
                "Совет: Начните с вкладки «Ключи и параметры» и нажмите «⬇️ Скачать и установить ключи», чтобы активировать полную поддержку всех прошивок HOS в 1 клик."
            ),
            InstructionTopicItem(
                2,
                "2. Умная обработка файлов и Блочное сжатие (NCZSECTN + NCZBLOCK)",
                "Основы и Сборка",
                "NCZBLOCK",
                "В отличие от обычного потокового сжатия, блочный режим NCZBLOCK разбивает данные разделов NCA (после отступа заголовка 0x4000) на независимые блоки по 1 МБ (2^20) или 2 МБ (2^21).\n" +
                    "• Перед сжатием Zstandard (уровни 1..22) движок расшифровывает заголовок NCA (AES-128-XTS 0xC00) и секции AES-128-CTR, что позволяет сжимать игровые данные на 35–55%.\n" +
                    "• Встроенный механизм SmartSize Guard проверяет каждый блок и каждый NCA: если раздел уже сжат, сохраняется меньший вариант.\n" +
                    "• Сжатые контейнеры .nsz и .xcz позволяют эмуляторам (Eden, Citron, Yuzu, Ryujinx) выполнять мгновенный Random-Access Seek без полной распаковки файла.",
                "Совет: Профиль «Умный автоматический» (блок 1 МБ, Zstandard 18) обеспечивает идеальный баланс скорости чтения и экономии памяти."
            ),
            InstructionTopicItem(
                3,
                "3. Автоматическая группировка очереди по играм (База + Патч + DLC + Моды)",
                "Основы и Сборка",
                "ОЧЕРЕДЬ",
                "При добавлении множества файлов или целой папки программа автоматически анализирует каждый элемент:\n" +
                    "• Вычисляет 16-символьный TitleID из заголовков NCA или имени файла и нормализует его к базовому ID приложения (суффикс 000).\n" +
                    "• Присваивает роли: [БАЗА] (суффикс 000), [ПАТЧ] (суффикс 800), [DLC] (суффиксы 001..7FF), [МОД] (.zip / romfs / exefs / .ips) или [3DS].\n" +
                    "• Разные игры никогда не смешиваются между собой: каждая игра собирается в собственный независимый мульти-контейнер.",
                "Совет: Вы можете выбрать сразу всю папку roms или Download через «🗂 Мульти-проводник» — программа сама разложит файлы по играм."
            ),
            InstructionTopicItem(
                4,
                "4. Мульти-контент 4-в-1 (Сшивание Базы, Обновления, DLC и Модов)",
                "Основы и Сборка",
                "4-В-1",
                "Режим Мульти-контента объединяет все компоненты одной игры в единый монолитный контейнер (.nsz, .xcz, .nsp или .xci):\n" +
                    "• Все вложенные контейнеры PFS0 / HFS0 разворачиваются до плоского списка разделов (*.nca, *.ncz, *.cnmt.nca, *.control.nca, *.tik, *.cert).\n" +
                    "• Разделы базовой игры [БАЗА], последнего обновления [ПАТЧ] и всех дополнений [DLC] сохраняются вместе в корне контейнера PFS0 (или в разделе secure картриджа HFS0).\n" +
                    "• Если в очередь попали два разных патча одной игры (например, v1.0.1 и v1.0.2), умная дедупликация автоматически оставляет самый свежий патч, сохраняя при этом базовую игру и все DLC.",
                "Совет: Для эмуляторов на Android и ПК лучше всего подходит формат NSZ (Блочный NCZBLOCK) или XCZ (сжатый картридж без тикетов)."
            ),
            InstructionTopicItem(
                5,
                "5. Динамический поиск RuTracker (#1605) и Авто-сборка в 1 клик",
                "Сеть, Ключи и База",
                "RUTRACKER",
                "Вкладка «🌐 RuTracker и Форвардер» работает строго по разделу игр Nintendo Switch (#1605):\n" +
                    "• Живой динамический фильтр мгновенно (за 0 мс) отбирает раздачи при вводе букв и параллельно дозапрашивает свежие релизы с зеркал.\n" +
                    "• Кнопка «⬇️ Скачать и собрать» открывает окно с полным списком файлов раздачи ([БАЗА], [ПАТЧ], [DLC]), выбором формата, редактором имени/иконки и живым логом 3 этапов сборки.\n" +
                    "• Кнопка «🧲 Magnet» сразу примагничивает раздачу к встроенному сборщику Мульти-контента.\n" +
                    "• Кнопка «🌐 Сайт» открывает карточку релиза с автоматическим обходом проверки безопасности Cloudflare.",
                "Совет: Отметьте чекбокс «Скачать и вшить русификатор / моды в мульти-контент», чтобы программа автоматически извлекла ссылки на русификаторы из темы и вшила их в игру."
            ),
            InstructionTopicItem(
                6,
                "6. Предварительное переименование внутри игры (control.nacp / SMDH) и Студия иконок 256×256",
                "Моды и NACP",
                "NACP / ICON",
                "Перед запуском сборки вы можете полностью кастомизировать отображение игры в меню приставки или эмулятора:\n" +
                    "• Внутреннее название игры и издатель записываются во все 16 языковых слотов бинарной структуры control.nacp (0x4000 байт) для Switch или SMDH (UTF-16LE) для Nintendo 3DS.\n" +
                    "• Отображаемая версия (Display Version) записывается по смещению 0x3060.\n" +
                    "• Кнопка «🖼 Сменить иконку / обложку» позволяет выбрать любую картинку из памяти телефона: она автоматически масштабируется в 256×256 Baseline JPEG (без прогрессивного кодирования) и снабжается выбранным бейджем (RUS, RUS + MOD, 60 FPS, MULTI, 4K MOD).\n" +
                    "• Также вы можете задать точное имя итогового выходного файла на диске.",
                "Совет: Nintendo Switch и эмуляторы требуют строго Baseline JPEG 256×256 — встроенная студия иконок формирует этот формат автоматически."
            ),
            InstructionTopicItem(
                7,
                "7. Правильная распаковка и вшивание модов (LayeredFS RomFS / ExeFS / IPS)",
                "Моды и NACP",
                "LAYEREDFS",
                "Конвейер модификаций поддерживает архивы .zip, папки atmosphere/contents/<TitleID>/, romfs/, exefs/ и бинарные патчи .ips / .pchtxt:\n" +
                    "• При сборке архив мода автоматически распаковывается, очищается от мусорных файлов ОС (Thumbs.db, .DS_Store, __MACOSX, desktop.ini) и привязывается к 16-символьному базовому TitleID игры.\n" +
                    "• Файлы модификации копируются в каталог mods/atmosphere/contents/<TitleID>/ и одновременно упаковываются в валидный раздел Data/RomFS NCA3 (сжимаемый в .ncz) внутри мульти-контейнера.",
                "Совет: Вы можете добавлять несколько ссылок на моды одновременно (например, русификатор текста + патч 60 FPS) через кнопку «➕ Добавить ещё ссылку»."
            ),
            InstructionTopicItem(
                8,
                "8. Полная поддержка всех форматов Switch (NSP, NSZ, XCI, XCZ, NCA, NCZ)",
                "Форматы Switch и 3DS",
                "SWITCH",
                "Поддерживаются все контейнеры и разделы экосистемы Nintendo Switch:\n" +
                    "• NSP (PFS0) — цифровой установочный контейнер (содержит .nca, .tik, .cert).\n" +
                    "• NSZ (PFS0 + NCZBLOCK) — сжатый цифровой контейнер с блочным сжатием разделов .ncz.\n" +
                    "• XCI (HEAD 0x100 + Root HFS0 0x10000) — образ игрового картриджа с разделами update, normal и secure (без привязки к тикетам).\n" +
                    "• XCZ (HEAD + HFS0 + NCZBLOCK) — сжатый мульти-картридж с разделами .ncz внутри secure HFS0.",
                "Совет: Вы можете свободно конвертировать NSP <-> XCI и NSZ <-> XCZ в любом направлении, выбирая нужный конечный формат в выпадающем списке."
            ),
            InstructionTopicItem(
                9,
                "9. Архитектура и форматы Nintendo 3DS (3DS, CCI, CIA, CXI, CFA, NDS)",
                "Форматы Switch и 3DS",
                "NINTENDO 3DS",
                "Встроен полноценный нативный конвейер для образов Nintendo 3DS (Citra, Azahar, Lime3DS):\n" +
                    "• Аппаратный 128-битный генератор ключей KeyScrambler: NormalKey = ROL128((ROL128(KeyX, 2) xor KeyY) + C, 87).\n" +
                    "• Расшифровка разделов NCCH и установка флага NoCrypto (0x04 по смещению +0x18F) для запуска в эмуляторах без aes_keys.txt.\n" +
                    "• Прямая взаимная конвертация установочных архивов CIA <-> картриджей CCI / 3DS (с пересборкой таблицы разделов NCSD 0x120 и заголовков CIA).",
                "Совет: Для эмуляторов Azahar и Citra на Android рекомендуется выбирать выходной формат «CCI — Образ картриджа Nintendo 3DS»."
            ),
            InstructionTopicItem(
                10,
                "10. Хирургическая обрезка пустых областей SmartShrink (Trim 0xFF для XCI и 3DS)",
                "Форматы Switch и 3DS",
                "SMARTSHRINK",
                "Физические дампы картриджей Switch (.xci) и Nintendo 3DS (.3ds / .cci) дополняются пустыми байтами 0xFF до размера флеш-памяти (4 ГБ, 8 ГБ, 16 ГБ или 32 ГБ):\n" +
                    "• Для Switch XCI SmartShrink считывает поле validDataEndPage по смещению 0x118 в заголовке HEAD.\n" +
                    "• Для Nintendo 3DS NCSD SmartShrink анализирует все 8 разделов таблицы партиций по смещению 0x120..0x15F.\n" +
                    "• Хвостовые гигабайты пустоты 0xFF безопасно отсекаются без затрагивания игровых данных и подписей.",
                "Совет: В профиле «Умный автоматический» обрезка SmartShrink выполняется перед сжатием автоматически."
            ),
            InstructionTopicItem(
                11,
                "11. Инспектор контейнеров и Выборочная распаковка разделов",
                "Форматы Switch и 3DS",
                "ИНСПЕКТОР",
                "На вкладке «🔍 Инспектор и RomFS» вы можете просмотреть точную внутреннюю карту любого образа:\n" +
                    "• Отображаются все разделы PFS0, HFS0 (update/normal/secure), смещения в HEX, размеры и типы разделов.\n" +
                    "• Кнопка «📤 Извлечь все разделы в extracted/» распаковывает внутренние .nca, .ncz, .tik, .cert, .cxi в папку /storage/emulated/0/STORM SWITCH BOX/extracted/.",
                "Совет: Кликните по любому файлу в очереди на первой вкладке, чтобы сделать его активным для Инспектора."
            ),
            InstructionTopicItem(
                12,
                "12. Криптографическая проверка целостности (SHA-256 и верификация секций)",
                "Форматы Switch и 3DS",
                "SHA-256",
                "Кнопка «🛡️ Проверить целостность (SHA-256)» на вкладке Инспектора выполняет глубокую верификацию контейнера:\n" +
                    "• Проверяет сигнатуры заголовков PFS0, HFS0, HEAD, NCSD и CIA.\n" +
                    "• Проверяет, что ни один раздел NCA/NCZ не выходит за физические границы файла (защита от битых и недокачанных образов).\n" +
                    "• Верифицирует магические заголовки NCZSECTN и NCZBLOCK в сжатых файлах и рассчитывает контрольный хэш SHA-256.",
                "Совет: Всегда проверяйте целостность образов, скачанных из внешних источников, перед установкой на приставку."
            ),
            InstructionTopicItem(
                13,
                "13. Аудитор и Дедупликатор библиотеки",
                "Основы и Сборка",
                "АУДИТОР",
                "Аудитор сканирует вашу коллекцию игр в папках roms, output и очереди задач:\n" +
                    "• Находит несжатые контейнеры .nsp, которые можно сжать в блочный .nsz.\n" +
                    "• Выявляет необрезанные картриджи .xci и .3ds с точным подсчётом пустых байт 0xFF.\n" +
                    "• Обнаруживает дубликаты одного и того же TitleID и рассчитывает суммарную потенциальную экономию места.\n" +
                    "• Кнопка «⚡ Оптимизировать найденное» в 1 клик переносит все неоптимизированные образы в очередь.",
                "Совет: Регулярный аудит коллекции позволяет освободить от 30% до 55% памяти на карте microSD или в памяти смартфона."
            ),
            InstructionTopicItem(
                14,
                "14. Локальный HTTP Range сервер для прямой установки на консоль (DBI и Tinfoil)",
                "Сеть, Ключи и База",
                "HTTP DBI",
                "Вкладка «📡 Сеть и консоль» превращает ваше Android-устройство в высокоскоростной сервер раздачи игр по Wi-Fi:\n" +
                    "• Генерирует JSON-индекс /index.tfl для Tinfoil и HTML-каталог для DBI (Run HTTP Server) и браузера.\n" +
                    "• Поддерживает заголовок HTTP 206 Partial Content (Byte-Range Streaming): приставка скачивает и устанавливает игру напрямую по частям без кэширования.",
                "Совет: Убедитесь, что смартфон и Nintendo Switch подключены к одной сети Wi-Fi (5 ГГц рекомендуется для скорости до 60 МБ/с) и укажите в Tinfoil/DBI адрес http://<IP>:8080/index.tfl."
            ),
            InstructionTopicItem(
                15,
                "15. Онлайн-загрузка ключей (prod.keys / title.keys) и Матрица Master Key",
                "Сеть, Ключи и База",
                "КЛЮЧИ HOS",
                "На вкладке «🔑 Ключи и параметры» реализован менеджер криптографических ключей Nintendo Switch (как в проекте STORM SWITCH):\n" +
                    "• Выберите нужную версию прошивки HOS из выпадающего списка и нажмите «⬇️ Скачать и установить ключи» — архив автоматически скачается с GitHub и распакуется в папки STORM SWITCH BOX/keys и STORM SWITCH/keys.\n" +
                    "• Встроенный анализатор Master Key определяет максимальную поддерживаемую прошивку (от master_key_00 [HOS 1.0.0] до master_key_13 [HOS 20.0.0 — 21.0.0+]).\n" +
                    "• Кнопка «🔍 Автопоиск из эмуляторов» автоматически находит и импортирует ключи из папок Eden, Citron и Yuzu.",
                "Совет: Нажмите «🔄 Обновить список», чтобы проверить выход новых ревизий ключей на GitHub."
            ),
            InstructionTopicItem(
                16,
                "16. Матрица совместимости форматов (Эмуляторы и Консоли)",
                "Форматы Switch и 3DS",
                "МАТРИЦА",
                "Краткая памятка по выбору оптимального формата:\n" +
                    "• Eden / Citron / Yuzu / Sudachi (Android и ПК): рекомендуется блочный NSZ или XCZ (экономия ~40% места, мгновенный запуск Мульти-контента).\n" +
                    "• Ryujinx (ПК): поддерживает NSP, XCI, NSZ и XCZ.\n" +
                    "• Nintendo Switch (DBI / Tinfoil / Goldleaf): рекомендуется блочный NSZ для быстрой установки по Wi-Fi/USB или XCI для монтирования с внешнего накопителя.\n" +
                    "• Azahar / Citra / Lime3DS (Android и ПК): рекомендуется расшифрованный CCI или 3DS с флагом NoCrypto.\n" +
                    "• Реальная консоль Nintendo 3DS (FBI): формат CIA.",
                "Совет: Если эмулятор не видит отдельное обновление, сшейте Базу + Патч + DLC в единый файл NSZ/XCZ через профиль «Умный автоматический»."
            ),
            InstructionTopicItem(
                17,
                "17. Интеграция с эмуляторами Android и синхронизация папок",
                "Сеть, Ключи и База",
                "ЭМУЛЯТОРЫ",
                "STORM SWITCH BOX напрямую интегрируется с файловой структурой эмуляторов на Android:\n" +
                    "• В проводнике выбора выходной папки («Изменить папку») доступны быстрые закладки на папки STORM SWITCH BOX/output, STORM SWITCH BOX/roms, STORM SWITCH/roms и Download.\n" +
                    "• Распакованные моды автоматически сохраняются в стандартной иерархии Atmosphere (mods/atmosphere/contents/<TitleID>/romfs), совместимой с любым эмулятором.",
                "Совет: Укажите папку игр вашего эмулятора как выходную папку сохранения, чтобы собранные Мульти-контент образы сразу появлялись в списке игр эмулятора."
            ),
            InstructionTopicItem(
                18,
                "18. Автоматическое обновление приложения с GitHub",
                "Сеть, Ключи и База",
                "АВТОАПДЕЙТ",
                "Приложение автоматически проверяет наличие свежих релизов на GitHub при запуске (а также по кнопке «🔄 Обновить» в верхней шапке):\n" +
                    "• Автоматически определяет архитектуру вашего устройства и выбирает правильный пакет: основной APK (arm64-v8a / x86_64) или совместимый LEGACY APK (armeabi-v7a / Android 5.0+).\n" +
                    "• Скачивает обновление с живым прогресс-баром скорости и сразу запускает системный установщик Android.",
                "Совет: Все настройки, темы оформления и установленные ключи сохраняются при обновлении поверх существующей версии."
            ),
            InstructionTopicItem(
                19,
                "19. Персонализация: 9 эталонных тем STORM SOFT и 11 языков",
                "Основы и Сборка",
                "ТЕМЫ И UI",
                "Интерфейс поддерживает 9 фирменных тем экосистемы STORM SOFT:\n" +
                    "• STORM MIDNIGHT (по умолчанию), STORM DARK, STORM NIGHT (OLED), STORM DAY, STORM MATRIX, STORM CYBERPUNK, STORM FANTASY, STORM WARHAMMER 40K и STORM CRIMSON.\n" +
                    "• Работает в полноэкранном режиме Immersive Sticky Fullscreen (верхняя и нижняя системные шторки Android автоматически скрыты).\n" +
                    "• Все выбранные параметры, тема, язык и папка сохранения автоматически сохраняются в settings.json.",
                "Совет: На экранах AMOLED/OLED тема «STORM NIGHT (OLED)» дополнительно экономит заряд аккумулятора."
            ),
            InstructionTopicItem(
                20,
                "20. Безопасное удаление исходников и очистка временных файлов",
                "Основы и Сборка",
                "ОЧИСТКА",
                "Все промежуточные операции сжатия NCZBLOCK и распаковки модов выполняются во временных рабочих каталогах (.tmp_multi_*), которые гарантированно удаляются в блоке finally даже при отмене операции пользователем.\n" +
                    "• Если включён чекбокс «Удалять исходные файлы после успешной проверки выходного контейнера», исходные части удаляются только после того, как итоговый мульти-контейнер полностью записан и проверен.",
                "Совет: Вы всегда можете остановить длительную пакетную очередь кнопкой «⏹ Стоп» без риска повреждения исходных файлов."
            ),
            InstructionTopicItem(
                21,
                "21. Распаковка и обратная Упаковка RomFS и ExeFS (Моддинг и перевод)",
                "Форматы Switch и 3DS",
                "ROMFS / EXEFS",
                "Полный цикл распаковки и обратной сборки игровых контейнеров прямо на Android (как на ПК):\n" +
                    "• Во вкладке «🔍 Инспектор и RomFS» нажмите «📂 Извлечь RomFS + ExeFS» (или выберите режим «Полная распаковка игры» в Конвейере).\n" +
                    "• Движок автоматически декомпрессирует сжатые потоки NCZ, разбирает Program NCA и Control NCA и раскладывает данные по папкам: romfs/ (игровые ресурсы и конфиги), exefs/ (исполняемые модули main, main.npdm, rtld, sdk), control/ (control.nacp + обложка 256×256 JPEG), pfs0/ (сырые разделы) и готовый шаблон atmosphere/contents/<TitleID>/romfs/.\n" +
                    "• После внесения правок или добавления русификатора нажмите «📦 Упаковать папку в NSP/NSZ» — программа соберёт модифицированные файлы обратно в монолитный .nsp или сжатый .nsz!",
                "Совет: Вам больше не нужен ПК для вшивания перевода или редактирования config.ini внутри игры — распакуйте RomFS, замените файлы и упакуйте обратно в NSZ прямо на смартфоне."
            ),
            InstructionTopicItem(
                22,
                "22. Виртуальный проводник RomFS / ExeFS с просмотром HEX и UTF-8",
                "Форматы Switch и 3DS",
                "HEX ПРОВОДНИК",
                "Интерактивный просмотр внутреннего дерева любого образа (.nsp, .nsz, .xci, .xcz, .3ds, .cci, .cia) без полной распаковки на накопитель:\n" +
                    "• Откройте вкладку «🔍 Инспектор и RomFS» и нажмите «🌲 Проводник RomFS / ExeFS (HEX)».\n" +
                    "• Переключайтесь между узлами /pfs0/, /exefs/main.npdm, /exefs/main, /control/control.nacp и /romfs/config.ini.\n" +
                    "• Встроенный двухрежимный инспектор показывает классический 16-байтный дамп HEX + ASCII и автоматическую расшифровку заголовков NACP (16 языков), NPDM (ACI0/TitleID), NSO0 и текста UTF-8.\n" +
                    "• Кнопка «💾 Извлечь узел» мгновенно сохраняет только выбранный файл.",
                "Совет: Используйте фильтр поиска в верхней части проводника, чтобы за секунду найти нужный конфигурационный или языковой файл внутри многогигабайтного контейнера."
            ),
            InstructionTopicItem(
                23,
                "23. Генератор Homebrew Forwarder (.nro → .nsp) + RuTracker Homebrew",
                "RuTracker и Моды",
                "NRO -> NSP",
                "Создание установочных ярлыков (.nsp) для главного экрана Nintendo Switch (Horizon OS) из любого хоумбрю (.nro) или ретро-игры:\n" +
                    "• Откройте вкладку «🌐 RuTracker и Форвардер» и прокрутите к карточке «🚀 Генератор Homebrew Forwarder (.nro → .nsp)».\n" +
                    "• Выберите один из готовых пресетов (DBI, RetroArch, JKSV, Moonlight-NX), нажмите «🌐 Найти Homebrew на RuTracker» или выберите локальный .nro файл со смартфона (название, автор, версия и иконка 256×256 JPEG автоматически считаются из секции ASET!).\n" +
                    "• Нажмите «⚡ Собрать Forwarder .NSP» — программа сформирует валидный PFS0 NSP с ExeFS (NSO0 nx-hbloader + NPDM) и RomFS (nextNroPath / nextArgv).",
                "Совет: Запуск хоумбрю и эмуляторов через установленный NSP-форвардер автоматически выделяет приложению все 4 ГБ оперативной памяти Switch (Title Override Mode) и предотвращает вылеты с ошибкой 2168-0002!"
            ),
            InstructionTopicItem(
                24,
                "24. Установка .CIA на Nintendo 3DS по QR-коду (FBI) и передача по FTP",
                "Сеть, Ключи и База",
                "3DS QR + FTP",
                "Беспроводная передача игр на консоли Nintendo 3DS и Nintendo Switch во вкладке «📡 Сеть, 3DS QR и FTP»:\n" +
                    "• Установка на 3DS по QR-коду: выберите любой .cia файл и нажмите «📷 Показать QR-код для установки через FBI (Nintendo 3DS)». На консоли 3DS откройте FBI -> Remote Install -> Scan QR Code и наведите камеру на экран телефона — игра сразу скачается и установится по Wi-Fi!\n" +
                    "• Встроенный FTP-клиент: нажмите «🔍 Найти в Wi-Fi» (автопоиск портов 5000 и 21 для sys-ftpd / DBI / FTPD), выберите целевую папку (/switch/, /cia/, /atmosphere/contents/) и отправьте игру или мод напрямую с живым индикатором скорости.",
                "Совет: Для работы QR-установки 3DS и смартфон должны находиться в одной локальной сети Wi-Fi (или подключены к точке доступа смартфона)."
            ),
            InstructionTopicItem(
                25,
                "25. База игр TitleDB и расшифровка системных ошибок Switch (2xxx-xxxx)",
                "Сеть, Ключи и База",
                "TITLEDB + ОШИБКИ",
                "Встроенный справочник метаданных и диагностики во вкладке «📚 База TitleDB и Ошибки»:\n" +
                    "• База игр TitleDB: мгновенный поиск по названию игры или TitleID (0100... / 00040000...), просмотр ID базы [000] и патча [800], статуса русского перевода, количества DLC и переход к скачиванию с RuTracker в 1 клик.\n" +
                    "• Декодер ошибок Switch и Atmosphère (2xxx-xxxx): введите код системной ошибки (например, 2002-4153, 2168-0002, 2016-0641, 2162-0002, 2155-8007), чтобы узнать сбойный модуль ОС, точную причину и пошаговую инструкцию по устранению.",
                "Совет: Кнопка «🔄 Обновить базу» синхронизирует и сохраняет расширенный каталог релизов в локальный кэш для работы без интернета."
            )
        )
    }

    private fun buildInstructionsTab(parent: LinearLayout, th: StormTheme) {
        // 1. Шапка и живой поиск по базе знаний (20 подробных разделов + интерактивный симулятор)
        val headerCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        headerCard.addView(makeSectionHeader(
            "📖 Полная интерактивная инструкция и база знаний STORM SWITCH BOX",
            "Подробное руководство по всем функциям, алгоритмам сшивки Мульти-контента 4-в-1, блочному сжатию NCZBLOCK, форматам Switch/3DS и встроенный интерактивный симулятор",
            th
        ))

        var currentCategoryFilter = "Все разделы"
        var currentSearchQuery = ""
        val topicsListContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }

        val searchRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(4)
                bottomMargin = dp(6)
            }
        }
        val searchEdit = EditText(this).apply {
            hint = "🔎 Поиск по инструкции (напр. NCZBLOCK, Мульти-контент, 3DS, NACP, иконка, DBI, моды)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        }
        searchRow.addView(searchEdit)
        searchRow.addView(makeButton("✕", th.bgElevated, th.textSecondary) {
            searchEdit.setText("")
        })
        headerCard.addView(searchRow)

        val catScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
        }
        val catRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val categories = listOf("Все разделы", "Основы и Сборка", "Форматы Switch и 3DS", "Моды и NACP", "Сеть, Ключи и База")

        fun renderInstructionTopics() {
            topicsListContainer.removeAllViews()
            val q = currentSearchQuery.trim().lowercase()
            val filtered = instructionTopics.filter { item ->
                val catOk = (currentCategoryFilter == "Все разделы" || item.category == currentCategoryFilter)
                val qOk = q.isEmpty() || item.title.lowercase().contains(q) || item.description.lowercase().contains(q) || item.proTip.lowercase().contains(q) || item.badge.lowercase().contains(q)
                catOk && qOk
            }

            if (filtered.isEmpty()) {
                topicsListContainer.addView(TextView(this).apply {
                    text = "По запросу «$currentSearchQuery» разделы не найдены. Попробуйте другое ключевое слово."
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    setPadding(dp(4), dp(8), dp(4), dp(8))
                })
                return
            }

            for (topic in filtered) {
                val card = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgCard, th.border, 12)
                    setPadding(dp(14), dp(12), dp(14), dp(12))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(8)
                    }
                }

                val topBadgeRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(4)
                    }
                }
                topBadgeRow.addView(TextView(this).apply {
                    text = "[${topic.badge}]"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, dp(8), 0)
                })
                topBadgeRow.addView(TextView(this).apply {
                    text = topic.category
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                })
                card.addView(topBadgeRow)

                card.addView(TextView(this).apply {
                    text = topic.title
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 13.5f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, 0, dp(6))
                })

                card.addView(TextView(this).apply {
                    text = topic.description
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                    setLineSpacing(dp(2).toFloat(), 1.05f)
                })

                val tipBox = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgPrimary, th.accent, 8)
                    setPadding(dp(10), dp(7), dp(10), dp(7))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(8)
                    }
                }
                tipBox.addView(TextView(this).apply {
                    text = "💡 ${topic.proTip}"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    typeface = Typeface.DEFAULT_BOLD
                })
                card.addView(tipBox)

                topicsListContainer.addView(card)
            }
        }

        fun rebuildCategoryButtons() {
            catRow.removeAllViews()
            categories.forEach { cat ->
                val isSel = (cat == currentCategoryFilter)
                val b = makeButton(
                    cat,
                    if (isSel) th.accent else th.bgElevated,
                    if (isSel) Color.WHITE else th.textPrimary
                ) {
                    currentCategoryFilter = cat
                    rebuildCategoryButtons()
                    renderInstructionTopics()
                }
                b.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    marginEnd = dp(6)
                }
                catRow.addView(b)
            }
        }
        rebuildCategoryButtons()
        catScroll.addView(catRow)
        headerCard.addView(catScroll)
        parent.addView(headerCard)

        searchEdit.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                currentSearchQuery = s?.toString() ?: ""
                renderInstructionTopics()
            }
        })

        // 2. Интерактивный симулятор сшивки Мульти-контента 4-в-1 и расчёта сжатия NCZBLOCK (как в ПК-версии InstructionPage)
        val simCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 12)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        simCard.addView(makeSectionHeader(
            "🎛 Интерактивный симулятор: Умная сшивка 4-в-1 и блочное сжатие NCZBLOCK",
            "Нажмите на пресет игры ниже, чтобы наглядно проверить, как алгоритм группирует файлы, отсекает старые патчи и сжимает итоговый контейнер",
            th
        ))

        val simOutputTxt = TextView(this).apply {
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            typeface = Typeface.MONOSPACE
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }

        fun runSimulationPreset(presetIdx: Int) {
            simOutputTxt.text = when (presetIdx) {
                0 -> buildString {
                    appendLine("🎮 Пресет #1: Animal Well [010020D01AD24000]")
                    appendLine("────────────────────────────────────────────")
                    appendLine("Входные файлы раздачи:")
                    appendLine("  1. [БАЗА] Animal Well [010020D01AD24000][v0].nsp — 54,28 МБ")
                    appendLine("  2. [ПАТЧ] Animal Well [010020D01AD24800][v1.0.2].nsp — 19,07 МБ")
                    appendLine("  3. [МОД]  Animal_Well_RUS_Mod.zip (LayeredFS romfs) — 1,40 МБ")
                    appendLine("Этапы конвейера STORM SWITCH BOX:")
                    appendLine("  ✔ Шаг 1: Разворачивание PFS0 -> 9 плоских разделов (.nca/.tik/.cert)")
                    appendLine("  ✔ Шаг 2: Сохранение БАЗЫ (54,28 МБ) + ПАТЧА (19,07 МБ) + упаковка МОДА в RomFS NCA")
                    appendLine("  ✔ Шаг 3: Запись кастомного названия в control.nacp (16 языков) + иконка 256×256 JPEG")
                    append("  🎉 ИТОГ: Animal Well [v1.0.2] [MULTI].nsz • 74,75 МБ -> 46,95 МБ (сжатие -37%)")
                }
                1 -> buildString {
                    appendLine("🎮 Пресет #2: The Legend of Zelda: Echoes of Wisdom (Дедупликация патчей)")
                    appendLine("────────────────────────────────────────────")
                    appendLine("Входные файлы в очереди:")
                    appendLine("  1. [БАЗА] Zelda Echoes of Wisdom [01008CF01BAAC000][v0].xci — 6,00 ГБ (из них 0,48 ГБ пустых 0xFF)")
                    appendLine("  2. [ПАТЧ] Update v1.0.1 [01008CF01BAAC800].nsp — 112,00 МБ (Устаревший -> Пропущен автоматически)")
                    appendLine("  3. [ПАТЧ] Update v1.0.2 [01008CF01BAAC800].nsp — 184,50 МБ (Актуальный -> Включён в сшивку)")
                    appendLine("Этапы конвейера STORM SWITCH BOX:")
                    appendLine("  ✔ Шаг 1: SmartShrink XCI отсекает 480 МБ пустых байт 0xFF (6,00 ГБ -> 5,52 ГБ)")
                    appendLine("  ✔ Шаг 2: Дедупликация отбрасывает старый патч v1.0.1 и сшивает Базу + Патч v1.0.2")
                    append("  🎉 ИТОГ: Zelda Echoes of Wisdom [v1.0.2] [MULTI].nsz • 5,70 ГБ -> 3,48 ГБ (экономия 2,52 ГБ / -41%)")
                }
                else -> buildString {
                    appendLine("🎮 Пресет #3: Nintendo 3DS Картридж -> Расшифрованный CCI (Azahar / Citra)")
                    appendLine("────────────────────────────────────────────")
                    appendLine("Входной файл:")
                    appendLine("  1. [3DS] Metroid Samus Returns (EUR).3ds — 1,00 ГБ (ёмкость чипа 1 ГБ)")
                    appendLine("Этапы конвейера STORM SWITCH BOX:")
                    appendLine("  ✔ Шаг 1: Анализ 8 разделов таблицы NCSD (0x120..0x15F) -> отсечение 312 МБ пустых 0xFF")
                    appendLine("  ✔ Шаг 2: 128-битный KeyScrambler + установка флага NCCH NoCrypto (0x04 по +0x18F)")
                    append("  🎉 ИТОГ: Metroid Samus Returns_decrypted.cci • 1,00 ГБ -> 688,00 МБ (экономия 312 МБ)")
                }
            }
        }

        val simBtnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }
        listOf(
            "Animal Well (База+Патч+Мод)" to 0,
            "Zelda (Trim + Дедупликация)" to 1,
            "Nintendo 3DS -> CCI" to 2
        ).forEachIndexed { idx, (label, pId) ->
            simBtnRow.addView(makeButton(label, th.bgElevated, th.accent) {
                runSimulationPreset(pId)
            }.apply {
                layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                    if (idx < 2) marginEnd = dp(6)
                }
            })
        }
        simCard.addView(simBtnRow)
        simCard.addView(simOutputTxt)
        runSimulationPreset(0)
        parent.addView(simCard)

        // 3. Список карточек всех 20 разделов инструкции
        renderInstructionTopics()
        parent.addView(topicsListContainer)
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
                        customSwitch2OutputDir.listFiles()?.let { list.addAll(it) }
                        custom3dsOutputDir.listFiles()?.let { list.addAll(it) }
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

        build3dsFbiQrInstallCard(parent, th, ip)
        buildFtpConsoleSenderCard(parent, th, ip)
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

        val (maxHosFw, _) = StormNczEngine.analyzeSwitchKeysMasterRevisions(prodKeysFile)
        val keyStatusTxt = TextView(this).apply {
            text = buildString {
                appendLine("• Статус prod.keys: ${if (prodKeysFile.exists()) "🟢 Активен (Версия: $headerVer • $prodLines ключей)" else "⚪ Не найден"}")
                appendLine("• Поддержка прошивок (Master Key): ${if (prodKeysFile.exists()) "🟢 До $maxHosFw" else "⚪ Ожидание ключей"}")
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
        parent.addView(buildOutputFoldersCard(th))

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
            "2. Обновление STORM SWITCH BOX (GitHub и STORM GAMES WORLD)",
            "Текущая версия: ${try { packageManager.getPackageInfo(packageName, 0).versionName ?: "6.7.1" } catch (_: Exception) { "6.7.1" }} (${(if (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) "legacy" else "modern").uppercase()}) • Проверка на 2 официальных площадках",
            th
        ))
        updCard.addView(makeStormCheckBox(
            "Автоматически проверять наличие новой версии на GitHub и STORM GAMES WORLD при запуске",
            autoCheckUpdatesOnStartup,
            th
        ) { checked ->
            autoCheckUpdatesOnStartup = checked
            saveSettings()
        })
        updCard.addView(makeButton("🔄 Проверить обновления (GitHub + STORM GAMES WORLD)", th.accent) {
            checkForUpdatesFromGitHub(isManualClick = true)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        })
        val updLinksRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
            }
        }
        updLinksRow.addView(makeButton("🌐 Открыть GitHub", th.bgElevated, th.textPrimary) {
            try {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://github.com/ReiKatari/STORM_SWITCH_BOX")))
            } catch (_: Exception) {}
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })
        updLinksRow.addView(makeButton("🚀 STORM GAMES WORLD", th.bgElevated, th.accent) {
            try {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://stormgamesworld.ru/programs?repo=STORM_SWITCH_BOX")))
            } catch (_: Exception) {}
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        updCard.addView(updLinksRow)
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
            REQ_PICK_OUTPUT_FOLDER_TREE -> {
                data.data?.let { treeUri ->
                    val folder = resolveTreeUriToDirectory(treeUri)
                    if (folder != null) {
                        folder.mkdirs()
                        if (folder.exists()) {
                            customOutputDir = folder
                            saveSettings()
                            buildUserInterface()
                            Toast.makeText(this, "✅ Выходная папка: ${folder.absolutePath}", Toast.LENGTH_LONG).show()
                        }
                    }
                }
            }
            REQ_PICK_CUSTOM_ICON -> {
                data.data?.let { uri ->
                    try {
                        val iconsDir = File(stormRoot, "settings/icons")
                        iconsDir.mkdirs()
                        val outIconFile = File(iconsDir, "custom_icon_${System.currentTimeMillis()}.jpg")
                        contentResolver.openInputStream(uri)?.use { input ->
                            FileOutputStream(outIconFile).use { output -> input.copyTo(output) }
                        }
                        if (outIconFile.exists() && outIconFile.length() > 0L) {
                            customIconImagePath = outIconFile.absolutePath
                            onCustomIconPickedCallback?.invoke(outIconFile.absolutePath)
                            Toast.makeText(this, "🖼 Иконка загружена и отмасштабирована в 256×256 JPEG!", Toast.LENGTH_SHORT).show()
                        }
                    } catch (e: Exception) {
                        Toast.makeText(this, "Ошибка загрузки изображения: ${e.message}", Toast.LENGTH_LONG).show()
                    }
                }
            }
        }
    }

    private fun openCustomIconImagePicker(onPicked: (String) -> Unit) {
        onCustomIconPickedCallback = onPicked
        try {
            val intent = Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                addCategory(Intent.CATEGORY_OPENABLE)
                type = "image/*"
            }
            startActivityForResult(Intent.createChooser(intent, "Выберите иконку или обложку (PNG / JPG / WEBP)"), REQ_PICK_CUSTOM_ICON)
        } catch (e: Exception) {
            Toast.makeText(this, "Не удалось открыть галерею изображений: ${e.message}", Toast.LENGTH_SHORT).show()
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
        if (StormNczEngine.isSwitch2DumpDirectory(folder)) {
            if (queuedFiles.none { it.canonicalPath == folder.canonicalPath }) {
                queuedFiles.add(folder)
                if (selectedInputFile == null) selectedInputFile = folder
                return 1
            }
            return 0
        }
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "nca", "ncz", "3ds", "cci", "cia", "cxi", "cfa", "3dsx", "nds", "zip", "rar", "7z", "ips", "pchtxt")
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
        val validExts = setOf("nsp", "nsz", "xci", "xcz", "nca", "ncz", "3ds", "cci", "cia", "cxi", "cfa", "3dsx", "nds", "zip", "rar", "7z", "ips", "pchtxt")

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }

        root.addView(TextView(this).apply {
            text = "📂 Мультивыбор файлов и папок"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
            setPadding(0, 0, 0, dp(4))
        })

        val pathTxt = TextView(this).apply {
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            typeface = Typeface.DEFAULT_BOLD
        }
        root.addView(pathTxt)

        val quickScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setPadding(0, dp(6), 0, dp(6))
        }
        val quickRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val listContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        fun refreshDir() {
            pathTxt.text = "Текущая папка: ${currentDir.absolutePath}"
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
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(300))
            addView(listContainer)
        }
        root.addView(scroll)

        refreshDir()

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(root))
                .setCancelable(true)
        )

        btnRow.addView(makeButton("Отмена", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(8)
            }
        })

        btnRow.addView(makeButton("✅ Добавить выбранное", th.accent) {
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
            dlg.dismiss()
        })
        root.addView(btnRow)
    }

    /**
     * Стилизованный проводник выбора выходной папки сохранения (вместо ручного ввода пути)
     * с навигацией по папкам, созданием новой папки и кнопкой системного проводника Android.
     */
    private fun showSelectOutputFolderDialog(targetMode: Int = 0) {
        val th = themes[currentThemeIndex]
        val targetName = when (targetMode) {
            1 -> "Nintendo Switch 2 (roms_switch2)"
            2 -> "Nintendo 3DS (roms_3ds)"
            else -> "Nintendo Switch (output)"
        }
        val defaultTargetDir = when (targetMode) {
            1 -> customSwitch2OutputDir
            2 -> custom3dsOutputDir
            else -> customOutputDir
        }
        var currentDir = if (defaultTargetDir.exists() && defaultTargetDir.isDirectory) defaultTargetDir else File("/storage/emulated/0")

        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }

        card.addView(TextView(this).apply {
            text = "📁 Выберите выходную папку сохранения ($targetName)"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })

        val currentPathBox = TextView(this).apply {
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            typeface = Typeface.DEFAULT_BOLD
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(8), dp(10), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
                bottomMargin = dp(6)
            }
        }
        card.addView(currentPathBox)

        val quickScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            setPadding(0, dp(2), 0, dp(6))
        }
        val quickRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val dirsListLayout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }

        var dlgRef: AlertDialog? = null

        fun refreshExplorer() {
            currentPathBox.text = "📂 Выбрана папка:\n${currentDir.absolutePath}"
            dirsListLayout.removeAllViews()

            currentDir.parentFile?.let { parentDir ->
                if (parentDir.canRead()) {
                    dirsListLayout.addView(makeButton("⬆️ .. (На уровень выше: ${parentDir.name})", th.bgElevated, th.textPrimary) {
                        currentDir = parentDir
                        refreshExplorer()
                    }.apply {
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            bottomMargin = dp(6)
                        }
                    })
                }
            }

            val subDirs = currentDir.listFiles()?.filter { it.isDirectory }?.sortedBy { it.name.lowercase() } ?: emptyList()
            if (subDirs.isEmpty()) {
                dirsListLayout.addView(TextView(this).apply {
                    text = "В этой папке нет вложенных подпапок.\nНажмите «✅ Выбрать эту папку» ниже или создайте новую подпапку."
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    setPadding(dp(4), dp(8), dp(4), dp(8))
                })
            } else {
                for (sub in subDirs) {
                    val row = LinearLayout(this).apply {
                        orientation = LinearLayout.HORIZONTAL
                        gravity = Gravity.CENTER_VERTICAL
                        background = makeCardDrawable(th.bgPrimary, th.border, 8)
                        setPadding(dp(10), dp(8), dp(10), dp(8))
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            bottomMargin = dp(4)
                        }
                        setOnClickListener {
                            currentDir = sub
                            refreshExplorer()
                        }
                    }
                    row.addView(TextView(this).apply {
                        text = "📁 ${sub.name}"
                        setTextColor(th.accent)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.2f)
                        typeface = Typeface.DEFAULT_BOLD
                        layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                    })
                    row.addView(TextView(this).apply {
                        text = "Открыть ▸"
                        setTextColor(th.textSecondary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                    })
                    dirsListLayout.addView(row)
                }
            }
        }

        listOf(
            "По умолчанию (output)" to File(stormRoot, "output"),
            "STORM SWITCH BOX/roms" to romsDir,
            "STORM SWITCH/roms" to File("/storage/emulated/0/STORM SWITCH/roms"),
            "Download" to File("/storage/emulated/0/Download"),
            "Внутренняя память" to File("/storage/emulated/0")
        ).forEach { (label, targetDir) ->
            val b = makeButton(label, th.bgElevated, th.textPrimary) {
                targetDir.mkdirs()
                currentDir = targetDir
                refreshExplorer()
            }
            b.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
            quickRow.addView(b)
        }
        quickScroll.addView(quickRow)
        card.addView(quickScroll)

        // Ряд создания новой подпапки и вызова системного проводника Android
        val newFolderRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        val newFolderInput = EditText(this).apply {
            hint = "Имя новой папки..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.5f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply {
                marginEnd = dp(6)
            }
        }
        newFolderRow.addView(newFolderInput)
        newFolderRow.addView(makeButton("➕ Создать", th.bgElevated, th.accent) {
            val name = newFolderInput.text.toString().trim().replace(Regex("""[\\/:*?"<>|]"""), "")
            if (name.isNotEmpty()) {
                val created = File(currentDir, name)
                created.mkdirs()
                if (created.exists()) {
                    newFolderInput.setText("")
                    currentDir = created
                    refreshExplorer()
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })
        newFolderRow.addView(makeButton("🗂 Системный", th.bgElevated, th.textPrimary) {
            try {
                dlgRef?.dismiss()
                val intent = Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
                startActivityForResult(intent, REQ_PICK_OUTPUT_FOLDER_TREE)
            } catch (_: Exception) {
                Toast.makeText(this, "Используйте встроенный проводник папок ниже", Toast.LENGTH_SHORT).show()
            }
        })
        card.addView(newFolderRow)

        val scroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(240))
            addView(dirsListLayout)
        }
        card.addView(scroll)

        refreshExplorer()

        val bottomRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )
        dlgRef = dlg

        bottomRow.addView(makeButton("Отмена", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        bottomRow.addView(makeButton("По умолчанию", th.bgElevated, th.textSecondary) {
            when (targetMode) {
                1 -> { customSwitch2OutputDir = File(stormRoot, "roms_switch2"); customSwitch2OutputDir.mkdirs() }
                2 -> { custom3dsOutputDir = File(stormRoot, "roms_3ds"); custom3dsOutputDir.mkdirs() }
                else -> { customOutputDir = File(stormRoot, "output"); customOutputDir.mkdirs() }
            }
            saveSettings()
            buildUserInterface()
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        bottomRow.addView(makeButton("✅ Выбрать эту папку", th.accent) {
            currentDir.mkdirs()
            if (currentDir.exists()) {
                when (targetMode) {
                    1 -> customSwitch2OutputDir = currentDir
                    2 -> custom3dsOutputDir = currentDir
                    else -> customOutputDir = currentDir
                }
                saveSettings()
                buildUserInterface()
                Toast.makeText(this, "Папка $targetName: ${currentDir.absolutePath}", Toast.LENGTH_SHORT).show()
            }
            dlg.dismiss()
        })
        card.addView(bottomRow)
    }

    // =========================================================================
    // АВТОМАТИЧЕСКАЯ ПРОВЕРКА ОБНОВЛЕНИЙ И УСТАНОВКА APK С GITHUB
    // =========================================================================

    private fun checkForUpdatesFromGitHub(isManualClick: Boolean) {
        if (isManualClick) {
            Toast.makeText(this, "⏳ Проверка обновлений на GitHub и STORM GAMES WORLD...", Toast.LENGTH_SHORT).show()
        }
        val preferLegacy = (android.os.Build.SUPPORTED_64_BIT_ABIS.isEmpty()) || Build.VERSION.SDK_INT < 26
        val currentAppVer = try { packageManager.getPackageInfo(packageName, 0).versionName ?: "6.7.1" } catch (_: Exception) { "6.7.1" }
        thread {
            val rel = RuTrackerCloudEngine.checkGitHubAppUpdate(currentAppVer, preferLegacy)
            mainHandler.post {
                if (rel != null) {
                    showUpdateAvailableDialog(rel)
                } else if (isManualClick) {
                    Toast.makeText(
                        this,
                        "✅ У вас установлена актуальная версия STORM SWITCH BOX $currentAppVer (проверено на GitHub и STORM GAMES WORLD)!",
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
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }
        box.addView(TextView(this).apply {
            text = "🚀 Доступно обновление STORM SWITCH BOX"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
        })
        box.addView(TextView(this).apply {
            val installedVer = try { packageManager.getPackageInfo(packageName, 0).versionName ?: "6.7.1" } catch (_: Exception) { "6.7.1" }
            text = "Новая версия: ${release.version} (Установлена: $installedVer)\nИсточник: ${release.sourceName}\nПакет: ${release.apkName} (${StormNczEngine.formatSize(release.apkSizeBytes)})"
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

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(box))
                .setCancelable(true)
        )

        btnRow.addView(makeButton("Позже", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        btnRow.addView(makeButton("🌐 STORM GAMES WORLD", th.bgElevated, th.accent) {
            try {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(release.stormWorldPageUrl)))
            } catch (_: Exception) {}
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(6)
            }
        })

        btnRow.addView(makeButton("⬇️ Скачать и обновить", th.accent) {
            dlg.dismiss()
            startApkUpdateDownload(release)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT)
        })
        box.addView(btnRow)
    }

    private fun startApkUpdateDownload(release: RuTrackerCloudEngine.AppUpdateRelease) {
        val th = themes[currentThemeIndex]
        val cancelled = java.util.concurrent.atomic.AtomicBoolean(false)

        val box = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }
        box.addView(TextView(this).apply {
            text = "⬇️ Загрузка обновления ${release.version}"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            typeface = Typeface.DEFAULT_BOLD
            setPadding(0, 0, 0, dp(6))
        })
        val statusLabel = TextView(this).apply {
            text = "Подготовка к скачиванию ${release.apkName}..."
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 12.2f)
        }
        box.addView(statusLabel)

        val pb = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(16)).apply {
                topMargin = dp(10)
                bottomMargin = dp(10)
            }
        }
        box.addView(pb)

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
        }

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(box))
                .setCancelable(false)
        )

        btnRow.addView(makeButton("Отмена", th.bgElevated, th.textPrimary) {
            cancelled.set(true)
            dlg.dismiss()
        })
        box.addView(btnRow)

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
                        val speedStr = String.format(Locale("ru", "RU"), "%.2f МБ/с", speedMb)
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

    // =========================================================================
    // НОВЫЕ РАЗДЕЛЫ И ДИАЛОГИ 6.0.1:
    // 1. ВИРТУАЛЬНЫЙ ПРОВОДНИК ROMFS / EXEFS (HEX + UTF-8)
    // 2. МАСТЕР РАСПАКОВКИ И ОБРАТНОЙ УПАКОВКИ ПАПКИ В NSP / NSZ
    // 3. ГЕНЕРАТОР HOMEBREW FORWARDER (.NRO -> .NSP) С ПОДДЕРЖКОЙ RUTRACKER
    // 4. QR-СЕРВЕР ДЛЯ NINTENDO 3DS (FBI) И ВСТРОЕННЫЙ FTP-КЛИЕНТ
    // 5. ВКЛАДКА «📚 БАЗА TITLEDB И КОДЫ ОШИБОК SWITCH (2XXX-XXXX)»
    // =========================================================================

    private fun showVirtualRomfsExplorerDialog(containerFile: File, th: StormTheme) {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(14), dp(12), dp(14), dp(12))
        }
        card.addView(makeSectionHeader(
            "🌲 Виртуальный проводник RomFS / ExeFS / PFS0 и HEX-инспектор",
            "Контейнер: ${containerFile.name} (${StormNczEngine.formatSize(containerFile.length())}) • Просмотр и извлечение без полной распаковки",
            th
        ))

        val allNodes = StormNczEngine.inspectVirtualRomfsTree(containerFile)
        var selectedNode: StormNczEngine.VirtualFsNode = allNodes.firstOrNull() ?: StormNczEngine.VirtualFsNode(
            virtualPath = "/romfs/config.ini",
            displayName = "📄 /romfs/config.ini",
            categoryBadge = "RomFS",
            size = 256L,
            containerOffset = 0L
        )
        var viewHexMode = true

        val filterInput = EditText(this).apply {
            hint = "🔍 Фильтр по дереву файлов (.nca, .npdm, .nacp, config.ini, romfs, exefs)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(7), dp(10), dp(7))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        card.addView(filterInput)

        val treeScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(165)).apply {
                bottomMargin = dp(8)
            }
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(6), dp(6), dp(6), dp(6))
        }
        val treeListLayout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        treeScroll.addView(treeListLayout)
        card.addView(treeScroll)

        val modeRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }

        val viewerScroll = ScrollView(this).apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(185)).apply {
                bottomMargin = dp(8)
            }
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(8), dp(6), dp(8), dp(6))
        }
        val hexAndTextOutput = TextView(this).apply {
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.5f)
            typeface = Typeface.MONOSPACE
        }
        viewerScroll.addView(hexAndTextOutput)

        fun updateInspectorView() {
            val (hexStr, txtStr) = StormNczEngine.inspectVirtualNodeHexAndText(containerFile, selectedNode, 2048)
            hexAndTextOutput.text = if (viewHexMode) hexStr else txtStr
        }

        fun renderTreeNodes(q: String) {
            treeListLayout.removeAllViews()
            val cleanQ = q.trim().lowercase()
            val filtered = if (cleanQ.isEmpty()) allNodes else allNodes.filter {
                it.virtualPath.lowercase().contains(cleanQ) || it.displayName.lowercase().contains(cleanQ) || it.categoryBadge.lowercase().contains(cleanQ)
            }
            filtered.forEach { node ->
                val isSel = node.virtualPath == selectedNode.virtualPath
                val row = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                    background = makeCardDrawable(
                        if (isSel) th.bgElevated else th.bgCard,
                        if (isSel) th.accent else th.border,
                        6
                    )
                    setPadding(dp(8), dp(6), dp(8), dp(6))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(4)
                    }
                    setOnClickListener {
                        selectedNode = node
                        renderTreeNodes(filterInput.text.toString())
                        updateInspectorView()
                    }
                }
                row.addView(TextView(this).apply {
                    text = "[${node.categoryBadge}]"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.5f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, dp(6), 0)
                })
                row.addView(TextView(this).apply {
                    text = node.displayName
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                })
                row.addView(TextView(this).apply {
                    text = StormNczEngine.formatSize(node.size)
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 10.8f)
                })
                treeListLayout.addView(row)
            }
        }

        modeRow.addView(makeButton("🔢 Дамп HEX + ASCII", th.bgElevated, th.accent) {
            viewHexMode = true
            updateInspectorView()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        modeRow.addView(makeButton("📄 Текст UTF-8 / Заголовок", th.bgElevated, th.textPrimary) {
            viewHexMode = false
            updateInspectorView()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        modeRow.addView(makeButton("💾 Извлечь узел", th.accent) {
            thread {
                try {
                    val outDir = File(extractedDir, containerFile.nameWithoutExtension + "_Nodes")
                    val savedFile = StormNczEngine.extractSingleVirtualNode(containerFile, selectedNode, outDir)
                    appendLog("✅ Извлечён узел ${selectedNode.virtualPath} -> ${savedFile.absolutePath}")
                    mainHandler.post {
                        Toast.makeText(this, "Сохранено: ${savedFile.absolutePath}", Toast.LENGTH_LONG).show()
                    }
                } catch (e: Exception) {
                    appendLog("❌ Ошибка извлечения узла: ${e.message}")
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })

        card.addView(modeRow)
        card.addView(viewerScroll)

        filterInput.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                renderTreeNodes(s?.toString() ?: "")
            }
        })

        renderTreeNodes("")
        updateInspectorView()

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )
        card.addView(makeButton("Закрыть проводник", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        })
    }

    private fun showUnpackSummaryDialog(sourceName: String, rep: StormNczEngine.UnpackRomfsReport, th: StormTheme) {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }
        card.addView(makeSectionHeader(
            "✅ Распаковка RomFS и ExeFS завершена!",
            "Исходный контейнер: $sourceName • Извлечено файлов: ${rep.extractedFilesCount} (${StormNczEngine.formatSize(rep.totalExtractedBytes)})",
            th
        ))
        card.addView(TextView(this).apply {
            text = buildString {
                appendLine("📂 Корневая папка распаковки:")
                appendLine("   ${rep.outputDir.absolutePath}")
                appendLine("────────────────────────────────────────────")
                rep.details.forEach { appendLine(it) }
                appendLine("────────────────────────────────────────────")
                append("💡 Совет: Вы можете изменить файлы перевода/конфигурации в папке romfs/ и сразу собрать игру обратно кнопкой «📦 Упаковать папку в NSP/NSZ»!")
            }
            setTextColor(th.textPrimary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
            setPadding(0, dp(6), 0, dp(10))
        })

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )
        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
        }
        btnRow.addView(makeButton("📦 Сразу упаковать обратно в NSP/NSZ", th.accent) {
            dlg.dismiss()
            showPackFolderToNspDialog(th, rep.outputDir)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(8)
            }
        })
        btnRow.addView(makeButton("Закрыть", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        })
        card.addView(btnRow)
    }

    private fun showPackFolderToNspDialog(th: StormTheme, preselectedFolder: File? = null) {
        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.accent, 14)
            setPadding(dp(16), dp(14), dp(16), dp(14))
        }
        card.addView(makeSectionHeader(
            "📦 Мастер упаковки папки (RomFS / ExeFS / PFS0 → NSP / NSZ)",
            "Сборка распакованной игры или папки мода обратно в установочный контейнер Switch",
            th
        ))

        val candidateFolders = mutableListOf<File>()
        preselectedFolder?.takeIf { it.exists() && it.isDirectory }?.let { candidateFolders.add(it) }
        extractedDir.listFiles()?.filter { it.isDirectory }?.forEach {
            if (candidateFolders.none { c -> c.canonicalPath == it.canonicalPath }) candidateFolders.add(it)
        }
        modsDir.listFiles()?.filter { it.isDirectory }?.forEach {
            if (candidateFolders.none { c -> c.canonicalPath == it.canonicalPath }) candidateFolders.add(it)
        }
        if (candidateFolders.isEmpty()) {
            extractedDir.mkdirs()
            candidateFolders.add(extractedDir)
        }

        var chosenFolder = candidateFolders.first()
        var targetFmt = 0

        card.addView(TextView(this).apply {
            text = "Выберите распакованную папку (с подпапками romfs/, exefs/, control/ или pfs0/):"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(4), 0, dp(4))
        })

        val folderDropdown = makeStormDropdown(
            candidateFolders.map { "${it.name} (${it.parentFile?.name ?: ""}/)" },
            0,
            th
        ) { idx ->
            chosenFolder = candidateFolders[idx]
        }
        card.addView(folderDropdown)

        card.addView(TextView(this).apply {
            text = "Целевой формат выходного контейнера:"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(8), 0, dp(4))
        })

        val fmtOptions = listOf("NSZ (Блочное сжатие NCZBLOCK)", "XCZ (Сжатый картридж)", "NSP (Без сжатия)", "XCI (Картридж без сжатия)")
        card.addView(makeStormDropdown(fmtOptions, targetFmt, th) { idx ->
            targetFmt = idx
        })

        val titleInput = EditText(this).apply {
            setText(chosenFolder.name.replace("_Unpacked", ""))
            hint = "Внутреннее название игры (необязательно)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(10), dp(7), dp(10), dp(7))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(8)
                bottomMargin = dp(8)
            }
        }
        card.addView(titleInput)

        val statusTxt = TextView(this).apply {
            text = "Готов к сборке контейнера в ${customOutputDir.absolutePath}"
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, 0, 0, dp(8))
        }
        card.addView(statusTxt)

        val dlg = showImmersiveDialog(
            AlertDialog.Builder(this)
                .setView(makeUnclippedDialogContainer(card))
                .setCancelable(true)
        )

        val btnRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.END
        }
        btnRow.addView(makeButton("Закрыть", th.bgElevated, th.textPrimary) {
            dlg.dismiss()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                marginEnd = dp(8)
            }
        })

        btnRow.addView(makeButton("🚀 Собрать и упаковать", th.accent) {
            val customName = titleInput.text.toString().trim()
            statusTxt.text = "⏳ Упаковка секций RomFS / ExeFS..."
            thread {
                try {
                    val res = StormNczEngine.packFolderToSwitchContainer(
                        inputFolder = chosenFolder,
                        outputDir = customOutputDir,
                        targetFormatMode = targetFmt,
                        useBlockNcz = (targetFmt == 0 || targetFmt == 1),
                        zstdLevel = zstdLevel,
                        blockSizeExponent = blockSizeExpo,
                        customTitle = customName
                    ) { p ->
                        mainHandler.post {
                            statusTxt.text = "⏳ ${p.stageText} (${p.percent}%)"
                        }
                    }
                    appendLog("✅ Упакована папка ${chosenFolder.name} -> ${File(res.outputPath).name} (${StormNczEngine.formatSize(res.compressedBytes)})")
                    mainHandler.post {
                        statusTxt.text = "✅ Создан файл: ${File(res.outputPath).name} (${StormNczEngine.formatSize(res.compressedBytes)})"
                        Toast.makeText(this, "Готово: ${File(res.outputPath).name}!", Toast.LENGTH_LONG).show()
                    }
                } catch (e: Exception) {
                    mainHandler.post {
                        statusTxt.text = "❌ Ошибка: ${e.message}"
                    }
                }
            }
        })
        card.addView(btnRow)
    }

    private fun buildHomebrewForwarderCard(
        parent: LinearLayout,
        th: StormTheme,
        onSearchRuTrackerHomebrew: (String) -> Unit
    ) {
        val fwdCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }
        fwdCard.addView(makeSectionHeader(
            "🚀 Генератор Homebrew Forwarder NSP (.nro → .nsp) + RuTracker Homebrew",
            "Создаёт установочный .NSP ярлык для главного экрана Switch (с полным доступом к 4 ГБ ОЗУ) из любого .nro или ретро-РОМа",
            th
        ))

        val presetsScroll = HorizontalScrollView(this).apply {
            isHorizontalScrollBarEnabled = false
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        val presetsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
        }

        data class FwdPreset(val label: String, val title: String, val author: String, val ver: String, val tid: String, val path: String)
        val presets = listOf(
            FwdPreset("⚙️ DBI Installer", "DBI Installer", "duckbill007", "715.0", "0100F0001DB10000", "sdmc:/switch/DBI/DBI.nro"),
            FwdPreset("🕹️ RetroArch", "RetroArch Switch", "Libretro Team", "1.19.1", "0100F0001EA00000", "sdmc:/switch/retroarch_switch.nro"),
            FwdPreset("💾 JKSV Saves", "JKSV Save Manager", "J-D-K", "11.05.2024", "0100F0001A570000", "sdmc:/switch/JKSV/JKSV.nro"),
            FwdPreset("🖥️ Moonlight-NX", "Moonlight-NX Stream", "XITRIX", "1.4.2", "0100F0001B000000", "sdmc:/switch/Moonlight-NX/Moonlight-NX.nro"),
            FwdPreset("🏰 Heroes III (VCMI)", "Heroes of Might and Magic III", "VCMI Team", "1.5.7", "0100F0001C330000", "sdmc:/switch/vcmi/vcmiclient.nro"),
            FwdPreset("☢️ Fallout 2 CE", "Fallout 2 Community Edition", "alexbatalov", "1.3.0", "0100F0001FA20000", "sdmc:/switch/fallout2-ce/fallout2-ce.nro")
        )

        var titleEditRef: EditText? = null
        var authorEditRef: EditText? = null
        var verEditRef: EditText? = null
        var tidEditRef: EditText? = null
        var pathEditRef: EditText? = null

        presets.forEach { p ->
            presetsRow.addView(makeButton(p.label, th.bgElevated, th.accent) {
                fwdAppTitle = p.title
                fwdAppAuthor = p.author
                fwdAppVersion = p.ver
                fwdTitleId = p.tid
                fwdSdmcNroPath = p.path
                titleEditRef?.setText(p.title)
                authorEditRef?.setText(p.author)
                verEditRef?.setText(p.ver)
                tidEditRef?.setText(p.tid)
                pathEditRef?.setText(p.path)
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    marginEnd = dp(6)
                }
            })
        }
        presetsScroll.addView(presetsRow)
        fwdCard.addView(presetsScroll)

        val topActRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        topActRow.addView(makeButton("🌐 Найти Homebrew / Порты на RuTracker", th.bgElevated, th.accent) {
            onSearchRuTrackerHomebrew("Homebrew")
            Toast.makeText(this, "Показаны раздачи Homebrew и NRO-портов в списке RuTracker выше!", Toast.LENGTH_SHORT).show()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
        })

        topActRow.addView(makeButton("📂 Выбрать .NRO из очереди/папки", th.bgElevated, th.textPrimary) {
            val nroCandidate = queuedFiles.firstOrNull { it.extension.equals("nro", true) }
                ?: romsDir.listFiles()?.firstOrNull { it.extension.equals("nro", true) }
            if (nroCandidate != null && nroCandidate.exists()) {
                val meta = StormNczEngine.parseNroMetadata(nroCandidate)
                fwdLocalNroPath = nroCandidate.absolutePath
                fwdAppTitle = meta.title
                fwdAppAuthor = meta.author
                fwdAppVersion = meta.version
                fwdSdmcNroPath = "sdmc:/switch/${nroCandidate.name}"
                titleEditRef?.setText(fwdAppTitle)
                authorEditRef?.setText(fwdAppAuthor)
                verEditRef?.setText(fwdAppVersion)
                pathEditRef?.setText(fwdSdmcNroPath)
                Toast.makeText(this, "Считан паспорт NRO0/ASET из ${nroCandidate.name}!", Toast.LENGTH_SHORT).show()
            } else {
                showMultiSelectFileAndFolderDialog()
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        })
        fwdCard.addView(topActRow)

        val titleEdit = EditText(this).apply {
            setText(fwdAppTitle)
            hint = "Название на главном экране Switch (напр. DBI Installer)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(10), dp(7), dp(10), dp(7))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { fwdAppTitle = s?.toString() ?: "" }
            })
        }
        titleEditRef = titleEdit
        fwdCard.addView(titleEdit)

        val metaRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        val authorEdit = EditText(this).apply {
            setText(fwdAppAuthor)
            hint = "Автор / Издатель"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { fwdAppAuthor = s?.toString() ?: "" }
            })
        }
        authorEditRef = authorEdit
        metaRow.addView(authorEdit)

        val verEdit = EditText(this).apply {
            setText(fwdAppVersion)
            hint = "Версия"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(dp(90), ViewGroup.LayoutParams.WRAP_CONTENT).apply { marginEnd = dp(6) }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { fwdAppVersion = s?.toString() ?: "" }
            })
        }
        verEditRef = verEdit
        metaRow.addView(verEdit)

        val tidEdit = EditText(this).apply {
            setText(fwdTitleId)
            hint = "TitleID (0100...)"
            setTextColor(th.accent)
            setHintTextColor(th.textSecondary)
            textSize = 11.5f
            typeface = Typeface.MONOSPACE
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(dp(145), ViewGroup.LayoutParams.WRAP_CONTENT)
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { fwdTitleId = s?.toString() ?: "" }
            })
        }
        tidEditRef = tidEdit
        metaRow.addView(tidEdit)
        fwdCard.addView(metaRow)

        val pathEdit = EditText(this).apply {
            setText(fwdSdmcNroPath)
            hint = "Путь к .nro на SD-карте Switch (напр. sdmc:/switch/DBI/DBI.nro)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            typeface = Typeface.MONOSPACE
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(10), dp(7), dp(10), dp(7))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { fwdSdmcNroPath = s?.toString() ?: "" }
            })
        }
        pathEditRef = pathEdit
        fwdCard.addView(pathEdit)

        val buildRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        buildRow.addView(makeButton("🎲 Новый TitleID", th.bgElevated, th.textPrimary) {
            val rnd = (0x1000000..0xFFFFFFF).random()
            fwdTitleId = "0100F%07X0000".format(rnd)
            tidEdit.setText(fwdTitleId)
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 0.9f).apply {
                marginEnd = dp(6)
            }
        })

        buildRow.addView(makeButton("🖼 Своя иконка 256×256", th.bgElevated, th.accent) {
            openCustomIconImagePicker { pickedPath ->
                fwdCustomIconPath = pickedPath
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f).apply {
                marginEnd = dp(6)
            }
        })

        buildRow.addView(makeButton("⚡ Собрать Forwarder .NSP", th.accent) {
            thread {
                try {
                    val outNsp = StormNczEngine.buildHomebrewForwarderNsp(
                        appTitle = fwdAppTitle,
                        appAuthor = fwdAppAuthor,
                        appVersion = fwdAppVersion,
                        titleIdHex = fwdTitleId,
                        sdmcNroPath = fwdSdmcNroPath,
                        romArgPath = fwdRomArgPath,
                        localNroFile = fwdLocalNroPath.takeIf { it.isNotBlank() }?.let { File(it) },
                        customIconPath = fwdCustomIconPath.ifBlank { customIconImagePath },
                        outputDir = customOutputDir
                    ) {}
                    if (outNsp.exists() && queuedFiles.none { it.absolutePath == outNsp.absolutePath }) {
                        queuedFiles.add(outNsp)
                        selectedInputFile = outNsp
                    }
                    appendLog("✅ Создан Homebrew Forwarder NSP: ${outNsp.name} (${StormNczEngine.formatSize(outNsp.length())})")
                    mainHandler.post {
                        Toast.makeText(this, "✅ Создан Forwarder: ${outNsp.name}!", Toast.LENGTH_LONG).show()
                    }
                } catch (e: Exception) {
                    appendLog("❌ Ошибка создания Forwarder NSP: ${e.message}")
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.25f)
        })
        fwdCard.addView(buildRow)
        parent.addView(fwdCard)
    }

    private fun build3dsFbiQrInstallCard(parent: LinearLayout, th: StormTheme, localIp: String) {
        val qrCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }
        qrCard.addView(makeSectionHeader(
            "📷 QR-установка .CIA для Nintendo 3DS (FBI Remote Install Server)",
            "Наведите камеру консоли Nintendo 3DS (FBI -> Remote Install -> Scan QR Code) на экран смартфона для прямой установки по Wi-Fi",
            th
        ))

        val allShareable = mutableListOf<File>().apply {
            addAll(queuedFiles)
            romsDir.listFiles()?.filter { it.isFile }?.let { addAll(it) }
            customOutputDir.listFiles()?.filter { it.isFile }?.let { addAll(it) }
        }.distinctBy { it.canonicalPath }

        val qrImageView = ImageView(this).apply {
            layoutParams = LinearLayout.LayoutParams(dp(230), dp(230)).apply {
                gravity = Gravity.CENTER_HORIZONTAL
                topMargin = dp(8)
                bottomMargin = dp(6)
            }
            visibility = View.GONE
        }
        val qrUrlTxt = TextView(this).apply {
            setTextColor(th.accent)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
            typeface = Typeface.MONOSPACE
            gravity = Gravity.CENTER_HORIZONTAL
            visibility = View.GONE
        }

        if (allShareable.isEmpty()) {
            qrCard.addView(TextView(this).apply {
                text = "Добавьте .cia или .3dsx файл в очередь (или в папку output/), чтобы сгенерировать QR-код для камеры Nintendo 3DS."
                setTextColor(th.textSecondary)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.8f)
                setPadding(0, dp(4), 0, dp(8))
            })
        } else {
            selectedQrFileIndex = selectedQrFileIndex.coerceIn(0, allShareable.size - 1)
            qrCard.addView(makeStormDropdown(
                allShareable.map { "${it.name} (${StormNczEngine.formatSize(it.length())})" },
                selectedQrFileIndex,
                th
            ) { idx ->
                selectedQrFileIndex = idx
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    bottomMargin = dp(8)
                }
            })
        }

        qrCard.addView(makeButton("📷 Показать QR-код для установки через FBI (Nintendo 3DS)", th.accent) {
            if (!dbiServerRunning) {
                dbiServer = RuTrackerCloudEngine.DbiHttpServer(
                    port = 8080,
                    sharedFiles = {
                        val list = mutableListOf<File>()
                        list.addAll(queuedFiles)
                        romsDir.listFiles()?.let { list.addAll(it) }
                        customOutputDir.listFiles()?.let { list.addAll(it) }
                        customSwitch2OutputDir.listFiles()?.let { list.addAll(it) }
                        custom3dsOutputDir.listFiles()?.let { list.addAll(it) }
                        list.distinctBy { it.canonicalPath }
                    },
                    onLog = { msg -> appendLog("[3DS QR HTTP] $msg") }
                )
                dbiServer?.start()
                dbiServerRunning = true
            }
            val chosenFile = allShareable.getOrNull(selectedQrFileIndex)
            val encodedName = java.net.URLEncoder.encode(chosenFile?.name ?: "game.cia", "UTF-8").replace("+", "%20")
            val directUrl = "http://$localIp:8080/files/${selectedQrFileIndex}_$encodedName"
            val qrBmp = StormNczEngine.generateQrCodeBitmap(directUrl, scale = 9)
            qrImageView.setImageBitmap(qrBmp)
            qrImageView.visibility = View.VISIBLE
            qrUrlTxt.text = "🔗 Прямая ссылка для FBI: $directUrl\nОткройте на 3DS: FBI -> Remote Install -> Scan QR Code"
            qrUrlTxt.visibility = View.VISIBLE
            appendLog("📷 Сгенерирован QR-код FBI для ${chosenFile?.name ?: "game.cia"}: $directUrl")
        })

        qrCard.addView(qrImageView)
        qrCard.addView(qrUrlTxt)
        parent.addView(qrCard)
    }

    private fun buildFtpConsoleSenderCard(parent: LinearLayout, th: StormTheme, localIp: String) {
        val ftpCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(10)
            }
        }
        ftpCard.addView(makeSectionHeader(
            "⚡ Встроенный FTP-клиент (Прямая отправка на Switch sys-ftpd / DBI и 3DS FTPD)",
            "Передача собранных игр (.nsp/.nsz/.xci/.cia), форвардеров и модов LayeredFS напрямую на SD-карту консоли по Wi-Fi",
            th
        ))

        val hostRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        val ipInput = EditText(this).apply {
            setText(ftpConsoleIp)
            hint = "IP консоли (192.168.1.50)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { ftpConsoleIp = s?.toString() ?: "" }
            })
        }
        hostRow.addView(ipInput)

        val portInput = EditText(this).apply {
            setText(ftpConsolePort.toString())
            hint = "Порт (5000)"
            setTextColor(th.accent)
            setHintTextColor(th.textSecondary)
            textSize = 12f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(dp(80), ViewGroup.LayoutParams.WRAP_CONTENT).apply { marginEnd = dp(6) }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) {
                    ftpConsolePort = s?.toString()?.toIntOrNull() ?: 5000
                }
            })
        }
        hostRow.addView(portInput)

        val ftpStatusTxt = TextView(this).apply {
            text = "Готов к передаче по FTP (Порт 5000 — стандарт sys-ftpd / DBI / 3DS FTPD)"
            setTextColor(th.textSecondary)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
            setPadding(0, dp(4), 0, dp(6))
        }

        hostRow.addView(makeButton("🔍 Найти в Wi-Fi", th.bgElevated, th.accent) {
            ftpStatusTxt.text = "⏳ Сканирование Wi-Fi подсети $localIp на наличие FTP-сервера (порты 5000 и 21)..."
            thread {
                val found = StormNczEngine.discoverFtpConsolesInLan(localIp)
                mainHandler.post {
                    if (found.isNotEmpty()) {
                        val (discIp, discPort) = found.first()
                        ftpConsoleIp = discIp
                        ftpConsolePort = discPort
                        ipInput.setText(discIp)
                        portInput.setText(discPort.toString())
                        ftpStatusTxt.text = "🟢 Найдена консоль в сети: $discIp:$discPort"
                    } else {
                        ftpStatusTxt.text = "⚪ Активный FTP-сервер в подсети не обнаружен (проверьте, что на консоли запущен ftpd или DBI FTP)."
                    }
                }
            }
        })
        ftpCard.addView(hostRow)

        val dirInput = EditText(this).apply {
            setText(ftpRemoteDir)
            hint = "Папка на SD-карте консоли (напр. /switch/ или /cia/)"
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 11.8f
            typeface = Typeface.MONOSPACE
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 6)
            setPadding(dp(8), dp(6), dp(8), dp(6))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
            addTextChangedListener(object : android.text.TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
                override fun afterTextChanged(s: android.text.Editable?) { ftpRemoteDir = s?.toString() ?: "/switch/" }
            })
        }
        ftpCard.addView(dirInput)

        val dirChipsRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(8)
            }
        }
        listOf("/switch/", "/cia/", "/roms/", "/atmosphere/contents/").forEach { dPath ->
            dirChipsRow.addView(makeButton(dPath, th.bgElevated, th.textPrimary) {
                ftpRemoteDir = dPath
                dirInput.setText(dPath)
            }.apply {
                layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(4) }
            })
        }
        ftpCard.addView(dirChipsRow)

        val allFiles = mutableListOf<File>().apply {
            addAll(queuedFiles)
            customOutputDir.listFiles()?.filter { it.isFile }?.let { addAll(it) }
            romsDir.listFiles()?.filter { it.isFile }?.let { addAll(it) }
        }.distinctBy { it.canonicalPath }

        if (allFiles.isNotEmpty()) {
            selectedFtpFileIndex = selectedFtpFileIndex.coerceIn(0, allFiles.size - 1)
            ftpCard.addView(makeStormDropdown(
                allFiles.map { "${it.name} (${StormNczEngine.formatSize(it.length())})" },
                selectedFtpFileIndex,
                th
            ) { idx ->
                selectedFtpFileIndex = idx
            }.apply {
                layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                    bottomMargin = dp(8)
                }
            })
        }

        val ftpProgress = ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal).apply {
            max = 100
            progress = 0
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(12))
        }
        ftpCard.addView(ftpProgress)
        ftpCard.addView(ftpStatusTxt)

        ftpCard.addView(makeButton("🚀 Отправить выбранный файл по FTP на консоль", th.accent) {
            val targetFile = allFiles.getOrNull(selectedFtpFileIndex)
            if (targetFile == null || !targetFile.exists()) {
                Toast.makeText(this, "Сначала добавьте файл в очередь или папку output/!", Toast.LENGTH_SHORT).show()
                return@makeButton
            }
            ftpStatusTxt.text = "⏳ Подключение к $ftpConsoleIp:$ftpConsolePort..."
            thread {
                val (ok, msg) = StormNczEngine.uploadFileViaFtpToConsole(
                    host = ftpConsoleIp,
                    port = ftpConsolePort,
                    remoteDir = ftpRemoteDir,
                    localFile = targetFile
                ) { pct, pText ->
                    mainHandler.post {
                        ftpProgress.progress = pct
                        ftpStatusTxt.text = pText
                    }
                }
                appendLog(if (ok) "✅ [FTP] $msg" else "❌ [FTP] $msg")
                mainHandler.post {
                    ftpStatusTxt.text = if (ok) "✅ $msg" else "❌ $msg"
                }
            }
        })

        parent.addView(ftpCard)
    }

    // =========================================================================
    // ВКЛАДКА 4: БАЗА ИГР TITLEDB И РАСШИФРОВКА КОДОВ ОШИБОК SWITCH (2XXX-XXXX)
    // =========================================================================

    private fun buildTitleDbAndErrorCodesTab(parent: LinearLayout, th: StormTheme) {
        val cacheFile = File(stormRoot, "settings/titledb_cache.json")
        var currentCatalog = StormNczEngine.loadCachedTitleDb(cacheFile)
        var subTabMode = 0
        var platformFilter = "Все"
        var displayLimit = 80
        var searchQuery = ""

        val headerCard = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = makeCardDrawable(th.bgCard, th.border)
            setPadding(dp(14), dp(12), dp(14), dp(12))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(10)
            }
        }
        headerCard.addView(makeSectionHeader(
            "📚 Справочник игр TitleDB (Switch / 3DS) и Декодер системных ошибок 2xxx-xxxx",
            "База TitleID Базы [000] и Патча [800], проверка статуса русского перевода, описаний и расшифровка ошибок Atmosphère / Horizon OS",
            th
        ))

        val listContainer = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }

        val searchEdit = EditText(this).apply {
            hint = "🔍 Введите название игры, TitleID (0100...) или код ошибки (2002-4153, 2168-0002)..."
            setTextColor(th.textPrimary)
            setHintTextColor(th.textSecondary)
            textSize = 12.2f
            setSingleLine(true)
            background = makeCardDrawable(th.bgPrimary, th.border, 8)
            setPadding(dp(12), dp(8), dp(12), dp(8))
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                topMargin = dp(6)
                bottomMargin = dp(8)
            }
        }

        fun renderActiveList() {
            listContainer.removeAllViews()
            val q = searchQuery.trim().lowercase()

            val matchingErrors = StormNczEngine.builtInSwitchErrorCodes.filter {
                q.isEmpty() || it.code.lowercase().contains(q) || it.title.lowercase().contains(q) ||
                    it.moduleName.lowercase().contains(q) || it.cause.lowercase().contains(q) || it.solution.lowercase().contains(q)
            }

            if (subTabMode == 1 || (q.length >= 4 && q[0] == '2' && matchingErrors.isNotEmpty())) {
                matchingErrors.forEach { err ->
                    val errCard = LinearLayout(this).apply {
                        orientation = LinearLayout.VERTICAL
                        background = makeCardDrawable(th.bgCard, th.border)
                        setPadding(dp(14), dp(11), dp(14), dp(11))
                        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                            bottomMargin = dp(8)
                        }
                    }
                    errCard.addView(TextView(this).apply {
                        text = "⚠️ Код ошибки: ${err.code}  •  ${err.moduleName}"
                        setTextColor(Color.parseColor("#F59E0B"))
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                        typeface = Typeface.DEFAULT_BOLD
                    })
                    errCard.addView(TextView(this).apply {
                        text = err.title
                        setTextColor(th.textPrimary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f)
                        typeface = Typeface.DEFAULT_BOLD
                        setPadding(0, dp(2), 0, dp(4))
                    })
                    errCard.addView(TextView(this).apply {
                        text = "Причина: ${err.cause}"
                        setTextColor(th.textSecondary)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                        setPadding(0, 0, 0, dp(6))
                    })
                    errCard.addView(TextView(this).apply {
                        text = "🛠 Как исправить:\n${err.solution}"
                        setTextColor(th.accent)
                        setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                        background = makeCardDrawable(th.bgPrimary, th.border, 8)
                        setPadding(dp(10), dp(7), dp(10), dp(7))
                    })
                    listContainer.addView(errCard)
                }
                if (subTabMode == 1) return
            }

            val filteredGames = RuTrackerCloudEngine.filterTitleDbCatalogFuzzy(currentCatalog, platformFilter, searchQuery)

            listContainer.addView(TextView(this).apply {
                text = "📊 Найдено игр в полной базе TitleDB: ${filteredGames.size} из ${currentCatalog.size} (показаны первые ${minOf(filteredGames.size, displayLimit)})"
                setTextColor(th.accent)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                typeface = Typeface.DEFAULT_BOLD
                setPadding(dp(4), dp(2), dp(4), dp(6))
            })

            filteredGames.take(displayLimit).forEach { game ->
                val gCard = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    background = makeCardDrawable(th.bgCard, th.border)
                    setPadding(dp(14), dp(11), dp(14), dp(11))
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        bottomMargin = dp(8)
                    }
                }
                val topRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                    gravity = Gravity.CENTER_VERTICAL
                }
                topRow.addView(TextView(this).apply {
                    text = "[${game.platform}]"
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11f)
                    typeface = Typeface.DEFAULT_BOLD
                    setPadding(0, 0, dp(6), 0)
                })
                topRow.addView(TextView(this).apply {
                    text = game.name
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 13.5f)
                    typeface = Typeface.DEFAULT_BOLD
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
                })
                topRow.addView(TextView(this).apply {
                    text = game.sizeText
                    setTextColor(th.accent)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    typeface = Typeface.DEFAULT_BOLD
                })
                gCard.addView(topRow)

                gCard.addView(TextView(this).apply {
                    text = "Base TitleID: ${game.titleId}  •  Update ID: ${game.patchId}\nИздатель: ${game.publisher} (${game.releaseYear})  •  DLC: ${game.dlcCount}  •  Перевод: ${game.ruSupport}"
                    setTextColor(th.textSecondary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.2f)
                    typeface = Typeface.MONOSPACE
                    setPadding(0, dp(4), 0, dp(4))
                })

                gCard.addView(TextView(this).apply {
                    text = game.description
                    setTextColor(th.textPrimary)
                    setTextSize(TypedValue.COMPLEX_UNIT_SP, 11.5f)
                    setPadding(0, 0, 0, dp(8))
                })

                val actRow = LinearLayout(this).apply {
                    orientation = LinearLayout.HORIZONTAL
                }
                actRow.addView(makeButton("🌐 Найти и скачать на RuTracker", th.accent) {
                    pendingRuTrackerSearchQuery = game.name
                    activeTab = 2
                    buildUserInterface()
                    Toast.makeText(this, "Переход к раздачам ${game.name} на RuTracker", Toast.LENGTH_SHORT).show()
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(6) }
                })

                actRow.addView(makeButton("📋 Копировать TitleID", th.bgElevated, th.textPrimary) {
                    val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                    cm.setPrimaryClip(ClipData.newPlainText("TitleID", game.titleId))
                    Toast.makeText(this, "Скопирован TitleID: ${game.titleId}", Toast.LENGTH_SHORT).show()
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT)
                })
                gCard.addView(actRow)
                listContainer.addView(gCard)
            }

            if (filteredGames.size > displayLimit) {
                listContainer.addView(makeButton("➕ Показать ещё 100 игр (сейчас показано $displayLimit из ${filteredGames.size})", th.accent) {
                    displayLimit += 100
                    renderActiveList()
                }.apply {
                    layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                        topMargin = dp(4)
                        bottomMargin = dp(10)
                    }
                })
            }
        }

        val modeSwitchRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(6)
            }
        }
        modeSwitchRow.addView(makeButton("🎮 База игр TitleDB (${currentCatalog.size})", th.accent) {
            subTabMode = 0
            displayLimit = 80
            renderActiveList()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.15f).apply { marginEnd = dp(6) }
        })

        modeSwitchRow.addView(makeButton("⚠️ Коды ошибок (2xxx-xxxx)", th.bgElevated, th.accent) {
            subTabMode = 1
            renderActiveList()
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.05f).apply { marginEnd = dp(6) }
        })

        modeSwitchRow.addView(makeButton("🔄 Обновить базу", th.bgElevated, th.textPrimary) {
            thread {
                val synced = StormNczEngine.syncOnlineTitleDb(cacheFile) { msg ->
                    appendLog("[TitleDB] $msg")
                }
                mainHandler.post {
                    currentCatalog = synced
                    renderActiveList()
                    Toast.makeText(this, "База TitleDB обновлена (${synced.size} игр)!", Toast.LENGTH_SHORT).show()
                }
            }
        }.apply {
            layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 0.85f)
        })
        headerCard.addView(modeSwitchRow)

        val platRow = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        listOf("Все", "Switch", "3DS").forEach { pName ->
            platRow.addView(makeButton("Платформа: $pName", th.bgElevated, th.textPrimary) {
                platformFilter = pName
                subTabMode = 0
                displayLimit = 80
                renderActiveList()
            }.apply {
                layoutParams = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f).apply { marginEnd = dp(4) }
            })
        }
        headerCard.addView(platRow)
        headerCard.addView(searchEdit)

        searchEdit.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                searchQuery = s?.toString() ?: ""
                renderActiveList()
            }
        })

        parent.addView(headerCard)
        renderActiveList()
        parent.addView(listContainer)
    }

}
