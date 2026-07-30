# Example — Logistics: package distribution

> Imagine a company that moves packages. A package comes in; you classify it by weight, assign a vehicle,
> flag it high-priority if the client is VIP, and send it out for delivery. If delivery fails, retry. If it
> failed three times, return it. Today that's usually rigid code: every new rule is a dev task. With RuleFlow,
> whoever runs the operation changes the weight limit, adds the VIP rule or the retry count **without waiting
> for the tech team**. And the actual computation — how much it weighs, which route fits — lives in a plugin
> you update whenever you want, without stopping the operation.

This guide shows the whole thing end to end: the plugin (DLL), the process built **inside RuleFlow** click by
click, and **driving it live over the API**.

---

## The lifecycle you'll build

```
Received → Classified → InTransit → OutForDelivery → Delivered            (success)
                                          │  ▲
                                    (failed) │  │ (attempts < 3, retry)
                                          ▼  │
                                     AttemptFailed → Returned              (after 3 attempts)
```

| Business rule | Where it lives |
|---|---|
| Weight > 25 kg → special truck | plugin `pkg-classify` (output `vehicle`) |
| VIP client → high priority | plugin `pkg-classify` (output `priority`) |
| Declared value > 50 000 → requires signature | plugin `pkg-classify` (output `requiresSignature`) |
| Failed delivery → retry while attempts < 3 | transition rule on `attempts` |
| Failed 3 times → return | transition rule on `attempts` |

The **plugins do the computation**; the **rules do the routing**.

---

## 0. Prerequisites

RuleFlow running (e.g. `docker run … ghcr.io/roberthdudiver/ruleflow:latest`), and you completed the setup
wizard so you can log in as admin.

---

## 1. The plugin (DLL)

These three plugins ship **preinstalled** with RuleFlow, so you can skip to step 2. Build them yourself only
if you want to customize the logic.

`PackagePlugins.cs` (in this folder) contains:

| Plugin key | What it computes |
|---|---|
| `pkg-classify` | `vehicle` (by weight), `requiresSignature` (by declared value), `priority` (VIP) |
| `pkg-delivery-attempt` | increments `attempts` |
| `pkg-delivery-audit` | fails if a signature is required but missing (a strict guard) |

**To build your own version:**

1. In RuleFlow, **Plugins → Template** downloads a ready-to-build project (with `Dudiver.RuleFlow.Sdk.dll`
   bundled). Unzip it.
2. Replace the template's plugin `.cs` with [`PackagePlugins.cs`](PackagePlugins.cs) from this folder
   (and use the [`PackageDistribution.csproj`](PackageDistribution.csproj) here, keeping `Dudiver.RuleFlow.Sdk.dll`
   next to it).
3. Build:
   ```bash
   dotnet build -c Release
   # → bin/Release/net10.0/PackageDistribution.dll
   ```
4. In RuleFlow, **Plugins → Upload plugin** and pick that `.dll`. Each upload is a new version; you can roll
   back to any previous one in one click (**Plugins → Use this version**).

---

## 2. Build the process **inside RuleFlow** (step by step)

Go to **Processes → New process** → it opens the **Designer**.

### 2.1 Name it
In the right panel set **Process name** = `Logistics — package distribution`. The key is generated (e.g.
`logistics`); note it, you'll use it in the API.

### 2.2 Add the states
Use **+ State** for each. Click a state to configure it in the right panel.

| State | Mark | Entry action |
|---|---|---|
| `Received` | **Initial** | plugin `pkg-classify` |
| `Classified` | — | — |
| `InTransit` | — | — |
| `OutForDelivery` | — | — (this is the waiting state) |
| `AttemptFailed` | — | plugin `pkg-delivery-attempt` |
| `Delivered` | **Final** | — |
| `Returned` | **Final** | — |

**On `Received`**, add the action: **+ Plugin** → choose `pkg-classify` → map its inputs from the DataBag:
```
WeightKg      ←  $.weightKg
DeclaredValue ←  $.declaredValue
ClientType    ←  $.clientType
```

**On `AttemptFailed`**, add the action: **+ Plugin** → `pkg-delivery-attempt` → map:
```
Attempts  ←  $.attempts
```

