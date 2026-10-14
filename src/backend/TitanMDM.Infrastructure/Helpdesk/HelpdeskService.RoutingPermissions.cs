namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    /*
     * La elegibilidad de técnicos se centraliza en
     * HelpdeskTechnicianEligibility para evitar divergencias
     * entre el motor de routing, la planificación de grupos y
     * el controlador de staff.
     */

    private IQueryable<Guid>
        EligibleTechnicians(
            Guid organizationId)
    {
        return
            HelpdeskTechnicianEligibility
                .EligibleTechnicianIds(
                    _db,
                    organizationId);
    }
}
