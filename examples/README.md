# RuleFlow — worked examples

Real, step-by-step guides for setting up a process **inside RuleFlow** (the visual designer) and **driving it
over the API**. Both examples use plugins that ship **preinstalled** with RuleFlow, so you can follow along on
a fresh install — and the plugin source is here if you want to build and customize your own.

| Example | Domain | What it teaches |
|---|---|---|
| [Logistics — package distribution](logistics-package-distribution/) | Shipping / operations | Classify by weight, VIP priority, a waiting state, and a **retry → return** loop driven by signals. |
| [Payments / fraud detection](fraud-detection/) | Risk / payments | Geo mismatch, velocity attack, monthly-limit aggregation → `hold` / `reject` / `clear`, plus an account-block cascade. |

Each guide covers: the plugin (DLL), building the process click-by-click in the designer, running it in the UI,
and consuming it from your own system over the HTTP API with real `curl` requests and expected responses.

New to RuleFlow? Start it first (see the [main README](../README.md)) and complete the setup wizard.
