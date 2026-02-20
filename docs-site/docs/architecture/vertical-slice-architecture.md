---
sidebar_position: 4
title: Vertical Slice Architecture
description: Organize features as self-contained slices instead of horizontal layers
---

# Vertical Slice Architecture

Vertical slice architecture organizes code **by feature** instead of by technical layer. Each feature (or "slice") contains everything it needs -- the request DTO, the message, the handler, and the endpoint registration -- in one place.

## Why Vertical Slices?

Traditional layered architecture groups code by technical concern:

```
Controllers/
  PatientsController.cs
  AppointmentsController.cs
Services/
  PatientService.cs
  AppointmentService.cs
Repositories/
  PatientRepository.cs
  AppointmentRepository.cs
Models/
  Patient.cs
  Appointment.cs
```

Adding a new feature means touching multiple folders across the project. Vertical slices flip this: each folder is a self-contained feature.

### Layered vs Vertical Slice

| Aspect | Layered | Vertical Slice |
|--------|---------|----------------|
| **Organize by** | Technical concern (Controllers, Services, Repos) | Feature (Patients, Appointments) |
| **Adding a feature** | Touch 3-5 folders | Add 1 folder |
| **Coupling** | Layers couple to each other horizontally | Slices are independent |
| **Shared code** | Everything is shared (by layer) | Sharing is explicit and intentional |
| **Navigation** | Jump between folders to understand a feature | One folder tells the whole story |
| **Delete a feature** | Hunt across layers | Delete one folder |

## Screaming Architecture

A **screaming architecture** makes the domain obvious from the folder structure. When you open the project, the folder names should tell you what the system *does*, not what framework it uses.

```
// Screaming: folders describe the domain
Features/
  Patients/
  Appointments/
  Prescriptions/
  Notifications/

// Not screaming: folders describe the framework
Controllers/
Services/
Repositories/
Models/
```

## Why Dispatch Maps Naturally to Slices

Dispatch's message-handler model is a natural fit for vertical slices:

- **One message = one operation** (`RegisterPatientCommand`, `GetPatientQuery`)
- **One handler = one behavior** (`RegisterPatientHandler`)
- **Events enable cross-slice communication** without direct coupling
- **No service layer needed** -- the handler *is* the behavior

Each slice becomes: **Request DTO + Message + Handler + Endpoint registration**.

## Folder Structure

```
Features/
  Patients/                            <-- Feature slice
    RegisterPatient/                   <-- One folder per operation
      RegisterPatientRequest.cs        <-- API request DTO
      RegisterPatientCommand.cs        <-- IDispatchAction<TResult> message
      RegisterPatientHandler.cs        <-- IActionHandler implementation
    GetPatient/
      GetPatientRequest.cs
      GetPatientQuery.cs
      GetPatientHandler.cs
    UpdatePatientInfo/
      UpdatePatientInfoRequest.cs
      UpdatePatientInfoCommand.cs
      UpdatePatientInfoHandler.cs
    Events/                            <-- Domain events published by this slice
      PatientRegistered.cs
    Shared/                            <-- DTOs, repositories scoped to the slice
      PatientDto.cs
      IPatientRepository.cs
      InMemoryPatientRepository.cs
    PatientsEndpoints.cs               <-- Maps HTTP routes to Dispatch messages
    PatientsServiceCollectionExtensions.cs  <-- Per-slice DI registration

  Appointments/                        <-- Another independent slice
  Prescriptions/                       <-- Shows [Authorize] on message types
  Notifications/                       <-- Event-only slice (no HTTP endpoints)
```

### Naming Conventions

| Item | Pattern | Example |
|------|---------|---------|
| Operation folder | `{Verb}{Noun}` | `RegisterPatient/` |
| Request DTO | `{Operation}Request` | `RegisterPatientRequest.cs` |
| Command message | `{Operation}Command` | `RegisterPatientCommand.cs` |
| Query message | `{Operation}Query` | `GetPatientQuery.cs` |
| Handler | `{Operation}Handler` | `RegisterPatientHandler.cs` |
| Domain event | `{PastTense}` | `PatientRegistered.cs` |
| Endpoints file | `{Feature}Endpoints` | `PatientsEndpoints.cs` |
| DI extensions | `{Feature}ServiceCollectionExtensions` | `PatientsServiceCollectionExtensions.cs` |

## Per-Slice DI Registration

Each slice registers its own services using an extension method in the `Microsoft.Extensions.DependencyInjection` namespace:

