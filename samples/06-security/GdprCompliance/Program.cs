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
//   5. Crypto-shredding — personal fields encrypted at rest under a per-subject key, which an
//      erasure destroys, so the read returns null on the strength of a durable destruction record
//
// Two DIFFERENT erasure mechanisms are shown, and the sample keeps them apart on purpose:
// the /erase and /tombstone endpoints OVERWRITE [PersonalData] fields that were stored in the clear;
// /crypto-shred/walkthrough stores ciphertext from the first write and destroys the key instead.
//
// ============================================================================

using Excalibur.Dispatch;
using Excalibur.Compliance;

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Erasure;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.InMemory;

using GdprCompliance.Commands;
using GdprCompliance.CryptoShredding;
using GdprCompliance.Domain;
using GdprCompliance.Projections;
using GdprCompliance.Retention;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// 0. Dispatch pipeline (command + event handlers discovered from this assembly)
// ----------------------------------------------------------------------------

builder.Services.AddDispatch(typeof(Program).Assembly);

// The ambient tenant context, which the two-tenant walkthrough in section 6 depends on and which a
// single-tenant host does not need. A subject key's identity includes its tenant, and the WRITE path reads
// that tenant from ITenantContext — so without a context that OBSERVES TenantContextHolder.BeginScope,
// every write in this host resolves the one default tenant, and two tenants sharing a data-subject
// identifier would share one key. The framework's default context is deliberately not that: it reports a
// single fixed tenant whatever scope is open.
//
// Both lines are required, and neither is interchangeable with the other.
//
// RequireTenant = true, because a resolving tenant context while it is false is REFUSED at start-up by
// name. That combination would apply the single-tenant (no discriminator) schema while routing two tenants
// through the same keyed rows, so one tenant's write would collide on the composite key and be silently
// deduplicated against the other's.
//
// TenantContextHolder.AmbientContext rather than the context AddTenantContext installs on its own, because
// outside any scope it resolves the reserved UNTENANTED PARTITION instead of leaving the tenant unresolved.
// Much of this host legitimately runs outside a scope — the seeding below, the erase/tombstone endpoints,
// and the background erasure scheduler — and a context resolving nothing there makes every tenant-required
// store fail closed on work that belongs to no tenant. The untenanted partition is a partition this
// framework names, and reads and writes there are confined to it exactly as a named tenant's are.
//
// Registered after AddDispatch rather than before, because the framework's single-tenant default is a
// TryAdd: the last unconditional registration is the one that resolves.
builder.Services.AddTenantContext(o => o.RequireTenant = true);
builder.Services.AddSingleton<ITenantContext>(TenantContextHolder.AmbientContext);

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

// The encryption provider registry, and the AES-256-GCM provider that does the actual work.
//
// Crypto-shredding needs BOTH halves and registers neither: key MANAGEMENT mints and destroys the
// per-subject key material, and an encryption PROVIDER turns a value into an envelope under it. The erasure
// registration above already supplies the first; this call supplies the second. Omit it and the host
// refuses to start with a dependency-injection error naming IEncryptionProviderRegistry.
//
// DEMO ONLY: in-memory key material does not survive a restart, which makes every value encrypted under it
// permanently unrecoverable — see the durability note at the top of this file. A real deployment keeps this
// call and swaps the provider for a cloud KMS one via UseKeyManagement<TProvider>().
builder.Services.AddEncryption(encryption => encryption
	.UseInMemoryKeyManagement("sample-inmemory", options => options.AutoGenerateDefaultKey = true)
	.SetAsPrimary("sample-inmemory"));

// Per-subject crypto-shredding is registered further down, in section 1b — it has to come AFTER the event
// store it encrypts.

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

// ----------------------------------------------------------------------------
// 1b. Per-subject crypto-shredding
// ----------------------------------------------------------------------------

