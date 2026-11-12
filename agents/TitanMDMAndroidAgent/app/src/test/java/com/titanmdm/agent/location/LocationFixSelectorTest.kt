package com.titanmdm.agent.location

import com.titanmdm.agent.location.LocationFixSelector.Fix
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class LocationFixSelectorTest {

    private val now = 1_800_000_000_000L
    private val minute = 60_000L

    @Test
    fun `sin candidatas no inventa una posicion`() {
        assertNull(LocationFixSelector.best<String>(emptyList(), now))
        assertNull(LocationFixSelector.best(listOf<Fix<String>?>(null, null), now))
    }

    @Test
    fun `gana la mas reciente aunque sea menos precisa`() {
        val best = LocationFixSelector.best(
            listOf(
                Fix("gps-viejo", now - 30 * minute, 5f),
                Fix("red-nueva", now - minute, 80f)
            ),
            now
        )
        assertEquals("red-nueva", best?.value)
    }

    @Test
    fun `en la misma ventana gana la mas precisa`() {
        val best = LocationFixSelector.best(
            listOf(
                Fix("red", now - 10_000, 60f),
                Fix("gps", now - 70_000, 8f),
                Fix("sin-precision", now - 5_000, null)
            ),
            now
        )
        assertEquals("gps", best?.value)
    }

    @Test
    fun `ultima conocida de horas atras se conserva con su hora real`() {
        val old = Fix("almacen", now - 6 * 60 * minute, 30f)
        assertEquals(old, LocationFixSelector.best(listOf(old), now))
    }

    @Test
    fun `descarta posiciones de mas de una semana o con hora futura`() {
        assertNull(
            LocationFixSelector.best(
                listOf(
                    Fix("vieja", now - LocationFixSelector.MAX_AGE_MS - 1, 10f),
                    Fix("futura", now + 10 * minute, 10f),
                    Fix("sin-hora", 0L, 10f)
                ),
                now
            )
        )
    }
}
