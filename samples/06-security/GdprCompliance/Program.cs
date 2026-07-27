// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// GDPR Compliance Sample
// ============================================================================
//
// This sample demonstrates Excalibur's GDPR compliance primitives end-to-end:
//
//   1. [PersonalData] attributes on domain entities for auto-discovery
//   2. IErasureService — the Data Subject Right-to-Erasure (Article 17) API
//   3. AddGdprErasure(options => ...) registration + in-memory store for demos
//   4. REST endpoints showing Erase-in-place and Tombstone patterns
//
// ============================================================================

using Excalibur.Dispatch;
using Excalibur.Compliance;

using GdprCompliance.Commands;
using GdprCompliance.Domain;
using GdprCompliance.Projections;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// 0. Dispatch pipeline (command + event handlers discovered from this assembly)
// ----------------------------------------------------------------------------

builder.Services.AddDispatch(typeof(Program).Assembly);

// ----------------------------------------------------------------------------
// 1. Register GDPR erasure
// ----------------------------------------------------------------------------

// This sample runs on the in-memory key provider, whose keys do not survive a restart. That is fine
// for a demo and unacceptable in production: key material lost on restart makes every value encrypted
// with it permanently unrecoverable. Nothing stops you deploying this, so the choice is yours to get
// right — a real deployment deletes these two lines and registers a durable provider instead
// (for example the Azure Key Vault, AWS KMS, or HashiCorp Vault provider), which the startup
// durability gate then accepts without any opt-in.
builder.Services.Configure<KeyDurabilityOptions>(o => o.AllowVolatileKeyProvider = true);

builder.Services.AddGdprErasure(options =>
{
	// Grace period during which a user can cancel their erasure request
	// before the data is actually deleted. Production default: 72h.
	options.DefaultGracePeriod = TimeSpan.FromHours(72);

	// Automatically discover PII via reflection on [PersonalData]-annotated types.
	options.EnableAutoDiscovery = true;

	// Require an independent verification step before running the erasure.
	options.RequireVerification = true;
});

// Data-subject identifiers are pseudonymized with a keyed HMAC that requires a secret pepper; the framework
// fails closed at startup if one is not configured (a missing/weak pepper would make the pseudonymization
// reversible). DEMO ONLY: read it from configuration with a runnable fallback — in production supply a
// high-entropy secret from your secret manager / KMS (never a literal in source), stored apart from the data.
builder.Services.Configure<Excalibur.Compliance.Erasure.DataSubjectHashingOptions>(o =>
	o.Pepper = builder.Configuration["Gdpr:DataSubjectPepper"]
		?? "sample-demo-pepper-not-a-secret-change-me-0123456789");

// In-memory erasure store for the demo; production uses SQL Server.
builder.Services.AddInMemoryErasureStore();

// Compliance monitoring (audit log, metrics, alerts).
builder.Services.AddComplianceMonitoring();

// ----------------------------------------------------------------------------
// 2. In-memory customer store (production: a real DB + PII-encryption)
// ----------------------------------------------------------------------------

builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();

// ----------------------------------------------------------------------------
// 2a. Privacy-state projection store (updated by IEventHandler<T> projections)
// ----------------------------------------------------------------------------

builder.Services.AddSingleton<ICustomerPrivacyViewStore, InMemoryCustomerPrivacyViewStore>();

var app = builder.Build();

// Seed two demo customers
using (var scope = app.Services.CreateScope())
{
	var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
	await repo.SaveAsync(new Customer
	{
		Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
		FullName = "Alice Anderson",
		Email = "alice@example.com",
		PhoneNumber = "+44 1234 567890",
		NationalIdNumber = "AB-123-456",
		RegisteredAt = DateTimeOffset.UtcNow.AddYears(-1)
	}).ConfigureAwait(false);

	await repo.SaveAsync(new Customer
	{
		Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
		FullName = "Bob Baker",
		Email = "bob@example.com",
		RegisteredAt = DateTimeOffset.UtcNow.AddMonths(-3)
	}).ConfigureAwait(false);
}