```csharp
using HealthcareApi.Features.Patients.Shared;

namespace Microsoft.Extensions.DependencyInjection;

public static class PatientsServiceCollectionExtensions
{
    public static IServiceCollection AddPatientsFeature(this IServiceCollection services)
    {
        services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
        return services;
    }
}
```

The composition root calls each feature's registration method:

```csharp
builder.Services.AddPatientsFeature();
builder.Services.AddAppointmentsFeature();
builder.Services.AddPrescriptionsFeature();
builder.Services.AddNotificationsFeature();
```

## Cross-Slice Communication

Slices communicate through domain events, never by direct reference to another slice's handlers or repositories.

### Publishing Events

A handler in the Patients slice publishes `PatientRegistered`:

```csharp
public sealed class RegisterPatientHandler
    : IActionHandler<RegisterPatientCommand, RegisterPatientResult>
{
    private readonly IPatientRepository _patients;
    private readonly IDispatcher _dispatcher;

    public async Task<RegisterPatientResult> HandleAsync(
        RegisterPatientCommand action, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        // ... save patient ...

        await _dispatcher.DispatchAsync(
            new PatientRegistered(id, action.Email), cancellationToken)
            .ConfigureAwait(false);

        return new RegisterPatientResult { PatientId = id };
    }
}
```

### Handling Events in Another Slice

The Notifications slice subscribes to events from other slices without any coupling:

```csharp
namespace HealthcareApi.Features.Notifications.Handlers;

public sealed class PatientRegisteredNotificationHandler
    : IEventHandler<PatientRegistered>
{
    private readonly INotificationService _notifications;

    public async Task HandleAsync(
        PatientRegistered eventMessage, CancellationToken cancellationToken)
    {
        await _notifications.SendAsync(
            $"patient-{eventMessage.PatientId}",
            "Welcome",
            "Your account has been created.",
            cancellationToken).ConfigureAwait(false);
    }
}
```

The only shared artifact is the event record itself (`PatientRegistered`), which lives in the publishing slice's `Events/` folder.

## Per-Slice Authorization

Use `[Authorize]` attributes directly on message types. The [ASP.NET Core Authorization Bridge](../deployment/minimal-api-bridge.md#authorization-bridge) evaluates them in the Dispatch pipeline:

```csharp
[Authorize(Roles = "Physician")]
public record CreatePrescriptionCommand(Guid PatientId, string Medication, string Dosage)
    : IDispatchAction<CreatePrescriptionResult>;
```

No need for policy configuration in the endpoint registration -- the attribute on the message type handles it.

## Endpoint Registration

Each slice registers its own routes via an extension method on `IEndpointRouteBuilder`:

```csharp
public static class PatientsEndpoints
{
    public static RouteGroupBuilder MapPatientsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/patients").WithTags("Patients");

        group.DispatchPostAction<RegisterPatientRequest, RegisterPatientCommand, RegisterPatientResult>(
            "/", static (request, _) => new RegisterPatientCommand(
                request.FirstName, request.LastName, request.DateOfBirth, request.Email));

        group.DispatchGetAction<GetPatientRequest, GetPatientQuery, PatientDto>(
            "/{id:guid}", static (request, _) => new GetPatientQuery(request.Id));

        group.DispatchPutAction<UpdatePatientInfoRequest, UpdatePatientInfoCommand>(
            "/{id:guid}", static (request, httpContext) => new UpdatePatientInfoCommand(
                Guid.Parse(httpContext.GetRouteValue("id")!.ToString()!),
                request.Email, request.Phone));

        return group;
    }
}
```

The composition root wires everything together:

```csharp
var api = app.MapGroup("/api");
api.MapPatientsEndpoints();
api.MapAppointmentsEndpoints();
api.MapPrescriptionsEndpoints();
```

See [Minimal API Hosting Bridge](../deployment/minimal-api-bridge.md) for the full endpoint routing API reference.

## When to Use Vertical Slices

| Situation | Recommendation |
|-----------|---------------|
| Feature-rich API with many independent operations | Vertical slices |
| CRUD app with uniform data access patterns | Layered may be simpler |
| Team organized by feature ownership | Vertical slices align with team structure |
| Shared business logic across many features | Extract to a shared library, keep slices for orchestration |
| Microservices with focused responsibilities | Each service is already a "slice" -- use either approach internally |

Vertical slices work well when features are independent and the team wants to minimize cross-cutting changes. They pair naturally with Dispatch's message-per-operation model.

## See Also

- [Minimal API Hosting Bridge](../deployment/minimal-api-bridge.md) -- Map HTTP endpoints to Dispatch messages
- [Healthcare API Sample](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/12-vertical-slice-api) -- Full working example
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) -- General hosting guide
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) -- Message types and handler patterns

