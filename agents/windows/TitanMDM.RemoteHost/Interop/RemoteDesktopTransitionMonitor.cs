
namespace TitanMDM.RemoteHost.Interop;

/// <summary>
/// Supervisa cambios del escritorio activo sin intentar
/// atravesar los límites de seguridad de Windows.
///
/// No guarda capturas, credenciales ni eventos de teclado.
/// </summary>
public sealed class RemoteDesktopTransitionMonitor
{
    private readonly WindowsDesktopStateProbe _probe;
    private readonly object _sync = new();

    private WindowsDesktopState? _previous;
    private long _transitionNumber;

    public RemoteDesktopTransitionMonitor(
        WindowsDesktopStateProbe? probe = null)
    {
        _probe = probe ?? new WindowsDesktopStateProbe();
    }

    public RemoteDesktopTransitionSnapshot Check()
    {
        lock (_sync)
        {
            var current = _probe.GetState();

            var changed =
                _previous is null ||
                _previous.Kind != current.Kind ||
                _previous.Availability != current.Availability ||
                _previous.WindowsSessionId !=
                    current.WindowsSessionId;

            if (changed)
            {
                _transitionNumber++;
            }

            var snapshot = new RemoteDesktopTransitionSnapshot(
                Sequence: _transitionNumber,
                Changed: changed,
                Current: current,
                Previous: _previous,
                CanCaptureDefaultDesktop:
                    current.Kind == WindowsDesktopKind.Default &&
                    current.CanUseDefaultDesktop,
                ProtectedOrUnavailable:
                    current.Kind != WindowsDesktopKind.Default,
                Recovered:
                    _previous is not null &&
                    _previous.Kind != WindowsDesktopKind.Default &&
                    current.Kind == WindowsDesktopKind.Default,
                ObservedAtUtc: current.ObservedAtUtc);

            _previous = current;

            return snapshot;
        }
    }

    public static string Describe(
        RemoteDesktopTransitionSnapshot snapshot)
    {
        return snapshot.Current.Kind switch
        {
            WindowsDesktopKind.Default
                when snapshot.Recovered =>
                "Escritorio normal recuperado; reanudando transmisión",

            WindowsDesktopKind.Default =>
                "Escritorio normal disponible",

            WindowsDesktopKind.Winlogon =>
                "Windows cambió al escritorio protegido Winlogon",

            WindowsDesktopKind.ScreenSaver =>
                "Windows activó el escritorio del protector de pantalla",

            WindowsDesktopKind.AccessDenied =>
                "Windows denegó acceso al escritorio de entrada",

            WindowsDesktopKind.Unavailable =>
                "Escritorio de entrada temporalmente inaccesible",

            WindowsDesktopKind.Other =>
                "Windows activó otro escritorio interactivo",

            _ =>
                "Estado de escritorio desconocido"
        };
    }
}

public sealed record RemoteDesktopTransitionSnapshot(
    long Sequence,
    bool Changed,
    WindowsDesktopState Current,
    WindowsDesktopState? Previous,
    bool CanCaptureDefaultDesktop,
    bool ProtectedOrUnavailable,
    bool Recovered,
    DateTime ObservedAtUtc);
