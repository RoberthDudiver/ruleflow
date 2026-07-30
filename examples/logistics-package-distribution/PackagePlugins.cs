using Dudiver.RuleFlow.Sdk;

namespace PackageDistribution;

// -----------------------------------------------------------------------------------------------------
// Package processing & distribution.
//
// In RuleFlow, the LIFECYCLE (states + transitions + rules) is designed in the visual designer; these
// PLUGINS are the actions that compute the fields the rules then route on. See README.md for the full flow.
//
// Lifecycle: Received -> Classified -> InTransit -> OnDelivery -> Delivered | Returned
// -----------------------------------------------------------------------------------------------------

/// <summary>
/// Classifies a just-received package. Combines three business rules that all apply in the same phase
/// (weight, insurance, SLA) into computed fields — the transitions then route on them.
/// </summary>
[ProcessDefinition("pkg-classify",
    DisplayName = "Package classification",
    Version = "1.0",
    Description = "Assigns vehicle by weight, signature by declared value, and priority by client type.")]
public sealed class ClassifyPackage : IProcess
{
    [ProcessInput(Label = "Weight (kg)", Required = true)]
    public decimal WeightKg { get; set; }

    [ProcessInput(Label = "Declared value", Required = true)]
    public decimal DeclaredValue { get; set; }

    [ProcessInput(Label = "Client type")]
    public string ClientType { get; set; } = "";

    public Task<ProcessResult> ExecuteAsync(ProcessContext ctx, CancellationToken ct)
    {
        var output = new Dictionary<string, object?>
        {
            // Rule "Weight classification": > 25 kg → special truck.
            ["vehicle"] = WeightKg > 25 ? "Special truck" : "Standard van",

            // Rule "Insurance policy": declared value > 50000 → requires signature.
            // (Applies in the same phase as the weight rule, without interrupting it.)
            ["requiresSignature"] = DeclaredValue > 50000,

            // Rule "SLA priority": VIP client → high priority.
            ["priority"] = ClientType.Equals("VIP", StringComparison.OrdinalIgnoreCase) ? "High" : "Normal",
        };
        return Task.FromResult(ProcessResult.Ok(output));
    }
}

/// <summary>
/// Records a failed delivery attempt (run when a "failed attempt" signal arrives while OnDelivery).
/// The loop-vs-terminate decision is a flow rule on <c>attempts</c> (see README).
/// </summary>
[ProcessDefinition("pkg-delivery-attempt",
    DisplayName = "Delivery attempt",
    Version = "1.0",
    Description = "Increments the delivery-attempt counter.")]
public sealed class DeliveryAttempt : IProcess
{
    [ProcessInput(Label = "Attempts so far")]
    public int Attempts { get; set; }

    public Task<ProcessResult> ExecuteAsync(ProcessContext ctx, CancellationToken ct)
        => Task.FromResult(ProcessResult.Ok(new() { ["attempts"] = Attempts + 1 }));
}

/// <summary>
/// Delivery audit — a strict guard. If a signature is required but missing, it FAILS the action, which
/// blocks the delivery confirmation (the instance stays OnDelivery / faults instead of moving to Delivered).
/// </summary>
[ProcessDefinition("pkg-delivery-audit",
    DisplayName = "Delivery audit",
    Version = "1.0",
    Description = "Blocks confirmation when a signature is required but missing.")]
public sealed class DeliveryAudit : IProcess
{
    [ProcessInput(Label = "Requires signature")]
    public bool RequiresSignature { get; set; }

    [ProcessInput(Label = "Signature")]
    public string Signature { get; set; } = "";

    public Task<ProcessResult> ExecuteAsync(ProcessContext ctx, CancellationToken ct)
    {
        if (RequiresSignature && string.IsNullOrWhiteSpace(Signature))
            return Task.FromResult(ProcessResult.Fail("Signature required but missing — delivery rejected."));

        return Task.FromResult(ProcessResult.Ok(new() { ["delivered"] = true }));
    }
}