### 2.3 Add the transitions
Click a state and use **+ Transition** to draw an arrow to another state. For each one set the **priority**
(lower wins first) and build the **rule** with clicks (field / operator / value):

| From → To | Priority | Rule (click-built) |
|---|---|---|
| Received → Classified | 1 | *(always — no conditions)* |
| Classified → InTransit | 1 | *(always)* |
| InTransit → OutForDelivery | 1 | *(always)* |
| OutForDelivery → Delivered | 1 | `deliveryResult` **equals** `ok` |
| OutForDelivery → AttemptFailed | 2 | `deliveryResult` **equals** `failed` |
| AttemptFailed → Returned | 1 | `attempts` **greater or equal** `3` |
| AttemptFailed → OutForDelivery | 2 | *(always — retry)* |

> `OutForDelivery` has **no "always" transition** — that makes it a **waiting state**: the instance parks
> there until a delivery outcome arrives (from the driver's app, a scan, etc.) as a **signal**.

### 2.4 Publish
Click **Publish**. The process turns **On** and can now receive instances.

---

## 3. Try it in the UI (no code)

**Instances → Simulate a flow** → pick `Logistics — package distribution`. Fill the start inputs:
```
weightKg      = 30
declaredValue = 8000
clientType    = VIP
```
**Run flow.** The instance classifies (special truck, high priority) and parks in **OutForDelivery**. Click the
instance to see the timeline and the DataBag (`vehicle`, `priority`, `requiresSignature`). To finish it, you'll
send a signal — easiest over the API, next.

---

## 4. Drive it over the API (real requests)

### 4.1 Get an API key
**Settings → API keys → Create key**. Copy it (shown once). Export it:
```bash
KEY="rfk_your_key_here"
BASE="http://localhost:8080"
```

### 4.2 Start a shipment
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "logistics",
        "input": { "weightKg": 30, "declaredValue": 8000, "clientType": "VIP" } }'
```
Response (trimmed) — note it classified and is now **waiting**:
```json
{
  "id": "3f2b…",
  "status": "Waiting",
  "currentState": "OutForDelivery",
  "dataBag": { "weightKg": 30, "declaredValue": 8000, "clientType": "VIP",
               "vehicle": "Special truck", "priority": "High", "requiresSignature": false }
}
```
Keep the `id`:
```bash
ID="3f2b…"
```

### 4.3 Happy path — the driver delivers
```bash
curl -s -X POST $BASE/api/instances/$ID/signal \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "deliveryResult": "ok" } }'
# → status "Completed", currentState "Delivered"
```

### 4.4 Failure path — retry, then return
Send `failed` three times and watch `attempts` climb and the routing change:
```bash
# attempt 1 → back to OutForDelivery (attempts = 1)
curl -s -X POST $BASE/api/instances/$ID/signal -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "deliveryResult": "failed" } }'
# attempt 2 → OutForDelivery (attempts = 2)
curl -s -X POST $BASE/api/instances/$ID/signal -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "deliveryResult": "failed" } }'
# attempt 3 → attempts = 3 → Returned
curl -s -X POST $BASE/api/instances/$ID/signal -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "deliveryResult": "failed" } }'
# → status "Completed", currentState "Returned"
```

### 4.5 Inspect status and the "why"
```bash
curl -s $BASE/api/instances/$ID -H "X-Api-Key: $KEY"          # current state + DataBag + history
curl -s $BASE/api/audit/$ID      -H "X-Api-Key: $KEY"          # full audit trail (what/who/when/why)
```

---

## 5. The payoff — change a rule with no deploy

Your ops lead wants to allow **5 attempts** instead of 3, or route packages over **20 kg** to the special
truck. Two ways, both without touching code:

- **In RuleFlow**: open the process in the Designer, edit the rule (`attempts >= 3` → `>= 5`), **Publish**.
- **Or via API**: `POST /api/definitions` the updated definition with a bumped `version`.

Need a smarter weight/route calculation? Recompile only the `pkg-classify` **plugin**, upload the new DLL —
it hot-reloads, and if it misbehaves you roll back in one click. The operation never stops.
