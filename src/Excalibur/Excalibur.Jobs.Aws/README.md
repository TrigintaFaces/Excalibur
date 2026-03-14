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
