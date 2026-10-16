package com.titanmdm.agent.launcher

enum class TitanLauncherImageFormat {
    Png,
    Jpeg,
    Webp
}

object TitanLauncherImageSignatures {

    const val MAX_WALLPAPER_BYTES = 4 * 1024 * 1024
    const val MAX_LOGO_BYTES = 1024 * 1024

    fun detect(bytes: ByteArray): TitanLauncherImageFormat? {
        if (bytes.size < 12) return null
        if (bytes[0] == 0x89.toByte() &&
            bytes[1] == 0x50.toByte() &&
            bytes[2] == 0x4E.toByte() &&
            bytes[3] == 0x47.toByte()
        ) {
            return TitanLauncherImageFormat.Png
        }
        if (bytes[0] == 0xFF.toByte() &&
            bytes[1] == 0xD8.toByte() &&
            bytes[2] == 0xFF.toByte()
        ) {
            return TitanLauncherImageFormat.Jpeg
        }
        val riff = bytes.copyOfRange(0, 4).decodeToString()
        val webp = bytes.copyOfRange(8, 12).decodeToString()
        return if (riff == "RIFF" && webp == "WEBP") {
            TitanLauncherImageFormat.Webp
        } else {
            null
        }
    }
}
