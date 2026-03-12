# Excalibur.Hosting.Aws

AWS configuration integration for Excalibur hosting applications.

## Features

- AWS Systems Manager Parameter Store integration
- Environment-aware configuration loading

## Usage

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddParameterStoreSettings("my-app");
```
