# Excalibur.Data.Analyzers

Compile-time diagnostics for relational data requests.

This assembly is **not published as a package**. It is bundled into `Excalibur.Data.Abstractions` at
`analyzers/dotnet/cs`, so referencing that package is all a consumer does to get these rules — the same way
`System.Text.Json` carries its own source generator. There is one delivery path on purpose: a second one
would mean duplicate diagnostics for anyone who referenced both.

## Rules

| ID | Severity | Reports |
|---|---|---|
| `EXDATA001` | Warning | A `[NoTenantTerm]` declaration whose justification is empty or whitespace. |
| `EXDATA002` | Warning | A data request that accepts a `TenantScope` or `KeyedTenantPartition` and never uses it. |

## What these rules deliberately do not do

**Neither rule fires because a tenant term is absent.** A request addressed by a unique key, and a request
that reports on the whole estate, both carry no tenant term and both compile clean with no annotation.

That restraint is the design, not an omission. A statement whose `WHERE` already addresses a globally
unique key matches at most one row, so adding a tenant term to it cannot admit a foreign row — the only
reachable effect is turning the correct row into zero rows. A framework-wide consistency pass that added
tenant terms uniformly is what once stopped an outbox marking the messages it had claimed, and an analyzer
that fired on absence would be that pass running forever, on every request anyone writes.

So both rules report only **positive evidence of an inconsistency the compiler can see**: a justification
that says nothing, and a partition that was accepted and then dropped. `EXDATA002` in particular does not
try to prove that a partition reaches the outgoing parameters — any use at all silences it. A proof that
gives up is indistinguishable from a defect, and under a build that promotes warnings to errors, guessing
wrong fails a compilation over correct code.

## Suppressing a rule

Both rules are ordinary diagnostics. Configure them in `.editorconfig` like any other:

```ini
dotnet_diagnostic.EXDATA002.severity = suggestion
```

If `EXDATA002` fires on a request that is correct as written, the two intended fixes are to bind the
partition into the statement, or to remove the parameter — a request that carries no tenant term takes no
tenant. Reach for a suppression only when neither is true, and say why at the suppression site.
