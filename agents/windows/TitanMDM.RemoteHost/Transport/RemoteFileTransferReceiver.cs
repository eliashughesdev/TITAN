using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TitanMDM.RemoteHost.Transport;

/// <summary>
/// Recepcion de archivos tecnico -> equipo durante la sesion remota.
///
/// - El RemoteHost corre con el token del usuario: el archivo se crea en
///   su perfil (Descargas\TitanMDM) con su propiedad, nunca como SYSTEM.
/// - Escritura secuencial a un ".partial", SHA-256 incremental y
///   verificacion al finalizar. Si el hash no coincide se elimina.
/// - El nombre final nunca sobrescribe: se agrega " (n)".
/// </summary>
public sealed class RemoteFileTransferReceiver : IDisposable
{
    public const long MaxFileBytes = 200L * 1024 * 1024;

    private sealed class Incoming : IDisposable
    {
        public required string FileName { get; init; }
        public required string PartialPath { get; init; }
        public required long SizeBytes { get; init; }
        public required FileStream Stream { get; init; }
        public IncrementalHash Hash { get; } = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public long Received;

        public void Dispose()
        {
            Stream.Dispose();
            Hash.Dispose();
        }
    }

    private readonly ConcurrentDictionary<Guid, Incoming> _transfers = new();
    private readonly string _targetDirectory;

    public RemoteFileTransferReceiver(string? targetDirectory = null)
    {
        _targetDirectory = targetDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "TitanMDM");
    }

    public string TargetDirectory => _targetDirectory;

    public bool Begin(Guid transferId, string fileName, long sizeBytes)
    {
        if (sizeBytes is <= 0 or > MaxFileBytes)
        {
            return false;
        }

        var safeName = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(safeName) ||
            safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        Directory.CreateDirectory(_targetDirectory);

        var partialPath = Path.Combine(_targetDirectory, $".{transferId:N}.partial");

        var incoming = new Incoming
        {
            FileName = safeName,
            PartialPath = partialPath,
            SizeBytes = sizeBytes,
            Stream = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan),
        };

        if (!_transfers.TryAdd(transferId, incoming))
        {
            incoming.Dispose();
            File.Delete(partialPath);
            return false;
        }

        return true;
    }

    /// <returns>Total de bytes recibidos.</returns>
    public long Receive(Guid transferId, long offset, byte[] data)
    {
        if (!_transfers.TryGetValue(transferId, out var incoming))
        {
            throw new InvalidOperationException("Transferencia desconocida.");
        }

        lock (incoming)
        {
            if (offset != incoming.Received || incoming.Received + data.Length > incoming.SizeBytes)
            {
                throw new InvalidOperationException("Bloque fuera de secuencia.");
            }

            incoming.Stream.Write(data, 0, data.Length);
            incoming.Hash.AppendData(data);
            incoming.Received += data.Length;
            return incoming.Received;
        }
    }

    /// <returns>Ruta final del archivo.</returns>
    public string Complete(Guid transferId, string expectedSha256)
    {
        if (!_transfers.TryRemove(transferId, out var incoming))
        {
            throw new InvalidOperationException("Transferencia desconocida.");
        }

        string actual;

        try
        {
            if (incoming.Received != incoming.SizeBytes)
            {
                throw new InvalidOperationException("Archivo incompleto.");
            }

            incoming.Stream.Flush(flushToDisk: true);
            actual = Convert.ToHexString(incoming.Hash.GetHashAndReset()).ToLowerInvariant();
        }
        catch
        {
            incoming.Dispose();
            TryDelete(incoming.PartialPath);
            throw;
        }

        incoming.Dispose();

        if (!string.IsNullOrEmpty(expectedSha256) &&
            !string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(incoming.PartialPath);
            throw new InvalidOperationException("La verificación SHA-256 falló; el archivo se descartó.");
        }

        var finalPath = UniquePath(Path.Combine(_targetDirectory, incoming.FileName));
        File.Move(incoming.PartialPath, finalPath);
        return finalPath;
    }

    public void Cancel(Guid transferId)
    {
        if (_transfers.TryRemove(transferId, out var incoming))
        {
            incoming.Dispose();
            TryDelete(incoming.PartialPath);
        }
    }

    public void Dispose()
    {
        foreach (var id in _transfers.Keys)
        {
            Cancel(id);
        }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name} ({Guid.NewGuid():N}){extension}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // archivo bloqueado o ya eliminado
        }
    }
}
