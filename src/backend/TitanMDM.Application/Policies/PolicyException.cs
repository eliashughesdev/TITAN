namespace TitanMDM.Application.Policies;

public sealed class PolicyException : Exception
{
    public PolicyException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}