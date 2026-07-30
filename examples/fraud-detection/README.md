# Example — Payments / Fraud detection

> Another domain, another logic, same engine. A transaction comes in and you want to hold it if the IP country
> doesn't match the account, reject it if there was a velocity attack — many operations in a few minutes — or
> if the month's total went over the limit. And if a transaction is flagged as fraud, the account blocks itself
> and an alert fires. None of that is a stray `if`: it's a flow with rules you tune by hand, without touching
> the core every time the risk team learns something new.

Same as the [logistics example](../logistics-package-distribution/), this shows the plugin, the process built
**inside RuleFlow**, and **driving it over the API**.

---

## The lifecycle you'll build

```
Pending ──▶ Rejected   (fraud)
   │
   ├──▶ Held      (needs review)
   │
   └──▶ Cleared   (all good)
```

All the risk logic runs in one plugin, `fraud-check`, which outputs a single `decision`
(`ok` | `retain` | `reject`); the transitions just route on it.

| Business rule | How `fraud-check` decides |
|---|---|
| IP country ≠ account country | `decision = retain` (+ risk) → **Held** |
| > 3 transactions in the last 5 min (velocity) | `decision = reject` → **Rejected** |
| month's total + this amount > monthly limit | `decision = reject` → **Rejected** |
| none of the above | `decision = ok` → **Cleared** |

Keeping the risk logic in a plugin means the risk team can ship a smarter check by uploading a new DLL
(hot-reload, one-click rollback), while the routing stays as plain, editable rules.

---

## 0. Prerequisites

RuleFlow running and you're logged in as admin. The `fraud-check` plugin ships **preinstalled** (build your own
from [`FraudPlugins.cs`](FraudPlugins.cs) using the plugin template if you want to customize it — see the
logistics guide, step 1).

---

## 1. Build the process **inside RuleFlow** (step by step)

**Processes → New process** → **Designer**.

### 1.1 Name it
**Process name** = `Payments — fraud detection`. Note the generated key (e.g. `fraud`).

### 1.2 States
Add with **+ State**:

| State | Mark | Entry action |
|---|---|---|
| `Pending` | **Initial** | plugin `fraud-check` |
| `Held` | — | — |
| `Rejected` | **Final** | — |
| `Cleared` | **Final** | — |

**On `Pending`**, add **+ Plugin** → `fraud-check` → map its inputs:
```
Amount            ←  $.amount
IpCountry         ←  $.ipCountry
AccountCountry    ←  $.accountCountry
AccountRiskLevel  ←  $.accountRiskLevel
MonthlyLimit      ←  $.monthlyLimit
HistoryJson       ←  $.historyJson
```

### 1.3 Transitions (rules by clicks)
From `Pending`, add three transitions and build each rule (field / operator / value):

| From → To | Priority | Rule |
|---|---|---|
| Pending → Rejected | 1 | `decision` **equals** `reject` |
| Pending → Held | 2 | `decision` **equals** `retain` |
| Pending → Cleared | 3 | `decision` **equals** `ok` |

### 1.4 Publish
Click **Publish** → the process is **On**.

> **Cascade (optional):** to block the account when a transaction is rejected, add a second action on the
> `Rejected` state → plugin `account-cascade` (maps `Decision ← $.decision`, `AccountState ← $.accountState`),
> which outputs `accountState = "Blocked"` and `alert = true`. That's the "account blocks itself" part.

---

## 2. Drive it over the API (real requests)

Create an API key (**Settings → API keys**) and:
```bash
KEY="rfk_your_key_here"
BASE="http://localhost:8080"
```

### 2.1 Clean transaction → Cleared
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "fraud",
        "input": { "amount": 120, "ipCountry": "PA", "accountCountry": "PA", "monthlyLimit": 5000 } }'
# → status "Completed", currentState "Cleared", dataBag.decision "ok"
```

### 2.2 Geographic mismatch → Held
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "fraud",
        "input": { "amount": 300, "ipCountry": "RU", "accountCountry": "PA", "monthlyLimit": 5000 } }'
# → currentState "Held", dataBag.decision "retain", dataBag.reason "Geo mismatch: IP RU vs account PA."
```

### 2.3 Monthly limit exceeded → Rejected
The account already spent 800 this month; this 300 pushes it over the 1000 limit:
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "fraud",
        "input": { "amount": 300, "ipCountry": "PA", "accountCountry": "PA", "monthlyLimit": 1000,
                   "historyJson": "[{\"amount\":800,\"at\":\"2026-07-05T10:00:00Z\"}]" } }'
# → currentState "Rejected", dataBag.decision "reject", reason "Monthly limit exceeded (1100 > 1000)."
```
> Use an `at` date in the **current month** for the aggregation to count it.

### 2.4 Velocity attack → Rejected
More than 3 transactions in the last 5 minutes. Put 4 entries with timestamps **within the last few minutes**
(UTC ISO-8601):
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "fraud",
        "input": { "amount": 50, "ipCountry": "PA", "accountCountry": "PA", "monthlyLimit": 100000,
                   "historyJson": "[{\"amount\":10,\"at\":\"NOW-1m\"},{\"amount\":10,\"at\":\"NOW-2m\"},{\"amount\":10,\"at\":\"NOW-3m\"},{\"amount\":10,\"at\":\"NOW-4m\"}]" } }'
# → currentState "Rejected", reason "Velocity attack: 4 transactions in 5 minutes."
```
Replace `NOW-1m`… with real recent UTC times, e.g. `2026-07-30T21:59:00Z`. (`date -u -d '-1 min' +%FT%TZ` on Linux.)

### 2.5 Inspect
```bash
curl -s $BASE/api/instances/<id> -H "X-Api-Key: $KEY"   # state + decision + reason
curl -s $BASE/api/audit/<id>     -H "X-Api-Key: $KEY"   # why each step happened
```

---

## 3. The payoff

The risk team wants a stricter monthly cap, a tighter velocity window, or to send borderline cases to **Held**
for a human instead of auto-rejecting? Edit the rule in the Designer and **Publish** — no deploy, no downtime.
A smarter risk model? Ship it as a new `fraud-check` DLL version; roll back instantly if it misfires.
