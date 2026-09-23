# Excalibur.Jobs.GoogleCloud

Google Cloud Scheduler integration for the Excalibur Jobs framework.

## Installation

```bash
dotnet add package Excalibur.Jobs.GoogleCloud
```

## Usage

```csharp
services.AddGoogleCloudScheduler(options =>
{
    options.ProjectId = "my-gcp-project";
    options.LocationId = "us-central1";
    options.TargetUrl = "https://my-api.example.com/jobs/execute";
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
