import java.io.FileInputStream
import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

val stormSigningProps = Properties().apply {
    val secretsFile = file("F:/STORM ALL PROJECTS/.storm-secrets/storm_signing.properties")
    if (secretsFile.exists()) {
        FileInputStream(secretsFile).use { load(it) }
    }
}

android {
    namespace = "com.storm.switchbox"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.storm.switchbox"
        targetSdk = 36
        versionCode = 662
        versionName = "6.7.1"
    }

    signingConfigs {
        create("stormUnifiedRelease") {
            val certPath = stormSigningProps.getProperty("STORM_CERT_FILE") ?: System.getenv("STORM_CERT_FILE")
            if (!certPath.isNullOrBlank() && file(certPath).exists()) {
                storeFile = file(certPath)
                storePassword = stormSigningProps.getProperty("STORM_CERT_PASSWORD") ?: System.getenv("STORM_CERT_PASSWORD")
                keyAlias = stormSigningProps.getProperty("STORM_KEY_ALIAS") ?: System.getenv("STORM_KEY_ALIAS")
                keyPassword = stormSigningProps.getProperty("STORM_KEY_PASSWORD") ?: System.getenv("STORM_KEY_PASSWORD")
                enableV1Signing = true
                enableV2Signing = true
                enableV3Signing = true
            }
        }
    }

    flavorDimensions += "edition"
    productFlavors {
        create("modern") {
            dimension = "edition"
            minSdk = 26
            ndk {
                abiFilters += listOf("arm64-v8a", "x86_64")
            }
        }
        create("legacy") {
            dimension = "edition"
            minSdk = 21
            ndk {
                abiFilters += listOf("armeabi-v7a", "arm64-v8a", "x86", "x86_64")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            isShrinkResources = false
            signingConfig = signingConfigs.getByName("stormUnifiedRelease")
        }
        debug {
            signingConfig = signingConfigs.getByName("stormUnifiedRelease")
        }
    }

    packaging {
        jniLibs {
            useLegacyPackaging = true
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    implementation("com.github.luben:zstd-jni:1.5.6-6@aar")
    implementation("org.libtorrent4j:libtorrent4j:2.1.0-31")
    implementation("org.libtorrent4j:libtorrent4j-android-arm64:2.1.0-31")
    implementation("org.libtorrent4j:libtorrent4j-android-arm:2.1.0-31")
    implementation("org.libtorrent4j:libtorrent4j-android-x86_64:2.1.0-31")
    implementation("org.libtorrent4j:libtorrent4j-android-x86:2.1.0-31")
    // ?????????? ????????????? ? ??????? RAR4 / RAR5 / 7z (7-Zip 16.02, ??? ABI: armeabi-v7a, arm64-v8a, x86, x86_64)
    implementation("com.github.omicronapps:7-Zip-JBinding-4Android:Release-16.02-2.02")
}
