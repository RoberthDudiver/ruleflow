# Example — Expense approval (no DLL, no AI)

**The point of this one:** a complete, useful business flow with **zero code**. No plugin, no DLL, no AI — just
RuleFlow's built-in rules, a waiting state, and a human signal. This is the "look how much you get without
writing anything" example.

> An expense request comes in. Under $100 it's auto-approved. From $100 up it goes to a manager, who approves
> or rejects it. Tomorrow finance wants a third tier over $5 000 that needs a director — you add it with clicks,
> not with a deploy.

---

## The lifecycle you'll build

```
Submitted ──▶ AutoApproved                    (amount < 100)
     │
     └──▶ ManagerReview ──▶ Approved           (manager signals "approve")
                     └────▶ Rejected           (manager signals "reject")
```

Everything here is **rules + a waiting state**. `ManagerReview` has no automatic exit, so the instance parks
there until a human decision arrives as a **signal**.

---

## 1. Build it **inside RuleFlow** (step by step)

**Processes → New process** → **Designer**. Set **Process name** = `Expense approval`. Note the key (e.g.
`expense-approval`).

### 1.1 States (no actions at all)
Add with **+ State**:

| State | Mark |
|---|---|
| `Submitted` | **Initial** |
| `ManagerReview` | — (waiting state) |
| `AutoApproved` | **Final** |
| `Approved` | **Final** |
| `Rejected` | **Final** |

> Notice: **none** of these states has an action. There's nothing to compute — the caller sends the numbers,
> the rules do the rest.

### 1.2 Transitions (rules by clicks)

| From → To | Priority | Rule |
|---|---|---|
| Submitted → AutoApproved | 1 | `amount` **less than** `100` |
| Submitted → ManagerReview | 2 | `amount` **greater or equal** `100` |
| ManagerReview → Approved | 1 | `decision` **equals** `approve` |
| ManagerReview → Rejected | 2 | `decision` **equals** `reject` |

### 1.3 Publish
Click **Publish**. Done — a working approval flow, and you didn't write a line of code.

> **Built-in "calc" action (optional):** if you ever need to copy or rename a field in the DataBag without a
> plugin — e.g. `normalizedCategory ← $.category` — add an action of type **internal calc** on a state. It's the
> no-code way to shuffle data between steps.

---

## 2. Try it in the UI

**Instances → Simulate a flow** → `Expense approval`.
- `amount = 50` → **Run flow** → ends in **AutoApproved**.
- `amount = 500` → **Run flow** → parks in **ManagerReview** (waiting). You'll resolve it with a signal below.

---

## 3. Drive it over the API

```bash
KEY="rfk_your_key_here"; BASE="http://localhost:8080"
```

### 3.1 Small expense → auto-approved
```bash
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "expense-approval",
        "input": { "amount": 50, "category": "office", "requester": "ana" } }'
# → status "Completed", currentState "AutoApproved"
```

### 3.2 Larger expense → waits for the manager, then resolve
```bash
# start it → parks in ManagerReview
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "expense-approval",
        "input": { "amount": 500, "category": "travel", "requester": "ana" } }'
# → status "Waiting", currentState "ManagerReview", id "…"

# the manager approves (or "reject") — send a signal
curl -s -X POST $BASE/api/instances/<id>/signal \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "decision": "approve" } }'
# → status "Completed", currentState "Approved"
```
The audit trail (`GET /api/audit/<id>`) shows exactly why each step happened.

---

## 4. The payoff

Finance adds a third tier: over $5 000 needs a director. In the Designer, add a `DirectorReview` state and two
transitions (`amount >= 5000` from Submitted, and its approve/reject), reorder priorities, **Publish**. No
plugin, no deploy, no downtime.

This is RuleFlow at its leanest: **a real approval process, entirely by configuration.** When you *do* need
custom computation (a score, an external API call, a heavy calculation), that's when you reach for a **plugin**
— see the [logistics](../logistics-package-distribution/) and [fraud](../fraud-detection/) examples.
