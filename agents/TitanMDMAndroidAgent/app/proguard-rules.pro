# TitanMDM Android Agent - Release R8 rules
#
# Estas reglas preservan los contratos reflexivos mínimos usados por
# Retrofit, OkHttp, kotlinx.serialization y los modelos de transporte.
# Debug no se ve afectado porque isMinifyEnabled=false.

# --- Anotaciones y metadata ---
-keepattributes *Annotation*, InnerClasses, Signature, EnclosingMethod
-keepattributes RuntimeVisibleAnnotations, RuntimeVisibleParameterAnnotations

# --- kotlinx.serialization ---
-keepattributes RuntimeVisibleAnnotations, AnnotationDefault

-if @kotlinx.serialization.Serializable class **
-keepclassmembers class <1> {
    static <1>$Companion Companion;
}

-if @kotlinx.serialization.Serializable class ** {
    static **$* *;
}
-keepclassmembers class <2>$<3> {
    kotlinx.serialization.KSerializer serializer(...);
}

-if @kotlinx.serialization.Serializable class ** {
    public static ** INSTANCE;
}
-keepclassmembers class <1> {
    public static <1> INSTANCE;
    kotlinx.serialization.KSerializer serializer(...);
}

-keepclasseswithmembers class ** {
    kotlinx.serialization.KSerializer serializer(...);
}

# --- Retrofit ---
-keepattributes Exceptions
-keep,allowobfuscation,allowshrinking interface retrofit2.Call
-keep,allowobfuscation,allowshrinking class retrofit2.Response
-keep,allowobfuscation,allowshrinking class kotlin.coroutines.Continuation

# --- OkHttp ---
-dontwarn okhttp3.**
-dontwarn okio.**
-dontwarn javax.annotation.**

# --- TitanMDM DTOs (serialización de red) ---
-keep class com.titanmdm.agent.core.network.** { *; }

# --- DevicePolicyManager / managed configuration entry points ---
-keep class com.titanmdm.agent.launcher.** { *; }
-keep class com.titanmdm.agent.receivers.** { *; }
-keep class com.titanmdm.agent.workers.** { *; }
