using System.Text.Json.Nodes;
using TitanMDM.Infrastructure.Commands;

namespace TitanMDM.UnitTests;

/*
 * Bloqueo por extravío: el PIN temporal viaja al agente pero nunca se
 * muestra en la consola (historial, SignalR, auditoría).
 */
public sealed class CommandPayloadRedactorTests
{
    [Fact]
    public void Oculta_el_pin_de_lock_device_y_conserva_el_mensaje()
    {
        var redacted = CommandPayloadRedactor.Redact(
            "LOCK_DEVICE",
            """{"pin":"2468","message":"Llame a TI"}""");

        var payload = JsonNode.Parse(redacted)!.AsObject();
        Assert.Equal(CommandPayloadRedactor.Mask, payload["pin"]!.GetValue<string>());
        Assert.Equal("Llame a TI", payload["message"]!.GetValue<string>());
        Assert.DoesNotContain("2468", redacted);
    }

    [Theory]
    [InlineData("RING_DEVICE", """{"pin":"2468"}""")]
    [InlineData("LOCK_DEVICE", "{}")]
    [InlineData("LOCK_DEVICE", "no-json pin")]
    public void No_toca_otros_payloads(string type, string payload)
    {
        Assert.Equal(payload, CommandPayloadRedactor.Redact(type, payload));
    }

    [Theory]
    [InlineData("""{"pin":"2468"}""", true)]
    [InlineData("""{"message":"Extraviado"}""", true)]
    [InlineData("""{"message":"  "}""", false)]
    [InlineData("{}", false)]
    public void Detecta_el_bloqueo_por_extravio(string payload, bool expected)
    {
        Assert.Equal(expected, CommandPayloadRedactor.IsLostModeLock("LOCK_DEVICE", payload));
        Assert.False(CommandPayloadRedactor.IsLostModeLock("RING_DEVICE", payload));
    }
}
