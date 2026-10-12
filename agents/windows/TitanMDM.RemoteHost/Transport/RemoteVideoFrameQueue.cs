
using System.Threading.Channels;

namespace TitanMDM.RemoteHost.Transport;

public enum RemoteVideoCodec
{
    H264 = 1,
    Jpeg = 2
}

public sealed record RemoteEncodedVideoFrame(
    Guid SessionId,
    long Sequence,
    RemoteVideoCodec Codec,
    int Width,
    int Height,
    bool IsKeyFrame,
    DateTime CapturedAtUtc,
    byte[] Payload)
{
    public int ByteLength => Payload.Length;
}

public sealed record RemoteVideoQueueMetrics(
    long FramesEnqueued,
    long FramesDequeued,
    long FramesDropped,
    long BytesDequeued,
    double AverageQueueDelayMs);

/// <summary>
/// Cola acotada para el transporte multimedia.
///
/// Principio fundamental:
/// bajo congestión se priorizan imágenes recientes,
/// evitando acumular segundos de retraso.
///
/// No contiene autenticación ni acceso al escritorio.
/// </summary>
public sealed class RemoteVideoFrameQueue
{
    private readonly Channel<RemoteEncodedVideoFrame> _channel;
    private readonly object _sync = new();

    private readonly int _capacity;
    private long _enqueued;
    private long _dequeued;
    private long _dropped;
    private long _bytesDequeued;
    private long _queueDepth;
    private double _averageDelayMs;

    public RemoteVideoFrameQueue(int capacity = 2)
    {
        if (capacity < 1 || capacity > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "La capacidad debe estar entre 1 y 8.");
        }

        _capacity = capacity;

        _channel =
            Channel.CreateBounded<RemoteEncodedVideoFrame>(
                new BoundedChannelOptions(capacity)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });
    }

    public bool TryEnqueue(RemoteEncodedVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.SessionId == Guid.Empty ||
            frame.Width <= 0 ||
            frame.Height <= 0 ||
            frame.Payload.Length == 0)
        {
            throw new ArgumentException(
                "El frame multimedia no es válido.",
                nameof(frame));
        }

        lock (_sync)
        {
            // H.264 tiene dependencias entre imágenes.
            // Descartar un frame arbitrario puede invalidar
            // los siguientes hasta recibir otro keyframe.
            //
            // Esta cola no elimina frames H.264 internamente.
            // Su productor debe reaccionar a false
            // reduciendo bitrate o generando un IDR.
            if (_queueDepth >= _capacity)
            {
                if (frame.Codec == RemoteVideoCodec.H264)
                {
                    _dropped++;
                    return false;
                }

                // Para JPEG, cada imagen es independiente:
                // es seguro eliminar la más antigua.
                if (_channel.Reader.TryRead(out _))
                {
                    _queueDepth--;
                    _dropped++;
                }
            }

            if (!_channel.Writer.TryWrite(frame))
            {
                _dropped++;
                return false;
            }

            _queueDepth++;
            _enqueued++;
            return true;
        }
    }

    public async ValueTask<RemoteEncodedVideoFrame>
        DequeueAsync(CancellationToken cancellationToken)
    {
        var frame = await _channel.Reader.ReadAsync(
            cancellationToken);

        lock (_sync)
        {
            _queueDepth--;
            _dequeued++;
            _bytesDequeued += frame.ByteLength;

            var delay = Math.Max(
                0,
                (DateTime.UtcNow -
                 frame.CapturedAtUtc).TotalMilliseconds);

            _averageDelayMs =
                _dequeued == 1
                    ? delay
                    : (_averageDelayMs * 0.9) +
                      (delay * 0.1);
        }

        return frame;
    }

    public RemoteVideoQueueMetrics GetMetrics()
    {
        lock (_sync)
        {
            return new RemoteVideoQueueMetrics(
                _enqueued,
                _dequeued,
                _dropped,
                _bytesDequeued,
                Math.Round(_averageDelayMs, 2));
        }
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
