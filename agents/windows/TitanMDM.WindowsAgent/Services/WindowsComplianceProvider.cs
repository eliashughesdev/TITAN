using System.Text.Json;

namespace TitanMDM.WindowsAgent.Services;

public sealed class WindowsComplianceProvider
{
    private readonly WindowsSecurityProvider
        _securityProvider;

    public WindowsComplianceProvider(
        WindowsSecurityProvider securityProvider)
    {
        _securityProvider =
            securityProvider;
    }

    public async Task<WindowsComplianceSnapshot>
        EvaluateAsync(
            CancellationToken cancellationToken = default)
    {
        var security =
            await _securityProvider
                .CollectAsync(
                    cancellationToken);

        var checks =
            new List<
                WindowsComplianceCheck>();

        checks.Add(
            CreateCheck(
                "UAC_ENABLED",
                "Control de cuentas de usuario",
                security.UacEnabled,
                "UAC debe permanecer habilitado."));

        checks.Add(
            CreateCheck(
                "SECURE_BOOT",
                "Secure Boot",
                security.SecureBootEnabled
                    is not false,
                security.SecureBootEnabled is null
                    ? "No fue posible determinar Secure Boot."
                    : "Secure Boot debe estar habilitado."));

        checks.Add(
            CreateCheck(
                "DEFENDER_AVAILABLE",
                "Microsoft Defender",
                security.Defender.Available,
                security.Defender.Error
                    ?? "Microsoft Defender debe estar disponible."));

        checks.Add(
            CreateCheck(
                "FIREWALL_AVAILABLE",
                "Windows Firewall",
                security.Firewall.Available,
                security.Firewall.Error
                    ?? "Windows Firewall debe estar disponible."));

        checks.Add(
            CreateCheck(
                "BITLOCKER_STATUS",
                "BitLocker",
                security.BitLocker.Available,
                security.BitLocker.Error
                    ?? "Debe poder verificarse el cifrado del dispositivo."));

        checks.Add(
            CreateCheck(
                "TPM_STATUS",
                "Trusted Platform Module",
                security.Tpm.Available,
                security.Tpm.Error
                    ?? "Debe poder verificarse el TPM."));

        checks.Add(
            CreateCheck(
                "NO_PENDING_REBOOT",
                "Reinicio pendiente",
                !security.PendingReboot,
                security.PendingReboot
                    ? "Windows requiere un reinicio."
                    : "No existe reinicio pendiente."));

        var passed =
            checks.Count(
                check =>
                    check.Passed);

        var failed =
            checks.Count - passed;

        var score =
            checks.Count == 0
                ? 100
                : (int)Math.Round(
                    passed * 100d /
                    checks.Count);

        var status =
            failed == 0
                ? "Compliant"
                : "NonCompliant";

        var riskLevel =
            score >= 90
                ? "Low"
                : score >= 70
                    ? "Medium"
                    : score >= 50
                        ? "High"
                        : "Critical";

        return new WindowsComplianceSnapshot(
            Status:
                status,

            Score:
                score,

            RiskLevel:
                riskLevel,

            TotalChecks:
                checks.Count,

            PassedChecks:
                passed,

            FailedChecks:
                failed,

            Checks:
                checks,

            Security:
                security,

            EvaluatedAtUtc:
                DateTime.UtcNow);
    }

    public async Task<string>
        EvaluateAsJsonAsync(
            CancellationToken cancellationToken = default)
    {
        var result =
            await EvaluateAsync(
                cancellationToken);

        return JsonSerializer.Serialize(
            result);
    }

    private static WindowsComplianceCheck
        CreateCheck(
            string code,
            string name,
            bool passed,
            string message)
    {
        return new WindowsComplianceCheck(
            Code:
                code,

            Name:
                name,

            Passed:
                passed,

            Message:
                message);
    }
}

public sealed record WindowsComplianceSnapshot(
    string Status,
    int Score,
    string RiskLevel,
    int TotalChecks,
    int PassedChecks,
    int FailedChecks,
    IReadOnlyCollection<
        WindowsComplianceCheck> Checks,
    WindowsSecuritySnapshot Security,
    DateTime EvaluatedAtUtc);

public sealed record WindowsComplianceCheck(
    string Code,
    string Name,
    bool Passed,
    string Message);