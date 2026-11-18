# Excalibur.Jobs.Cdc

CDC (Change Data Capture) job types for the Excalibur Jobs framework.

## Features

- Quartz-based CDC job with multi-database support
- Health check integration
- Configurable via `Jobs:CdcJob` configuration section

## Configuration binding

Register the job's dependencies with `services.AddSqlServerCdcJob(configuration)`, then call
`CdcJob.ConfigureJob` from your Quartz builder callback. `CdcJob.ConfigureHealthChecks` adds its health registration.
These package methods use generated configuration binding; consumer applications do not need to enable
reflection binding for them.

Configure `Jobs:CdcJob` with `JobName`, `JobGroup`, `CronSchedule` and `DatabaseConfigs`. Each database
requires `DatabaseName`, `DatabaseConnectionIdentifier`, `StateConnectionIdentifier` and at least one
tracked `Tables` entry with a `TableName`. Optional `CaptureInstance` maps the SQL Server capture name
to that logical table. Validation reports nested paths such as `DatabaseConfigs[0].Tables[0].TableName`.

`DatabaseOptions` supports parameterless binding and public setters, with processing defaults and
positive batch-size guards. Its three-identifier constructor is available for programmatic setup.
A processor captures execution settings once, including capture mappings and recovery callbacks.
Configure options before constructing it; reloads affect subsequently constructed processors.

The AOT sample's `--jobs-binding-smoke` mode verifies the public configuration paths, nested settings,
defaults, validation and reload in a native executable. This does not certify CDC database processing;
database execution requires its own native acceptance checks. The fixed CDC SQL paths use static
ADO.NET execution; this configuration smoke does not exercise database queries or checkpoint writes.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
