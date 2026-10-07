namespace TitanMDM.Api.Services;

public sealed class WindowsAgentDistributionOptions
{
    public const string SectionName =
        "WindowsAgentDistribution";

    public string PublicServerUrl
    {
        get;
        set;
    } =
        string.Empty;

    public string PackagePath
    {
        get;
        set;
    } =
        "../../../artifacts/windows-agent/TitanMDM-WindowsAgent-x64.zip";

    public string PackageFileName
    {
        get;
        set;
    } =
        "TitanMDM-WindowsAgent-x64.zip";

    public string ScriptsPath
    {
        get;
        set;
    } =
        "../../../scripts/windows";

    public string InnoSetupCompilerPath
    {
        get;
        set;
    } =
        @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe";

    public string IndividualInstallerFileName
    {
        get;
        set;
    } =
        "TitanMDM-Agent-Setup.exe";

    public string GpoPackageFileName
    {
        get;
        set;
    } =
        "TitanMDM-GPO.zip";
}