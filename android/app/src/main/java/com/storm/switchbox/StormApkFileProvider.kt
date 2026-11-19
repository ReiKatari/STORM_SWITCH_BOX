package com.storm.switchbox

import android.content.ContentProvider
import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import android.database.MatrixCursor
import android.net.Uri
import android.os.Environment
import android.os.ParcelFileDescriptor
import android.provider.OpenableColumns
import java.io.File
import java.io.FileNotFoundException

/**
 * Нативный ContentProvider для безопасной передачи скачанного APK обновления
 * системному установщику пакетов Android (Android 7.0 - Android 16).
 */
class StormApkFileProvider : ContentProvider() {

    companion object {
        fun getUriForFile(context: Context, file: File): Uri {
            val authority = "${context.packageName}.provider"
            return Uri.Builder()
                .scheme("content")
                .authority(authority)
                .appendPath("apk")
                .appendPath(file.name)
                .build()
        }

        fun resolveApkFile(context: Context, fileName: String): File {
            val cleanName = File(fileName).name
            if (cleanName.isBlank() || cleanName == "." || cleanName == "..") {
                return File(context.cacheDir, "update.apk")
            }
            val allowedRoots = listOfNotNull(
                context.externalCacheDir,
                context.cacheDir,
                File(Environment.getExternalStorageDirectory(), "STORM SWITCH BOX/downloads")
            )
            for (root in allowedRoots) {
                try {
                    val rootCanon = root.canonicalPath
                    val candidate = File(root, cleanName)
                    val candCanon = candidate.canonicalPath
                    if (candCanon.startsWith(rootCanon + File.separator) && candidate.exists() && candidate.isFile) {
                        return candidate
                    }
                } catch (_: Exception) {}
            }
            return File(context.cacheDir, cleanName)
        }
    }

    override fun onCreate(): Boolean = true

    override fun query(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor {
        val ctx = context ?: return MatrixCursor(arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE))
        val file = resolveApkFile(ctx, uri.lastPathSegment ?: "update.apk")
        val cols = projection ?: arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE)
        val cursor = MatrixCursor(cols, 1)
        val row = cursor.newRow()
        for (col in cols) {
            when (col) {
                OpenableColumns.DISPLAY_NAME -> row.add(file.name)
                OpenableColumns.SIZE -> row.add(if (file.exists()) file.length() else 0L)
                else -> row.add(null)
            }
        }
        return cursor
    }

    override fun getType(uri: Uri): String = "application/vnd.android.package-archive"

    override fun openFile(uri: Uri, mode: String): ParcelFileDescriptor {
        val ctx = context ?: throw FileNotFoundException("No context")
        val file = resolveApkFile(ctx, uri.lastPathSegment ?: "update.apk")
        if (!file.exists()) throw FileNotFoundException("APK not found: ${file.absolutePath}")
        return ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY)
    }

    override fun insert(uri: Uri, values: ContentValues?): Uri? = null
    override fun delete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int = 0
    override fun update(uri: Uri, values: ContentValues?, selection: String?, selectionArgs: Array<out String>?): Int = 0
}
