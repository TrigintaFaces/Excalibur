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