// ----------------------------------------------------------------------------
// 3. REST endpoints
// ----------------------------------------------------------------------------

app.MapGet("/", () => Results.Text(
	"""
	GDPR Compliance sample. Endpoints:

	  GET  /customers/{id}                read (PII is masked in the response)
	  GET  /customers/{id}/privacy-view   projected privacy state (populated by
	                                      IEventHandler<CustomerErasedEvent>)
	  POST /customers/{id}/erase          dispatch EraseCustomerCommand
	  POST /customers/{id}/tombstone      dispatch TombstoneCustomerCommand

	Canonical pipeline (each erasure endpoint):
	  IDispatcher -> IActionHandler -> IErasureService + repo
	               -> CustomerErasedEvent / CustomerTombstonedEvent
	               -> IEventHandler<T> -> CustomerPrivacyView projection

	Try:
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111
	  curl -X POST http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/erase
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/privacy-view
	"""));

// Read (PII masked)
app.MapGet("/customers/{id:guid}", async (Guid id, ICustomerRepository repo) =>
{
	var customer = await repo.FindAsync(id).ConfigureAwait(false);
	return customer is null
		? Results.NotFound()
		: Results.Ok(new
		{
			customer.Id,
			FullName = MaskPii(customer.FullName),
			Email = MaskPii(customer.Email),
			customer.RegisteredAt
		});
});

// Privacy-state projection read (populated by IEventHandler<CustomerErasedEvent>
// and IEventHandler<CustomerTombstonedEvent>).
app.MapGet("/customers/{id:guid}/privacy-view", async (
	Guid id,
	ICustomerPrivacyViewStore store,
	CancellationToken ct) =>
{
	var view = await store.GetAsync(id, ct).ConfigureAwait(false);
	return view is null ? Results.NotFound() : Results.Ok(view);
});

// Erase-in-place: dispatch the EraseCustomerCommand. The handler files an
// audit-tracked IErasureService request, clears every [PersonalData] field,
// and raises CustomerErasedEvent so the privacy-view projection updates.
app.MapPost("/customers/{id:guid}/erase", async (
	Guid id,
	IDispatcher dispatcher,
	CancellationToken ct) =>
{
	var command = new EraseCustomerCommand(Guid.NewGuid()) { CustomerId = id };
	var dispatchResult = await dispatcher
		.DispatchAsync<EraseCustomerCommand, CustomerErasureResponse>(command, ct)
		.ConfigureAwait(false);

	if (!dispatchResult.Succeeded)
	{
		return dispatchResult.ProblemDetails is { } problem
			? Results.Problem(detail: problem.Detail, statusCode: problem.Status ?? 500, title: problem.Title)
			: Results.Problem(detail: dispatchResult.ErrorMessage, statusCode: 500);
	}

	return Results.Ok(dispatchResult.ReturnValue);
});

// Tombstone: dispatch the TombstoneCustomerCommand. The handler replaces the
// row with a marker record and raises CustomerTombstonedEvent.
app.MapPost("/customers/{id:guid}/tombstone", async (
	Guid id,
	IDispatcher dispatcher,
	CancellationToken ct) =>
{
	var command = new TombstoneCustomerCommand(Guid.NewGuid()) { CustomerId = id };
	var dispatchResult = await dispatcher
		.DispatchAsync<TombstoneCustomerCommand, CustomerErasureResponse>(command, ct)
		.ConfigureAwait(false);

	if (!dispatchResult.Succeeded)
	{
		return dispatchResult.ProblemDetails is { } problem
			? Results.Problem(detail: problem.Detail, statusCode: problem.Status ?? 500, title: problem.Title)
			: Results.Problem(detail: dispatchResult.ErrorMessage, statusCode: 500);
	}

	return Results.Ok(dispatchResult.ReturnValue);
});

await app.RunAsync().ConfigureAwait(false);

// Simple PII masking helper — a real sample would use Excalibur.Compliance's IDataMasker.
static string MaskPii(string value) =>
	string.IsNullOrEmpty(value) ? "<erased>" : value.Length <= 2 ? "**" : value[0] + new string('*', value.Length - 2) + value[^1];
