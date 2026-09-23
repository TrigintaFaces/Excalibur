# Excalibur.Jobs.Aws

AWS EventBridge Scheduler integration for the Excalibur Jobs framework.

## Installation

```bash
dotnet add package Excalibur.Jobs.Aws
```

## Usage

```csharp
services.AddAwsScheduler(options =>
{
    options.TargetArn = "arn:aws:lambda:us-east-1:123456789:function:my-job";
    options.ExecutionRoleArn = "arn:aws:iam::123456789:role/scheduler-role";
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
