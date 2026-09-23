// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ============================================================================
// Healthcare API - Vertical Slice Architecture Sample
// ============================================================================
// Demonstrates how to build a Minimal API using Dispatch's hosting bridge
// with vertical slice architecture and screaming folder structure.
//
// Each feature slice (Patients, Appointments, Prescriptions, Notifications)
// is self-contained with its own messages, handlers, DTOs, and endpoints.
//
// Key patterns shown:
//   - DispatchPostAction / DispatchGetAction / DispatchPutAction extensions
//   - Per-feature endpoint registration via MapGroup + extension methods
//   - Cross-slice communication via IDispatchEvent (pub-sub)
//   - Per-slice DI registration (AddPatientsFeature, etc.)
//   - ASP.NET Core authorization bridge with [Authorize] on message types
//
// Run with: dotnet run
// ============================================================================

using Excalibur.Dispatch.Configuration;

using HealthcareApi.Features.Appointments;
using HealthcareApi.Features.Patients;
using HealthcareApi.Features.Prescriptions;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch with the ASP.NET Core hosting bridge.
// AddDispatch on WebApplicationBuilder delegates to services.AddDispatch()
// and wires up the full dispatch pipeline.
builder.AddDispatch(dispatch =>
{
	// Scan this assembly for IActionHandler<T>, IActionHandler<T,R>, IEventHandler<T>
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Bridge ASP.NET Core [Authorize] attributes into the Dispatch pipeline.
	// RequireAuthenticatedUser = false so the sample runs without JWT setup.
	dispatch.UseAspNetCoreAuthorization(options =>
	{
		options.RequireAuthenticatedUser = false;
	});
});

// The authorization bridge evaluates policies through ASP.NET Core's own IAuthorizationService, so
// the host must register the authorization services it reuses. Without this the host does not start:
// the bridge middleware has a dependency the container cannot satisfy.
builder.Services.AddAuthorization();

// Per-slice DI registration — each feature registers its own services.
builder.Services.AddPatientsFeature();
builder.Services.AddAppointmentsFeature();
builder.Services.AddPrescriptionsFeature();
builder.Services.AddNotificationsFeature();

// Unhandled exceptions become a generic RFC 9457 Problem Details 500; the exception message is
// not written to the response. (Excalibur.Hosting.Web's AddGlobalExceptionHandler() adds status-code
// mapping for framework exceptions, such as 404 for ResourceNotFoundException.)
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// Map feature endpoints using route groups.
// Each slice owns its own endpoint registration via an extension method.
var api = app.MapGroup("/api");
api.MapPatientsEndpoints();
api.MapAppointmentsEndpoints();
api.MapPrescriptionsEndpoints();

// Info endpoint
app.MapGet("/", () => Results.Ok(new
{
	Name = "Healthcare API - Vertical Slice Architecture Sample",
	Architecture = "Vertical Slices + Screaming Folder Structure",
	Endpoints = (IReadOnlyList<string>)
	[
		"POST   /api/patients                       - Register a patient",
		"GET    /api/patients/{id}                   - Get patient by ID",
		"PUT    /api/patients/{id}                   - Update patient info",
		"POST   /api/appointments                    - Schedule an appointment",
		"GET    /api/appointments/{id}               - Get appointment by ID",
		"DELETE /api/appointments/{id}               - Cancel an appointment",
		"POST   /api/prescriptions                   - Create a prescription",
		"GET    /api/prescriptions/{id}              - Get prescription by ID",
	],
}));

// Printed once the host is actually listening. A startup failure never reaches this line, so it
// separates "started and serving" from "died or hung during startup" for anyone -- or anything --
// watching the output.
app.Lifetime.ApplicationStarted.Register(static () =>
	Console.WriteLine("Sample ready: Healthcare API listening. Press Ctrl+C to stop."));

app.Run();

