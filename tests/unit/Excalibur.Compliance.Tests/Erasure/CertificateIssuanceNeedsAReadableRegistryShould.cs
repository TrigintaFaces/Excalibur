// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds what the data-location registry has to say before a completion certificate may be issued, and
/// binds the two ways it can say nothing APART from one another.
/// </summary>
/// <remarks>
/// <para>
/// <b>Empty and unreadable are different facts and only one of them is a misconfiguration.</b> A registry
/// that holds no rows is a legitimate clean install: registrations are runtime data, written through
/// <c>RegisterDataLocationAsync</c>, so a host that has never registered anything is in a normal state and
/// may not be refused at startup — it could not start in order to run the registration that would let it
/// start. It is refused at ISSUANCE instead, where a first boot is trivially distinguishable because a
/// first boot has not requested an erasure.
/// </para>
/// <para>
/// A registry that cannot be READ is never a legitimate first boot, and reporting "nothing is registered"
/// for "I could not tell" is the absence-of-evidence error the whole coverage gate exists to prevent. The
/// startup guard already binds that distinction. These arms bind it at the other end — the moment the
/// certificate is claimed — because that is the point at which the wrong answer becomes a compliance
/// attestation rather than a log line.
/// </para>
/// <para>
/// <b>The liveness arm is what makes the two safety arms mean anything.</b> "No certificate was issued" is
/// satisfied by a gate that refuses every erasure ever, which is a defect this subsystem has already
/// shipped once: a subject-scoped registry read that structurally could not match refused every erasure in
/// every configuration while looking like a working gate. The second arm registers one location, has a
/// contributor discharge it, and requires a certificate to come out.
/// </para>
/// <para>
/// Everything is resolved from a real container built by the production registration path. A
/// hand-constructed service with fakes supplying an inventory would not exercise the wiring that decides
/// whether the registry is consulted at all.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CertificateIssuanceNeedsAReadableRegistryShould
{
	private const string TestPepper = "test-pepper-0123456789abcdef0123456789ab";

	private static readonly DataLocationKey RegisteredPair = new("Customers", "Email");

	// SAFETY (empty registry). A discovery source is wired, nothing is registered, and the host did not
	// opt into key-destruction-only erasure. There is no obligation to check the erasure against, so a
	// certificate would attest to an absence of evidence. RED against a gate that drops the
	// nothing-is-registered arm.
	[Fact]
	public async Task Refuse_to_issue_a_certificate_when_nothing_is_registered()
	{
		await using var provider = BuildProvider();
		using var scope = provider.CreateScope();
		var service = scope.ServiceProvider.GetRequiredService<IErasureService>();

		var requestId = await ScheduleAsync(service).ConfigureAwait(false);
		var result = await scope.ServiceProvider.GetRequiredService<IErasureExecutor>()
			.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeFalse(
			"nothing is registered, so there is no coverage to verify this erasure against and it must not "
			+ "be reported complete");

		var status = await service.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldNotBe(ErasureRequestStatus.Completed);
		status.ErrorMessage.ShouldNotBeNull();
		status.ErrorMessage!.ShouldContain(
			"no data location is registered",
			customMessage: "the refusal must name the condition and its remedy, or the operator cannot act on it");

		// The refusal is only worth anything if it reaches the ARTIFACT. Certificate generation refuses a
		// request that is not Completed, so this is the assertion that binds "no certificate exists".
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => service.GenerateCertificateAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	// LIVENESS. One registration, discharged by a contributor that names the pair it erased. The gate must
	// let this through and a certificate must come out. Without this arm, refusing every erasure passes
	// both safety arms -- which is exactly the shape the withdrawn subject-scoped read had.
	[Fact]
	public async Task Issue_a_certificate_when_the_registered_location_was_discharged()
	{
		await using var provider = BuildProvider(withDischargingContributor: true);
		using var scope = provider.CreateScope();
		var service = scope.ServiceProvider.GetRequiredService<IErasureService>();

		await RegisterAsync(scope.ServiceProvider).ConfigureAwait(false);

		var requestId = await ScheduleAsync(service).ConfigureAwait(false);
		var result = await scope.ServiceProvider.GetRequiredService<IErasureExecutor>()
			.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue(
			$"the one registered location was reported erased by a contributor that named it, so the "
			+ $"coverage gate has its evidence and must not block completion. Error: {result.ErrorMessage}");

		var certificate = await service.GenerateCertificateAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);
		certificate.Payload.RequestId.ShouldBe(requestId);
	}

	// SAFETY (unreadable registry). The store answers once -- the request is accepted -- and then fails,
	// which is a backing store going away between the request and its execution. The refusal must surface
	// the READ failure. RED by wrapping the registry read in a catch that returns an empty list: the
	// erasure is still refused, but for the wrong stated reason, and the certificate's own gate would then
	// be reporting "nothing is registered" about a registry it never managed to look at.
	[Fact]
	public async Task Refuse_over_an_unreadable_registry_without_calling_it_empty()
	{
		var store = new RegistryThatStopsAnswering();
		await using var provider = BuildProvider(registryStore: store);
		using var scope = provider.CreateScope();
		var service = scope.ServiceProvider.GetRequiredService<IErasureService>();

		var requestId = await ScheduleAsync(service).ConfigureAwait(false);
		var result = await scope.ServiceProvider.GetRequiredService<IErasureExecutor>()
			.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		store.Reads.ShouldBeGreaterThan(
			1,
			"the arm is only about the execution-time read if execution actually performed one; a single "
			+ "read would mean the request never reached the registry and this asserts nothing");

		result.Success.ShouldBeFalse();

		var status = await service.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldNotBe(ErasureRequestStatus.Completed);
		status.ErrorMessage.ShouldNotBeNull();

		status.ErrorMessage!.ShouldContain(
			RegistryThatStopsAnswering.Failure,
			customMessage: "the underlying read failure must reach the operator; it is the only thing that "
			+ "tells them the registry is broken rather than merely unpopulated");

		status.ErrorMessage.ShouldNotContain(
			"no data location is registered",
			customMessage: "reporting an unreadable registry as an empty one states a fact the framework "
			+ "never established -- the absence-of-evidence error this gate exists to prevent");

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => service.GenerateCertificateAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	/// <summary>
	/// Builds a real provider from the production registration path, with a data-inventory discovery
	/// source wired and key-destruction-only erasure NOT opted into — the configuration in which the
	/// registry is the evidence.
	/// </summary>
	/// <param name="withDischargingContributor">Registers a contributor that names the registered pair.</param>
	/// <param name="registryStore">An explicit registry store; the in-memory one is used when omitted.</param>
	/// <returns>The built provider.</returns>
	private static ServiceProvider BuildProvider(
		bool withDischargingContributor = false,
		IDataInventoryStore? registryStore = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Registered BEFORE AddGdprErasure so it wins the TryAdd.
		if (registryStore is not null)
		{
			services.TryAddSingleton(registryStore);
		}

		// Pinned so the annotated-coverage arm contributes nothing: left to the production default it
		// scans this whole test assembly's own [PersonalData] fixtures, which is a property of which
		// fixtures share the assembly rather than of the code under test.
		services.TryAddSingleton(TestAnnotationSource.None);

		_ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = TestPepper);

		_ = services.AddGdprErasure(o =>
		{
			o.KeyShredOnlyErasure = false;
			o.Retention.SigningKey = new byte[32];
		});

		_ = services.AddNoLegalHolds();
		_ = services.AddInMemoryErasureStore();
		_ = services.AddDataInventoryService();
		_ = services.AddInMemoryDataInventoryStore();

		if (withDischargingContributor)
		{
			_ = services.AddSingleton<IErasureContributor, PairNamingContributor>();
		}

		return services.BuildServiceProvider();
	}

	private static async Task RegisterAsync(IServiceProvider provider) =>
		await provider.GetRequiredService<IDataInventoryService>().RegisterDataLocationAsync(
			new DataLocationRegistration
			{
				TableName = RegisteredPair.TableName,
				FieldName = RegisteredPair.FieldName,
				DataCategory = nameof(PersonalDataCategory.ContactInfo),
				DataSubjectIdColumn = "CustomerId",
				IdType = DataSubjectIdType.UserId,
				KeyIdColumn = "EncryptionKeyId",
			},
			CancellationToken.None).ConfigureAwait(false);

	private static async Task<Guid> ScheduleAsync(IErasureService service)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-registry-issuance",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		var scheduled = await service.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);
		scheduled.Status.ShouldBe(
			ErasureRequestStatus.Scheduled,
			"the request must schedule before execution — ExecuteAsync only runs a Scheduled request");

		return request.RequestId;
	}

	/// <summary>
	/// A contributor that reports the table-and-field pair it erased, which is what discharges a
	/// registered obligation. Reporting success without naming anything discharges nothing.
	/// </summary>
	private sealed class PairNamingContributor : IErasureContributor
	{
		public string Name => "test-pair-naming";

		// Declared so the contributor is a realistic one, and deliberately NOT what discharges anything:
		// a covered store kind says what this contributor is ABLE to reach, while the discharge below
		// says what it actually erased for this subject. Only the second is evidence.
		public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } =
			new HashSet<DataStoreKind> { DataStoreKind.Create("CustomerDatabase") };

		public Task<ErasureContributorResult> EraseAsync(
			ErasureContributorContext context,
			CancellationToken cancellationToken) =>
			Task.FromResult(ErasureContributorResult.Succeeded(1, [RegisteredPair]));
	}

	/// <summary>
	/// A registry store that answers the first read and fails every read after it — a backing store that
	/// goes away between the moment a request is accepted and the moment it executes. Every member that
	/// is not part of that scenario throws, because this fixture exists for one question and a member
	/// answering silently would let an arm pass for the wrong reason.
	/// </summary>
	private sealed class RegistryThatStopsAnswering : IDataInventoryStore, IDataInventoryQueryStore
	{
		internal const string Failure = "registry backing store is unreachable";

		private int _reads;

		internal int Reads => _reads;

		public Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
			CancellationToken cancellationToken) =>
			Interlocked.Increment(ref _reads) == 1
				? Task.FromResult<IReadOnlyList<DataLocationRegistration>>([])
				: throw new InvalidOperationException(Failure);

		// Nothing was ever discovered for this subject, which is true of the scenario and is NOT what the
		// arm turns on: the registry read above runs first and is what fails.
		public Task<IReadOnlyList<DataLocation>> GetDiscoveredLocationsAsync(
			string dataSubjectId,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<DataLocation>>([]);

		public Task SaveRegistrationAsync(
			DataLocationRegistration registration,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("fixture: registry writes are out of scope for this arm");

		public Task<bool> RemoveRegistrationAsync(
			string tableName,
			string fieldName,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("fixture: registry writes are out of scope for this arm");

		public Task RecordDiscoveredLocationAsync(
			DataLocation location,
			string dataSubjectId,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("fixture: discovery writes are out of scope for this arm");

		public Task<IReadOnlyList<DataLocationRegistration>> FindRegistrationsForDataSubjectAsync(
			string dataSubjectId,
			DataSubjectIdType idType,
			string? tenantId,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException(
				"fixture: the subject-scoped read is not the obligation set and nothing here may use it");

		public Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
			string? tenantId,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("fixture: RoPA reporting is out of scope for this arm");

		// DataInventoryService derives its query store from the registry store rather than from DI, so
		// this fixture has to answer for both halves or it cannot be constructed at all.
		public object? GetService(Type serviceType) =>
			serviceType == typeof(IDataInventoryQueryStore) ? this : null;
	}
}
