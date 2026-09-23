# Excalibur.Jobs.Azure

Azure Logic Apps integration for the Excalibur Jobs framework.

## Installation

```bash
dotnet add package Excalibur.Jobs.Azure
```

## Usage

```csharp
services.AddAzureLogicApps(options =>
{
    options.ResourceGroupName = "my-resource-group";
    options.SubscriptionId = "00000000-0000-0000-0000-000000000000";
    options.JobExecutionEndpoint = "https://my-api.example.com/jobs/execute";
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
