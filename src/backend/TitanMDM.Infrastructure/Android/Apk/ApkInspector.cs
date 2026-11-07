using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;

namespace TitanMDM.Infrastructure.Android.Apk;

/// <summary>Datos del AndroidManifest.xml binario de un APK.</summary>
public sealed record ApkManifestInfo(
    string PackageName,
    long VersionCode,
    string? VersionName);

/// <summary>
/// Lectura de APK sin herramientas del SDK: nombre de paquete y versión
/// (AndroidManifest.xml compilado) y certificado de firma (APK Signature
/// Scheme v2/v3, o JAR v1 como respaldo).
/// </summary>
public static class ApkInspector
{
    private const int ChunkXml = 0x0003;
    private const int ChunkStringPool = 0x0001;
    private const int ChunkResourceMap = 0x0180;
    private const int ChunkStartElement = 0x0102;

    private const uint AttrVersionCode = 0x0101021b;
    private const uint AttrVersionName = 0x0101021c;

    private const uint SignatureSchemeV2 = 0x7109871a;
    private const uint SignatureSchemeV3 = 0xf05368c0;
    private const uint EndOfCentralDirectory = 0x06054b50;

    private static readonly byte[] SigningBlockMagic =
        Encoding.ASCII.GetBytes("APK Sig Block 42");

    public static ApkManifestInfo ReadManifest(string apkPath)
    {
        using var archive = ZipFile.OpenRead(apkPath);
        var entry = archive.GetEntry("AndroidManifest.xml")
            ?? throw new InvalidDataException("El APK no contiene AndroidManifest.xml.");

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ParseManifest(buffer.ToArray());
    }

    /// <summary>
    /// Valor de PROVISIONING_DEVICE_ADMIN_SIGNATURE_CHECKSUM: SHA-256 del
    /// certificado de firma en base64 URL-safe sin relleno.
    /// </summary>
    public static string ReadSignatureChecksum(string apkPath)
        => ToUrlSafeBase64(SHA256.HashData(ReadSigningCertificate(apkPath)));

