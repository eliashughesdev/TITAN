using System.Text.Json.Nodes;

namespace TitanMDM.Application.Android.Policies;

public interface IAndroidPolicyCompiler
{
    AndroidPolicyCompilationResult Compile(
        string configurationJson);
}

public sealed record AndroidPolicyCompilationResult(
    bool IsValid,
    JsonNode? GooglePolicy,
    IReadOnlyCollection<string> Errors,
    IReadOnlyCollection<string> Warnings)
{
    public static AndroidPolicyCompilationResult Success(
        JsonNode policy,
        IReadOnlyCollection<string>? warnings = null)
    {
        return new AndroidPolicyCompilationResult(
            true,
            policy,
            Array.Empty<string>(),
            warnings ?? Array.Empty<string>());
    }

    public static AndroidPolicyCompilationResult Failure(
        IReadOnlyCollection<string> errors)
    {
        return new AndroidPolicyCompilationResult(
            false,
            null,
            errors,
            Array.Empty<string>());
    }
}