package com.titanmdm.agent.location

/**
 * Elige la mejor posición entre varias candidatas (GPS, red, pasiva,
 * caché del servicio, última conocida). Lógica pura, probada en unit tests.
 *
 * Reglas:
 *  - Se descartan las posiciones con hora futura o más viejas que [maxAgeMs].
 *  - Gana la más reciente; si dos están a menos de [sameWindowMs], gana la
 *    de mejor precisión (radio menor). Sin precisión cuenta como peor.
 *  - Nunca se inventa una posición: sin candidatas válidas devuelve null.
 *    Una posición vieja se envía con su hora real, así la consola la
 *    muestra como "señal antigua" en lugar de "sin ubicación".
 */
object LocationFixSelector {

    /** Una semana: un equipo guardado en almacén sigue apareciendo en el mapa. */
    const val MAX_AGE_MS = 7L * 24 * 60 * 60 * 1000

    /** Tolerancia de reloj para horas ligeramente adelantadas. */
    private const val CLOCK_SKEW_MS = 60_000L

    data class Fix<T>(
        val value: T,
        val timeMs: Long,
        val accuracyMeters: Float?
    )

    fun <T> best(
        candidates: List<Fix<T>?>,
        nowMs: Long,
        maxAgeMs: Long = MAX_AGE_MS,
        sameWindowMs: Long = 2 * 60_000L
    ): Fix<T>? {
        val valid = candidates
            .filterNotNull()
            .filter { it.timeMs > 0 && it.timeMs <= nowMs + CLOCK_SKEW_MS && nowMs - it.timeMs <= maxAgeMs }
        if (valid.isEmpty()) return null

        val newest = valid.maxOf { it.timeMs }
        return valid
            .filter { newest - it.timeMs <= sameWindowMs }
            .minWith(
                compareBy<Fix<T>> { it.accuracyMeters ?: Float.MAX_VALUE }
                    .thenByDescending { it.timeMs }
            )
    }
}