// Registers SubjectFieldCryptor, IFieldEncryptor and the per-subject key manager, AND wires the encrypting
// decorators so [PersonalData] fields are ciphertext at rest in the event store, the inbox and the outbox.
//
// AFTER the store registrations, deliberately. Encryption lands as the innermost decorator, so bytes reach
// each store already encrypted; calling it earlier would have nothing to wrap.
//
// A HOST WITH AN EVENT STORE MUST USE THIS CALL RATHER THAN THE BARE AddCryptoShredding(). The plain call
// registers the services without wiring the store, and a start-up gate refuses that combination by name —
// annotated event fields would persist as plaintext and destroying a subject key would erase nothing.
// AddCryptoShredding() on its own is for a host that encrypts fields it manages itself, with no event store.
//
// NEITHER call registers the key-destruction ledger, and that omission is the whole design. A crypto-shredded
// field reads as erased only where a durable record says the key generation behind it was destroyed, so the
// ledger comes from whatever performs the destruction — the erasure store registered in section 1 supplies it
// from the same instance. Register no ledger at all and the host REFUSES TO START naming
// IKeyDestructionLedger, rather than failing later on the first read of an encrypted field.
//
// A deployment that encrypts personal data at rest and NEVER destroys a subject key — an encrypted event
// store with no erasure subsystem — calls AddCryptoShreddingWithoutErasure() instead of registering an
// erasure store. That supplies a ledger holding no rows, so nothing ever tombstones; in such a deployment
// that is the true answer rather than a stand-in. Registering both is also refused at start-up, because
// every ledger registration is a TryAdd and in one of the two orders an always-false ledger would stand in
// front of a real erasure store — the deployment would erase and never report it.
builder.Services.AddEventSourcingCryptoShredding();

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
	  POST /crypto-shred/walkthrough      a personal field encrypted at rest under its data
	                                      subject's own key, then read back as null after that
	                                      key is destroyed and the destruction recorded
	  POST /crypto-shred/two-tenants      two tenants holding a record for the same data-subject
	                                      id, each under its own key; one tenant erases and the
	                                      other tenant's field still decrypts

	Canonical pipeline (each erasure endpoint):
	  IDispatcher -> IActionHandler -> IErasureService + repo
	               -> CustomerErasedEvent / CustomerTombstonedEvent
	               -> IEventHandler<T> -> CustomerPrivacyView projection

	Try:
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111
	  curl -X POST http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/erase
	  curl http://localhost:5000/customers/11111111-1111-1111-1111-111111111111/privacy-view
	  curl -X POST http://localhost:5000/retention/walkthrough
	  curl -X POST http://localhost:5000/crypto-shred/walkthrough
	  curl -X POST http://localhost:5000/crypto-shred/two-tenants
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

	var status = await CryptoShredWalkthroughEndpoint
		.WaitForErasureAsync(erasureService, filed.RequestId, ct)
		.ConfigureAwait(false);

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

// ----------------------------------------------------------------------------
// 5. Crypto-shredding walkthrough
// ----------------------------------------------------------------------------
//
// A personal field encrypted at rest under its data subject's own key, then read back as null after
// that key is destroyed -- and ONLY because a durable ledger row says that key generation was
// destroyed. The handler, and the reasoning behind each step, are in
// CryptoShredding/CryptoShredWalkthroughEndpoint.cs.
app.MapCryptoShredWalkthrough();

// ----------------------------------------------------------------------------
// 6. Two tenants, one data-subject identifier
// ----------------------------------------------------------------------------
//
// The same personal field, written by two tenants for a data subject whose identifier they share --
// encrypted under a DIFFERENT key in each -- then one tenant's erasure, after which that tenant's field
// reads null and the other tenant's still decrypts. The handler is in
// CryptoShredding/TwoTenantCryptoShredEndpoint.cs.
app.MapTwoTenantCryptoShred();

// Printed once the host is actually listening. A startup failure never reaches this line, so it
// separates "started and serving" from "died or hung during startup" for anyone -- or anything --
// watching the output. The smoke profile for this sample matches on "Sample ready:", so without
// this line a run that stays up as designed is indistinguishable from one that hung, and the
// validator correctly refuses to call either a pass.
app.Lifetime.ApplicationStarted.Register(static () =>
	Console.WriteLine("Sample ready: GDPR erasure API listening. Press Ctrl+C to stop."));

await app.RunAsync().ConfigureAwait(false);

// Simple PII masking helper — a real sample would use Excalibur.Compliance's IDataMasker.
static string MaskPii(string value) =>
	string.IsNullOrEmpty(value) ? "<erased>" : value.Length <= 2 ? "**" : value[0] + new string('*', value.Length - 2) + value[^1];
