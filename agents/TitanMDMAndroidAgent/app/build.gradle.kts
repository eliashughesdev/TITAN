plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
}

android {
    namespace = "com.titanmdm.agent"
    compileSdk {
        version = release(37)
    }

    defaultConfig {
        applicationId = "com.titanmdm.agent"

        minSdk = 26
        targetSdk = 37

        versionCode = 4
        versionName = "1.0.3"

        testInstrumentationRunner =
            "androidx.test.runner.AndroidJUnitRunner"


        buildConfigField(
            "String",
            "AGENT_VERSION",
            "\"1.0.3\""
        )
    }

    val configuredReleaseEndpoint = providers.gradleProperty("titanApiBaseUrl")
        .orElse(providers.environmentVariable("TITAN_API_BASE_URL"))
        .map { it.trim() }
        .orNull

    /*
     * La resolución del endpoint release es perezosa.
     * Sólo se exige TITAN_API_BASE_URL cuando realmente se compila un
     * build type release; así assembleDebug / testDebugUnitTest no se
     * ven afectados por la configuración de release.
     */
    val isReleaseBuildRequested = gradle.startParameter.taskNames.any {
        it.contains("release", ignoreCase = true)
    }

    /*
     * Inscripcion interna por QR: el servidor viaja en el QR y puede ser
     * http://<IP-LAN>:8020 (sin certificado). Solo hosts privados
     * (ServerEndpointPolicy). Release: -PtitanAllowLanHttp=true.
     */
    val releaseAllowsLanHttp = providers.gradleProperty("titanAllowLanHttp")
        .orElse(providers.environmentVariable("TITAN_ALLOW_LAN_HTTP"))
        .map { it.trim().equals("true", ignoreCase = true) }
        .getOrElse(false)

    /*
     * Servidor de fabrica del APK de pruebas (se instala en equipos reales,
     * no en el emulador): el que el agente propone en la inscripcion manual.
     * -PtitanDebugApiUrl=http://<IP>:8020/ para otro servidor.
     */
    val debugApiUrl = providers.gradleProperty("titanDebugApiUrl")
        .orElse("http://172.21.20.14:8020/")
        .get()
        .trim()
    require(debugApiUrl.startsWith("http") && !debugApiUrl.contains('"') && !debugApiUrl.contains('\\')) {
        "titanDebugApiUrl debe ser una URL http(s) valida"
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
            buildConfigField("String", "TITAN_API_BASE_URL", "\"$debugApiUrl\"")
            buildConfigField("boolean", "ALLOW_LAN_HTTP", "true")
            manifestPlaceholders["allowCleartext"] = "true"
        }

        release {
            isMinifyEnabled = true

            buildConfigField("boolean", "ALLOW_LAN_HTTP", releaseAllowsLanHttp.toString())
            manifestPlaceholders["allowCleartext"] = releaseAllowsLanHttp.toString()

            if (configuredReleaseEndpoint.isNullOrEmpty()) {
                if (isReleaseBuildRequested) {
                    logger.lifecycle(
                        "TITAN_API_BASE_URL no definido: el servidor se tomará del QR de inscripción."
                    )
                }

                // Sin servidor de fabrica: el agente exige escanear el QR.
                buildConfigField(
                    "String",
                    "TITAN_API_BASE_URL",
                    "\"\""
                )
            } else {
                require(
                    (configuredReleaseEndpoint.startsWith("https://") ||
                        (releaseAllowsLanHttp && configuredReleaseEndpoint.startsWith("http://"))) &&
                        !configuredReleaseEndpoint.contains('"') &&
                        !configuredReleaseEndpoint.contains('\\')
                ) {
                    "Release API URL must be HTTPS (or http:// with titanAllowLanHttp=true)"
                }

                buildConfigField(
                    "String",
                    "TITAN_API_BASE_URL",
                    "\"$configuredReleaseEndpoint\""
                )
            }

            proguardFiles(
                getDefaultProguardFile(
                    "proguard-android-optimize.txt"
                ),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

/*
 * Auto-publicación del APK de aprovisionamiento: tras assembleDebug, el APK
 * se copia a deploy/app-debug.apk, se regenera el QR y se empuja SOLO ese
 * archivo a origin/main (scripts/android/Publish-AgentApk.ps1), para que la
 * URL raw del QR sirva siempre la última compilación.
 * Desactivar: -PtitanAutoPublish=false o TITAN_AUTO_PUBLISH=false.
 */
val autoPublishApk = providers.gradleProperty("titanAutoPublish")
    .orElse(providers.environmentVariable("TITAN_AUTO_PUBLISH"))
    .map { it.trim().lowercase() !in setOf("false", "0", "no") }
    .getOrElse(true)

val publishDeployApk = tasks.register<Exec>("publishDeployApk") {
    group = "titan"
    description = "Copia el APK debug a deploy/, regenera el QR y lo publica en origin/main."
    // Valores resueltos en configuración: el closure de onlyIf no debe
    // capturar el script (configuration cache).
    val publishScript = rootProject.layout.projectDirectory
        .file("../../scripts/android/Publish-AgentApk.ps1")
        .asFile
    val canRun = System.getProperty("os.name").startsWith("Windows") && publishScript.exists()
    onlyIf { canRun }
    commandLine(
        "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", publishScript.absolutePath,
    )
}

if (autoPublishApk) {
    tasks.matching { it.name == "assembleDebug" }.configureEach {
        finalizedBy(publishDeployApk)
    }
}

dependencies {

    // Compose
    implementation(
        platform(libs.androidx.compose.bom)
    )

    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)

    // Android Core
    implementation(libs.androidx.core.ktx)
    implementation(libs.play.services.location)
    implementation(libs.androidx.lifecycle.runtime.ktx)

    // Lifecycle / ViewModel
    implementation(
        libs.androidx.lifecycle.viewmodel.compose
    )

    implementation(
        libs.androidx.lifecycle.runtime.compose
    )

    // Background jobs
    implementation(
        libs.androidx.work.runtime.ktx
    )

    // Persistent configuration
    implementation(
        libs.androidx.datastore.preferences
    )

    // HTTP
    implementation(libs.retrofit.core)

    implementation(
        libs.retrofit.kotlinx.serialization
    )

    implementation(libs.okhttp.core)
    implementation(libs.okhttp.logging)

    // JSON
    implementation(
        libs.kotlinx.serialization.json
    )

    // QR de inscripcion con camara (ZXing, sin Google Play Services)
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")

    // Tests
    testImplementation(libs.junit)

    androidTestImplementation(
        platform(libs.androidx.compose.bom)
    )

    androidTestImplementation(
        libs.androidx.compose.ui.test.junit4
    )

    androidTestImplementation(
        libs.androidx.espresso.core
    )

    androidTestImplementation(
        libs.androidx.junit
    )

    debugImplementation(
        libs.androidx.compose.ui.test.manifest
    )

    debugImplementation(
        libs.androidx.compose.ui.tooling
    )
}