package com.titanmdm.agent.location

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationManager
import android.util.Log
import androidx.core.content.edit
import androidx.core.location.LocationListenerCompat
import androidx.core.location.LocationManagerCompat
import androidx.core.location.LocationRequestCompat
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicReference

/**
 * Escucha continua de ubicación dentro del servicio en primer plano.
 *
 * getCurrentLocation() en cada heartbeat solo espera unos segundos: en
 * interiores (almacén) el GPS no llega a fijar y sin Google Play Services
 * no hay proveedor de red, así que el heartbeat salía sin coordenadas.
 * Con los proveedores GPS, red y pasivo suscritos de forma permanente, el
 * agente conserva la mejor posición que el equipo consiga en cualquier
 * momento (incluidas las que piden otras apps, vía proveedor pasivo) y la
 * persiste para que sobreviva a un reinicio del proceso.
 *
 * No usa Google Play Services: funciona en Honeywell sin GMS.
 */
object LocationWatcher {

    private const val TAG = "TitanLocationWatcher"
    private const val PREFS = "titan_location_cache"

    private const val GPS_INTERVAL_MS = 2 * 60_000L
    private const val NETWORK_INTERVAL_MS = 60_000L
    private const val PASSIVE_INTERVAL_MS = 30_000L

    private val executor = Executors.newSingleThreadExecutor()
    private val latest = AtomicReference<Location?>(null)

    private var manager: LocationManager? = null
    private var registered: Set<String> = emptySet()

    private val listener = LocationListenerCompat { location -> offer(location) }

    /** Última posición conseguida por el equipo (memoria o disco), sin filtrar antigüedad. */
    fun cached(context: Context): Location? = latest.get() ?: readPersisted(context)?.also { latest.compareAndSet(null, it) }

    /** Guarda una posición si mejora la actual (más reciente, o igual de reciente y más precisa). */
    fun offer(location: Location?, context: Context? = null) {
        if (location == null) return
        if (location.latitude !in -90.0..90.0 || location.longitude !in -180.0..180.0) return
        if (location.latitude == 0.0 && location.longitude == 0.0) return

        val current = latest.get()
        val chosen = LocationFixSelector.best(
            listOf(fix(current), fix(location)),
            nowMs = System.currentTimeMillis()
        )?.value ?: return

        if (chosen !== current) {
            latest.set(chosen)
            (context ?: appContext)?.let { persist(it, chosen) }
        }
    }

    @Volatile
    private var appContext: Context? = null

    /**
     * Idempotente: registra los proveedores habilitados y vuelve a
     * registrar si cambiaron (se activó el GPS, se concedió el permiso).
     */
    @SuppressLint("MissingPermission")
    @Synchronized
    fun ensure(context: Context) {
        val app = context.applicationContext
        appContext = app

        if (!NativeLocationProvider.hasPermission(app)) {
            stop()
            return
        }

        val lm = manager ?: app.getSystemService(LocationManager::class.java) ?: return
        manager = lm

        val wanted = listOf(
            LocationManager.GPS_PROVIDER,
            LocationManager.NETWORK_PROVIDER,
            LocationManager.PASSIVE_PROVIDER
        ).filter { provider ->
            runCatching { lm.allProviders.contains(provider) && lm.isProviderEnabled(provider) }
                .getOrDefault(false)
        }.toSet()

        if (wanted == registered) return

        runCatching { LocationManagerCompat.removeUpdates(lm, listener) }
        registered = emptySet()

        val subscribed = wanted.filter { provider ->
            val request = LocationRequestCompat.Builder(intervalFor(provider))
                .setQuality(
                    if (provider == LocationManager.GPS_PROVIDER) LocationRequestCompat.QUALITY_HIGH_ACCURACY
                    else LocationRequestCompat.QUALITY_BALANCED_POWER_ACCURACY
                )
                .setMinUpdateDistanceMeters(0f)
                .build()
            runCatching {
                LocationManagerCompat.requestLocationUpdates(lm, provider, request, executor, listener)
            }.onFailure {
                Log.w(TAG, "No fue posible suscribirse a $provider.", it)
            }.isSuccess
        }.toSet()

        registered = subscribed
        Log.i(TAG, "Proveedores de ubicación activos: ${subscribed.ifEmpty { setOf("ninguno") }}")

        // Semilla inmediata: la última conocida del sistema.
        subscribed.forEach { provider ->
            offer(runCatching { lm.getLastKnownLocation(provider) }.getOrNull(), app)
        }
    }

    // removeUpdates no exige permiso; runCatching cubre la SecurityException
    // de algunos fabricantes si el permiso se revocó.
    @SuppressLint("MissingPermission")
    @Synchronized
    fun stop() {
        manager?.let { lm -> runCatching { LocationManagerCompat.removeUpdates(lm, listener) } }
        registered = emptySet()
    }

    private fun intervalFor(provider: String): Long = when (provider) {
        LocationManager.GPS_PROVIDER -> GPS_INTERVAL_MS
        LocationManager.NETWORK_PROVIDER -> NETWORK_INTERVAL_MS
        else -> PASSIVE_INTERVAL_MS
    }

    internal fun fix(location: Location?): LocationFixSelector.Fix<Location>? =
        location?.let {
            LocationFixSelector.Fix(it, it.time, if (it.hasAccuracy()) it.accuracy else null)
        }

    private fun persist(context: Context, location: Location) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit {
            putString("provider", location.provider ?: "cache")
            putLong("lat", location.latitude.toRawBits())
            putLong("lon", location.longitude.toRawBits())
            putLong("time", location.time)
            putFloat("accuracy", if (location.hasAccuracy()) location.accuracy else -1f)
        }
    }

    private fun readPersisted(context: Context): Location? {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val time = prefs.getLong("time", 0L)
        if (time <= 0L) return null
        return Location(prefs.getString("provider", "cache")).apply {
            latitude = Double.fromBits(prefs.getLong("lat", 0L))
            longitude = Double.fromBits(prefs.getLong("lon", 0L))
            this.time = time
            prefs.getFloat("accuracy", -1f).takeIf { it >= 0f }?.let { accuracy = it }
        }
    }
}
