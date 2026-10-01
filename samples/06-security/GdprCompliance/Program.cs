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

using Excalibur.Compliance.Erasure;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.InMemory;

using GdprCompliance.Commands;
using GdprCompliance.Domain;
using GdprCompliance.Projections;
using GdprCompliance.Retention;

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

	// DEMO ONLY. The floor a request may lower the grace period to, which defaults to one hour. The
	// retention walkthrough below asks for an immediate erasure so you can watch it happen; without
	// this floor lowered, that request would be raised back to the minimum and you would wait an hour.
	// A real deployment leaves the floor where it is — the grace period is the data subject's window
	// to change their mind, and the erasure is irreversible.
	options.MinimumGracePeriod = TimeSpan.Zero;

	// Certificates are signed with HMAC-SHA256 and the framework refuses to write an unsigned one, so a
	// host with no key configured completes the erasure and produces no evidence of it.
	// DEMO ONLY: read from configuration with a runnable fallback. In production supply a
	// high-entropy key from your secret manager — never a literal in source.
	options.Retention.SigningKey = System.Text.Encoding.UTF8.GetBytes(
		builder.Configuration["Gdpr:CertificateSigningKey"]
			?? "sample-demo-certificate-signing-key-not-a-secret-change-me-0123456789");
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

// Erasure consults legal holds before it destroys anything, and refuses to start when no
// ILegalHoldService is registered — a hold that is never checked is a hold that does not exist, and an
// erasure cannot be undone. A deployment that genuinely operates no holds says so instead, by setting
// ErasureOptions.OperatesNoLegalHolds; the absence has to be a decision rather than an oversight.
builder.Services.AddInMemoryLegalHoldStore();
builder.Services.AddLegalHoldService();

// The discovery source. Erasure refuses to start with neither this nor an explicit
// ErasureOptions.KeyShredOnlyErasure opt-in, because a completion certificate would otherwise attest
// coverage that nothing verified. The service reads a store of declared data locations; register both.
builder.Services.AddInMemoryDataInventoryStore();
builder.Services.AddDataInventoryService();

// Runs scheduled erasures once their grace period has elapsed. Nothing executes an erasure without it —
// IErasureService files the request; this is what carries it out. Polled fast here so the walkthrough
// below finishes while you are watching it; the production default is five minutes.
builder.Services.AddErasureScheduler(o => o.PollingInterval = TimeSpan.FromSeconds(1));

// Compliance monitoring (audit log, metrics, alerts).
builder.Services.AddComplianceMonitoring();

// ----------------------------------------------------------------------------
// 1a. Event-sourced aggregates, and the retention that spares one of them
// ----------------------------------------------------------------------------

// Event types are registered explicitly so the serializer resolves them without an assembly scan.
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

builder.Services.AddSingleton<SubjectAggregateIndex>();

builder.Services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es => es
	.UseInMemory()
	.AddRepository<CustomerProfile, Guid>(id => new CustomerProfile(id))
	.AddRepository<SalesRecord, Guid>(id => new SalesRecord(id))

	// Opts the event store into erasure and names the type that maps a data subject to their
	// aggregates. Whatever that mapping returns is what gets tombstoned — wholly, every event,
	// irreversibly — unless a retention below spares it.
	.UseEventStoreErasure<SalesRetentionMapping>()));

// The aggregate types this deployment is legally obliged to keep through an erasure.
//
// Declaring one here is a legal decision, not a technical one: every type named survives an erasure
// whole and readable, INCLUDING the personal data inside it, and the erasure certificate names it with
// the basis, the justification and the period below. A type not named here is erased exactly as it
// would be if this call were absent.
//
// Declare it BEFORE the data is written. Naming a type here changes which key its personal fields are
// encrypted under, and that is what keeps the record readable afterwards; events written earlier were
// encrypted under the subject's own key, which the erasure destroys.
builder.Services.AddErasureRetention(new ErasureRetention
{
	// Matched ordinally against the type name the event store recorded. nameof keeps the declaration
	// and the class from drifting apart — a declaration of "salesrecord" would not match, and the
	// record would be destroyed.
	AggregateType = nameof(SalesRecord),

	// This deployment is not multi-tenant, so it names the untenanted sentinel — the same value an
	// untenanted erasure request carries. A retention is a statement about a jurisdiction and a
	// controller, so it is always tenanted; declaring the wrong tenant does not match, and the record
	// is erased rather than silently withheld from every other tenant's data subjects.
	TenantId = TenantScope.UntenantedSentinel,

	Basis = LegalHoldBasis.LegalObligation,

	// Written about the RECORD, not about a person. Every data subject named on a retained record is
	// shown this text, including people the statute was not written with in mind — so "the buyer's
	// identity is required" would be wrong for the salesperson named on the same record.
	Justification = "Vehicle sales records are kept for six years under the tax code's record-keeping "
				  + "requirement and for product-recall traceability.",

	// A statutory retention ends. The period is recorded and reported; it is not a timer, and nothing
	// here erases the record when it lapses. Releasing the retention then is yours.
	RetentionPeriod = TimeSpan.FromDays(365 * 6),
});

