# Healthcare API - Vertical Slice Architecture Sample

Demonstrates how to build an ASP.NET Core Minimal API using Dispatch's hosting bridge with **vertical slice architecture** and **screaming folder structure**.

## Architecture

```
Features/
  Patients/              <-- Each slice is self-contained
    RegisterPatient/     <-- One folder per operation
      Request.cs         <-- API request DTO
      Command.cs         <-- IDispatchAction message
      Handler.cs         <-- IActionHandler implementation
    GetPatient/
    UpdatePatientInfo/
    Events/              <-- Domain events published by this slice
    Shared/              <-- DTOs, repositories scoped to the slice
    PatientsEndpoints.cs <-- Maps HTTP routes to Dispatch messages

  Appointments/          <-- Another independent slice
  Prescriptions/         <-- Shows [Authorize] on message types
  Notifications/         <-- Event-only slice (no HTTP endpoints)
```

## Key Patterns

| Pattern | Where |
|---------|-------|
| `DispatchPostAction<TRequest, TAction, TResponse>` | POST endpoints with typed response |
| `DispatchGetAction<TRequest, TAction, TResponse>` | GET endpoints with query binding |
| `DispatchPutAction<TRequest, TAction>` | PUT endpoints (202 Accepted) |
| `DispatchDeleteAction<TRequest, TAction>` | DELETE endpoints |
| `MapGroup("/patients").WithTags("Patients")` | Route groups per slice |
| `[Authorize(Roles = "Physician")]` on command | Authorization bridge |
| `IEventHandler<AppointmentScheduled>` | Cross-slice event handling |
| `AddPatientsFeature()` in MS DI namespace | Per-slice DI registration |

## Run

```bash
dotnet run --project samples/12-vertical-slice-api/HealthcareApi
```

## Test with curl

```bash
# Register a patient
curl -X POST http://localhost:5000/api/patients \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Jane","lastName":"Doe","dateOfBirth":"1990-05-15","email":"jane@example.com"}'

# Get patient by ID (use the ID from the response above)
curl http://localhost:5000/api/patients/{id}

# Schedule an appointment
curl -X POST http://localhost:5000/api/appointments \
  -H "Content-Type: application/json" \
  -d '{"patientId":"{id}","physicianName":"Dr. Smith","scheduledAt":"2026-03-01T10:00:00Z","reason":"Annual checkup"}'

# Create a prescription
curl -X POST http://localhost:5000/api/prescriptions \
  -H "Content-Type: application/json" \
  -d '{"patientId":"{id}","medication":"Amoxicillin","dosage":"500mg","daysSupply":10}'
```

## Documentation

- [Minimal API Hosting Bridge](../../../docs-site/docs/deployment/minimal-api-bridge.md)
- [Vertical Slice Architecture](../../../docs-site/docs/architecture/vertical-slice-architecture.md)

