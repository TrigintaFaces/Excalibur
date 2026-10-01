// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A retention has to have an EXIT, and the exit is a key handle. The erasure record names it, and the
/// name it records is the one that actually protects the retained data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a guarantee and not a convenience.</b> A retained aggregate type keeps a key of its own,
/// so the surviving record stays readable after the data subject's own key is destroyed. That key is NOT
/// the subject's handle, so nothing in the erasure path ever queues it again — and the declared retention
/// period, which this framework makes mandatory precisely so an obligation cannot become permanent, can
/// only be honoured by destroying that key BY NAME. A period the consumer cannot act on is decorative,
/// and a record asserting a bound the system cannot keep is worse than one asserting no bound at all.
/// </para>
/// <para>
/// <b>These arms bind the handle by USING it, not by matching a string.</b> The first asserts the recorded
/// handle is byte-for-byte the one the real write path put on a real envelope; the second destroys the
/// recorded handle and shows the retained data becomes unrecoverable. A recorded handle that named
/// something else would satisfy a presence assertion and fail both of these.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheRecordNamesTheHandleThatReleasesARetentionShould
{
	private const string Subject = "the-buyer-on-a-retained-sales-record";
	private const string RetainedType = "SalesRecord";
	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.";

	/// <summary>
	/// SAFETY. The handle on the record is the handle on the envelope — so destroying what the record
	/// names destroys what the retention is protecting, and nothing else.
	/// </summary>
	/// <remarks>
	/// RED input: stop stamping the handle onto the retained entry, or derive it from anything other than
	/// the map the spare-decision built. A handle that names a different key would be destroyed with
	/// confidence and leave the retained data intact — the failure that looks like success.
	/// </remarks>
	[Fact]
	public async Task Name_the_same_handle_the_write_path_put_on_the_envelope()
	{
		await using var stack = new RetainedSubjectStack();
		var envelope = await stack.EncryptForTheRetainedTypeAsync();

		var certificate = await stack.EraseAsync();

		var entry = certificate.Payload.Exceptions.ShouldHaveSingleItem();
		entry.DataCategory.ShouldBe(RetainedType);
		entry.RetainedKeyHandle.ShouldNotBeNull(
			"a retention whose exit is not named cannot be released, so the mandatory period it declares "
			+ "is decorative and the record asserts a bound the system cannot keep");
		entry.RetainedKeyHandle.ShouldBe(
			envelope.KeyId,
			"the recorded handle must be the one the write path actually used. A handle that names a "
			+ "different key is destroyed with confidence and leaves the retained data readable");
	}

	/// <summary>
	/// SAFETY, and it is the property a consumer acts on. Destroying exactly what the record names makes
	/// the retained data unrecoverable — the retention is released, by name, with nothing else needed.
	/// </summary>
	/// <remarks>
	/// This is the arm that would go red on a plausible-but-wrong handle. It reads the handle off the
	/// signed record, hands it to the same admin API a consumer would, and then asks the retained data to
	/// decrypt.
	/// </remarks>
	[Fact]
	public async Task Release_the_retention_when_the_recorded_handle_is_destroyed()
	{
		await using var stack = new RetainedSubjectStack();
		var envelope = await stack.EncryptForTheRetainedTypeAsync();

		var certificate = await stack.EraseAsync();

		(await stack.DecryptAsync(envelope)).ShouldNotBeNull(
			"the arm is worthless unless the retention genuinely survived the erasure first");

		var handle = certificate.Payload.Exceptions.ShouldHaveSingleItem().RetainedKeyHandle;
		_ = await stack.DestroyAsync(handle!);

		(await stack.DecryptAsync(envelope)).ShouldBeNull(
			"the obligation has lapsed and the consumer destroyed what the record named; the retained "
			+ "data must now be unrecoverable, or the retention has no exit");
	}

	/// <summary>
	/// LIVENESS, and it is what stops the arms above being satisfied by stamping a handle on everything.
	/// An entry this erasure made no key decision about carries NO handle.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A contributor can report a type the registry never declared for this tenant. That is a real
	/// disagreement, and the honest record of it is an absent handle: the erasure decided nothing about
	/// that key, so it has no name to offer. Inventing one would invite a consumer to destroy a key that
	/// is protecting live data — the opposite failure, and the worse of the two.
	/// </para>
	/// <para>
	/// RED input: stamp a handle onto every entry rather than only the ones the spare-decision produced.
	/// </para>
	/// <para>
	/// NOTE ON ITS OWN HISTORY, because it is the lesson: this arm first asserted over a certificate whose
	/// exception list was EMPTY, so it was vacuously true and stayed green under exactly the mutation it
	/// exists to catch. It now names an undeclared type, which puts a real entry on the record.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Leave_an_entry_this_erasure_made_no_key_decision_about_without_a_handle()
	{
		await using var stack = new RetainedSubjectStack(contributorReports: "Warranty");
		_ = await stack.EncryptForTheRetainedTypeAsync();

		var certificate = await stack.EraseAsync();

		var entry = certificate.Payload.Exceptions.ShouldHaveSingleItem(
			"the arm is vacuous unless the record actually carries an entry to inspect");
		entry.DataCategory.ShouldBe("Warranty");
		entry.RetainedKeyHandle.ShouldBeNull(
			"no retention is declared for this type, so the erasure decided nothing about its key and has "
			+ "no handle to name. A handle here would invite a consumer to destroy a key protecting live "
			+ "data");
	}

	/// <summary>
	/// SAFETY, and it is the arm a measurement forced. A single-tenant deployment's retention is honoured
	/// against the tenant term the REAL store stamped, not one the test chose.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The store stamps an untenanted request with <see cref="TenantScope.UntenantedSentinel"/>, while a
	/// declaration is written by a human. Every earlier arm here ran against a faked store whose status
	/// carried a null tenant, so none of them could see a disagreement between those two spellings —
	/// which is exactly the defect class that reached this code twice.
	/// </para>
	/// <para>
	/// RED input: stamp the request's tenant raw in the store rather than through the partition, and the
	/// two sides stop agreeing about what untenanted is called.
	/// </para>
	/// <para>
	/// A second RED input was claimed here and withdrawn: "compare the declared tenant as written rather
	/// than normalising it". This fixture's declaration already NAMES the sentinel, so dropping declaration-
	/// side normalisation leaves it unchanged and this arm stays green — the mutation is unreachable by
	/// this generator. What makes that mutation safe is the validator refusing a blank tenant, which has its
	/// own arm; a remark promising coverage the generator cannot produce is the same defect as an arm that
	/// cannot fail.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Honour_a_retention_against_the_tenant_term_the_real_store_stamped()
	{
		await using var stack = new RetainedSubjectStack();
		var envelope = await stack.EncryptForTheRetainedTypeAsync();

		var certificate = await stack.EraseAsync();

		stack.StoredTenantTerm.ShouldBe(
			TenantScope.UntenantedSentinel,
			"the arm is vacuous unless the store really did stamp a term of its own choosing; if it stamped "
			+ "null this is the faked-store situation again and proves nothing about the spellings");

		(await stack.DecryptAsync(envelope)).ShouldNotBeNull(
			"the declaration and the erasure name the same tenant, so the retained record survives");
		certificate.Payload.Exceptions.ShouldNotBeEmpty(
			"and the record must SAY it was retained, or a surviving record is indistinguishable from one "
			+ "the erasure simply missed");
	}

	/// <summary>
	/// SAFETY for the case a NAMED tenant actually gets: a retention declared for <c>tenant-a</c> protects
	/// <c>tenant-a</c>'s own erasure, and the protection is asserted as PROTECTION rather than as a lookup.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm exists because a mutation showed nothing could fail without it. Resolving this erasure's
	/// tenant to <see langword="null"/> left every targeted suite green, because every fixture named no
	/// tenant on every side — so a unification that quietly dropped the tenant would have tombstoned
	/// the legally-required records of every NAMED tenant with no arm to see it. The registry's own tenant
	/// term was already guarded; the hole was one level out, at this consumer.
	/// </para>
	/// <para>
	/// So the fixture names a non-null tenant on BOTH sites at once — the declaration and the erasure
	/// request. An arm that named one and left the other null could not tell <em>resolved correctly</em>
	/// from <em>resolved to null</em>: both answers match when both sides are null.
	/// </para>
	/// <para>
	/// RED input: resolve the tenant this erasure was recorded with to <see langword="null"/> before the
	/// retention lookup. The declaration stops matching, so the handle is not spared — and, because a
	/// widened handle no declaration spares is now destroyed, the retained record is actively tombstoned
	/// while the certificate still names the retention.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Honour_a_retention_for_the_named_tenant_that_declared_it()
	{
		await using var stack = new RetainedSubjectStack(
			declarationTenant: "tenant-a", erasureTenant: "tenant-a");
		var envelope = await stack.EncryptForTheRetainedTypeAsync();

		(await stack.DecryptAsync(envelope)).ShouldNotBeNull(
			"the arm is worthless unless the value genuinely round-tripped first");

		var certificate = await stack.EraseAsync();

		stack.StoredTenantTerm.ShouldBe(
			"tenant-a",
			"the arm is vacuous unless the store really recorded the tenant; if it stamped anything else "
			+ "the lookup below is not being asked the question this arm names");

		(await stack.DecryptAsync(envelope)).ShouldNotBeNull(
			"the declaring tenant's own subject is the case the retention was written for. This is the "
			+ "PROTECTION, not the lookup: a registry match that did not spare the key is no protection");

		certificate.Payload.Exceptions.ShouldNotBeEmpty(
			"and the record must say the data was retained, or a surviving record cannot be told from one "
			+ "the erasure missed");

		stack.Unretained.ShouldNotBeNull();
		(await stack.DecryptAsync(stack.Unretained!)).ShouldBeNull(
			"LIVENESS: this same erasure must still destroy what it was entitled to. Without this the arm "
			+ "above is satisfied by an erasure that did nothing at all");
	}

	/// <summary>
	/// SAFETY, and it is the half the tenant-blind write path owes: a handle widened on ANOTHER tenant's
	/// declaration is DESTROYED by this subject's erasure, not merely left unspared.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The write widens on the aggregate type alone, so a subject of a tenant with no obligation still has
	/// their fields placed under <c>H(subject)-&lt;type&gt;</c>. Nothing else would ever destroy that key:
	/// the subject's own handle is a different key, and the data inventory reports only what a consumer
	/// registered. A key nothing destroys is a subject never erased, so the erasure must enumerate the
	/// widened handles from the DECLARATIONS and destroy every one no declaration spares.
	/// </para>
	/// <para>
	/// RED input: skip the foreign-tenant handle instead of destroying it — the spare set is already
	/// correct, so an erasure that only decides what to SPARE passes every other arm in this file while
	/// leaving this subject's data readable forever under a key nobody can name.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Destroy_a_handle_widened_for_a_tenant_whose_retention_does_not_apply()
	{
		await using var stack = new RetainedSubjectStack(declarationTenant: "another-tenant");
		var envelope = await stack.EncryptForTheRetainedTypeAsync();

		(await stack.DecryptAsync(envelope)).ShouldNotBeNull(
			"the arm is worthless unless the value genuinely round-tripped first");

		_ = await stack.EraseAsync();

		(await stack.DecryptAsync(envelope)).ShouldBeNull(
			"this subject's controller is under no obligation, so the widened handle holds ordinary "
			+ "personal data. Leaving it is a key nothing else destroys");
	}

	/// <summary>
	/// The real crypto-shredding stack and a real retention registry, with an <see cref="ErasureService"/>
	/// whose subject hash is the one the write path used — so a handle that matches is a real match and
	/// not an artefact of two components agreeing on a literal.
	/// </summary>
	private sealed class RetainedSubjectStack : IAsyncDisposable
	{
		private readonly ServiceProvider _provider;
		private readonly ILegalHoldService _legalHolds = A.Fake<ILegalHoldService>();
		private readonly string _contributorReports;
		private readonly string? _erasureTenant;
		private bool _started;

		// contributorReports names the type the contributor says it retained. Defaulting it to the
		// DECLARED type is the ordinary case; passing an undeclared one is the disagreement case.
		// declarationTenant names the tenant the retention is declared FOR; the erasure itself always names
		// no tenant, so passing another tenant is the case where the obligation is somebody else's.
		public RetainedSubjectStack(
			string contributorReports = RetainedType,
			string? declarationTenant = null,
			string? erasureTenant = null)
		{
			_contributorReports = contributorReports;
			_erasureTenant = erasureTenant;

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddDataSubjectHashing();
			_ = services.Configure<DataSubjectHashingOptions>(options =>
				options.Pepper = "test-pepper-0123456789abcdef0123456789ab");
			_ = services.AddEncryption(builder =>
				builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
			_ = services.AddSingleton<IKeyManagementAdmin>(sp =>
				(IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

			_ = services.AddErasureRetention(new ErasureRetention
			{
				AggregateType = RetainedType,
				TenantId = declarationTenant ?? TenantScope.UntenantedSentinel,
				Basis = LegalHoldBasis.LegalObligation,
				Justification = Justification,
				RetentionPeriod = TimeSpan.FromDays(365 * 6),
			});

			_ = services.AddCryptoShredding();

			// THE REAL STORE, and it is the point rather than fidelity for its own sake. A faked store
			// returns a status whose tenant the TEST chose, so every arm built on one is blind to how the
			// store actually spells an untenanted request -- which is the disagreement that reached this
			// code. The store needs a tenant context of its own; the crypto-shredding path does not, and no
			// longer registers one.
			_ = services.AddDefaultTenantContext();
			_ = services.AddSingleton<InMemoryErasureStore>();
			_ = services.AddSingleton<IErasureStore>(sp => sp.GetRequiredService<InMemoryErasureStore>());

			_provider = services.BuildServiceProvider();
		}

		// What the REAL store stamped as this erasure's tenant, so an arm can assert the spelling rather
		// than assume it.
		public string? StoredTenantTerm { get; private set; }

		public async ValueTask<EncryptedData> EncryptForTheRetainedTypeAsync()
		{
			await StartAsync();

			var encryptor = _provider.GetRequiredService<IFieldEncryptor>();

			// The subject also holds data OUTSIDE the retained type. That is the case the retention
			// exists for -- the customer aggregate is destroyed, the sales record survives -- and it is
			// also what gives the erasure a key to destroy: without it the subject's own handle was
			// never minted, nothing is destroyed, and no certificate is owed at all.
			Unretained = await encryptor.EncryptAsync(
				Subject,
				RetentionScope.NotInAnAggregate,
				"the customer's own record"u8.ToArray(),
				TestContext.Current.CancellationToken);

			return await encryptor.EncryptAsync(
				Subject,
				RetentionScope.For(RetainedType),
				"the buyer's identity on the sales record"u8.ToArray(),
				TestContext.Current.CancellationToken);
		}

		public async ValueTask<byte[]?> DecryptAsync(EncryptedData envelope)
		{
			await StartAsync();

			return await _provider.GetRequiredService<IFieldEncryptor>()
				.DecryptAsync(envelope, TestContext.Current.CancellationToken);
		}

		public EncryptedData? Unretained { get; private set; }

		public async ValueTask<KeyDestructionOutcome> DestroyAsync(string handle) =>
			await _provider.GetRequiredService<IKeyManagementAdmin>()
				.DeleteKeyAsync(handle, retentionDays: 0, TestContext.Current.CancellationToken);

		public async ValueTask<ErasureCertificate> EraseAsync()
		{
			var requestId = await GivenAnErasureAsync();

			_ = await NewService().ExecuteAsync(requestId, TestContext.Current.CancellationToken);

			var saved = await Store.GetCertificateAsync(requestId, TestContext.Current.CancellationToken);

			saved.ShouldNotBeNull(
				"the execution path is the only place the structured residue exists; with nothing saved "
				+ "here there is no record for a consumer to read the handle off");

			return saved!;
		}

		public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

		// The registry populates on first resolution of IEncryptionProvider and selects its primary only
		// once every registered IHostedService has started, which a real host does for us.
		private async ValueTask StartAsync()
		{
			if (_started)
			{
				return;
			}

			_started = true;
			_ = _provider.GetServices<IEncryptionProvider>().ToList();
			foreach (var hostedService in _provider.GetServices<IHostedService>())
			{
				await hostedService.StartAsync(TestContext.Current.CancellationToken);
			}
		}

		private InMemoryErasureStore Store => _provider.GetRequiredService<InMemoryErasureStore>();

		// A REAL request, saved through the REAL store, which is what makes the tenant term on the status
		// the store's own rather than this test's. The subject hash is the store's too: it hashes the
		// identifier itself, so the erasure and the write path agree by construction instead of by a
		// literal repeated in two places.
		private async ValueTask<Guid> GivenAnErasureAsync()
		{
			await StartAsync();

			var requestId = Guid.NewGuid();

			await Store.SaveRequestAsync(
				new ErasureRequest
				{
					RequestId = requestId,
					DataSubjectId = Subject,
					IdType = DataSubjectIdType.UserId,
					Scope = ErasureScope.User,
					LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
					RequestedBy = "admin",
					RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
					TenantId = _erasureTenant,
				},
				DateTimeOffset.UtcNow,
				TestContext.Current.CancellationToken);

			var status = await Store.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
			status.ShouldNotBeNull("the arms below are about what the store recorded; it must have recorded");
			StoredTenantTerm = status!.TenantId;

			A.CallTo(() => _legalHolds.CheckHoldsAsync(
					A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
				.Returns(Task.FromResult(
					new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

			return requestId;
		}

		// The REAL key admin, so the erasure destroys the subject's own handle for real and the retained
		// handle's survival is a fact about the key store rather than a fake's configured answer.
		//
		// THE SERVICE IS STILL HAND-CONSTRUCTED HERE, AND THESE ARMS THEREFORE PROVE NOTHING ABOUT THE
		// WIRING. That is not a defect in them -- they bind the RECORD's behaviour, which needs a fake
		// legal-hold service and a contributor this container does not register. It IS a limit on what
		// they can be cited for: while the registration omitted the retention registry, this helper
		// supplied it by hand and stayed green, so the guarantee's stated evidence could not fail on the
		// one thing that was broken in production. The wiring is bound instead by
		// TheRegisteredErasureServiceSeesDeclaredRetentionsShould, which resolves the service from a real
		// container. Read these two as a pair; neither is sufficient alone.
		private ErasureService NewService() =>
			new(Store,
				_provider.GetRequiredService<IKeyManagementAdmin>(),
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions
				{
					KeyShredOnlyErasure = true,
					Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
				}),
				NullLogger<ErasureService>.Instance,
				_provider.GetRequiredService<IDataSubjectHasher>(),
				_legalHolds,
				null,
				null,
				_provider.GetRequiredService<IErasureRetentionRegistry>(),
				[new RetainedContributor(_contributorReports)]);

		/// <summary>
		/// The shape the event-store contributor has when one aggregate type is lawfully retained. It
		/// names the type; the service is what knows the handle, and the join between them is what these
		/// arms bind.
		/// </summary>
		private sealed class RetainedContributor(string reports) : IErasureContributor
		{
			public string Name => "EventStore";

			public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

			public Task<ErasureContributorResult> EraseAsync(
				ErasureContributorContext context,
				CancellationToken cancellationToken) =>
				Task.FromResult(ErasureContributorResult.Succeeded(
					1,
					[],
					[
						new ErasureException
						{
							Basis = LegalHoldBasis.LegalObligation,
							DataCategory = reports,
							Reason = Justification,
							RetentionPeriod = TimeSpan.FromDays(365 * 6),
						},
					]));
		}
	}
}
