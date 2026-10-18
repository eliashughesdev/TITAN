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

        versionCode = 1
        versionName = "1.0.0"

        testInstrumentationRunner =
            "androidx.test.runner.AndroidJUnitRunner"


        buildConfigField(
            "String",
            "AGENT_VERSION",
            "\"1.0.0\""
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

    buildTypes {
        debug {
            isMinifyEnabled = false
            buildConfigField("String", "TITAN_API_BASE_URL", "\"http://10.0.2.2:8020/\"")
        }

        release {
            isMinifyEnabled = true

            if (configuredReleaseEndpoint.isNullOrEmpty()) {
                if (isReleaseBuildRequested) {
                    error("TITAN_API_BASE_URL is required for release builds")
                }

                // Placeholder inerte: nunca se usa porque el build release
                // falla en configuración si no hay endpoint válido.
                buildConfigField(
                    "String",
                    "TITAN_API_BASE_URL",
                    "\"https://unconfigured.titanmdm.invalid/\""
                )
            } else {
                require(
                    configuredReleaseEndpoint.startsWith("https://") &&
                        !configuredReleaseEndpoint.contains('"') &&
                        !configuredReleaseEndpoint.contains('\\')
                ) {
                    "Release API URL must be valid HTTPS"
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