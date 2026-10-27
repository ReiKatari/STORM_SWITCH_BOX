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
        versionCode = 553
        versionName = "5.5.3"
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
}
