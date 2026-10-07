using System.Drawing.Drawing2D;
using TitanMDM.RemoteHost.Models;
using TitanMDM.RemoteHost.Transport;

namespace TitanMDM.RemoteHost.UI;

public sealed class RemoteSessionIndicatorForm
    : Form
{
    private readonly RemoteHostSession
        _session;

    private readonly Label
        _durationLabel;

    private readonly Label
        _statusLabel;

    private readonly System.Windows.Forms.Timer
        _timer;

    private readonly DateTime
        _startedAtUtc;

    private RemoteTransportClient?
        _transport;

    private bool
        _allowProgrammaticClose;

    public RemoteSessionIndicatorForm(
        RemoteHostSession session)
    {
        _session =
            session;

        _startedAtUtc =
            DateTime.UtcNow;

        FormBorderStyle =
            FormBorderStyle.None;

        ShowInTaskbar =
            true;

        TopMost =
            true;

        StartPosition =
            FormStartPosition.Manual;

        Width = 460;
        Height = 142;

        BackColor =
            Color.FromArgb(
                14,
                20,
                32);

        Padding =
            new Padding(1);

        var workingArea =
            Screen.PrimaryScreen?.WorkingArea
            ?? new Rectangle(
                0,
                0,
                1920,
                1080);

        Location =
            new Point(
                workingArea.Right -
                Width -
                20,

                workingArea.Top +
                20);

        var container =
            new Panel
            {
                Dock =
                    DockStyle.Fill,

                BackColor =
                    Color.FromArgb(
                        20,
                        29,
                        46),

                Padding =
                    new Padding(
                        16,
                        12,
                        16,
                        12)
            };

        var title =
            new Label
            {
                AutoSize =
                    true,

                Text =
                    "●  TitanMDM · Soporte remoto activo",

                Font =
                    new Font(
                        "Segoe UI",
                        11F,
                        FontStyle.Bold),

                ForeColor =
                    Color.FromArgb(
                        125,
                        211,
                        252),

                Location =
                    new Point(
                        16,
                        12)
            };

        _statusLabel =
            new Label
            {
                AutoSize =
                    true,

                Text =
                    "Estado: conectando...",

                Font =
                    new Font(
                        "Segoe UI",
                        8.5F,
                        FontStyle.Bold),

                ForeColor =
                    Color.FromArgb(
                        251,
                        191,
                        36),

                Location =
                    new Point(
                        18,
                        40)
            };

        var technician =
            new Label
            {
                AutoSize =
                    true,

                Text =
                    $"Personal TIC: {_session.TechnicianName}",

                Font =
                    new Font(
                        "Segoe UI",
                        9F),

                ForeColor =
                    Color.White,

                Location =
                    new Point(
                        18,
                        64)
            };

        var reason =
            new Label
            {
                AutoSize =
                    false,

                Width =
                    410,

                Height =
                    22,

                Text =
                    $"Motivo: {_session.Reason}",

                Font =
                    new Font(
                        "Segoe UI",
                        8.5F),

                ForeColor =
                    Color.FromArgb(
                        203,
                        213,
                        225),

                Location =
                    new Point(
                        18,
                        88),

                AutoEllipsis =
                    true
            };

        _durationLabel =
            new Label
            {
                AutoSize =
                    true,

                Text =
                    "Duración: 00:00:00",

                Font =
                    new Font(
                        "Segoe UI",
                        8F),

                ForeColor =
                    Color.FromArgb(
                        148,
                        163,
                        184),

                Location =
                    new Point(
                        18,
                        114)
            };

        container.Controls.Add(
            title);

        container.Controls.Add(
            _statusLabel);

        container.Controls.Add(
            technician);

        container.Controls.Add(
            reason);

        container.Controls.Add(
            _durationLabel);

        Controls.Add(
            container);

        _timer =
            new System.Windows.Forms.Timer
            {
                Interval =
                    1000
            };

        _timer.Tick +=
            (_, _) =>
            {
                var elapsed =
                    DateTime.UtcNow -
                    _startedAtUtc;

                _durationLabel.Text =
                    $"Duración: {elapsed:hh\\:mm\\:ss}";

                if (DateTime.UtcNow >=
                    _session.ExpiresAtUtc)
                {
                    _allowProgrammaticClose =
                        true;

                    Close();
                }
            };

        Shown +=
            async (_, _) =>
            {
                await ConnectTransportAsync();
            };

        _timer.Start();
    }

    private async Task ConnectTransportAsync()
    {
        try
        {
            _transport =
                new RemoteTransportClient(
                    _session);

            _transport.StatusChanged +=
                status =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    BeginInvoke(
                        () =>
                        {
                            _statusLabel.Text =
                                $"Estado: {status}";

                            _statusLabel.ForeColor =
                                status == "Conectado"
                                    ? Color.FromArgb(
                                        74,
                                        222,
                                        128)
                                    : Color.FromArgb(
                                        251,
                                        191,
                                        36);
                        });
                };

                _transport.ConnectionLost += () =>
{
                    if (IsDisposed || !IsHandleCreated)
                    {
                        return;
                    }

                    BeginInvoke(() =>
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        _allowProgrammaticClose = true;
                        Close();
                    });
                };

            await _transport.ConnectAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text =
                "Estado: error de conexión";

            _statusLabel.ForeColor =
                Color.FromArgb(
                    248,
                    113,
                    113);

            MessageBox.Show(
                ex.Message,
                "TitanMDM Remote Support",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen =
            new Pen(
                Color.FromArgb(
                    14,
                    165,
                    233),
                1F);

        e.Graphics.SmoothingMode =
            SmoothingMode.AntiAlias;

        e.Graphics.DrawRectangle(
            pen,
            0,
            0,
            Width - 1,
            Height - 1);
    }

    protected override void OnFormClosing(
        FormClosingEventArgs e)
    {
        if (!_allowProgrammaticClose
            &&
            e.CloseReason ==
                CloseReason.UserClosing)
        {
            e.Cancel =
                true;

            return;
        }

        _timer.Stop();

        base.OnFormClosing(e);
    }

    protected override async void OnFormClosed(
        FormClosedEventArgs e)
    {
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }

        base.OnFormClosed(e);
    }

    protected override bool
        ShowWithoutActivation =>
            true;

    protected override CreateParams
        CreateParams
    {
        get
        {
            var parameters =
                base.CreateParams;

            const int WS_EX_TOOLWINDOW =
                0x00000080;

            const int WS_EX_NOACTIVATE =
                0x08000000;

            parameters.ExStyle |=
                WS_EX_TOOLWINDOW |
                WS_EX_NOACTIVATE;

            return parameters;
        }
    }
}