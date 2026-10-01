// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The erasure service assembled by the PRODUCTION registration, rather than by a constructor call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every other arm for this capability hand-constructs the service and passes it a registry, and that
/// is exactly what let the defect ship.</b> The registration omitted the retention registry for its whole
/// life. The constructor accepted the omission because the parameter had a null default, so the service
/// was blind to every declared retention in production while every unit arm — each supplying the registry
/// itself — stayed green.
/// </para>
/// <para>
/// It hid because the CONTRIBUTOR does get the registry, from a different registration. A declared type
/// was therefore spared the tombstone and the record survived, which is what a reader checks first. Only
/// the SERVICE was blind: the spare-set was empty, so the key the record needs was destroyed anyway and
/// every retention entry reached the signed certificate with no handle to release it. The surviving record
/// was unreadable and the certificate said it was kept.
/// </para>
/// <para>
/// So these arms resolve the service from a real container built the way a consumer builds one. A unit arm
/// cannot see a wiring defect, however carefully it is written.
/// </para>
/// </remarks>
public class TheRegisteredErasureServiceSeesDeclaredRetentionsShould
{
	private const string RetainedType = "SalesRecord";
	private const string Subject = "subject-42";

	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.";

	/// <summary>
	/// SAFETY, and it is a WIRING claim rather than a behavioural one: the service the container hands a
	/// consumer can see the retentions that consumer declared.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The handle is the observable. A retention entry carrying one says the erasure decided about that key
	/// and spared it; an entry with none says nothing decided about it, which is the state the registration
	/// produced for every deployment.
	/// </para>
	/// <para>
	/// RED input: resolve the registry optionally in the registration again, or restore the constructor's
	/// null default and drop the argument. The record still survives the tombstone, every other arm in this
	/// suite stays green, and this one fails on a missing handle.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Name_the_retained_handle_on_a_certificate_from_a_container_built_the_consumer_way()
	{
		await using var provider = BuildConsumerStack();
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var sp = scope.ServiceProvider;

		var encryptor = sp.GetRequiredService<IFieldEncryptor>();
		var hasher = sp.GetRequiredService<IDataSubjectHasher>();
		var store = sp.GetRequiredService<InMemoryErasureStore>();

		// The subject holds data outside the retained type as well, so the erasure has a key of its own to
		// destroy. Without it nothing is erased, no certificate is owed, and the arm would pass on silence.
		_ = await encryptor.EncryptAsync(
			Subject,
			RetentionScope.NotInAnAggregate,
			"the customer's own record"u8.ToArray(),
			TestContext.Current.CancellationToken);

		var retainedEnvelope = await encryptor.EncryptAsync(
			Subject,
			RetentionScope.For(RetainedType),
			"the buyer's identity on the sales record"u8.ToArray(),
			TestContext.Current.CancellationToken);

		retainedEnvelope.KeyId.ShouldNotBeNullOrEmpty(
			"the arm is vacuous unless the write path produced a widened handle to be spared");

		var requestId = Guid.NewGuid();
		await store.SaveRequestAsync(
			new ErasureRequest
			{
				RequestId = requestId,
				DataSubjectId = Subject,
				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				RequestedBy = "admin",
				RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
				TenantId = null,
			},
			DateTimeOffset.UtcNow,
			TestContext.Current.CancellationToken);

		_ = await sp.GetRequiredService<IErasureExecutor>()
			.ExecuteAsync(requestId, TestContext.Current.CancellationToken);

		var certificate = await store.GetCertificateAsync(requestId, TestContext.Current.CancellationToken);
		certificate.ShouldNotBeNull("the execution path is where the record is written");

		var entry = certificate!.Payload.Exceptions.ShouldHaveSingleItem();
		entry.DataCategory.ShouldBe(RetainedType);
		entry.RetainedKeyHandle.ShouldBe(
			retainedEnvelope.KeyId,
			"the entry must name the SAME handle the write path used, or a consumer cannot release the "
			+ "retention when the period ends. A null here is the registration never having supplied the "
			+ "registry, which is invisible from the surviving record alone");

		(await encryptor.DecryptAsync(retainedEnvelope, TestContext.Current.CancellationToken))
			.ShouldNotBeNull("and the retained record has to still be readable, which is the whole point");
	}

	/// <summary>
	/// LIVENESS for the same wiring: the erasure the container assembles still destroys what it was
	/// entitled to. Without this the arm above is satisfied by a service that erases nothing.
	/// </summary>
	[Fact]
	public async Task Still_destroy_the_subjects_own_handle_in_that_same_container()
	{
		await using var provider = BuildConsumerStack();
		await StartAsync(provider);

		using var scope = provider.CreateScope();
		var sp = scope.ServiceProvider;

		var encryptor = sp.GetRequiredService<IFieldEncryptor>();
		var store = sp.GetRequiredService<InMemoryErasureStore>();

		var unretained = await encryptor.EncryptAsync(
			Subject,
			RetentionScope.NotInAnAggregate,
			"the customer's own record"u8.ToArray(),
			TestContext.Current.CancellationToken);

		var requestId = Guid.NewGuid();
		await store.SaveRequestAsync(
			new ErasureRequest
			{
				RequestId = requestId,
				DataSubjectId = Subject,
				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				RequestedBy = "admin",
				RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
				TenantId = null,
			},
			DateTimeOffset.UtcNow,
			TestContext.Current.CancellationToken);

		_ = await sp.GetRequiredService<IErasureExecutor>()
			.ExecuteAsync(requestId, TestContext.Current.CancellationToken);

		(await encryptor.DecryptAsync(unretained, TestContext.Current.CancellationToken))
			.ShouldBeNull("nothing protects this value, so this erasure must have destroyed it");
	}

	// Built the way a consumer builds it: the shipped registration helpers, in the order their own
	// documentation puts them, with nothing hand-constructed. That is the whole point of this file -- a
	// stack assembled by hand cannot tell you whether the registration wires what it claims to.
	private static ServiceProvider BuildConsumerStack()
	{
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
			TenantId = TenantScope.UntenantedSentinel,
			Basis = LegalHoldBasis.LegalObligation,
			Justification = Justification,
			RetentionPeriod = TimeSpan.FromDays(365 * 6),
		});

		_ = services.AddCryptoShredding();
		_ = services.AddInMemoryErasureStore();
		_ = services.AddNoLegalHolds();
		_ = services.AddGdprErasure(options =>
		{
			options.KeyShredOnlyErasure = true;
			options.Retention.SigningKey = new byte[32];
		});

		_ = services.AddSingleton<IErasureContributor>(new RetainedContributor());

		return services.BuildServiceProvider();
	}

	private static async ValueTask StartAsync(ServiceProvider provider)
	{
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(TestContext.Current.CancellationToken);
		}
	}

	// Stands in for the event-store contributor: it names the type it retained, exactly as the real one
	// does. The join between that name and the handle is the service's job, and it is what these arms bind.
	private sealed class RetainedContributor : IErasureContributor
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
						DataCategory = RetainedType,
						Reason = Justification,
						RetentionPeriod = TimeSpan.FromDays(365 * 6),
					},
				]));
	}
}
