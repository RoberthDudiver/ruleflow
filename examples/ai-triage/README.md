# Example — AI triage (no DLL, decision by AI)

Same idea as the [expense approval](../expense-approval/) example — **no plugin, no DLL** — but here the fuzzy
decision is made by a built-in **AI step**. You give it a prompt, it returns one of the options *you* define,
and normal rules route on that. Great for things a plain `amount > 100` can't judge: the tone of a message, an
intent, a free-text reason.

> A customer writes a free-text message. An AI step reads it and decides **approve / review / reject** with a
> short reason. The flow routes accordingly. Still zero code — the AI is part of RuleFlow.

---

## The lifecycle

```
New ──(AI decides)──▶ Approved        (decision = approve)
                 ├────▶ ManualReview   (decision = review)
                 └────▶ Rejected       (decision = reject)
```

The AI never touches the routing directly. It only writes a `decision` field into the DataBag; the transitions
are the same plain rules as any other flow. That's the safe pattern: **AI proposes, rules decide.**

---

## 0. Prerequisites

An AI provider configured in **Settings → AI provider** (OpenAI, Claude, Gemini, Groq, LM Studio, Ollama…). For
just trying the wiring, pick **Fake (deterministic, no key)** — it returns a stable decision so you can see the
routing without spending a token.

---

## 1. Build it **inside RuleFlow** (step by step)

**Processes → New process** → **Designer**. Name = `AI triage`. Key e.g. `ai-triage`.

### 1.1 States

| State | Mark | Action |
|---|---|---|
| `New` | **Initial** | **AI step** (see below) |
| `Approved` | **Final** | — |
| `ManualReview` | **Final** | — |
| `Rejected` | **Final** | — |

**On `New`**, add an action with **+ AI**:
- **Prompt:**
  ```
  A customer wrote: "{message}". Decide whether to approve, review or reject
  the request, and give a short reason.
  ```
  (`{message}` is replaced with the DataBag field `message` at run time.)
- **Output fields** (this is the contract the model must return):
  - `decision` — type **enum**, values `approve, review, reject`
  - `reason` — type **string**

The enum means the model can only ever return one of your three options — the routing can't be surprised.

### 1.2 Transitions (rules by clicks)

| From → To | Priority | Rule |
|---|---|---|
| New → Approved | 1 | `decision` **equals** `approve` |
| New → Rejected | 2 | `decision` **equals** `reject` |
| New → ManualReview | 3 | `decision` **equals** `review` |

### 1.3 Publish
**Publish** → **On**.

---

## 2. Try it

**Instances → Simulate a flow** → `AI triage` → `message = I always pay on time, please raise my limit.` →
**Run flow**. The AI step fills `decision` and `reason`, and the instance lands in Approved / ManualReview /
Rejected. Open the instance to read the `reason` the model gave.

Over the API:
```bash
KEY="rfk_your_key_here"; BASE="http://localhost:8080"
curl -s -X POST $BASE/api/instances \
  -H "Content-Type: application/json" -H "X-Api-Key: $KEY" \
  -d '{ "definitionKey": "ai-triage",
        "input": { "message": "This charge is wrong, I never bought this. Refund me now." } }'
# → currentState one of Approved / ManualReview / Rejected, dataBag.decision + dataBag.reason
```

---

## 3. Why this matters

You just added *judgment* to a flow without writing or deploying anything, and without letting the model run
loose — it can only pick from your enum, and everything downstream is auditable rules. Swap the provider in
Settings (cloud or local) and the flow doesn't change.

Rules for the clear-cut cases, an AI step for the fuzzy ones, a **plugin** when you need real computation or to
call another system — mix them however the process needs. That's the whole point of RuleFlow.