// ----------------------------------------------------------------------------
// 2. In-memory customer store (production: a real DB + PII-encryption)
// ----------------------------------------------------------------------------

builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();

// ----------------------------------------------------------------------------
// 2a. Privacy-state projection store (updated by IEventHandler<T> projections)
// ----------------------------------------------------------------------------

builder.Services.AddSingleton<ICustomerPrivacyViewStore, InMemoryCustomerPrivacyViewStore>();

// Unhandled exceptions become RFC 9457 Problem Details responses, with the status code taken from
// the exception (404 for ResourceNotFoundException, 409 for ConcurrencyException). Details of a
// 5xx response are hidden outside Development.
builder.Services.AddGlobalExceptionHandler();

var app = builder.Build();

app.UseExceptionHandler();

// Seed two demo customers
using (var scope = app.Services.CreateScope())
{
	// Declare where personal data lives. An erasure will not report Completed while a
	// [PersonalData]-annotated category is not covered by a registered location, and it issues no
	// certificate over an empty registry — an empty registry is an absence of evidence, not proof of
	// erasure. Register one location per annotated category on the store that holds it.
	var inventory = scope.ServiceProvider.GetRequiredService<IDataInventoryService>();
	foreach (var category in new[] { nameof(PersonalDataCategory.Identity), nameof(PersonalDataCategory.ContactInfo) })
	{
		await inventory.RegisterDataLocationAsync(
			new DataLocationRegistration
			{
				TableName = "events",
				FieldName = category,
				DataCategory = category,
				DataSubjectIdColumn = "data_subject_id_hash",
				KeyIdColumn = "encryption_key_id",
				IdType = DataSubjectIdType.Hash,
				StoreKind = DataStoreKind.EventStore,
				Description = $"{category} fields carried on event-sourced aggregates.",
			},
			CancellationToken.None).ConfigureAwait(false);
	}

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
	  POST /retention/walkthrough         one erasure across an erasable aggregate and a
	                                      declared-retained one, with the outcome of each

	Canonical pipeline (each erasure endpoint):
	  IDispatcher -> IActionHandler -> IErasureService + repo
	               -> CustomerErasedEvent / CustomerTombstonedEvent
	               -> IEventHandler<T> -> CustomerPrivacyView projection

	Try:
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111
	  curl -X POST http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/erase
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/privacy-view
	  curl -X POST http://localhost:5000/retention/walkthrough
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

// ----------------------------------------------------------------------------
// 4. Declared-retention walkthrough
// ----------------------------------------------------------------------------
//
// Runs one erasure for a data subject whose personal data sits in TWO event-sourced aggregates:
//
//   CustomerProfile  nothing declares a retention for it  ->  every event tombstoned
//   SalesRecord      declared above                       ->  survives whole and readable
//
// and reports what is left of each afterwards, plus what the signed certificate says about the one
// that survived.
app.MapPost("/retention/walkthrough", async (
	IErasureService erasureService,
	IDataSubjectHasher hasher,
	SubjectAggregateIndex index,
	IEventSourcedRepository<CustomerProfile, Guid> profiles,
	IEventSourcedRepository<SalesRecord, Guid> sales,
	CancellationToken ct) =>
{
	// --- Seed: one buyer, one profile, one sale ------------------------------------------------
	var buyerId = Guid.NewGuid();
	var saleId = Guid.NewGuid();
	var dataSubjectId = buyerId.ToString("D");

	await profiles
		.SaveAsync(CustomerProfile.Register(buyerId, "Dana Okoro", "dana@example.com"), ct)
		.ConfigureAwait(false);

	// The buyer's details are copied ONTO the sale, not referenced from the profile. That is what
	// keeps this record meaningful once the profile is gone.
	await sales
		.SaveAsync(
			SalesRecord.Record(saleId, buyerId, "Dana Okoro", "14 Kingsway, Leeds", "VIN-4Y1SL65848Z", 24_500m),
			ct)
		.ConfigureAwait(false);

	// Tell the mapping where this subject's data is. Both aggregates are recorded, including the
	// retained one: the mapping answers where the data IS; the retention answers what may be destroyed.
	var subjectHash = hasher.HashDataSubjectId(dataSubjectId);
	index.Add(subjectHash, new AggregateReference(buyerId.ToString("D"), nameof(CustomerProfile)));
	index.Add(subjectHash, new AggregateReference(saleId.ToString("D"), nameof(SalesRecord)));

	// --- Erase ---------------------------------------------------------------------------------
	var filed = await erasureService.RequestErasureAsync(
		new ErasureRequest
		{
			DataSubjectId = dataSubjectId,
			IdType = DataSubjectIdType.UserId,
			RequestedBy = "api/retention/walkthrough",
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Scope = ErasureScope.User,

			// Demo only, so the scheduler picks it up now instead of in 72 hours. A real request
			// leaves this unset and takes the configured grace period.
			GracePeriodOverride = TimeSpan.Zero,
		},
		ct).ConfigureAwait(false);

	// The scheduler executes it on its next poll. Poll the request's own status rather than sleeping
	// for a fixed interval, so the walkthrough returns as soon as the erasure is done.
	ErasureStatus? status = null;
	for (var attempt = 0; attempt < 60; attempt++)
	{
		status = await erasureService.GetStatusAsync(filed.RequestId, ct).ConfigureAwait(false);
		if (status?.Status is ErasureRequestStatus.Completed
			or ErasureRequestStatus.PartiallyCompleted
			or ErasureRequestStatus.Failed)
		{
			break;
		}

		await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
	}

	// --- Read both aggregates back -------------------------------------------------------------
	var profileAfter = await profiles.GetByIdAsync(buyerId, ct).ConfigureAwait(false);
	var saleAfter = await sales.GetByIdAsync(saleId, ct).ConfigureAwait(false);

	// --- What the certificate says about the survivor -------------------------------------------
	// A certificate can only be produced for a request whose status is exactly Completed;
	// GenerateCertificateAsync throws for any other status.
	object? retentionOnCertificate = null;
	if (status?.Status == ErasureRequestStatus.Completed)
	{
		var certificate = await erasureService
			.GenerateCertificateAsync(filed.RequestId, ct)
			.ConfigureAwait(false);

		retentionOnCertificate = certificate.Payload.Exceptions
			.Select(e => new
			{
				e.DataCategory,

				// NotEstablished means nobody stated an Article 17(3) ground -- a statement rather than a gap.
				// It is the enum's zero, so a value no binder, deserializer or cast ever assigned says so
				// instead of reading as Article 17(3)(a). An erasure carrying one is recorded as not complete,
				// so this only ever prints on a certificate that says so itself.
				Basis = e.Basis.ToString(),
				e.Reason,

				// The END of the retention, not its LENGTH — and it is not established here. A length with no
				// instant to measure it from invites the reader to anchor it on the certificate's own date,
				// which restarts the statutory clock at the moment the subject asked to be erased. The
				// obligation's real end depends on facts this deployment holds and the framework does not.
				RetentionEnd = "not established — see the declared obligation for this aggregate type",

				// The key still protecting this subject's fields inside the retained record. When the
				// statutory period ends, destroying it is the act that finishes the erasure, and it
				// can only be done by name — so the record names it.
				e.RetainedKeyHandle,
			})
			.ToList();
	}

	return Results.Ok(new
	{
		RequestId = filed.RequestId,
		Status = status?.Status.ToString(),
		Erasable = new
		{
			AggregateType = nameof(CustomerProfile),
			Survived = profileAfter is not null && !string.IsNullOrEmpty(profileAfter.FullName),
			FullName = profileAfter?.FullName,
			EmailAddress = profileAfter?.EmailAddress,
		},
		Retained = new
		{
			AggregateType = nameof(SalesRecord),
			Survived = saleAfter is not null && !string.IsNullOrEmpty(saleAfter.BuyerName),
			saleAfter?.BuyerName,
			saleAfter?.BuyerAddress,
			saleAfter?.VehicleIdentificationNumber,
			saleAfter?.SalePrice,
		},
		Certificate = retentionOnCertificate,
	});
});

await app.RunAsync().ConfigureAwait(false);

// Simple PII masking helper — a real sample would use Excalibur.Compliance's IDataMasker.
static string MaskPii(string value) =>
	string.IsNullOrEmpty(value) ? "<erased>" : value.Length <= 2 ? "**" : value[0] + new string('*', value.Length - 2) + value[^1];
