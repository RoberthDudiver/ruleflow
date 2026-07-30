# RuleFlow — worked examples

Real, step-by-step guides: build the process **inside RuleFlow** (the visual designer) and **drive it over the
API**. They're grouped to show the two sides of RuleFlow — how much you get with **zero code**, and how you plug
in a **DLL** when you need custom computation.

## ⭐ Best of both — one flow, DLL **and** no-DLL

| Example | What it shows |
|---|---|
| [Loan application](loan-application-mixed/) | A **single** lifecycle that uses a **plugin (DLL)** for the credit score, **plain rules** for the clear cases, an **AI step** for the borderline band, and a **human signal** for the final call. The whole engine in one process. |

## Without a DLL — pure configuration

No plugin, no code. Rules, waiting states, signals, and (optionally) a built-in AI step do all the work.

| Example | What it shows |
|---|---|
| [Expense approval](expense-approval/) | A full approval flow with **zero code**: auto-approve under a threshold, else a human approves/rejects via a signal. |
| [AI triage](ai-triage/) | A built-in **AI step** makes the fuzzy decision (approve/review/reject) and rules route on it — still no DLL. |

## With a DLL — custom computation

When you need a real calculation, an external API call, or heavy logic, you drop in a plugin (hot-reloaded, with
one-click version rollback).

| Example | What it shows |
|---|---|
| [Logistics — package distribution](logistics-package-distribution/) | Classify by weight, VIP priority, a waiting state, and a **retry → return** loop driven by signals. |
| [Payments / fraud detection](fraud-detection/) | Geo mismatch, velocity attack, monthly-limit aggregation → hold / reject / clear, plus an account-block cascade. |

---

**The pattern:** rules for the clear-cut cases, an AI step for the fuzzy ones, a plugin when you need
computation or to call another system — mix them however the process needs.

New to RuleFlow? Start it first (see the [main README](../README.md)) and complete the setup wizard. Every
example lists exactly which states, actions, transitions and rules to create.
