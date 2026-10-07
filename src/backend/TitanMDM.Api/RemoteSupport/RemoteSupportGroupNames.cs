namespace TitanMDM.Api.RemoteSupport;

public static class RemoteSupportGroupNames
{
    public static string Organization(
        Guid organizationId)
    {
        return
            $"organization:{organizationId:N}";
    }

    public static string Technicians(
        Guid organizationId,
        Guid sessionId)
    {
        return
            $"organization:{organizationId:N}:remote:{sessionId:N}:technicians";
    }

    public static string Host(
        Guid organizationId,
        Guid sessionId)
    {
        return
            $"organization:{organizationId:N}:remote:{sessionId:N}:host";
    }

    public static string Session(
        Guid organizationId,
        Guid sessionId)
    {
        return Technicians(
            organizationId,
            sessionId);
    }
}