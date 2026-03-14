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
