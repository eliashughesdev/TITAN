package com.titanmdm.agent.location

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LocationCapturePolicyTest {

    private val fix = LocationCapturePayload(
        latitude = -34.6037,
        longitude = -58.3816,
        accuracyMeters = 12.5,
        altitudeMeters = null,
        speedMetersPerSecond = null,
        source = LocationCapturePolicy.PROACTIVE_SOURCE,
        capturedAtUtc = "2026-10-18T00:00:00Z"
    )

    private fun decide(
        reportingEnabled: Boolean = true,
        permissionGranted: Boolean = true,
        batteryPercent: Int? = 80,
        batterySaverActive: Boolean = false,
        lastReportAtEpochMillis: Long? = null,
        nowEpochMillis: Long = 1_800_000_000_000L,
        capturedFix: LocationCapturePayload? = fix,
        reportingIntervalMinutes: Int =
            LocationCapturePolicy
                .MIN_REPORTING_INTERVAL_MINUTES
    ): LocationCaptureDecision {
        return LocationCapturePolicy.decide(
            reportingEnabled = reportingEnabled,
            permissionGranted = permissionGranted,
            batteryPercent = batteryPercent,
            batterySaverActive = batterySaverActive,
            lastReportAtEpochMillis = lastReportAtEpochMillis,
            nowEpochMillis = nowEpochMillis,
            capturedFix = capturedFix,
            reportingIntervalMinutes = reportingIntervalMinutes
        )
    }

    @Test
    fun `missing permission always reports status-only even when policy disabled`() {
        val decision = decide(
            permissionGranted = false,
            reportingEnabled = false,
            capturedFix = null
        )

        assertTrue(
            decision is LocationCaptureDecision.ReportStatusOnly
        )
        decision as LocationCaptureDecision.ReportStatusOnly
        assertEquals(
            LocationCapturePolicy.STATUS_PERMISSION_DENIED,
            decision.status
        )
        assertEquals(
            LocationCapturePolicy.PROACTIVE_SOURCE,
            decision.source
        )
    }

    @Test
    fun `disabled policy skips when permission granted`() {
        val decision = decide(reportingEnabled = false)

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }

    @Test
    fun `battery saver skips`() {
        val decision = decide(batterySaverActive = true)

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }

    @Test
    fun `low battery skips`() {
        val decision = decide(
            batteryPercent =
                LocationCapturePolicy.LOW_BATTERY_THRESHOLD_PERCENT - 1
        )

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }

    @Test
    fun `battery exactly at threshold is allowed`() {
        val decision = decide(
            batteryPercent =
                LocationCapturePolicy.LOW_BATTERY_THRESHOLD_PERCENT
        )

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
    }

    @Test
    fun `unknown battery level does not block reporting`() {
        val decision = decide(batteryPercent = null)

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
    }

    @Test
    fun `interval not elapsed skips`() {
        val now = 1_800_000_000_000L
        val intervalMillis =
            LocationCapturePolicy.MIN_REPORTING_INTERVAL_MINUTES * 60_000L

        val decision = decide(
            lastReportAtEpochMillis = now - intervalMillis + 1_000L,
            nowEpochMillis = now
        )

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }

    @Test
    fun `interval elapsed reports coordinates`() {
        val now = 1_800_000_000_000L
        val intervalMillis =
            LocationCapturePolicy.MIN_REPORTING_INTERVAL_MINUTES * 60_000L

        val decision = decide(
            lastReportAtEpochMillis = now - intervalMillis,
            nowEpochMillis = now
        )

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
    }

    @Test
    fun `no fix reports unavailable status-only`() {
        val decision = decide(capturedFix = null)

        assertTrue(
            decision is LocationCaptureDecision.ReportStatusOnly
        )
        decision as LocationCaptureDecision.ReportStatusOnly
        assertEquals(
            LocationCapturePolicy.STATUS_UNAVAILABLE,
            decision.status
        )
    }

    @Test
    fun `first report with fix is allowed without last report timestamp`() {
        val decision = decide(lastReportAtEpochMillis = null)

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
    }

    @Test
    fun `coordinates decision preserves the captured fix`() {
        val decision = decide()

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
        decision as LocationCaptureDecision.ReportCoordinates
        assertEquals(fix, decision.payload)
    }

    @Test
    fun `permission check takes priority over everything else`() {
        val decision = decide(
            permissionGranted = false,
            batterySaverActive = true,
            batteryPercent = 1,
            reportingEnabled = true,
            capturedFix = fix
        )

        assertTrue(
            decision is LocationCaptureDecision.ReportStatusOnly
        )
        decision as LocationCaptureDecision.ReportStatusOnly
        assertEquals(
            LocationCapturePolicy.STATUS_PERMISSION_DENIED,
            decision.status
        )
    }

    @Test
    fun `server interval larger than minimum is enforced`() {
        val now = 1_800_000_000_000L
        val serverIntervalMillis = 30 * 60_000L

        val decision = decide(
            lastReportAtEpochMillis =
                now - serverIntervalMillis + 1_000L,
            nowEpochMillis = now,
            reportingIntervalMinutes = 30
        )

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }

    @Test
    fun `server interval of 30 minutes allows report when elapsed`() {
        val now = 1_800_000_000_000L
        val serverIntervalMillis = 30 * 60_000L

        val decision = decide(
            lastReportAtEpochMillis =
                now - serverIntervalMillis,
            nowEpochMillis = now,
            reportingIntervalMinutes = 30
        )

        assertTrue(
            decision is LocationCaptureDecision.ReportCoordinates
        )
    }

    @Test
    fun `server interval below workmanager minimum is clamped to minimum`() {
        val now = 1_800_000_000_000L
        val minIntervalMillis =
            LocationCapturePolicy
                .MIN_REPORTING_INTERVAL_MINUTES * 60_000L

        val decision = decide(
            lastReportAtEpochMillis =
                now - minIntervalMillis + 1_000L,
            nowEpochMillis = now,
            reportingIntervalMinutes = 5
        )

        assertEquals(
            LocationCaptureDecision.Skip,
            decision
        )
    }
}