    public static string ToUrlSafeBase64(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Certificado DER del primer firmante.</summary>
    public static byte[] ReadSigningCertificate(string apkPath)
    {
        var bytes = File.ReadAllBytes(apkPath);
        return ReadSchemeCertificate(bytes, SignatureSchemeV2)
            ?? ReadSchemeCertificate(bytes, SignatureSchemeV3)
            ?? ReadJarCertificate(apkPath)
            ?? throw new InvalidDataException("El APK no está firmado.");
    }

    // ============================================================
    // AndroidManifest.xml (formato binario AXML)
    // ============================================================

    internal static ApkManifestInfo ParseManifest(byte[] xml)
    {
        if (xml.Length < 8 || ReadUInt16(xml, 0) != ChunkXml)
            throw new InvalidDataException("AndroidManifest.xml no tiene formato binario válido.");

        string[] strings = [];
        uint[] resourceIds = [];
        int offset = ReadUInt16(xml, 2);

        while (offset + 8 <= xml.Length)
        {
            var type = ReadUInt16(xml, offset);
            var size = (int)ReadUInt32(xml, offset + 4);
            if (size < 8 || offset + size > xml.Length)
                break;

            switch (type)
            {
                case ChunkStringPool:
                    strings = ReadStringPool(xml, offset);
                    break;

                case ChunkResourceMap:
                    var headerSize = ReadUInt16(xml, offset + 2);
                    var count = (size - headerSize) / 4;
                    resourceIds = new uint[count];
                    for (var i = 0; i < count; i++)
                        resourceIds[i] = ReadUInt32(xml, offset + headerSize + (i * 4));
                    break;

                case ChunkStartElement:
                    var manifest = TryReadManifestElement(xml, offset, strings, resourceIds);
                    if (manifest is not null)
                        return manifest;
                    break;
            }

            offset += size;
        }

        throw new InvalidDataException("AndroidManifest.xml no declara el paquete.");
    }

    private static ApkManifestInfo? TryReadManifestElement(
        byte[] xml,
        int offset,
        string[] strings,
        uint[] resourceIds)
    {
        var headerSize = ReadUInt16(xml, offset + 2);
        var ext = offset + headerSize;
        var name = StringAt(strings, ReadUInt32(xml, ext + 4));
        if (!string.Equals(name, "manifest", StringComparison.Ordinal))
            return null;

        var attributeStart = ReadUInt16(xml, ext + 8);
        var attributeSize = ReadUInt16(xml, ext + 10);
        var attributeCount = ReadUInt16(xml, ext + 12);

        string? packageName = null;
        string? versionName = null;
        long versionCode = 0;

        for (var i = 0; i < attributeCount; i++)
        {
            var attr = ext + attributeStart + (i * attributeSize);
            var nameIndex = ReadUInt32(xml, attr + 4);
            var raw = ReadUInt32(xml, attr + 8);
            var dataType = xml[attr + 15];
            var data = ReadUInt32(xml, attr + 16);

            var attrName = StringAt(strings, nameIndex);
            var resourceId = nameIndex < resourceIds.Length ? resourceIds[nameIndex] : 0;
            var text = raw != uint.MaxValue ? StringAt(strings, raw) : dataType == 0x03 ? StringAt(strings, data) : null;

            if (attrName == "package")
            {
                packageName = text;
            }
            else if (attrName == "versionCode" || resourceId == AttrVersionCode)
            {
                versionCode = text is not null && long.TryParse(text, out var parsed) ? parsed : data;
            }
            else if (attrName == "versionName" || resourceId == AttrVersionName)
            {
                versionName = text ?? (dataType is 0x10 or 0x11 ? data.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
            }
        }

        if (string.IsNullOrWhiteSpace(packageName))
            throw new InvalidDataException("AndroidManifest.xml no declara el paquete.");

        return new ApkManifestInfo(packageName.Trim(), versionCode, versionName);
    }

    private static string[] ReadStringPool(byte[] xml, int offset)
    {
        var count = (int)ReadUInt32(xml, offset + 8);
        var flags = ReadUInt32(xml, offset + 16);
        var stringsStart = (int)ReadUInt32(xml, offset + 20);
        var headerSize = ReadUInt16(xml, offset + 2);
        var utf8 = (flags & 0x100) != 0;

        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            var position = offset + stringsStart + (int)ReadUInt32(xml, offset + headerSize + (i * 4));
            result[i] = utf8 ? ReadUtf8(xml, position) : ReadUtf16(xml, position);
        }

        return result;
    }

    private static string ReadUtf8(byte[] xml, int position)
    {
        // Longitud en caracteres y luego en bytes (1 o 2 bytes cada una).
        position += (xml[position] & 0x80) != 0 ? 2 : 1;
        int length = xml[position];
        if ((length & 0x80) != 0)
        {
            length = ((length & 0x7f) << 8) | xml[position + 1];
            position += 2;
        }
        else
        {
            position += 1;
        }

        return Encoding.UTF8.GetString(xml, position, length);
    }

    private static string ReadUtf16(byte[] xml, int position)
    {
        int length = ReadUInt16(xml, position);
        if ((length & 0x8000) != 0)
        {
            length = ((length & 0x7fff) << 16) | ReadUInt16(xml, position + 2);
            position += 4;
        }
        else
        {
            position += 2;
        }

        return Encoding.Unicode.GetString(xml, position, length * 2);
    }

    private static string? StringAt(string[] strings, uint index)
        => index < strings.Length ? strings[index] : null;

    // ============================================================
    // APK Signing Block (v2 / v3)
    // ============================================================

    internal static byte[]? ReadSchemeCertificate(byte[] apk, uint schemeId)
    {
        var centralDirectory = FindCentralDirectoryOffset(apk);
        if (centralDirectory is null || centralDirectory < 32)
            return null;

        var cd = (int)centralDirectory.Value;
        if (!apk.AsSpan(cd - 16, 16).SequenceEqual(SigningBlockMagic))
            return null;

        var blockSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(apk.AsSpan(cd - 24, 8));
        var blockStart = cd - blockSize - 8;
        if (blockStart < 0)
            return null;

        var position = (int)blockStart + 8;
        var pairsEnd = cd - 24;
        while (position + 12 <= pairsEnd)
        {
            var length = (long)BinaryPrimitives.ReadUInt64LittleEndian(apk.AsSpan(position, 8));
            var id = BinaryPrimitives.ReadUInt32LittleEndian(apk.AsSpan(position + 8, 4));
            var valueStart = position + 12;
            var valueLength = (int)(length - 4);
            if (length < 4 || valueStart + valueLength > pairsEnd)
                return null;

            if (id == schemeId)
                return FirstSignerCertificate(apk.AsSpan(valueStart, valueLength));

            position = (int)(position + 8 + length);
        }

        return null;
    }

    private static byte[]? FirstSignerCertificate(ReadOnlySpan<byte> scheme)
    {
        // signers -> signer -> signed data -> (digests, certificates)
        var signers = LengthPrefixed(scheme, 0);
        var signer = LengthPrefixed(signers, 0);
        var signedData = LengthPrefixed(signer, 0);
        var digests = LengthPrefixed(signedData, 0);
        var certificates = LengthPrefixed(signedData, 4 + digests.Length);
        if (certificates.Length < 4)
            return null;

        return LengthPrefixed(certificates, 0).ToArray();
    }

    private static ReadOnlySpan<byte> LengthPrefixed(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 4 > data.Length)
            throw new InvalidDataException("Bloque de firma del APK truncado.");

        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        if (length < 0 || offset + 4 + length > data.Length)
            throw new InvalidDataException("Bloque de firma del APK truncado.");

        return data.Slice(offset + 4, length);
    }

    private static long? FindCentralDirectoryOffset(byte[] apk)
    {
        var minimum = Math.Max(0, apk.Length - 22 - 0xffff);
        for (var i = apk.Length - 22; i >= minimum; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(apk.AsSpan(i, 4)) == EndOfCentralDirectory)
                return BinaryPrimitives.ReadUInt32LittleEndian(apk.AsSpan(i + 16, 4));
        }

        return null;
    }

    private static byte[]? ReadJarCertificate(string apkPath)
    {
        using var archive = ZipFile.OpenRead(apkPath);
        var entry = archive.Entries.FirstOrDefault(x =>
            x.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase) &&
            (x.FullName.EndsWith(".RSA", StringComparison.OrdinalIgnoreCase) ||
             x.FullName.EndsWith(".DSA", StringComparison.OrdinalIgnoreCase) ||
             x.FullName.EndsWith(".EC", StringComparison.OrdinalIgnoreCase)));
        if (entry is null)
            return null;

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var cms = new SignedCms();
        cms.Decode(buffer.ToArray());
        return cms.Certificates.Count > 0 ? cms.Certificates[0].RawData : null;
    }

    private static ushort ReadUInt16(byte[] data, int offset)
        => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] data, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}
