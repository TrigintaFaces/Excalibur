// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// The half of an aggregate retention that makes the retained record READABLE rather than surviving as
/// dead ciphertext: a retained aggregate type's personal fields are protected by a key of their own, so
/// the erasure destroys the subject's key without taking the retained record with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tombstoning alone cannot achieve this.</b> An erasure destroys the data subject's key, and that
/// destruction reaches replicas and backups — everywhere the ciphertext went. A retained record encrypted
/// under that key survives the tombstone and decrypts to nothing, which discharges neither obligation:
/// the subject's data is gone and the record the law requires is unusable.
/// </para>
/// <para>
/// <b>The unit is (tenant, aggregate type), WHOLE.</b> An obligation to keep a record attaches to the
/// record: a statute requiring sales records to be kept does not require the buyer and permit deleting
/// the salesperson, because a partly-erased record is a mutated record with no evidentiary value. So
/// EVERY data subject named in a retained type keeps their key for it, and the second arm below binds
/// exactly that — it is the arm that fails if the scope is ever narrowed back to one person.
/// </para>
/// <para>
/// <b>Bound to the real stack.</b> A real in-memory key-management provider, real AES-GCM, and the real
/// <c>SubjectKeyManager</c>/<c>FieldEncryptor</c> pair — no substitutes on the path under test, so an arm
/// going green means the shipped composition behaves this way.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ARetainedAggregateTypeKeepsItsOwnKeyShould
{
	// These arms are single-tenant: an unconfigured ambient context resolves to the framework default
	// identity, so that is the tenant every handle in this file is derived for.
	private static readonly TenantId SingleTenant = new(TenantDefaults.DefaultTenantId);

	private const string Subject = "subject-with-a-sales-record";
	private const string SalesRecord = "SalesRecord";
	private const string Salesperson = "a-salesperson-on-the-same-record";
	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.";

	/// <summary>
	/// SAFETY, and the operator's own case. Destroying the subject's key leaves the retained type's
	/// fields decryptable and the unretained ones unrecoverable.
	/// </summary>
	/// <remarks>
	/// RED input: stop passing the scope to the cryptor, or ignore it in <c>SubjectKeyManager</c>, so both
	/// resolve to the subject's own handle. The retained assertion fails — the record decrypts to null.
	/// </remarks>
	[Fact]
	public async Task Leave_a_retained_types_fields_decryptable_after_the_subject_is_erased()
	{
		await using var provider = BuildStack(Retention());
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();

		var asBuyer = "the buyer's identity on the sales record"u8.ToArray();
		var asCustomer = "the customer's own record"u8.ToArray();

		var retainedEnvelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For(SalesRecord), asBuyer, TestContext.Current.CancellationToken);
		var erasedEnvelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For("Customer"), asCustomer, TestContext.Current.CancellationToken);

		(await encryptor.DecryptAsync(retainedEnvelope, TestContext.Current.CancellationToken))
			.ShouldBe(asBuyer, "the arm is worthless unless both values genuinely round-tripped first");
		(await encryptor.DecryptAsync(erasedEnvelope, TestContext.Current.CancellationToken))
			.ShouldBe(asCustomer);

		await EraseAsync(scope.ServiceProvider);

		(await encryptor.DecryptAsync(erasedEnvelope, TestContext.Current.CancellationToken))
			.ShouldBeNull("the unretained aggregate's personal fields must be unrecoverable");
		(await encryptor.DecryptAsync(retainedEnvelope, TestContext.Current.CancellationToken))
			.ShouldBe(
				asBuyer,
				"the retained record must stay READABLE, including the buyer's identity inside it. A "
				+ "retained record that survives as ciphertext nobody can open discharges nothing");
	}

	/// <summary>
	/// SAFETY, and it is the arm that binds the unit to the WHOLE record. A SECOND data subject named in
	/// the same retained aggregate keeps their fields readable through their own erasure.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The obligation attaches to the record, so a sales record kept for six years is kept entire — the
	/// salesperson's identity on it as much as the buyer's. Erasing either of them out of it would leave a
	/// mutated record, and a mutated record has no evidentiary value, which was the whole reason for
	/// keeping it.
	/// </para>
	/// <para>
	/// RED input: narrow the key scope back to one data subject, by any means — a role discriminator on
	/// the handle, a per-subject retention lookup. This second subject's field then decrypts to null while
	/// the record it sits on survives, which is the mutated-record outcome the ruling forbids.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Keep_a_second_data_subject_in_the_same_retained_type_readable_through_their_erasure()
	{
		await using var provider = BuildStack(Retention());
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();

		var asSalesperson = "the employee credited with the sale"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Salesperson, RetentionScope.For(SalesRecord), asSalesperson,
			TestContext.Current.CancellationToken);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken))
			.ShouldBe(asSalesperson, "the arm is worthless unless the value genuinely round-tripped first");

		await EraseAsync(scope.ServiceProvider, Salesperson);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken))
			.ShouldBe(
				asSalesperson,
				"the statute keeps the RECORD, and this subject is named on it. Erasing them out of it "
				+ "would leave a mutated record with no evidentiary value — which is what the retention "
				+ "exists to prevent");
	}

	/// <summary>
	/// SAFETY for the write path, and it is deliberately TENANT-BLIND: a type another TENANT declared still
	/// widens the handle, because the write cannot know whose retention will apply and the handle carries no
	/// tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This asserts only what the WRITE decides. It does NOT assert that such a subject is erased — that
	/// is the erasure's decision, and the arm binding it runs a real erasure service (the cross-tenant
	/// destruction arm in <c>TheRecordNamesTheHandleThatReleasesARetentionShould</c>). The split is not
	/// tidiness: the erase helper in this file destroys the plain subject handle directly, so an end-to-end
	/// claim made here would assert a set the test computed itself rather than the one the service computes,
	/// and would pass whether or not the service destroys the widened handle.
	/// </para>
	/// <para>
	/// RED input: key <c>IsRetainedForAnyTenant</c> on a tenant. The handle stops widening for a foreign
	/// declaration, so a write made outside any tenant scope lands under the plain subject handle while the
	/// declaring tenant's erasure spares a widened handle nothing was ever written under.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Widen_the_handle_for_a_type_another_tenant_declared()
	{
		await using var provider = BuildStack(Retention() with { TenantId = "another-tenant" });
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
		var hasher = scope.ServiceProvider.GetRequiredService<IDataSubjectHasher>();

		var data = "a subject of a different controller"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For(SalesRecord), data, TestContext.Current.CancellationToken);

		envelope.KeyId.ShouldNotBeNullOrEmpty("the arm is vacuous unless a handle was produced");
		envelope.KeyId.ShouldNotBe(
			SubjectKeyHandle.ForSubject(SingleTenant, Subject, hasher).Value,
			"the type is declared by SOMEONE, so the write widens. Whose declaration it was is not a "
			+ "question the write path can answer, and it does not try");
	}

	/// <summary>
	/// LIVENESS, and it is what stops the arms above being satisfied by giving every scope its own key. An
	/// aggregate type with no declared retention shares the subject's key and is shredded with it.
	/// </summary>
	/// <remarks>
	/// RED input: scope the handle for every aggregate type rather than only for declared retentions. The
	/// erasure then destroys a handle nothing was written under, and this arm fails — the total-erasure
	/// failure direction, the more dangerous of the two.
	/// </remarks>
	[Fact]
	public async Task Shred_an_undeclared_aggregate_type_with_the_subjects_own_key()
	{
		await using var provider = BuildStack(Retention());
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();

		var data = "personal data in an undeclared aggregate type"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For("Invoice"), data, TestContext.Current.CancellationToken);

		await EraseAsync(scope.ServiceProvider);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken))
			.ShouldBeNull(
				"an aggregate type nobody declared a retention for is erased exactly as it was before "
				+ "retentions existed");
	}

	/// <summary>
	/// LIVENESS. A deployment that declares no retention at all is unchanged: every scope resolves to the
	/// subject's own key.
	/// </summary>
	/// <remarks>RED input: scope the handle unconditionally — this arm fails.</remarks>
	[Fact]
	public async Task Shred_everything_when_the_deployment_declares_no_retention()
	{
		await using var provider = BuildStack(retention: null);
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();

		var data = "everything goes"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For(SalesRecord), data, TestContext.Current.CancellationToken);

		await EraseAsync(scope.ServiceProvider);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken))
			.ShouldBeNull("no retention is declared, so the same type is erased with the subject");
	}

	/// <summary>
	/// LIVENESS. A value not stored inside an aggregate resolves to the subject's own key and is shredded.
	/// </summary>
	/// <remarks>
	/// The unscoped case is the default and by far the commonest. RED input: treat
	/// <see cref="RetentionScope.NotInAnAggregate"/> as scoped — every such value would then outlive the
	/// erasure that was supposed to reach it.
	/// </remarks>
	[Fact]
	public async Task Shred_a_value_that_is_not_stored_inside_an_aggregate()
	{
		await using var provider = BuildStack(Retention());
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();

		var data = "a personal field outside any aggregate"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.NotInAnAggregate, data, TestContext.Current.CancellationToken);

		await EraseAsync(scope.ServiceProvider);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	/// <summary>
	/// SAFETY. The retained key is independently destroyable, so a controller whose obligation lapses can
	/// still erase the record — the retention withholds destruction, it does not make it impossible.
	/// </summary>
	[Fact]
	public async Task Let_the_retained_key_be_destroyed_on_its_own_once_the_obligation_lapses()
	{
		await using var provider = BuildStack(Retention());
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var encryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
		var keyAdmin = scope.ServiceProvider.GetRequiredService<IKeyManagementAdmin>();

		var data = "the buyer's identity"u8.ToArray();
		var envelope = await encryptor.EncryptAsync(
			Subject, RetentionScope.For(SalesRecord), data, TestContext.Current.CancellationToken);

		// The envelope carries the handle that protects it, which is what makes the retained key reachable
		// without anyone having to re-derive it.
		envelope.KeyId.ShouldNotBeNullOrEmpty();
		await DestroyAndRecordAsync(scope.ServiceProvider, envelope.KeyId!);

		(await encryptor.DecryptAsync(envelope, TestContext.Current.CancellationToken))
			.ShouldBeNull("the retention delays destruction; it does not put the record beyond erasure");
	}

	/// <summary>
	/// SAFETY, and it is the arm that makes a defaulted tenant context inexpressible. The handle for one
	/// subject in one scope is IDENTICAL however the host was composed — in particular, whether or not some
	/// unrelated feature registered a tenant context first.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A handle is the NAME of the thing an erasure destroys, so a handle that varies with composition is
	/// worse than a store that varies with it. One composition writes under a handle the erasure spares and
	/// the other under a handle it destroys: either the retained record becomes unreadable while the
	/// certificate attests it was kept, or erased data survives under a signed completion. Neither reports
	/// anything.
	/// </para>
	/// <para>
	/// It holds today by CONSTRUCTION rather than by care, which is the stronger position: the key manager
	/// takes no tenant at all, so there is no ambient value a composition could change. The arm is kept as
	/// the regression guard on exactly that — it is what fails if a tenant is ever read back onto this
	/// path, for whatever reason.
	/// </para>
	/// <para>
	/// RED input: make the handle depend on an ambient tenant again. The framework's single-tenant context
	/// names <c>TenantDefaults.DefaultTenantId</c>, a REAL identity rather than a reserved marker, so the
	/// composition that registered one produces a different handle from the composition that did not —
	/// and a consumer flips between the two by referencing an unrelated package.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Derive_the_same_handle_however_the_host_was_composed()
	{
		await using var plain = BuildStack(Retention());
		await using var withAnUnrelatedTenantContext = BuildStack(Retention(), registerTenantContextFirst: true);
		await StartAsync(plain);
		await StartAsync(withAnUnrelatedTenantContext);

		var data = "the buyer's identity on the sales record"u8.ToArray();

		var fromPlain = await plain.CreateScope().ServiceProvider.GetRequiredService<IFieldEncryptor>()
			.EncryptAsync(Subject, RetentionScope.For(SalesRecord), data, TestContext.Current.CancellationToken);
		var fromComposed = await withAnUnrelatedTenantContext.CreateScope().ServiceProvider
			.GetRequiredService<IFieldEncryptor>()
			.EncryptAsync(Subject, RetentionScope.For(SalesRecord), data, TestContext.Current.CancellationToken);

		fromPlain.KeyId.ShouldNotBeNullOrEmpty(
			"the arm is vacuous unless a handle was actually produced");
		fromComposed.KeyId.ShouldBe(
			fromPlain.KeyId,
			"a consumer flips whether a tenant context is registered by referencing an unrelated package. If "
			+ "that changes the key handle, one composition writes under a handle the erasure spares and the "
			+ "other under one it destroys — and the record and the certificate then disagree silently");
	}

	private static ErasureRetention Retention() => new()
	{
		AggregateType = SalesRecord,
		TenantId = TenantScope.UntenantedSentinel,
		Basis = LegalHoldBasis.LegalObligation,
		Justification = Justification,
		RetentionPeriod = TimeSpan.FromDays(365 * 6),
	};

	// Exactly what the erasure destroys: the data subject's own key handle for the tenant whose erasure this
	// is. These arms are single-tenant, so that is the framework default identity -- the same one the write
	// path resolves from an unconfigured ambient context. Destroying the bare subject-id hash, as this helper
	// used to, would destroy a key nothing was written under and leave every arm below satisfied by an
	// erasure that achieved nothing.
	private static async Task EraseAsync(IServiceProvider services, string subjectId = Subject)
	{
		var hasher = services.GetRequiredService<IDataSubjectHasher>();

		await DestroyAndRecordAsync(services, SubjectKeyHandle.ForSubject(SingleTenant, subjectId, hasher).Value);
	}

	/// <summary>
	/// Destroys a key handle and records the destruction, in the order the ledger requires.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The generation is read BEFORE the destruction, and that order is not stylistic.</b> The generation is
	/// backend material, so the destroy takes it with the key: read it afterwards and there is nothing left to
	/// read, which would leave the subject permanently undecryptable with no repair available -- the material
	/// gone and the identifier that would have recorded it unreadable.
	/// </para>
	/// <para>
	/// Recorded only on <see cref="KeyDestructionState.Completed"/>, because that is the only state in which the
	/// material is irrecoverable NOW. A scheduled destruction can be cancelled on some backends, and a record
	/// written for one would tombstone a key that is still restorable.
	/// </para>
	/// <para>
	/// These arms destroy directly rather than through the erasure service, so this is where its recording step
	/// has to happen instead. Without it the destruction is real and no record exists, so a read of the erased
	/// subject fails loudly rather than reporting the erasure -- which is the designed refusal, and would make
	/// every degrade-open assertion below fail for a reason that is not what the arm is about.
	/// </para>
	/// </remarks>
	private static async Task DestroyAndRecordAsync(IServiceProvider services, string handle)
	{
		var keyProvider = services.GetRequiredService<IKeyManagementProvider>();
		var keyAdmin = services.GetRequiredService<IKeyManagementAdmin>();
		var ledger = services.GetRequiredService<IKeyDestructionLedger>();
		var cancellationToken = TestContext.Current.CancellationToken;

		var generation =
			(await keyProvider.GetKeyAsync(handle, cancellationToken))?.Generation;

		var outcome = await keyAdmin.DeleteKeyAsync(handle, retentionDays: 0, cancellationToken);

		if (outcome.State == KeyDestructionState.Completed && generation is not null)
		{
			await ledger.RecordDestroyedGenerationAsync(handle, generation.Value.ToString(), cancellationToken);
		}
	}

	// registerTenantContextFirst stands in for any unrelated package whose registration helper supplies the
	// framework tenant context before the compliance registration runs -- the state a consumer flips without
	// touching their own code.
	private static ServiceProvider BuildStack(
		ErasureRetention? retention,
		bool registerTenantContextFirst = false)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (registerTenantContextFirst)
		{
			_ = services.AddDefaultTenantContext();
		}

		_ = services.AddDataSubjectHashing();
		_ = services.Configure<DataSubjectHashingOptions>(options =>
			options.Pepper = "test-pepper-0123456789abcdef0123456789ab");

		_ = services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		_ = services.AddSingleton<IKeyManagementAdmin>(sp =>
			(IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

		if (retention is not null)
		{
			_ = services.AddErasureRetention(retention);
		}

		_ = services.AddCryptoShredding();

		// The read path resolves a destruction ledger, and a tombstone is produced only from a record in it.
		// These arms destroy keys DIRECTLY through IKeyManagementAdmin rather than through the erasure service,
		// so nothing records the destruction for them -- see DestroyAndRecordAsync, which does what the service
		// does at that seam.
		_ = services.AddInMemoryErasureStore();

		return services.BuildServiceProvider();
	}

	// The encryption registry populates on first resolution of IEncryptionProvider and selects its primary
	// only once every registered IHostedService has started, which a real host does for us.
	private static async Task StartAsync(IServiceProvider provider)
	{
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(TestContext.Current.CancellationToken);
		}
	}

}
