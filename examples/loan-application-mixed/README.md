# Example — Loan application: **DLL + no-DLL in one flow**

This is the "best of both worlds" example. A **single** lifecycle that uses, in one place:

- a **plugin (DLL)** to compute the hard number (a credit score),
- **plain rules** for the clear-cut cases (auto-approve / auto-reject),
- a built-in **AI step** for the gray zone (judge the borderline applications), and
- a **waiting state + human signal** for the final call.

One process shows everything RuleFlow can do — and which parts need code and which don't.

---

## The lifecycle you'll build

```
Received ──(plugin: credit score)──▶  score ≥ 700 ─────────────▶ Approved
                                      score < 400 ─────────────▶ Rejected
                                      400–699  ─▶ AiReview ─(AI decides)─▶ Approved
                                                                     ├──▶ Rejected
                                                                     └──▶ HumanReview ─(signal)─▶ Approved / Rejected
```

| Step | Action type | Code? |
|---|---|---|
| Compute the credit score | **plugin** `credit-scoring` (DLL) | **yes** — custom computation |
| Route on the score | rules | no |
| Judge the borderline band | **AI step** (built-in) | no |
| Final human sign-off | waiting state + signal | no |

---

## 0. Prerequisites

RuleFlow running, logged in as admin. The `credit-scoring` plugin ships **preinstalled** (it turns income and
debts into a 0–1000 score). For the AI step, set an **AI provider** in Settings — pick **Fake (deterministic)**
if you just want to see it wired without spending a token.

---

## 1. Build it **inside RuleFlow** (step by step)

**Processes → New process** → **Designer**. Name = `Loan application`. Key e.g. `loan`.

### 1.1 States

| State | Mark | Action |
|---|---|---|
| `Received` | **Initial** | **plugin** `credit-scoring` |
| `AiReview` | — | **AI step** (below) |
| `HumanReview` | — | — (waiting) |
| `Approved` | **Final** | — |
| `Rejected` | **Final** | — |

**On `Received`** → **+ Plugin** → `credit-scoring` → map:
```
Income  ←  $.income
Debts   ←  $.debts
```
(it writes `score` into the DataBag.)

**On `AiReview`** → **+ AI** →
- **Prompt:** `Applicant note: "{note}". Credit score: {score}. Decide approve, review or reject, with a short reason.`
- **Output:** `decision` (enum: `approve, review, reject`), `reason` (string).

### 1.2 Transitions (rules by clicks)

| From → To | Priority | Rule |
|---|---|---|
| Received → Approved | 1 | `score` **greater or equal** `700` |
| Received → Rejected | 2 | `score` **less than** `400` |
| Received → AiReview | 3 | *(always — the 400–699 band)* |
| AiReview → Approved | 1 | `decision` **equals** `approve` |
| AiReview → Rejected | 2 | `decision` **equals** `reject` |
| AiReview → HumanReview | 3 | `decision` **equals** `review` |
| HumanReview → Approved | 1 | `decision` **equals** `approve` |
| HumanReview → Rejected | 2 | `decision` **equals** `reject` |

### 1.3 Publish

---

## 2. Try it over the API

```bash
KEY="rfk_your_key_here"; BASE="http://localhost:8080"
```

**Strong applicant → approved by a rule (no AI, no human):**
```bash
curl -s -X POST $BASE/api/instances -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "loan", "input": { "income": 120000, "debts": 20000, "note": "stable job" } }'
# score ≈ 833 → currentState "Approved"
```

**Weak applicant → rejected by a rule:**
```bash
curl -s -X POST $BASE/api/instances -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "loan", "input": { "income": 100000, "debts": 90000, "note": "" } }'
# score = 100 → currentState "Rejected"
```

**Borderline → the AI step decides:**
```bash
curl -s -X POST $BASE/api/instances -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "loan", "input": { "income": 100000, "debts": 45000, "note": "always pays on time" } }'
# score = 550 → AiReview → decision from the AI:
#   approve → Approved · reject → Rejected · review → HumanReview (waiting)
```
If it lands in **HumanReview**, a person resolves it with a signal:
```bash
curl -s -X POST $BASE/api/instances/<id>/signal -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "data": { "decision": "approve" } }'
```

`GET /api/audit/<id>` shows the whole chain: the score the plugin computed, the rule that fired, the AI's
decision and reason, and the human's signal.

---

## 3. Why this is the whole point

The **hard number** came from a plugin you can improve and hot-swap (with one-click rollback). The **routing**
is plain, auditable rules an ops person edits without a deploy. The **judgment call** is an AI step you can
point at any provider. The **final say** stays human. All in one process — mix them however the business needs,
and change any part without stopping the rest.
