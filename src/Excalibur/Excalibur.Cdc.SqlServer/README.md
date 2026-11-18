# Excalibur.Cdc.SqlServer

SQL Server Change Data Capture (CDC) implementation for Excalibur.

## SQL execution

CDC queries and checkpoint operations use explicit ADO.NET commands, parameters and typed
readers rather than runtime-generated Dapper execution. Capture identifiers remain validated;
record values remain parameterized. Every repository query, including `GetNextValidLsnAsync`,
honors the repository's configured command timeout and forwards cancellation.

Checkpoint storage opens and disposes a connection per operation. Enumeration buffers before
yielding so a paused consumer does not retain a SQL session. The state schema's `ProcessedAt`
column is `DATETIMEOFFSET`; use the shipped DDL. Fencing compares leadership tokens and does
not independently prevent a caller from writing an older LSN with an accepted token.

Static SQL execution alone is not certification of every transitive dependency or NativeAOT
application. Validate the actual published application and the provider operations it uses.

## Position-reset logging

`CdcRecoveryOptions.EnableStructuredLogging` controls the structured warning emitted after a stale checkpoint is successfully reset in memory. It defaults to `true`. Set it to `false` to suppress that reset warning; recovery strategy selection, the `OnPositionReset` callback, cancellation, and checkpoint persistence retain their behavior. Recovery failures and other diagnostic events are not disabled. A refused or canceled reset does not emit a successful-reset warning.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Database schema

**This package does not create its tables.** The DDL ships inside the `.nupkg` under `scripts/`, and
you apply it — so a schema change to your database is always something you did deliberately, never
something a version upgrade did to you while you were not looking.

| Script |
|---|
| `scripts/001_CreateCdcStateSchema.sql` |
| `scripts/002_CreateCdcIdempotencySchema.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.cdc.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.
