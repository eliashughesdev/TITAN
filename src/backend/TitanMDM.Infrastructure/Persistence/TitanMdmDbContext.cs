using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Automation;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence;

public sealed class TitanMdmDbContext
    : DbContext
{
    public TitanMdmDbContext(
        DbContextOptions<TitanMdmDbContext> options)
        : base(
            options)
    {
    }

    // ============================================================
    // CORE
    // ============================================================

    public DbSet<Organization>
        Organizations =>
            Set<Organization>();

    public DbSet<Department>
        Departments =>
            Set<Department>();

    public DbSet<User>
        Users =>
            Set<User>();

    public DbSet<Role>
        Roles =>
            Set<Role>();

    public DbSet<Permission>
        Permissions =>
            Set<Permission>();

    public DbSet<UserRole>
        UserRoles =>
            Set<UserRole>();

    public DbSet<RolePermission>
        RolePermissions =>
            Set<RolePermission>();

    public DbSet<RefreshToken>
        RefreshTokens =>
            Set<RefreshToken>();

    // ============================================================
    // DEVICES
    // ============================================================

    public DbSet<Device>
        Devices =>
            Set<Device>();

    public DbSet<DeviceCredential>
        DeviceCredentials =>
            Set<DeviceCredential>();

    public DbSet<DeviceCommand>
        DeviceCommands =>
            Set<DeviceCommand>();

    public DbSet<DeviceApplication>
        DeviceApplications =>
            Set<DeviceApplication>();

    // ============================================================
    // ENROLLMENT
    // ============================================================

    public DbSet<EnrollmentToken>
        EnrollmentTokens =>
            Set<EnrollmentToken>();

    // ============================================================
    // POLICIES
    // ============================================================

    public DbSet<Policy>
        Policies =>
            Set<Policy>();

    public DbSet<PolicyVersion>
        PolicyVersions =>
            Set<PolicyVersion>();

    public DbSet<DevicePolicyAssignment>
        DevicePolicyAssignments =>
            Set<DevicePolicyAssignment>();

    // ============================================================
    // ANDROID
    // ============================================================

    public DbSet<AndroidPolicyPublication>
        AndroidPolicyPublications =>
            Set<AndroidPolicyPublication>();

    public DbSet<AndroidEnterpriseConfiguration>
        AndroidEnterpriseConfigurations =>
            Set<AndroidEnterpriseConfiguration>();

    public DbSet<AndroidEnterpriseSignupSession>
        AndroidEnterpriseSignupSessions =>
            Set<AndroidEnterpriseSignupSession>();

    public DbSet<AndroidEnrollment>
        AndroidEnrollments =>
            Set<AndroidEnrollment>();

    public DbSet<AndroidDevice>
        AndroidDevices =>
            Set<AndroidDevice>();

    // ============================================================
    // SECURITY / COMPLIANCE
    // ============================================================

    public DbSet<DeviceSecurityPosture>
        DeviceSecurityPostures =>
            Set<DeviceSecurityPosture>();

    // ============================================================
    // DEVICE GROUPS
    // ============================================================

    public DbSet<DeviceGroup>
        DeviceGroups =>
            Set<DeviceGroup>();

    public DbSet<DeviceGroupMember>
        DeviceGroupMembers =>
            Set<DeviceGroupMember>();

    // ============================================================
    // LOCATION / GEOFENCING
    // ============================================================

    public DbSet<DeviceLocation>
        DeviceLocations =>
            Set<DeviceLocation>();

    public DbSet<Geofence>
        Geofences =>
            Set<Geofence>();

    public DbSet<GeofenceDeviceAssignment>
        GeofenceDeviceAssignments =>
            Set<GeofenceDeviceAssignment>();

    public DbSet<GeofenceDeviceState>
        GeofenceDeviceStates =>
            Set<GeofenceDeviceState>();

    public DbSet<GeofenceEvent>
        GeofenceEvents =>
            Set<GeofenceEvent>();

    // ============================================================
    // AUTOMATION
    // ============================================================

    public DbSet<AutomationRule>
        AutomationRules =>
            Set<AutomationRule>();

    public DbSet<AutomationExecution>
        AutomationExecutions =>
            Set<AutomationExecution>();

    // ============================================================
    // LOST MODE
    // ============================================================

    public DbSet<LostModeSession>
        LostModeSessions =>
            Set<LostModeSession>();

    // ============================================================
    // REMOTE SUPPORT
    // ============================================================

    public DbSet<RemoteSession>
        RemoteSessions =>
            Set<RemoteSession>();

    public DbSet<RemoteSessionEvent>
        RemoteSessionEvents =>
            Set<RemoteSessionEvent>();

    public DbSet<RemoteSessionControlLease>
        RemoteSessionControlLeases =>
            Set<RemoteSessionControlLease>();

    public DbSet<RemoteSessionParticipant>
        RemoteSessionParticipants =>
            Set<RemoteSessionParticipant>();

    // ============================================================
    // SOFTWARE
    // ============================================================

    public DbSet<SoftwarePackage>
        SoftwarePackages =>
            Set<SoftwarePackage>();

    public DbSet<SoftwareDeployment>
        SoftwareDeployments =>
            Set<SoftwareDeployment>();

    // ============================================================
    // HELPDESK CORE
    // ============================================================

    public DbSet<HelpdeskTicket>
        HelpdeskTickets =>
            Set<HelpdeskTicket>();

    public DbSet<HelpdeskTicketComment>
        HelpdeskTicketComments =>
            Set<HelpdeskTicketComment>();

    public DbSet<HelpdeskTicketEvent>
        HelpdeskTicketEvents =>
            Set<HelpdeskTicketEvent>();

    public DbSet<HelpdeskQueue>
        HelpdeskQueues =>
            Set<HelpdeskQueue>();

    // ============================================================
    // HELPDESK EMAIL
    // ============================================================

    public DbSet<HelpdeskEmailMessage>
        HelpdeskEmailMessages =>
            Set<HelpdeskEmailMessage>();

    public DbSet<HelpdeskMailSettings>
        HelpdeskMailSettings =>
            Set<HelpdeskMailSettings>();

    public DbSet<HelpdeskMailProcessingLog>
        HelpdeskMailProcessingLogs =>
            Set<HelpdeskMailProcessingLog>();

    public DbSet<HelpdeskOutboundEmail>
        HelpdeskOutboundEmails =>
            Set<HelpdeskOutboundEmail>();

    public DbSet<HelpdeskTicketAttachment>
        HelpdeskTicketAttachments =>
            Set<HelpdeskTicketAttachment>();

    // ============================================================
    // ENTRA ID
    // ============================================================

    public DbSet<EntraIdSettings>
        EntraIdSettings =>
            Set<EntraIdSettings>();

    public DbSet<EntraDirectoryUser>
        EntraDirectoryUsers =>
            Set<EntraDirectoryUser>();

    // ============================================================
    // HELPDESK ORGANIZATION
    // ============================================================

    public DbSet<HelpdeskZone>
        HelpdeskZones =>
            Set<HelpdeskZone>();

    public DbSet<HelpdeskTeam>
        HelpdeskTeams =>
            Set<HelpdeskTeam>();

    public DbSet<HelpdeskTeamZone>
        HelpdeskTeamZones =>
            Set<HelpdeskTeamZone>();

    public DbSet<HelpdeskTeamMember>
        HelpdeskTeamMembers =>
            Set<HelpdeskTeamMember>();

    public DbSet<HelpdeskUserZone>
        HelpdeskUserZones =>
            Set<HelpdeskUserZone>();

    public DbSet<HelpdeskAssistantAccess>
        HelpdeskAssistantAccess =>
            Set<HelpdeskAssistantAccess>();

    public DbSet<HelpdeskSiteCoverage>
        HelpdeskSiteCoverages =>
            Set<HelpdeskSiteCoverage>();

    // ============================================================
    // AUTHORIZATION / SCOPES
    // ============================================================

    public DbSet<UserScopeGrant>
        UserScopeGrants =>
            Set<UserScopeGrant>();

    // ============================================================
    // AUDIT
    // ============================================================

    public DbSet<AdministrativeAuditEvent>
        AdministrativeAuditEvents =>
            Set<AdministrativeAuditEvent>();

    // ============================================================
    // SITES
    // ============================================================

    public DbSet<Site>
        Sites =>
            Set<Site>();

    public DbSet<SiteLocation>
        SiteLocations =>
            Set<SiteLocation>();

    // ============================================================
    // MODEL CONFIGURATION
    // ============================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder);

        modelBuilder
            .ApplyConfigurationsFromAssembly(
                typeof(TitanMdmDbContext)
                    .Assembly);
    }
}