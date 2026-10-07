using System.Text.RegularExpressions;

namespace TitanMDM.Application.Devices.Naming;

public static partial class DeviceNameParser
{
    /*
     * ============================================================
     * NOMENCLATURA CORPORATIVA
     * ============================================================
     *
     * Ejemplos soportados:
     *
     * CILSPMCEDI01
     * CIDSPMFACT01
     * CILSPMCEDITI
     *
     * CI    = organización
     * L/D   = tipo de equipo
     * SPM   = ciudad
     *
     * Segmento restante:
     *
     * CEDI01
     * FACT01
     * CEDITI
     *
     * La interpretación de AREA + SUFFIX se realiza contra
     * DeviceNamingOptions.Areas.
     *
     * Esto evita depender de longitudes rígidas.
     * ============================================================
     */

    [GeneratedRegex(
        @"^(?<org>[A-Z]{2})(?<type>[A-Z])(?<city>[A-Z]{3})(?<rest>[A-Z0-9]{2,20})$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex
        DeviceNameRegex();

    public static DeviceNameParseResult
        Parse(
            string? deviceName,
            DeviceNamingOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        if (
            !options.Enabled
            ||
            string.IsNullOrWhiteSpace(
                deviceName))
        {
            return DeviceNameParseResult
                .NotMatched(
                    deviceName);
        }

        var normalized =
            deviceName
                .Trim()
                .ToUpperInvariant();

        var match =
            DeviceNameRegex()
                .Match(
                    normalized);

        if (
            !match.Success)
        {
            return DeviceNameParseResult
                .NotMatched(
                    normalized);
        }

        var organization =
            match.Groups["org"]
                .Value;

        var type =
            match.Groups["type"]
                .Value;

        var city =
            match.Groups["city"]
                .Value;

        var remainder =
            match.Groups["rest"]
                .Value;

        // ========================================================
        // ORGANIZATION
        // ========================================================

        if (
            !organization.Equals(
                options.OrganizationPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return DeviceNameParseResult
                .Invalid(
                    normalized,
                    $"Prefijo de organización no válido: {organization}.");
        }

        // ========================================================
        // DEVICE TYPE
        // ========================================================

        if (
            !options.DeviceTypes
                .TryGetValue(
                    type,
                    out var deviceType))
        {
            return DeviceNameParseResult
                .Invalid(
                    normalized,
                    $"Tipo de equipo desconocido: {type}.");
        }

        // ========================================================
        // CITY
        // ========================================================

        options.Cities
            .TryGetValue(
                city,
                out var cityName);

        // ========================================================
        // AREA
        // ========================================================

        var matchedArea =
            options.Areas
                .Keys
                .Where(
                    key =>
                        remainder.StartsWith(
                            key,
                            StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(
                    key =>
                        key.Length)
                .FirstOrDefault();

        if (
            string.IsNullOrWhiteSpace(
                matchedArea))
        {
            return DeviceNameParseResult
                .Invalid(
                    normalized,
                    $"No fue posible identificar un área configurada dentro de '{remainder}'.");
        }

        var areaCode =
            matchedArea
                .ToUpperInvariant();

        options.Areas
            .TryGetValue(
                areaCode,
                out var areaMapping);

        var suffix =
            remainder[
                areaCode.Length..];

        // ========================================================
        // SUFFIX
        // ========================================================

        int? sequence =
            null;

        string?
            qualifier =
                null;

        if (
            !string.IsNullOrWhiteSpace(
                suffix))
        {
            if (
                int.TryParse(
                    suffix,
                    out var parsedSequence))
            {
                sequence =
                    parsedSequence;
            }
            else
            {
                qualifier =
                    suffix
                        .Trim()
                        .ToUpperInvariant();
            }
        }

        /*
         * ========================================================
         * LOCALIDAD
         * ========================================================
         *
         * Si el Area mapping define SiteLocationName,
         * se usa normalmente.
         *
         * Si el hostname contiene un qualifier alfabético,
         * este puede funcionar como sublocalidad.
         *
         * Ejemplo:
         *
         * CILSPMCEDITI
         *
         * area      = CEDI
         * qualifier = TI
         *
         * El qualifier se expone como SuggestedSiteLocationName
         * solamente cuando el mapping no trae otra localización.
         * ========================================================
         */

        var suggestedLocation =
            !string.IsNullOrWhiteSpace(
                areaMapping?
                    .SiteLocationName)
                ? areaMapping!
                    .SiteLocationName
                : qualifier;

        return new DeviceNameParseResult(
            Matched:
                true,

            Valid:
                true,

            DeviceName:
                normalized,

            OrganizationCode:
                organization,

            DeviceTypeCode:
                type,

            DeviceType:
                deviceType,

            CityCode:
                city,

            City:
                cityName,

            AreaCode:
                areaCode,

            SuggestedSiteName:
                areaMapping?
                    .SiteName,

            SuggestedSiteLocationName:
                suggestedLocation,

            Sequence:
                sequence,

            Error:
                null);
    }
}

public sealed record DeviceNameParseResult(
    bool Matched,
    bool Valid,
    string? DeviceName,
    string? OrganizationCode,
    string? DeviceTypeCode,
    string? DeviceType,
    string? CityCode,
    string? City,
    string? AreaCode,
    string? SuggestedSiteName,
    string? SuggestedSiteLocationName,
    int? Sequence,
    string? Error)
{
    public static DeviceNameParseResult
        NotMatched(
            string? name)
    {
        return new(
            Matched:
                false,

            Valid:
                false,

            DeviceName:
                name,

            OrganizationCode:
                null,

            DeviceTypeCode:
                null,

            DeviceType:
                null,

            CityCode:
                null,

            City:
                null,

            AreaCode:
                null,

            SuggestedSiteName:
                null,

            SuggestedSiteLocationName:
                null,

            Sequence:
                null,

            Error:
                "El nombre no coincide con la nomenclatura corporativa.");
    }

    public static DeviceNameParseResult
        Invalid(
            string? name,
            string error)
    {
        return new(
            Matched:
                true,

            Valid:
                false,

            DeviceName:
                name,

            OrganizationCode:
                null,

            DeviceTypeCode:
                null,

            DeviceType:
                null,

            CityCode:
                null,

            City:
                null,

            AreaCode:
                null,

            SuggestedSiteName:
                null,

            SuggestedSiteLocationName:
                null,

            Sequence:
                null,

            Error:
                error);
    }
}