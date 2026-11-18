# Excalibur.Jobs.DataProcessing

Data processing job types for the Excalibur Jobs framework.

## Features

- Quartz-based data processing job with orchestration manager integration
- Health check integration
- Configurable via `Jobs:DataProcessingJob` configuration section

## Configuration binding

`DataProcessingJob.ConfigureJob` and `DataProcessingJob.ConfigureHealthChecks` bind the
`Jobs:DataProcessingJob` section using generated code. Configure `JobName`, `JobGroup`, `CronSchedule`,
`Disabled`, `DegradedThreshold` and `UnhealthyThreshold` as needed. Omitted settings retain their
documented `JobOptions` defaults. These package methods require no reflection-based configuration binder.

The AOT sample's `--jobs-binding-smoke` mode exercises both public methods from a native executable,
including enabled/disabled scheduling and health registration. Full data-processing execution and its
database dependencies require separate native validation; the package's broader AOT compatibility
declaration is unchanged.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
