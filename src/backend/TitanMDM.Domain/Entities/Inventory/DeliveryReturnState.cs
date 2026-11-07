namespace TitanMDM.Domain.Entities.Inventory;

/// <summary>
/// Estado de devolución de una entrega (Hand Held y RP4D por separado).
/// Las entregas anteriores a returned_handheld / returned_rp4d solo tenían
/// la columna "returned": si vale 1, la entrega se cerró completa y ambos
/// equipos se consideran devueltos.
/// </summary>
public static class DeliveryReturnState
{
    public static bool HandheldReturned(Delivery delivery)
        => delivery.ReturnedHandheld || delivery.Returned == true;

    public static bool Rp4dReturned(Delivery delivery)
        => !delivery.IncludeRp4d || delivery.ReturnedRp4d || delivery.Returned == true;

    /// <summary>Hand Held sin devolver, o RP4D incluida y sin devolver.</summary>
    public static bool IsPending(Delivery delivery)
        => !HandheldReturned(delivery) || !Rp4dReturned(delivery);

    /// <summary>Completa las marcas individuales de un registro legado (solo lectura).</summary>
    public static void NormalizeLegacy(Delivery delivery)
    {
        if (delivery.Returned != true)
            return;

        delivery.ReturnedHandheld = true;
        if (delivery.IncludeRp4d)
            delivery.ReturnedRp4d = true;
    }
}
