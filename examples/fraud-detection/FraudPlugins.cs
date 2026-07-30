using System.Text.Json;
using Dudiver.RuleFlow.Sdk;

namespace FraudDetection;

// -----------------------------------------------------------------------------------------------------
// Real-time fraud detection with two interacting lifecycles.
//
//   Transaction: Pending -> Held -> UnderInvestigation -> Cleared | Rejected/Fraud
//   Account:     Active   -> UnderWatch -> Blocked
//
// This plugin does the hard "engine" work in one place: cross-entity evaluation, a sliding time window,
// and a monthly aggregation over the account's transaction history. See README.md for the full flow.
// -----------------------------------------------------------------------------------------------------

/// <summary>
/// Evaluates a pending transaction against its account and history: geographic discrepancy (cross-entity),
/// velocity attack (5-minute sliding window), and monthly-limit aggregation.
/// </summary>
[ProcessDefinition("fraud-check",
    DisplayName = "Fraud check",
    Version = "1.0",
    Description = "Cross-entity + sliding window + monthly aggregation risk evaluation.")]
public sealed class FraudCheck : IProcess
{
    [ProcessInput(Label = "Amount", Required = true)]
    public decimal Amount { get; set; }

    [ProcessInput(Label = "IP country")]
    public string IpCountry { get; set; } = "";

    [ProcessInput(Label = "Account country")]
    public string AccountCountry { get; set; } = "";

    [ProcessInput(Label = "Account risk level")]
    public int AccountRiskLevel { get; set; }

    [ProcessInput(Label = "Monthly limit")]
    public decimal MonthlyLimit { get; set; }

    /// <summary>Account transaction history as a JSON array: [{ "amount": 100, "at": "2026-07-30T12:00:00Z" }, ...]</summary>
    [ProcessInput(Label = "History (JSON)")]
    public string HistoryJson { get; set; } = "[]";

    public Task<ProcessResult> ExecuteAsync(ProcessContext ctx, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var history = ParseHistory(HistoryJson);

        // Rule 1 — Geographic discrepancy (cross-entity: transaction IP vs account's registered country).
        var geoMismatch = !string.Equals(IpCountry, AccountCountry, StringComparison.OrdinalIgnoreCase)
                          && !string.IsNullOrWhiteSpace(AccountCountry);

        // Rule 2 — Velocity attack (sliding window): more than 3 transactions in the last 5 minutes.
        var recent = history.Count(h => now - h.At <= TimeSpan.FromMinutes(5));
        var velocity = recent > 3;

        // Rule 3 — Monthly-limit aggregation: sum(this month) + current amount > limit.
        var monthSum = history.Where(h => h.At.Year == now.Year && h.At.Month == now.Month).Sum(h => h.Amount);
        var overLimit = MonthlyLimit > 0 && monthSum + Amount > MonthlyLimit;

        string decision, reason;
        var riskDelta = 0;

        if (velocity) { decision = "reject"; reason = $"Velocity attack: {recent} transactions in 5 minutes."; }
        else if (overLimit) { decision = "reject"; reason = $"Monthly limit exceeded ({monthSum + Amount} > {MonthlyLimit})."; }
        else if (geoMismatch) { decision = "retain"; riskDelta = 20; reason = $"Geo mismatch: IP {IpCountry} vs account {AccountCountry}."; }
        else { decision = "ok"; reason = "No risk signals."; }

        return Task.FromResult(ProcessResult.Ok(new()
        {
            ["decision"] = decision,                     // ok | retain | reject
            ["riskDelta"] = riskDelta,
            ["newRiskLevel"] = AccountRiskLevel + riskDelta,
            ["reason"] = reason,
        }));
    }

    private static List<(decimal Amount, DateTimeOffset At)> ParseHistory(string json)
    {
        var list = new List<(decimal, DateTimeOffset)>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var amount = e.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m;
                var at = e.TryGetProperty("at", out var t) && t.GetString() is { } s
                         && DateTimeOffset.TryParse(s, out var d) ? d : DateTimeOffset.MinValue;
                list.Add((amount, at));
            }
        }
        catch (JsonException) { /* tolerate malformed input */ }
        return list;
    }
}

/// <summary>
/// Reactive side-effect: when a transaction is rejected for fraud and the account is still Active,
/// block the account and raise an alert (cascading rule across entities).
/// </summary>
[ProcessDefinition("account-cascade",
    DisplayName = "Account cascade",
    Version = "1.0",
    Description = "Blocks the account when a transaction is rejected for fraud.")]
public sealed class AccountCascade : IProcess
{
    [ProcessInput(Label = "Transaction decision")]
    public string Decision { get; set; } = "";

    [ProcessInput(Label = "Account state")]
    public string AccountState { get; set; } = "";

    public Task<ProcessResult> ExecuteAsync(ProcessContext ctx, CancellationToken ct)
    {
        var fraudReject = Decision.Equals("reject", StringComparison.OrdinalIgnoreCase);
        if (fraudReject && AccountState.Equals("Active", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(ProcessResult.Ok(new() { ["accountState"] = "Blocked", ["alert"] = true }));

        return Task.FromResult(ProcessResult.Ok(new() { ["accountState"] = AccountState, ["alert"] = false }));
    }
}
