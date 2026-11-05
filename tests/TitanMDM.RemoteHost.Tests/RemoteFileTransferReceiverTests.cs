using System.Security.Cryptography;

using TitanMDM.RemoteHost.Transport;

namespace TitanMDM.RemoteHost.Tests;

public sealed class RemoteFileTransferReceiverTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "titanmdm-ft-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ChunkedTransfer_WithMatchingHash_ProducesFile()
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);
        var data = RandomNumberGenerator.GetBytes(600_000);
        var id = Guid.NewGuid();

        Assert.True(receiver.Begin(id, "reporte.pdf", data.Length));

        long offset = 0;
        foreach (var chunk in data.Chunk(256 * 1024))
        {
            offset = receiver.Receive(id, offset, chunk);
        }

        var path = receiver.Complete(id, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());

        Assert.Equal(Path.Combine(_directory, "reporte.pdf"), path);
        Assert.Equal(data, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.partial"));
    }

    [Fact]
    public void HashMismatch_DiscardsFile()
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);
        var id = Guid.NewGuid();

        receiver.Begin(id, "a.txt", 3);
        receiver.Receive(id, 0, new byte[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() => receiver.Complete(id, new string('0', 64)));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void OutOfSequenceChunk_IsRejected()
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);
        var id = Guid.NewGuid();

        receiver.Begin(id, "a.txt", 10);

        Assert.Throws<InvalidOperationException>(() => receiver.Receive(id, 5, new byte[] { 1 }));
        Assert.Throws<InvalidOperationException>(() => receiver.Receive(id, 0, new byte[11]));
    }

    [Fact]
    public void ExistingFile_IsNeverOverwritten()
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);

        string Send(byte value)
        {
            var id = Guid.NewGuid();
            receiver.Begin(id, "notas.txt", 1);
            receiver.Receive(id, 0, new[] { value });
            return receiver.Complete(id, string.Empty);
        }

        var first = Send(1);
        var second = Send(2);

        Assert.EndsWith("notas.txt", first);
        Assert.EndsWith("notas (1).txt", second);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(first));
    }

    [Theory]
    [InlineData("..\\..\\Windows\\evil.dll", 10, true)]
    [InlineData("ok.txt", 0, false)]
    [InlineData("ok.txt", RemoteFileTransferReceiver.MaxFileBytes + 1, false)]
    public void Begin_ValidatesNameAndSize(string name, long size, bool accepted)
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);
        var id = Guid.NewGuid();

        Assert.Equal(accepted, receiver.Begin(id, name, size));

        if (accepted)
        {
            // Las rutas se reducen al nombre: nunca se escribe fuera del destino.
            receiver.Receive(id, 0, new byte[size]);
            var path = receiver.Complete(id, string.Empty);
            Assert.Equal(Path.Combine(_directory, "evil.dll"), path);
        }
    }

    [Fact]
    public void Cancel_RemovesPartialFile()
    {
        using var receiver = new RemoteFileTransferReceiver(_directory);
        var id = Guid.NewGuid();

        receiver.Begin(id, "big.iso", 100);
        receiver.Receive(id, 0, new byte[10]);
        receiver.Cancel(id);

        Assert.Empty(Directory.GetFiles(_directory));
    }
}
