// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Globalization;
using System.Text;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IErasureStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your erasure store implementation conforms to the IErasureStore contract.
/// </para>
/// <para>
/// The test kit verifies core erasure store operations including request lifecycle,
/// status updates, completion, cancellation state machine, scheduled queries, list queries,
/// certificate management, and certificate cleanup.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IErasureStore implements GDPR Article 17 "Right to Erasure"
/// (Right to be Forgotten) with:
/// <list type="bullet">
/// <item><description>Grace period scheduling before deletion</description></item>
/// <item><description><c>SaveRequestAsync</c> THROWS <c>DuplicateErasureRequestException</c> on a duplicate
/// RequestId — and raises that type for no other condition, so a caller can read it as "already on file"
/// without risking a lost erasure request</description></item>
/// <item><description>STATE MACHINE: <c>RecordCancellationAsync</c> only allows Pending/Scheduled to cancel</description></item>
/// <item><description><c>RecordCompletionAsync</c> THROWS KeyNotFoundException if request not found</description></item>
/// <item><description>Automatic DataSubjectId hashing (SHA256) for privacy</description></item>
/// <item><description>Erasure certificates, as compliance records, with retention periods</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The kit resolves the store from a container built by the store's own registration
/// // extension, so every arm runs against the object a consumer actually gets -- including
/// // the ambient ITenantContext the extension registers. Constructing the store by hand
/// // certifies an instance you assembled rather than the one your registration produces.
/// public class SqlServerErasureStoreConformanceTests : ErasureStoreConformanceTestKit
/// {
///     private readonly ServiceProvider _provider;
/// 
///     public SqlServerErasureStoreConformanceTests(SqlServerFixture fixture) =&gt;
///         _provider = new ServiceCollection()
///             .AddLogging()
///             .AddSqlServerErasureStore(o =&gt;
///             {
///                 o.ConnectionString = fixture.ConnectionString;
///                 o.AutoCreateSchema = true;
///             })
///             .BuildServiceProvider();
/// 
///     protected override IErasureStore CreateStore() =&gt;
///         _provider.GetRequiredService&lt;IErasureStore&gt;();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class ErasureStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh erasure store instance for testing.
	/// </summary>
	/// <returns>An IErasureStore implementation to test.</returns>
	protected abstract IErasureStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Clears residual data before an arm runs. Defaults to <see cref="CleanupAsync"/>.
	/// </summary>
	/// <returns>A task that completes when the store holds no data from a previous arm.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half — otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates the store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm in this kit obtains its store here rather than from <see cref="CreateStore"/> directly.
	/// That is the only thing that causes <see cref="CleanupAsync"/> to run: a cleanup a deriver overrides
	/// but the kit never calls is indistinguishable, from the deriver's side, from one that works.
	/// </remarks>
	protected async Task<IErasureStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Establishes <paramref name="tenantId"/> as the owner that saves made inside the returned scope will
	/// record, or returns <see langword="null"/> when this suite's store cannot resolve a named tenant.
	/// </summary>
	/// <param name="tenantId">The tenant that saves inside the scope must record as their owner.</param>
	/// <returns>
	/// A scope that restores the previous owner on disposal, or <see langword="null"/> when the store under
	/// test resolves no named tenant and the cross-owner arms therefore cannot run.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>A store's owner term is not supplied by the caller, so the kit cannot name it on a request.</b>
	/// The owner an erasure records is resolved by the HOST, and a request naming a different tenant is
	/// refused — which is what makes the per-subject key the erasure destroys the same key the write path
	/// minted. So an arm that needs two rows under two owners cannot get them by setting a property; it
	/// needs the deployment's own resolution to answer differently for each save, which is what this scope
	/// is for.
	/// </para>
	/// <para>
	/// Override it when your store resolves its tenant from ambient context — the shipped stores accept an
	/// <c>ITenantContext</c>, and the framework's ambient holder exposes a scoped setter for exactly this
	/// purpose, so the override is one line. The default returns <see langword="null"/>, and the arms that
	/// need it then report through <see cref="ConformanceTestKit.OnArmSkipped"/> rather than passing
	/// silently: an arm that did not run and an arm that ran clean must not be the same result.
	/// </para>
	/// <para>
	/// <b>Returning a scope that does NOT change the recorded owner is worse than returning
	/// <see langword="null"/>.</b> The arms assert that two saves recorded two different owners and fail
	/// the suite when they did not, so a no-op scope is reported as a defect in the store rather than
	/// quietly weakening the check — but the message will name the wrong cause. If your store cannot
	/// resolve a named tenant, leave this unoverridden.
	/// </para>
	/// </remarks>
	protected virtual IDisposable? BeginOwnerScope(string tenantId) => null;

	/// <summary>
	/// Creates the same store this suite tests, but configured so a tenant is REQUIRED, or returns
	/// <see langword="null"/> when the suite does not supply one.
	/// </summary>
	/// <returns>A tenancy-requiring store, or <see langword="null"/>.</returns>
	/// <remarks>
	/// <para>
	/// <b>One arm needs this and it cannot be faked by a scope.</b> Estate-wide background discovery must
	/// carry no ambient-tenant term, and the realistic way that defect arrives is an author copying the
	/// nearest sibling statement -- which applies the term ONLY when tenancy is required. Under optional
	/// tenancy that copied term compiles to nothing, so an arm running against the ordinary store cannot
	/// tell the defect from the fix. Measured: with the suite's default store, planting the term exactly
	/// as the sibling writes it left the arm GREEN on both SQL providers.
	/// </para>
	/// <para>
	/// A suite that returns <see langword="null"/> gets an honest skip rather than a silent pass, because
	/// the arm would be unable to fail.
	/// </para>
	/// </remarks>
	protected virtual IErasureStore? CreateTenantRequiringStore() => null;

	/// <summary>
	/// Creates a test erasure request with the given parameters.
	/// </summary>
	/// <param name="requestId">Optional request identifier. If not provided, a new GUID is generated.</param>
	/// <param name="dataSubjectId">Optional data subject identifier.</param>
	/// <param name="tenantId">Optional tenant identifier for multi-tenant isolation.</param>
	/// <returns>A test erasure request.</returns>
	protected virtual ErasureRequest CreateErasureRequest(
		Guid? requestId = null,
		string? dataSubjectId = null,
		string? tenantId = null) =>
		new()
		{
			RequestId = requestId ?? GenerateRequestId(),
			DataSubjectId = dataSubjectId ?? $"user-{Guid.NewGuid():N}",
			IdType = DataSubjectIdType.UserId,
			TenantId = tenantId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test-admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

	/// <summary>
	/// Creates a test erasure certificate with the given parameters.
	/// </summary>
	/// <param name="certificateId">Optional certificate identifier. If not provided, a new GUID is generated.</param>
	/// <param name="requestId">Optional request identifier. If not provided, a new GUID is generated.</param>
	/// <param name="retainUntil">Optional retention end date. Default is 7 years from now.</param>
	/// <returns>A test erasure certificate.</returns>
	protected virtual ErasureCertificate CreateErasureCertificate(
		Guid? certificateId = null,
		Guid? requestId = null,
		DateTimeOffset? retainUntil = null) =>
		new()
		{
			Payload = new()
			{
				CertificateId = certificateId ?? Guid.NewGuid(),
				RequestId = requestId ?? Guid.NewGuid(),
				DataSubjectReference = $"hash-{Guid.NewGuid():N}",
				RequestReceivedAt = DateTimeOffset.UtcNow.AddHours(-1),
				CompletedAt = DateTimeOffset.UtcNow,
				Method = ErasureMethod.CryptographicErasure,
				Summary =
				new ErasureSummary
				{
					KeysDeleted = 5,
					RecordsAffected = 100,
					DataCategories = ["personal", "contact"],
					TablesAffected = ["Users", "Contacts"],
					DataSizeBytes = 10240
				},
				Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog,
				VerifiedAt = DateTimeOffset.UtcNow,
				DeletedKeyIds = ["key-1", "key-2"]
			},
				LegalBasis = ErasureLegalBasis.DataSubjectRequest,
				RetainUntil = retainUntil ?? DateTimeOffset.UtcNow.AddYears(7)
			},
			Signature = $"sig-{Guid.NewGuid():N}"
		};

	/// <summary>
	/// The key the round-trip arms sign and verify with.
	/// </summary>
	/// <remarks>
	/// A fixed, obviously-synthetic value: it never leaves the test process, and the arms need both halves
	/// of the round trip to use the same key, which a generated one would make needlessly awkward to share.
	/// A fresh array on every read, so no arm can mutate the key another arm is about to use.
	/// </remarks>
	protected static byte[] ConformanceSigningKey =>
		Encoding.UTF8.GetBytes("erasure-store-conformance-signing-key-not-a-secret");

	/// <summary>
	/// Builds a certificate whose signature genuinely covers its payload, for the round-trip arms.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="CreateErasureCertificate"/> carries a placeholder signature, which is right for the arms
	/// that only care that a row was stored and read back. It cannot serve the arms that ask whether the
	/// payload survived the round trip, because a placeholder tells you nothing about the payload.
	/// </para>
	/// <para>
	/// Issued through the production boundary, so every derived provider suite exercises the claim validation
	/// as a side effect of asking for a signed certificate. A fixture whose claims do not hold is caught here
	/// rather than in whichever provider suite happens to look — and the payload this returns is the one that
	/// was signed, which is what the round-trip arms need.
	/// </para>
	/// </remarks>
	/// <returns>A certificate signed with <see cref="ConformanceSigningKey"/>.</returns>
	protected ErasureCertificate CreateSignedErasureCertificate() =>
		ErasureCertificateSigner.Issue(CreateErasureCertificate().Payload, ConformanceSigningKey, out _);

	/// <summary>
	/// Generates a unique request ID for test isolation.
	/// </summary>
	/// <returns>A unique request identifier.</returns>
	protected virtual Guid GenerateRequestId() => Guid.NewGuid();

	// A tenant no deployment under test is expected to resolve. The arms that use it assert it differs from
	// the owner the store actually recorded rather than trusting that, so a suite whose host does resolve
	// this term fails with a message saying so instead of asserting that a tenant does not match itself.
	private const string ForeignTenant = "conformance-tenant-the-host-does-not-resolve";

	// FirstOwner is a strict PREFIX of SecondOwner, and that is load-bearing rather than cosmetic: a filter
	// applied as a prefix or "starts with" match returns both rows when asked for the shorter term, which an
	// equality comparison does not. Two unrelated names would make that mutant invisible.
	private const string FirstOwner = "conformance-owner";
	private const string SecondOwner = "conformance-owner-second";

	// Carries a case distinction so a folding comparison has something to fold. The arm asserts the
	// distinction survived into the recorded owner before it relies on it.
	private const string MixedCaseOwner = "Conformance-Ordinal-Owner";

	/// <summary>
	/// The same three owners as OWNER IDENTITIES rather than as raw strings, for the mint-ledger arms.
	/// </summary>
	/// <remarks>
	/// The mint ledger takes a <see cref="TenantId"/> and not a string, deliberately: this subsystem carries
	/// an owner and a storage partition as two tenant-shaped values that are UNEQUAL in the default
	/// single-tenant deployment, and binding the partition where the owner belongs enumerates the empty set.
	/// The type is what makes that substitution fail to compile rather than fail silently at run time.
	/// </remarks>
	private static readonly TenantId FirstOwnerId = new(FirstOwner);

	/// <inheritdoc cref="FirstOwnerId"/>
	private static readonly TenantId SecondOwnerId = new(SecondOwner);

	/// <inheritdoc cref="FirstOwnerId"/>
	private static readonly TenantId MixedCaseOwnerId = new(MixedCaseOwner);

	/// <summary>Reads back the owner the store itself recorded for a request.</summary>
	/// <param name="store">The store under test.</param>
	/// <param name="requestId">The request whose recorded owner is wanted.</param>
	/// <returns>The owner term the store persisted, never null or empty.</returns>
	/// <remarks>
	/// The owner is READ rather than computed from a constant, and the difference is the whole reason these
	/// arms exist. A constant is a second derivation of a fact the store already decided; when the two
	/// disagree, the arm asserts against a term the store never used and reports a pass. Reading it back
	/// also means one arm covers a single-tenant host, a host with an explicit default identity, and a
	/// tenant-resolving host, without the kit having to know which it is looking at.
	/// </remarks>
	private static async Task<string> RecordedOwnerAsync(IErasureStore store, Guid requestId)
	{
		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		if (status is null)
		{
			throw new TestFixtureAssertionException(
				$"Request {requestId} was saved but is not readable, so the owner the store recorded for it "
				+ "cannot be established.");
		}

		if (string.IsNullOrWhiteSpace(status.TenantId))
		{
			throw new TestFixtureAssertionException(
				$"Request {requestId} was saved with no recorded owner. The owner decides which per-subject "
				+ "key an erasure destroys, so a request that carries none has no key to destroy and would "
				+ "report a completed erasure having destroyed nothing. Resolve an owner on the save path.");
		}

		return status.TenantId;
	}

	private static Task<IReadOnlyList<ErasureStatus>> ListByTenantAsync(IErasureStore store, string? tenantId) =>
		GetQueryStore(store).ListRequestsAsync(
			status: null,
			tenantId: tenantId,
			fromDate: null,
			toDate: null,
			pageNumber: 1,
			pageSize: 100,
			CancellationToken.None);

	private static IErasureQueryStore GetQueryStore(IErasureStore store) =>
		(IErasureQueryStore?)store.GetService(typeof(IErasureQueryStore))
		?? throw new TestFixtureAssertionException("Store does not implement IErasureQueryStore via GetService.");

	private static IErasureCertificateStore GetCertificateStore(IErasureStore store) =>
		(IErasureCertificateStore?)store.GetService(typeof(IErasureCertificateStore))
		?? throw new TestFixtureAssertionException("Store does not implement IErasureCertificateStore via GetService.");

	#region Request Lifecycle Tests

	/// <summary>
	/// Verifies that saving a new request persists it successfully.
	/// </summary>
	public virtual async Task SaveRequestAsync_ShouldPersistRequest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				$"Request with RequestId {request.RequestId} was not found after SaveRequestAsync");
		}

		if (status.RequestId != request.RequestId)
		{
			throw new TestFixtureAssertionException(
				$"RequestId mismatch. Expected: {request.RequestId}, Actual: {status.RequestId}");
		}

		if (status.Status != ErasureRequestStatus.Scheduled)
		{
			throw new TestFixtureAssertionException(
				$"New request should have status Scheduled. Actual: {status.Status}");
		}
	}

	/// <summary>
	/// Verifies that saving a request with a duplicate ID throws
	/// <see cref="DuplicateErasureRequestException"/> — and that a first save of a fresh ID still stores a
	/// retrievable request.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Both halves are required. The safety half alone ("a duplicate throws") is satisfied perfectly by a
	/// store that throws on every insert; the liveness half — a fresh request saves and is then visible —
	/// is what rules that store out.
	/// </para>
	/// <para>
	/// Neither half, and not both together, rules out the store that reports <i>every</i> failure as a
	/// duplicate. Liveness establishes only that some insert succeeds, which says nothing about how a
	/// failure is reported, and requiring the specific exception type does not help when the blanket catch
	/// throws that type. That store is the subject of
	/// <see cref="SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate"/>, which is a
	/// separate arm because it needs a failure that is not a duplicate in order to say anything at all.
	/// </para>
	/// </remarks>
	public virtual async Task SaveRequestAsync_DuplicateId_ShouldThrowDuplicateErasureRequestException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var requestId = GenerateRequestId();
		var request1 = CreateErasureRequest(requestId: requestId);
		var request2 = CreateErasureRequest(requestId: requestId);
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		// LIVENESS: the first save of a fresh id must succeed and be readable back. A store that refuses
		// every insert would otherwise pass the duplicate assertion below while storing nothing at all.
		await store.SaveRequestAsync(request1, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		var stored = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		if (stored is null)
		{
			throw new TestFixtureAssertionException(
				$"A first save of a fresh RequestId {requestId} must store a retrievable request. The "
				+ "duplicate assertion below is vacuous against a store that accepts nothing.");
		}

		// SAFETY: the second save of the same id must fail, and must say WHICH condition failed. A bare
		// InvalidOperationException is not enough: an unprovisioned schema, a disposed store and an
		// unresolved tenant all surface as that type, so a caller branching on it would read a request
		// that was never stored as one already on file, and never re-file it.
		try
		{
			await store.SaveRequestAsync(request2, scheduledTime, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected DuplicateErasureRequestException for duplicate RequestId but no exception was thrown");
		}
		catch (DuplicateErasureRequestException)
		{
			// Expected - SaveRequestAsync throws on duplicate, NOT upsert
		}
	}

	/// <summary>
	/// Verifies that SaveRequestAsync hashes the DataSubjectId.
	/// </summary>
	public virtual async Task SaveRequestAsync_ShouldHashDataSubjectId()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var rawDataSubjectId = "user@example.com";
		var request = CreateErasureRequest(dataSubjectId: rawDataSubjectId);
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after save");
		}

		// DataSubjectIdHash should NOT equal the raw DataSubjectId
		if (status.DataSubjectIdHash == rawDataSubjectId)
		{
			throw new TestFixtureAssertionException(
				"DataSubjectIdHash should be a hash, not the raw DataSubjectId");
		}

		// DataSubjectIdHash should be a hex string (SHA256 = 64 characters)
		if (string.IsNullOrEmpty(status.DataSubjectIdHash) || status.DataSubjectIdHash.Length != 64)
		{
			throw new TestFixtureAssertionException(
				$"DataSubjectIdHash should be a 64-character SHA256 hash. Actual length: {status.DataSubjectIdHash?.Length ?? 0}");
		}
	}

	/// <summary>
	/// Verifies that GetStatusAsync returns null for non-existent request.
	/// </summary>
	public virtual async Task GetStatusAsync_NonExistent_ShouldReturnNull()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();

		var status = await store.GetStatusAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (status is not null)
		{
			throw new TestFixtureAssertionException(
				"GetStatusAsync should return null for non-existent RequestId");
		}
	}

	#endregion

	#region Status Update Tests

	/// <summary>
	/// Verifies that the general status member applies a write presented by the holder of the claim.
	/// </summary>
	/// <remarks>
	/// The request is claimed first because the write is FENCED: a status write that advances the
	/// attestation is applied only for the identity the request currently carries. An arm that wrote
	/// without claiming would be measuring the refusal rather than the write.
	/// </remarks>
	public virtual async Task UpdateStatusAsync_ShouldUpdateStatus()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		var updated = await store.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.Failed,
			expectedStatus: null,
			"conformance",
			identity,
			CancellationToken.None).ConfigureAwait(false);

		if (updated != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				$"UpdateStatusAsync must apply a terminal write presented by the claim holder. Actual: {updated}");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after update");
		}

		if (status.Status != ErasureRequestStatus.Failed)
		{
			throw new TestFixtureAssertionException(
				$"Status should be Failed after update. Actual: {status.Status}");
		}
	}

	/// <summary>
	/// Verifies that taking execution custody stamps ExecutedAt.
	/// </summary>
	/// <remarks>
	/// ExecutedAt is the only record of WHEN an erasure began, and a store that leaves it null gives an
	/// operator no way to tell a request that started an hour ago from one that started last week -- so
	/// a stalled erasure becomes indistinguishable from a fresh one. It is stamped by the claim rather
	/// than by a status write because the claim is the moment execution begins.
	/// </remarks>
	public virtual async Task ClaimForExecutionAsync_ShouldSetExecutedAt()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		// Check initial state - ExecutedAt should be null
		var initialStatus = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		if (initialStatus?.ExecutedAt is not null)
		{
			throw new TestFixtureAssertionException(
				"ExecutedAt should be null before the request is claimed for execution");
		}

		_ = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after update");
		}

		if (status.ExecutedAt is null)
		{
			throw new TestFixtureAssertionException(
				"ExecutedAt should be stamped when execution custody is taken");
		}
	}

	/// <summary>
	/// Verifies that the general status member reports NOT FOUND for a request that does not exist.
	/// </summary>
	/// <remarks>
	/// A terminal status is written rather than the claimed one, deliberately: taking custody has its
	/// own member, so this arm would otherwise be refused for the wrong reason and would pass against a
	/// store that never looks the request up at all.
	/// </remarks>
	public virtual async Task UpdateStatusAsync_NonExistent_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();

		var updated = await store.UpdateStatusAsync(
			nonExistentId,
			ErasureRequestStatus.Failed,
			expectedStatus: null,
			"conformance",
			claimIdentity: null,
			CancellationToken.None).ConfigureAwait(false);

		if (updated == ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"UpdateStatusAsync should return false for non-existent RequestId");
		}
	}

	/// <summary>
	/// Verifies that custody is EXCLUSIVE: of two callers that both try to claim the same scheduled
	/// request, exactly one is granted it.
	/// </summary>
	/// <remarks>
	/// This is the SAFETY half. A store that grants the claim unconditionally passes every other arm in
	/// this region and fails here, because both callers are told they own the request — and each then
	/// erases the same subject and writes its own completion certificate, one of which attests to
	/// destroying keys that the other had already destroyed.
	/// <para>
	/// The arm also binds WHICH refusal the loser is given, because the two refusals oblige opposite
	/// behaviour of the caller and the wrong one strands the erasure. See the comments at the assertion.
	/// </para>
	/// </remarks>
	public virtual async Task ClaimForExecutionAsync_SecondClaimOfTheSameRequest_ShouldBeRefused()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var first = await store.ClaimForExecutionAsync(
			request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		// Asserted here rather than left to a sibling arm: without it, a store that refuses EVERY claim
		// passes the refusal below while being entirely inert.
		if (first.Outcome != ErasureClaimOutcome.Granted)
		{
			throw new TestFixtureAssertionException(
				$"The first claim of a Scheduled request must be granted; the store answered {first.Outcome}.");
		}

		var second = await store.ClaimForExecutionAsync(
			request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		if (second.Outcome == ErasureClaimOutcome.Granted)
		{
			throw new TestFixtureAssertionException(
				"The second claim of an already-claimed request must be refused. This store granted it, "
				+ "so two concurrent callers would both execute the erasure and both write a certificate.");
		}

		// THE REFUSAL MUST BE ALREADY-CLAIMED, NOT WRONG-STATUS, and the difference is not cosmetic: the
		// two oblige opposite behaviour of the caller. Already-claimed means another instance holds the
		// request and it is COMING BACK when that lease elapses, so the caller should keep offering it.
		// A status mismatch means the request has moved on and the caller should STOP. A store that
		// reports the mismatch -- which is tempting, because granting the claim advances the status --
		// tells the second contender to abandon a request that is still live. That strands the erasure,
		// which is the exact defect the lease exists to remove, reintroduced one level up.
		if (second.Outcome != ErasureClaimOutcome.AlreadyClaimed)
		{
			throw new TestFixtureAssertionException(
				$"The second claim must be refused as AlreadyClaimed; this store answered {second.Outcome}. "
				+ "A status mismatch tells the caller to stop offering a request that is still coming back.");
		}

		if (second.ClaimIdentity is not null)
		{
			throw new TestFixtureAssertionException(
				"A refused claim must carry no identity. This store handed one to a caller it refused, "
				+ "and the fence would then accept it as proof of custody.");
		}
	}

	/// <summary>
	/// Verifies that a request another caller has CLAIMED cannot be put back to
	/// <see cref="ErasureRequestStatus.Scheduled" />.
	/// </summary>
	/// <remarks>
	/// <para>
	/// THE ARM ABOVE GUARDS THE CLAIM. THIS ONE GUARDS THE CLAIM'S ENABLING STATE, and without it the
	/// pair is defeated from the other side. <c>Scheduled</c> is the single state the claim succeeds
	/// from, so a store that writes it unconditionally does not record an outcome -- it hands the claim
	/// to the next caller. The second claim is then refused by nobody, because it is not a second claim
	/// at all: it is a first claim of a request that was silently re-offered while its owner was still
	/// erasing.
	/// </para>
	/// <para>
	/// The consequence is the one the arm above already names, reached by a different route: two callers
	/// erase the same subject and each writes its own completion certificate, one of them attesting that
	/// keys were destroyed when its own pass found nothing left to destroy. A signed certificate is
	/// evidence a consumer hands an auditor, so the false one is an authenticated false statement.
	/// </para>
	/// <para>
	/// A READ-THEN-WRITE GUARD IN THE CALLER DOES NOT DISCHARGE THIS. The scheduler's retry path reads
	/// the current status and declines to reset a claimed request, which narrows the window and cannot
	/// close it: the claim may land between that read and the write. The refusal has to be the store's,
	/// in one indivisible operation, which is why this arm belongs here and not beside the caller.
	/// </para>
	/// <para>
	/// Its liveness twin is <see cref="UpdateStatusAsync_ResetToScheduledOfAFailedRequest_ShouldSucceed" />.
	/// Without that twin a store could pass this arm by refusing every reset, which would break the
	/// retry path completely and leave every transient failure terminal.
	/// </para>
	/// </remarks>
	public virtual async Task UpdateStatusAsync_ResetToScheduledOfAClaimedRequest_ShouldBeRefused()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		// THE HOLDER'S OWN IDENTITY IS PRESENTED, DELIBERATELY. Omitting it would also be refused, but by
		// the CLAIM FENCE rather than by the transition partition -- so the arm would pass against a store
		// whose partition permits the reset, which is the property it exists to bind. Presenting the
		// identity takes the fence out of the question and leaves only the partition to answer it.
		var reset = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.Scheduled, expectedStatus: null, "retry", identity, CancellationToken.None)
			.ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		// The status is checked as well as the return value, and deliberately: a store that returns false
		// and writes anyway is the worst of the three outcomes, because the caller is told it was refused.
		if (reset == ErasureWriteOutcome.Applied || status?.Status != ErasureRequestStatus.InProgress)
		{
			throw new TestFixtureAssertionException(
				"Putting a CLAIMED request back to Scheduled must be refused and must leave the status "
				+ $"InProgress. This store returned {reset} and left the status {status?.Status}. "
				+ "Scheduled is the state the claim succeeds from, so writing it over InProgress offers "
				+ "the request to a second caller while the first is still erasing the subject: both "
				+ "complete, and both sign a certificate.");
		}
	}

	/// <summary>
	/// Verifies that a CANCELLED request cannot be moved to
	/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction" />.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>AwaitingKeyDestruction</c> is the completion pass's DISCOVERY predicate, so writing it does not
	/// record an outcome -- it enrolls the request in a pass that asks the key-management provider to
	/// confirm that keys are irrecoverable, and that completes and CERTIFIES the request. Writing it over
	/// a cancelled request therefore feeds a cancelled erasure into the destruction pipeline and can
	/// produce a signed certificate for an erasure the consumer called off.
	/// </para>
	/// <para>
	/// The status's own documentation already says this cannot happen -- a request awaiting key
	/// destruction is not cancellable -- and nothing enforced the converse: that a cancelled request
	/// cannot come to be awaiting key destruction.
	/// </para>
	/// <para>
	/// Its liveness twin is
	/// <see cref="UpdateStatusAsync_AwaitingKeyDestructionFromTheClaimedWindow_ShouldSucceed" />. Both
	/// legitimate writers sit in the permitted source set: the execution path records this status from
	/// the claimed window, and the completion pass REWRITES it with a fresh detail message when it
	/// cannot yet complete -- so a store that permitted only <c>InProgress</c> would break that second
	/// writer, which is why the permitted set includes the target itself.
	/// </para>
	/// </remarks>
	public virtual async Task UpdateStatusAsync_ResetToAwaitingKeyDestructionOfACancelledRequest_ShouldBeRefused()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var cancelled = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.Cancelled, expectedStatus: null, "called off", claimIdentity: null, CancellationToken.None)
			.ConfigureAwait(false);

		// Asserted rather than assumed: a store that refused the cancellation would leave the request
		// Scheduled, where this transition is also refused, and the arm would pass while never reaching
		// the cancelled state it exists to test.
		if (cancelled != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"Cancelling a Scheduled request must succeed; the store refused it, so this arm never "
				+ "reached the cancelled state it exists to test.");
		}

		var enrolled = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, "awaiting provider", claimIdentity: null,
			CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		if (enrolled == ErasureWriteOutcome.Applied || status?.Status != ErasureRequestStatus.Cancelled)
		{
			throw new TestFixtureAssertionException(
				"Moving a CANCELLED request to AwaitingKeyDestruction must be refused and must leave the "
				+ $"status Cancelled. This store returned {enrolled} and left the status {status?.Status}. "
				+ "AwaitingKeyDestruction is the completion pass's discovery predicate, so the cancelled "
				+ "request would be enrolled in key destruction and could be certified as erased.");
		}
	}

	/// <summary>
	/// Verifies that a claimed request CAN be recorded as awaiting key destruction, and that the record
	/// can be rewritten while it waits.
	/// </summary>
	/// <remarks>
	/// The LIVENESS twin of
	/// <see cref="UpdateStatusAsync_ResetToAwaitingKeyDestructionOfACancelledRequest_ShouldBeRefused" />.
	/// A store that refused every write of this status would satisfy the safety arm and strand every
	/// executed erasure whose keys the provider has only scheduled -- it could never be recorded as
	/// waiting, never discovered by the completion pass, and never completed. The second write in this
	/// arm is the completion pass's own rewrite, which a "from InProgress only" rule would refuse.
	/// </remarks>
	public virtual async Task UpdateStatusAsync_AwaitingKeyDestructionFromTheClaimedWindow_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		var recorded = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, "awaiting provider", identity,
			CancellationToken.None).ConfigureAwait(false);

		if (recorded != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"A claimed request must be recordable as awaiting key destruction; this store refused it, "
				+ "so an executed erasure whose keys are only scheduled can never be completed.");
		}

		// The completion pass rewrites the detail while the provider still holds the keys.
		var rewritten = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, "still awaiting provider", claimIdentity: null,
			CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		if (rewritten != ErasureWriteOutcome.Applied || status?.Status != ErasureRequestStatus.AwaitingKeyDestruction)
		{
			throw new TestFixtureAssertionException(
				"A request already awaiting key destruction must accept a rewritten detail while it "
				+ $"waits. This store returned {rewritten} and left the status {status?.Status}, so the "
				+ "completion pass cannot report why a request is still waiting.");
		}
	}

	/// <summary>
	/// Verifies that a FAILED request can still be put back to
	/// <see cref="ErasureRequestStatus.Scheduled" /> for another attempt.
	/// </summary>
	/// <remarks>
	/// The LIVENESS twin of <see cref="UpdateStatusAsync_ResetToScheduledOfAClaimedRequest_ShouldBeRefused" />.
	/// Retrying a transient failure is the one legitimate reason to write <c>Scheduled</c> after
	/// creation, and it is how a request that failed on a store outage or a provider timeout is ever
	/// attempted again. A store that refuses every reset satisfies the safety arm completely while
	/// making every transient failure permanent, and nothing else in this kit would notice.
	/// </remarks>
	public virtual async Task UpdateStatusAsync_ResetToScheduledOfAFailedRequest_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);
		// ASSERTED, NOT ASSUMED. This is the arm's precondition: if the store refuses it the request is
		// still claimed rather than Failed, and the reset below is then a reset of the wrong state, which
		// a provider could pass for a reason this arm is not about.
		if (await store.UpdateStatusAsync(
				request.RequestId, ErasureRequestStatus.Failed, expectedStatus: null, "transient", identity, CancellationToken.None)
				.ConfigureAwait(false) != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"A terminal write by the claim holder must be applied; without it this arm never reaches "
				+ "the failed state whose reset it exists to test.");
		}

		var reset = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.Scheduled, expectedStatus: null, "retry 1/3", claimIdentity: null, CancellationToken.None)
			.ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		if (reset != ErasureWriteOutcome.Applied || status?.Status != ErasureRequestStatus.Scheduled)
		{
			throw new TestFixtureAssertionException(
				"A FAILED request must be resettable to Scheduled so it can be retried. This store "
				+ $"returned {reset} and left the status {status?.Status}, so a request that failed on a "
				+ "transient fault can never be attempted again.");
		}
	}

	/// <summary>
	/// Verifies that the claim guard refuses ONLY the claim: recording a terminal outcome on a request
	/// that is already InProgress still succeeds.
	/// </summary>
	/// <remarks>
	/// This is the LIVENESS half of the arm above. A store that refused every update would satisfy the
	/// refusal assertion while being unable to record any outcome at all, leaving every request stuck in
	/// InProgress with no way to report what happened to it.
	/// </remarks>
	public virtual async Task UpdateStatusAsync_TerminalTransitionAfterClaim_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		var recorded = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.Failed, expectedStatus: null, "erasure failed", identity, CancellationToken.None)
			.ConfigureAwait(false);

		if (recorded != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"A terminal transition from InProgress must succeed; the claim guard is over-refusing "
				+ "and no outcome can be recorded.");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		if (status?.Status != ErasureRequestStatus.Failed)
		{
			throw new TestFixtureAssertionException(
				$"Status should be Failed after the terminal transition. Actual: {status?.Status}");
		}
	}

	/// <summary>
	/// Verifies that a request recorded as awaiting key destruction round-trips, is found by a status-filtered
	/// listing, is NOT offered for execution again, and cannot be
	/// cancelled.
	/// </summary>
	/// <remarks>
	/// A store that dropped the status, mapped it to another value, returned it from the scheduled-execution query,
	/// or let it be cancelled would each break the erasure lifecycle differently: the request would be lost, be
	/// executed a second time against keys already scheduled for destruction, or be marked cancelled beside a key
	/// that is being destroyed anyway.
	/// </remarks>
	public virtual async Task AwaitingKeyDestruction_ShouldBeListable_NotRescheduled_AndNotCancellable()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		// Due now, so the scheduled-execution query WOULD return it if it only looked at the time.
		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None)
			.ConfigureAwait(false);
		var identity = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		var recorded = await store.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, "awaiting provider", identity, CancellationToken.None)
			.ConfigureAwait(false);
		if (recorded != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException("The transition to AwaitingKeyDestruction must succeed.");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		if (status?.Status != ErasureRequestStatus.AwaitingKeyDestruction)
		{
			throw new TestFixtureAssertionException(
				$"Status should round-trip as AwaitingKeyDestruction. Actual: {status?.Status}");
		}

		var queryStore = (IErasureQueryStore?)store.GetService(typeof(IErasureQueryStore))
			?? throw new TestFixtureAssertionException("The store must expose IErasureQueryStore.");

		var listed = await queryStore.ListRequestsAsync(
			ErasureRequestStatus.AwaitingKeyDestruction, null, null, null, 1, 100, CancellationToken.None)
			.ConfigureAwait(false);
		if (!listed.Any(r => r.RequestId == request.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Ordinary tenant-scoped listing must preserve the recorded awaiting status.");
		}

		var scheduled = await queryStore.GetScheduledRequestsAsync(100, CancellationToken.None).ConfigureAwait(false);
		if (scheduled.Any(r => r.RequestId == request.RequestId))
		{
			throw new TestFixtureAssertionException(
				"A request awaiting key destruction must not be returned for execution.");
		}

		var cancelled = await store.RecordCancellationAsync(
			request.RequestId, "operator", "operator", CancellationToken.None).ConfigureAwait(false);
		if (cancelled)
		{
			throw new TestFixtureAssertionException(
				"A request awaiting key destruction must not be cancellable: its keys are already being destroyed.");
		}
	}

	/// <summary>
	/// Verifies that estate-wide background discovery returns EVERY row of its own status, under every
	/// owner, while the caller holds no ambient tenant scope -- and that its page boundary is stable when
	/// requested times tie.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This one arm binds BOTH background discovery members</b>, by being parameterised over the member
	/// and run twice. The members are not one operation with a different constant -- each is a named
	/// discovery predicate owned by one background worker -- but what drifts between two hand-copied
	/// discovery statements is the paging, the ordering and the tenancy rather than the predicate. So the
	/// test is parameterised where the interface is not.
	/// </para>
	/// <para>
	/// <b>THE AMBIENT TERM IS WHAT THIS ARM EXISTS FOR, and its failure mode is the worst kind.</b> The
	/// nearest sibling statements in each provider DO apply an ambient-tenant match; these members must
	/// NOT. A provider author who copies the sibling gets the term, and under multi-tenancy the ambient
	/// scope then either throws or resolves to a tenant matching nothing -- so discovery returns an EMPTY
	/// SET, the background work is silently never done, and nothing anywhere reports it. An in-memory
	/// reference store cannot catch this, because its ambient match short-circuits to true under single
	/// tenancy, which is how most callers exercise it. Hence: two owners, and both rows asserted present
	/// under NO scope.
	/// </para>
	/// <para>
	/// <b>THE FIXTURE REACHES EACH STATUS THROUGH THE REAL TRANSITIONS.</b> Writing the status directly
	/// would succeed against a store whose conditional write is broken, so the fixture claims the request
	/// first and is therefore blind to nothing. The requested times are seeded EQUAL across the rows on
	/// purpose: without a tie, a dropped unique tie-breaker in the ORDER BY leaves the page boundary
	/// stable by accident and the paging half of this arm passes vacuously.
	/// </para>
	/// <para>
	/// <b>Mutants, RUN rather than recorded -- and two of the three first written here were FALSE.</b>
	/// RED: an ambient-tenant term added to either member, in both the unguarded form and the form the
	/// nearest sibling writes (which is why this arm requires tenancy -- under optional tenancy the
	/// sibling's guarded form compiles to nothing and the arm stayed GREEN on both SQL providers).
	/// RED: pointing either member at the other member's status.
	/// <b>NOT detected, stated rather than implied:</b> dropping the unique tie-breaker from the ORDER BY
	/// left this arm GREEN on real PostgreSQL and real SQL Server. A non-unique ordering PERMITS an
	/// unstable page boundary without requiring one, so no assertion can reliably redden on it. The
	/// paging assertions below are kept because they do catch a broken OFFSET or LIMIT, but they are NOT
	/// evidence that the tie-breaker is present -- reading the ORDER BY is.
	/// </para>
	/// </remarks>
	public virtual async Task BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped()
	{
		// Deliberately NOT CreateStoreForArmAsync: this arm needs tenancy REQUIRED, because the realistic
		// form of the defect it detects is inert under optional tenancy. ResetDataAsync is still called so
		// a deriver's cleanup runs for this arm as it does for every other.
		_ = await CreateStoreForArmAsync().ConfigureAwait(false);
		var store = CreateTenantRequiringStore();

		if (store is null)
		{
			SkipArm(
				nameof(BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped),
				capability: null,
				"the suite does not override CreateTenantRequiringStore, so this arm cannot run with tenancy "
				+ "required -- and under optional tenancy the ambient-tenant term it exists to detect "
				+ "compiles to nothing, so the arm could not fail. Estate-wide background discovery across "
				+ "owners is UNVERIFIED for this store.");
			return;
		}

		using (var probeScope = BeginOwnerScope(FirstOwner))
		{
			if (probeScope is null)
			{
				SkipArm(
					nameof(BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped),
					capability: null,
					"the suite does not override BeginOwnerScope, so this store resolves no named tenant and "
					+ "two rows under two owners cannot be created. Estate-wide background discovery across "
					+ "owners is UNVERIFIED for this store, and the ambient-term defect it detects would not "
					+ "be visible under a single tenant.");
				return;
			}
		}

		RecordArmExecuted(nameof(BackgroundDiscovery_AcrossOwners_ShouldReturnEveryRowOfItsOwnStatusUnscoped));

		var queryStore = (IErasureBackgroundQueryStore?)store.GetService(typeof(IErasureBackgroundQueryStore))
			?? throw new TestFixtureAssertionException(
				"The store must expose IErasureBackgroundQueryStore. Background workers run under no tenant "
				+ "scope, and ordinary tenant-scoped listing cannot substitute for estate-wide discovery.");

		await AssertBackgroundDiscoveryAsync(
			store,
			queryStore.GetAwaitingKeyDestructionRequestsAsync,
			ErasureRequestStatus.AwaitingKeyDestruction,
			ErasureRequestStatus.BlockedByLegalHold,
			nameof(IErasureBackgroundQueryStore.GetAwaitingKeyDestructionRequestsAsync)).ConfigureAwait(false);

		await AssertBackgroundDiscoveryAsync(
			store,
			queryStore.GetHoldDeferredRequestsAsync,
			ErasureRequestStatus.BlockedByLegalHold,
			ErasureRequestStatus.AwaitingKeyDestruction,
			nameof(IErasureBackgroundQueryStore.GetHoldDeferredRequestsAsync)).ConfigureAwait(false);
	}

	/// <summary>
	/// The body both background discovery members are asserted with.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <param name="discover">The member being bound, as a page reader.</param>
	/// <param name="discovered">The status this member must return.</param>
	/// <param name="excluded">A status it must NOT return, used as the cross-status control.</param>
	/// <param name="memberName">The member's name, for the failure messages.</param>
	private async Task AssertBackgroundDiscoveryAsync(
		IErasureStore store,
		Func<int, int, CancellationToken, Task<IReadOnlyList<ErasureStatus>>> discover,
		ErasureRequestStatus discovered,
		ErasureRequestStatus excluded,
		string memberName)
	{
		// EQUAL on purpose: a tie is what makes the unique tie-breaker in the ORDER BY load-bearing, so
		// dropping it reddens the page-boundary assertions below instead of passing by accident.
		var requestedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

		var owners = new[] { FirstOwner, SecondOwner };
		var ids = new Guid[owners.Length];
		var recordedOwners = new string[owners.Length];

		for (var i = 0; i < owners.Length; i++)
		{
			using var scope = BeginOwnerScope(owners[i])
				?? throw new TestFixtureAssertionException(
					"BeginOwnerScope returned a scope for one tenant and null for another. It must either "
					+ "support every tenant it is asked for or none, or the arms cannot tell an unsupported "
					+ "suite from a partially wired one.");

			var request = CreateErasureRequest() with { RequestedAt = requestedAt };
			await store.SaveRequestAsync(request, requestedAt, CancellationToken.None).ConfigureAwait(false);
			await MoveToAsync(store, request.RequestId, discovered).ConfigureAwait(false);

			ids[i] = request.RequestId;
			recordedOwners[i] = await RecordedOwnerAsync(store, request.RequestId).ConfigureAwait(false);
		}

		// The fixture's own precondition, asserted rather than assumed. With both owners equal this arm
		// would measure one tenant twice and the ambient-term defect would be invisible to it.
		if (string.Equals(recordedOwners[0], recordedOwners[1], StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Two saves made under two different owner scopes both recorded '{recordedOwners[0]}'. Until "
				+ "they differ this arm cannot detect an ambient-tenant term in " + memberName + ", which is "
				+ "the defect it exists to catch.");
		}

		// A row in the OTHER background status, so the arm fails against a member that ignores its own
		// predicate or is pointed at the sibling's status.
		Guid excludedId;
		using (var scope = BeginOwnerScope(FirstOwner))
		{
			_ = scope;
			var other = CreateErasureRequest() with { RequestedAt = requestedAt };
			await store.SaveRequestAsync(other, requestedAt, CancellationToken.None).ConfigureAwait(false);
			await MoveToAsync(store, other.RequestId, excluded).ConfigureAwait(false);
			excludedId = other.RequestId;
		}

		// EVERY read below happens with NO ambient scope established, which is how a background worker
		// actually calls these members.
		var all = await discover(1, 100, CancellationToken.None).ConfigureAwait(false);

		for (var i = 0; i < ids.Length; i++)
		{
			if (!all.Any(row => row.RequestId == ids[i]))
			{
				throw new TestFixtureAssertionException(
					$"{memberName} did not return the request owned by '{recordedOwners[i]}' while the caller "
					+ "held no ambient tenant scope. This member is estate-wide and must carry no "
					+ "ambient-tenant term: with one, discovery returns an empty set under multi-tenancy and "
					+ "the background work is silently never done.");
			}
		}

		if (all.Any(row => row.RequestId == excludedId))
		{
			throw new TestFixtureAssertionException(
				$"{memberName} returned a request in {excluded}, which is not its predicate. Each background "
				+ "member discovers for one worker; returning another status hands that worker rows it has no "
				+ "business acting on.");
		}

		// PAGE BOUNDARY, with the requested times tied. Two single-row pages must together cover both
		// owners' rows exactly once. A non-unique ORDER BY can repeat one row and omit the other while
		// each page looks perfectly well formed on its own.
		var firstPage = await discover(1, 1, CancellationToken.None).ConfigureAwait(false);
		var secondPage = await discover(2, 1, CancellationToken.None).ConfigureAwait(false);

		if (firstPage.Count != 1 || secondPage.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"{memberName} must return exactly one row per page when asked for a page size of one and at "
				+ $"least two rows match. Page 1 returned {firstPage.Count} and page 2 returned "
				+ $"{secondPage.Count}.");
		}

		if (firstPage[0].RequestId == secondPage[0].RequestId)
		{
			throw new TestFixtureAssertionException(
				$"{memberName} returned the SAME request on page 1 and page 2 while three rows had identical "
				+ "requested times. The ORDER BY needs a unique tie-breaker; without one a tie makes the page "
				+ "boundary arbitrary, so a row is served twice and another is never served at all -- and "
				+ "every individual page still looks well formed.");
		}
	}

	/// <summary>
	/// Takes execution custody of a freshly scheduled request and returns the identity the store minted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// CUSTODY HAS EXACTLY ONE DOOR. <see cref="IErasureStore.ClaimForExecutionAsync" /> is the only
	/// member that mints the identity a fenced write presents, so an arm needing a request in the
	/// claimed state claims it here. Writing that status through the general status member is refused
	/// outright, because a second door would advance a request into the claimed state for a caller
	/// holding no identity, and every fenced write it then made would compare against nothing.
	/// </para>
	/// <para>
	/// The identity is RETURNED rather than discarded because every later write that advances the
	/// attestation must present it. An arm that dropped it would be refused at its next write, and the
	/// failure would read as a broken provider rather than a fixture that lost its claim.
	/// </para>
	/// <para>
	/// The grant is asserted rather than assumed: a store that refuses every claim would otherwise
	/// reach the arm's real assertion with the request still scheduled, where most transitions
	/// legitimately behave differently, and the arm would pass while measuring nothing.
	/// </para>
	/// </remarks>
	private static async Task<string> ClaimForArmAsync(IErasureStore store, Guid requestId)
	{
		var claim = await store.ClaimForExecutionAsync(
			requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		if (claim.Outcome != ErasureClaimOutcome.Granted)
		{
			throw new TestFixtureAssertionException(
				$"The first claim of a Scheduled request must be granted; the store answered {claim.Outcome}. "
				+ "Without the claim the fixture cannot reach the claimed state the way production does.");
		}

		if (string.IsNullOrEmpty(claim.ClaimIdentity))
		{
			throw new TestFixtureAssertionException(
				"A granted claim must carry the identity it minted. This store granted custody and "
				+ "returned no identity, so no later write could ever present one and the fence would "
				+ "refuse every attestation the holder makes.");
		}

		return claim.ClaimIdentity;
	}

	/// <summary>
	/// Moves a freshly saved request into a background-discoverable status through the real transitions.
	/// </summary>
	/// <remarks>
	/// Custody is taken first rather than the status written directly. A direct write succeeds even
	/// against a store whose conditional write is broken -- so a fixture that took the shortcut would be
	/// blind to a provider that refuses the transition in production, and would still be green. It is
	/// also no longer expressible: the claimed status may only be written by the claim member.
	/// </remarks>
	private static async Task MoveToAsync(IErasureStore store, Guid requestId, ErasureRequestStatus status)
	{
		var identity = await ClaimForArmAsync(store, requestId).ConfigureAwait(false);

		if (ErasureWriteOutcome.Applied != await store.UpdateStatusAsync(requestId, status, expectedStatus: null, "conformance", identity, CancellationToken.None)
			.ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				$"Recording {status} on request {requestId} from the claimed window must succeed. That is the "
				+ "only path production reaches it by.");
		}
	}

	#endregion

	#region Completion Tests

	/// <summary>
	/// REFUSE A COMPLETION PRESENTING A SUPERSEDED CLAIM, and apply the current holder's.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the fence itself, and it is the arm a provider is most likely to pass by accident. A
	/// store that ignores the identity entirely satisfies every other arm in this region: the happy path
	/// still completes, the claim is still exclusive, and the status still moves. Only a SECOND claim
	/// existing makes the two implementations diverge, and then the divergence is a signed full-coverage
	/// certificate sitting beside a partial status -- the artifact a regulated consumer hands an auditor.
	/// </para>
	/// <para>
	/// BOTH HALVES ARE ASSERTED. A store that refused EVERY completion would pass the refusal and leave
	/// every erasure permanently unattested, so the current holder's completion must also apply.
	/// </para>
	/// <para>
	/// The fixture state that discriminates is a claim duration of ZERO, which the contract admits for
	/// exactly this purpose: it yields a claim that has already lapsed, so the request is re-claimable
	/// without waiting. A fresh request per arm only ever produces "nothing is claimed", and in that
	/// state the fenced and unfenced implementations agree.
	/// </para>
	/// </remarks>
	public virtual async Task RecordCompletionAsync_PresentingASupersededClaim_ShouldBeRefused()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var superseded = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false);

		if (superseded.Outcome != ErasureClaimOutcome.Granted || string.IsNullOrEmpty(superseded.ClaimIdentity))
		{
			throw new TestFixtureAssertionException(
				$"The first claim must be granted with an identity; the store answered {superseded.Outcome}. "
				+ "A zero claim duration is legal and yields an already-lapsed claim.");
		}

		var holder = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.InProgress, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		if (holder.Outcome != ErasureClaimOutcome.Granted || string.IsNullOrEmpty(holder.ClaimIdentity))
		{
			throw new TestFixtureAssertionException(
				"A request whose claim has LAPSED must be re-claimable; the store answered "
				+ $"{holder.Outcome}. Without this the arm never reaches the superseded state it tests.");
		}

		if (string.Equals(superseded.ClaimIdentity, holder.ClaimIdentity, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A re-claim must mint a FRESH identity. This store reused the first one, which leaves the "
				+ "fence structurally present and semantically inert: a superseded writer still compares equal.");
		}

		var refused = await store.RecordCompletionAsync(
				request.RequestId, keysDeleted: 1, recordsAffected: 1, Guid.NewGuid(),
				superseded.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		if (refused == ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"A completion presenting a SUPERSEDED claim must not be applied. This store applied it, so "
				+ "two executors can each sign a certificate over coverage neither established.");
		}

		var applied = await store.RecordCompletionAsync(
				request.RequestId, keysDeleted: 7, recordsAffected: 70, Guid.NewGuid(),
				holder.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		if (applied != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				$"The CURRENT holder's completion must be applied; the store answered {applied}. A store "
				+ "that refuses every completion passes the refusal above and attests nothing, ever.");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status?.Status != ErasureRequestStatus.Completed || status.KeysDeleted != 7)
		{
			throw new TestFixtureAssertionException(
				"After the holder's completion the request must be Completed and carry the HOLDER'S counts. "
				+ $"Actual status {status?.Status}, keysDeleted {status?.KeysDeleted} (expected 7). A store "
				+ "recording the superseded writer's counts has applied the write it reported as refused.");
		}
	}

	/// <summary>
	/// APPLY A COMPLETION FROM A HOLDER WHOSE LEASE ELAPSED BUT WHOM NOBODY SUPERSEDED.
	/// </summary>
	/// <remarks>
	/// A LAPSE DOES NOT REVOKE; only a re-claim does. This is the arm that stops a provider from
	/// implementing the fence as an expiry check, which is the obvious reading and the dangerous one: an
	/// executor that had already destroyed a subject's keys would be refused its own completion, leaving
	/// the erasure performed and no certificate able to attest it. The keys cannot come back, so the
	/// request becomes permanently uncertifiable.
	/// </remarks>
	public virtual async Task RecordCompletionAsync_FromALapsedButUnsupersededHolder_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var claim = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false);

		if (claim.Outcome != ErasureClaimOutcome.Granted || string.IsNullOrEmpty(claim.ClaimIdentity))
		{
			throw new TestFixtureAssertionException(
				$"The claim must be granted with an identity; the store answered {claim.Outcome}.");
		}

		var applied = await store.RecordCompletionAsync(
				request.RequestId, keysDeleted: 3, recordsAffected: 30, Guid.NewGuid(),
				claim.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		if (applied != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"A holder whose lease elapsed, but whom nobody superseded, must still be able to complete. "
				+ $"The store answered {applied}, so it is comparing the EXPIRY rather than the IDENTITY -- "
				+ "which refuses an executor that has already destroyed the subject's keys.");
		}
	}

	/// <summary>
	/// ADMIT A LEGAL HOLD REGARDLESS OF CUSTODY, and release custody when it lands.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hold is a superior authority and the one write that is deliberately OUTSIDE the fence. Refusing
	/// it would drop it silently and let the run complete over data the controller has asserted a legal
	/// basis to retain. The asymmetry is this subsystem's own: withholding an erasure is recoverable by
	/// releasing the hold, and destroying held records is not.
	/// </para>
	/// <para>
	/// The worst a stale actor can do through this door is DENY an erasure another instance owns, which
	/// is visible in the status and reversible.
	/// </para>
	/// </remarks>
	public virtual async Task UpdateStatusAsync_ALegalHoldUnderALiveClaim_ShouldBeAdmitted()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var claim = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		if (claim.Outcome != ErasureClaimOutcome.Granted)
		{
			throw new TestFixtureAssertionException(
				$"The claim must be granted; the store answered {claim.Outcome}. Without a LIVE claim this "
				+ "arm does not reach the state it exists to test.");
		}

		// NO identity is presented, which is the stricter argument everywhere else: it asserts that
		// nobody holds the request. Here it must be admitted anyway.
		var held = await store.UpdateStatusAsync(
				request.RequestId, ErasureRequestStatus.BlockedByLegalHold, expectedStatus: null,
				"Legal hold active", claimIdentity: null, CancellationToken.None)
			.ConfigureAwait(false);

		if (held != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				$"A legal hold must be admitted regardless of custody; the store answered {held}. A fence "
				+ "here drops the hold silently and the run completes over retained data.");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status?.Status != ErasureRequestStatus.BlockedByLegalHold)
		{
			throw new TestFixtureAssertionException(
				$"The request must be blocked after the hold write. Actual: {status?.Status}.");
		}

		// Custody is released by the write, so the request is claimable again once the hold is lifted.
		// Without this the hold would strand the request for a whole claim duration.
		var afterHold = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.BlockedByLegalHold, TimeSpan.FromMinutes(5),
				CancellationToken.None)
			.ConfigureAwait(false);

		if (afterHold.Outcome == ErasureClaimOutcome.AlreadyClaimed)
		{
			throw new TestFixtureAssertionException(
				"The hold write must RELEASE custody. This store still reports the request as claimed, so "
				+ "the executor's lease outlives a hold it did not write and nothing can act until it lapses.");
		}
	}

	/// <summary>
	/// Verifies that RecordCompletionAsync marks request as completed.
	/// </summary>
	public virtual async Task RecordCompletionAsync_ShouldMarkCompleted()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);
		var certificateId = Guid.NewGuid();

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		// A completion is a FENCED write: the run takes custody first and presents the identity the store
		// granted it. Nothing else can obtain one, which is what makes the fence a property of the design
		// rather than a convention every provider has to remember.
		var claim = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		if (claim.Outcome != ErasureClaimOutcome.Granted)
		{
			throw new TestFixtureAssertionException(
				"ClaimForExecutionAsync must grant a freshly scheduled request; it answered "
				+ claim.Outcome);
		}

		await store.RecordCompletionAsync(
			request.RequestId,
			keysDeleted: 10,
			recordsAffected: 500,
			certificateId,
			claim.ClaimIdentity!,
			CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after completion");
		}

		if (status.Status != ErasureRequestStatus.Completed)
		{
			throw new TestFixtureAssertionException(
				$"Status should be Completed after RecordCompletionAsync. Actual: {status.Status}");
		}

		if (status.KeysDeleted != 10)
		{
			throw new TestFixtureAssertionException(
				$"KeysDeleted should be 10. Actual: {status.KeysDeleted}");
		}

		if (status.RecordsAffected != 500)
		{
			throw new TestFixtureAssertionException(
				$"RecordsAffected should be 500. Actual: {status.RecordsAffected}");
		}

		if (status.CertificateId != certificateId)
		{
			throw new TestFixtureAssertionException(
				$"CertificateId mismatch. Expected: {certificateId}, Actual: {status.CertificateId}");
		}

		if (status.CompletedAt is null)
		{
			throw new TestFixtureAssertionException(
				"CompletedAt should be set after RecordCompletionAsync");
		}
	}

	/// <summary>
	/// Verifies that a destroyed key handle recorded against a request survives, appends, and is idempotent.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why a store must carry this.</b> Asking a key store to destroy a key it has ALREADY destroyed
	/// reports the key as absent — the same answer it gives for a key that never existed. So an erasure that
	/// destroyed some keys and then failed part-way through the rest attested those keys on its first pass
	/// and, on the retry, attested nothing for them: the subject's data destroyed and their erasure
	/// permanently uncertifiable. These rows are what let a retry attest the coverage its own earlier pass
	/// achieved, so a store that loses them cannot complete an interrupted erasure.
	/// </para>
	/// <para>
	/// Append-only and idempotent are both load-bearing. A pass that TRUNCATED the set would erase the
	/// evidence of every earlier pass — the same defect by another route — and re-recording a handle happens
	/// on any retry.
	/// </para>
	/// </remarks>
	public virtual async Task RecordKeyDestroyedAsync_ShouldAppendIdempotentlyAndSurvive()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		// Guard: a request that has destroyed nothing reports an empty set rather than null, or the
		// assertions below could pass over a store that never returns anything.
		var before = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (before is null || before.DestroyedKeyHandles.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"A request that has destroyed no keys must report an empty destroyed-handle set, but reported "
				+ (before is null ? "no request at all." : $"{before.DestroyedKeyHandles.Count} handle(s)."));
		}

		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key-a", "gen-a", CancellationToken.None)
			.ConfigureAwait(false);
		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key-b", "gen-b", CancellationToken.None)
			.ConfigureAwait(false);

		// Idempotent on the GENERATION: a retry re-records the same generation the store already holds. Note
		// that this is the SAME generation, not merely the same handle -- two different generations at one
		// handle are two destructions and must both be kept, which the dedicated arm below covers.
		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key-a", "gen-a", CancellationToken.None)
			.ConfigureAwait(false);

		var after = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (after is null)
		{
			throw new TestFixtureAssertionException("Request should be found after recording destroyed keys.");
		}

		if (!after.DestroyedKeyHandles.Contains("subject-key-a")
			|| !after.DestroyedKeyHandles.Contains("subject-key-b"))
		{
			throw new TestFixtureAssertionException(
				"Both recorded handles must be readable back. A later record must ADD to what earlier ones "
				+ $"wrote, never replace it. Got: {string.Join(", ", after.DestroyedKeyHandles)}");
		}

		if (after.DestroyedKeyHandles.Count != 2)
		{
			throw new TestFixtureAssertionException(
				"Recording a handle twice must leave one entry for it, so a retry does not duplicate coverage. "
				+ $"Expected 2 distinct handles, got {after.DestroyedKeyHandles.Count}.");
		}
	}

	/// <summary>
	/// Verifies that recording a destroyed key against a request that does not exist throws.
	/// </summary>
	/// <remarks>
	/// The caller attests a destruction on the strength of this record existing, so a silent no-op here hands
	/// it a false assurance and leaves the retry unable to attest the destruction either. This is also the
	/// arm that stops the one above being satisfied by a store that accepts every write and stores nothing.
	/// </remarks>
	public virtual async Task RecordKeyDestroyedAsync_NonExistentRequest_ShouldThrowKeyNotFoundException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();

		try
		{
			await store.RecordKeyDestroyedAsync(nonExistentId, "subject-key", "gen-a", CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (KeyNotFoundException)
		{
			return;
		}

		throw new TestFixtureAssertionException(
			"Expected RecordKeyDestroyedAsync to throw KeyNotFoundException for a request that does not exist. "
			+ "Accepting the record quietly would let a caller attest a destruction on evidence no request holds.");
	}

	/// <summary>
	/// Verifies that a destroyed key handle is compared ordinally, so case alone distinguishes two handles.
	/// </summary>
	/// <remarks>
	/// A key handle is an opaque identifier, not text to be folded. A store whose comparison folded case would
	/// treat two distinct subjects' handles as one and attest coverage for a key that was never destroyed —
	/// which on a SQL store is decided by the column's COLLATION rather than by any framework code.
	/// </remarks>
	public virtual async Task RecordKeyDestroyedAsync_ShouldTreatHandlesDifferingOnlyInCaseAsDistinct()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		await store.RecordKeyDestroyedAsync(request.RequestId, "Subject-Key", "gen-upper", CancellationToken.None)
			.ConfigureAwait(false);
		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key", "gen-lower", CancellationToken.None)
			.ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null || status.DestroyedKeyHandles.Count != 2)
		{
			throw new TestFixtureAssertionException(
				"Two handles differing only in case are two handles. Folding them would attest coverage for a "
				+ "key that was never destroyed. Expected 2, got "
				+ (status is null ? "no request" : status.DestroyedKeyHandles.Count.ToString()));
		}
	}

	/// <summary>
	/// Verifies that TWO generations destroyed at ONE handle produce TWO ledger records, not one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm is RED against a store whose conflict clause names the request and the handle rather than the
	/// generation. That was the live shape: the clause reasoned only about "two passes recording the same
	/// handle", so a SECOND destruction at one handle -- a different generation, a real destruction -- was
	/// silently absorbed as a duplicate and never recorded. The failure is silent and it lands on the
	/// catastrophic side: a read of ciphertext produced under the second generation finds no record and cannot
	/// report the subject's erasure, so a destruction that happened is not on file anywhere.
	/// </para>
	/// <para>
	/// Observed through the ledger predicate rather than by counting rows, because that predicate is the thing
	/// consumers actually depend on: both generations must independently answer "destroyed". A store that
	/// collapsed them answers for the first and not the second.
	/// </para>
	/// <para>
	/// The instants are deliberately NOT asserted to differ. Two writes microseconds apart can legitimately
	/// read the same clock, so a strict-inequality assertion would be a wall-clock race rather than a statement
	/// about the store. What this arm pins is that the second destruction produced its own record instead of
	/// being discarded; whether its timestamp differs from the first is a property of the clock.
	/// </para>
	/// </remarks>
	public virtual async Task RecordKeyDestroyedAsync_TwoGenerationsAtOneHandle_ShouldRecordBoth()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = RequireLedger(store);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		// Unique per run, so a shared database cannot let an earlier run's rows satisfy this arm. The ledger is
		// keyed on (handle, generation) and is deliberately not scoped by request, so a fixed literal here
		// would be answered by any previous execution and the arm could never fail.
		var first = $"gen-first-{Guid.NewGuid():N}";
		var second = $"gen-second-{Guid.NewGuid():N}";

		// Guard: neither generation is on file before this arm writes it. Without this the assertions below
		// could pass over a ledger that answers true for everything.
		if (await ledger.IsGenerationDestroyedAsync("subject-key", first, CancellationToken.None).ConfigureAwait(false)
			|| await ledger.IsGenerationDestroyedAsync("subject-key", second, CancellationToken.None).ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				"Neither generation may be reported destroyed before it is recorded. A ledger that answers "
				+ "true for an unrecorded generation would produce a tombstone over live personal data.");
		}

		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key", first, CancellationToken.None)
			.ConfigureAwait(false);
		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key", second, CancellationToken.None)
			.ConfigureAwait(false);

		var firstRecorded = await ledger.IsGenerationDestroyedAsync("subject-key", first, CancellationToken.None)
			.ConfigureAwait(false);
		var secondRecorded = await ledger.IsGenerationDestroyedAsync("subject-key", second, CancellationToken.None)
			.ConfigureAwait(false);

		if (!firstRecorded || !secondRecorded)
		{
			throw new TestFixtureAssertionException(
				"Two generations destroyed at one handle are two destructions and both must be recorded. "
				+ $"First recorded: {firstRecorded}; second recorded: {secondRecorded}. A store keying this "
				+ "record on the handle silently drops the second, and every read of ciphertext produced under "
				+ "that generation then fails to report the subject's erasure.");
		}
	}

	/// <summary>
	/// Verifies that a destruction recorded at ONE handle does not report a DIFFERENT handle's identically
	/// named generation as destroyed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The ledger's whole key is (handle, generation). Keyed on the generation alone, the tombstone oracle
	/// would rest on an invariant nothing enforces: that one generation identifies material uniquely across
	/// every handle. Every shipped provider satisfies it, because they mint generations from a cryptographic
	/// random source -- but a CONSUMER-SUPPLIED provider that DERIVED its generation from the data subject
	/// would hand two tenants' distinct keys one identifier. One ledger row would then answer for both, and
	/// one tenant's erasure would report the other tenant's live personal data as lawfully erased, silently
	/// and irreversibly.
	/// </para>
	/// <para>
	/// RED input: key this store's ledger on the generation alone. The second assertion below then reports a
	/// handle nobody destroyed as destroyed, which is the defect verbatim. The first assertion is its liveness
	/// twin -- a store that reports nothing destroyed satisfies the safety half perfectly.
	/// </para>
	/// <para>
	/// The two handles are distinct VALUES, not two spellings of one: a store that folded case or trimmed would
	/// pass this arm while still collapsing two real handles, which is a different defect and is not what this
	/// arm claims to detect.
	/// </para>
	/// </remarks>
	public virtual async Task IsGenerationDestroyedAsync_ShouldNotReportOneHandlesDestructionForAnother()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = RequireLedger(store);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		// ONE generation at TWO handles -- exactly what a provider deriving its generations produces, and the
		// state the composite key exists to keep separate. Unique per run so an earlier execution's rows cannot
		// satisfy the arm.
		var sharedGeneration = $"gen-shared-{Guid.NewGuid():N}";
		var destroyedHandle = $"handle-destroyed-{Guid.NewGuid():N}";
		var untouchedHandle = $"handle-untouched-{Guid.NewGuid():N}";

		// Guard: neither pair is on file before this arm writes one. Without it both assertions could pass over
		// a ledger that answers false for everything, or be satisfied by a previous run.
		if (await ledger.IsGenerationDestroyedAsync(destroyedHandle, sharedGeneration, CancellationToken.None)
				.ConfigureAwait(false)
			|| await ledger.IsGenerationDestroyedAsync(untouchedHandle, sharedGeneration, CancellationToken.None)
				.ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				"Neither handle may report this generation destroyed before the arm records one. A ledger that "
				+ "answers true for an unrecorded pair would produce a tombstone over live personal data.");
		}

		await store.RecordKeyDestroyedAsync(
				request.RequestId, destroyedHandle, sharedGeneration, CancellationToken.None)
			.ConfigureAwait(false);

		var destroyedReported = await ledger
			.IsGenerationDestroyedAsync(destroyedHandle, sharedGeneration, CancellationToken.None)
			.ConfigureAwait(false);
		var untouchedReported = await ledger
			.IsGenerationDestroyedAsync(untouchedHandle, sharedGeneration, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS first: the destruction that DID happen must be reported, or the safety half below is
		// satisfied by a store that reports nothing at all.
		if (!destroyedReported)
		{
			throw new TestFixtureAssertionException(
				"The handle whose key was destroyed must report its generation as destroyed. Without this the "
				+ "safety assertion below is satisfied by a ledger that answers false for everything, and a "
				+ "genuinely erased subject's reads would fail forever instead of reporting their erasure.");
		}

		// SAFETY: the untouched handle must not inherit the answer.
		if (untouchedReported)
		{
			throw new TestFixtureAssertionException(
				"A destruction recorded at one handle must NOT report a different handle's identically named "
				+ "generation as destroyed. This store is keying the ledger on the generation alone, so two "
				+ "handles share one row -- and a read of the untouched handle's ciphertext returns a tombstone, "
				+ "asserting a lawful erasure over personal data that was never erased and whose key is still "
				+ "live. Key the ledger on (handle, generation).");
		}
	}

	/// <summary>
	/// Verifies that STAGING a destruction does not make the generation report as destroyed.
	/// </summary>
	/// <remarks>
	/// The safety arm for the whole two-table design. A staged row is written BEFORE the destruction, so
	/// between the stage and the destroy it names LIVE material. If the ledger predicate could see it, a live
	/// key would report as destroyed and the read would tombstone recoverable personal data while reporting a
	/// lawful erasure -- silent in both directions. Its liveness twin is the arm above: a store that never
	/// reports anything destroyed satisfies this arm and fails that one.
	/// </remarks>
	public virtual async Task StageKeyDestructionAsync_ShouldNotReportTheGenerationAsDestroyed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = RequireLedger(store);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var generation = $"gen-staged-{Guid.NewGuid():N}";

		await store.StageKeyDestructionAsync(request.RequestId, "subject-key", generation, CancellationToken.None)
			.ConfigureAwait(false);

		// The stage must be readable back -- otherwise this arm is satisfied by a store that drops the write,
		// which would be the opposite defect: a destruction performed with no recoverable generation.
		var staged = await store.GetStagedKeyGenerationsAsync(request.RequestId, "subject-key", CancellationToken.None)
			.ConfigureAwait(false);

		if (!staged.Contains(generation, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A staged destruction must be readable back. It is the only copy of the generation that "
				+ "survives the destruction, so a store that drops it leaves a crashed pass with nothing to "
				+ $"recover from. Got: {string.Join(", ", staged)}");
		}

		if (await ledger.IsGenerationDestroyedAsync("subject-key", generation, CancellationToken.None)
			.ConfigureAwait(false))
		{
			throw new TestFixtureAssertionException(
				"A STAGED generation must NOT report as destroyed. The stage happens before the destruction, so "
				+ "it names material that is still live -- reporting it destroyed would tombstone recoverable "
				+ "personal data and attest an erasure that has not happened.");
		}
	}

	/// <summary>
	/// Verifies that recording a destruction removes its staged intent, and ONLY its own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// SAFETY and LIVENESS in one arm, because either half alone is satisfied by a wrong implementation. A
	/// store that deletes nothing passes the second assertion and fails the first; a store that clears every
	/// intent for the handle passes the first and fails the second — and that second failure is the dangerous
	/// one, because the generations it would wipe are destructions NOT yet recorded, and a staged intent is
	/// the only copy of a generation that survives its own destruction.
	/// </para>
	/// <para>
	/// The ordering this protects is not asserted here and cannot be from outside: that the delete happens
	/// AFTER the ledger row exists is a property of the write path, not of the observable state afterwards.
	/// What this arm pins is that the cleanup is scoped to the generation that was recorded.
	/// </para>
	/// </remarks>
	public virtual async Task RecordKeyDestroyedAsync_ShouldClearOnlyTheRecordedIntent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)
			.ConfigureAwait(false);

		var recorded = $"gen-recorded-{Guid.NewGuid():N}";
		var stillStaged = $"gen-pending-{Guid.NewGuid():N}";

		await store.StageKeyDestructionAsync(request.RequestId, "subject-key", recorded, CancellationToken.None)
			.ConfigureAwait(false);
		await store.StageKeyDestructionAsync(request.RequestId, "subject-key", stillStaged, CancellationToken.None)
			.ConfigureAwait(false);

		// Guard: both are staged before anything is recorded, or the assertions below could pass over a store
		// that never staged the second one at all.
		var staged = await store.GetStagedKeyGenerationsAsync(request.RequestId, "subject-key", CancellationToken.None)
			.ConfigureAwait(false);

		if (!staged.Contains(recorded, StringComparer.Ordinal)
			|| !staged.Contains(stillStaged, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Both generations must be staged before this arm records one. A store that dropped either "
				+ $"would make the assertions below meaningless. Got: {string.Join(", ", staged)}");
		}

		await store.RecordKeyDestroyedAsync(request.RequestId, "subject-key", recorded, CancellationToken.None)
			.ConfigureAwait(false);

		var after = await store.GetStagedKeyGenerationsAsync(request.RequestId, "subject-key", CancellationToken.None)
			.ConfigureAwait(false);

		// SAFETY: the recorded generation's intent is gone, so the table does not grow without bound.
		if (after.Contains(recorded, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A staged intent must be removed once its destruction is recorded in the ledger. It then "
				+ "carries nothing the ledger does not, and leaving it grows this table by one row per key per "
				+ "request forever.");
		}

		// LIVENESS: the unrecorded generation's intent SURVIVES. This is the half that matters — it is the
		// only remaining copy of a generation whose destruction has not been recorded.
		if (!after.Contains(stillStaged, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Recording ONE generation must not clear the intents of generations that are still unrecorded. "
				+ "A staged intent is the only copy of a generation that survives its own destruction, so "
				+ $"clearing it early makes that destruction permanently unrecordable. Got: {string.Join(", ", after)}");
		}
	}

	/// <summary>
	/// Resolves the destruction ledger from the store under test, failing the arm if the store has none.
	/// </summary>
	/// <remarks>
	/// Required rather than optional: without a ledger no read of a crypto-shredded field can ever report the
	/// subject's erasure, so a store that cannot answer this is not a conforming erasure store. The cast is
	/// explicit here so the failure names the missing capability instead of surfacing as a null-reference.
	/// </remarks>
	private static IKeyDestructionLedger RequireLedger(IErasureStore store) =>
		store as IKeyDestructionLedger
		?? store.GetService(typeof(IKeyDestructionLedger)) as IKeyDestructionLedger
		?? throw new TestFixtureAssertionException(
			$"{store.GetType().Name} does not implement IKeyDestructionLedger. Without it no read of a "
			+ "crypto-shredded field can report an erasure this store recorded, so the destruction records it "
			+ "writes are unreachable by the only consumer that needs them.");

	/// <summary>
	/// Resolves the mint ledger from the store under test, or <see langword="null"/> when the store has none.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A conforming erasure store MUST supply this: the mint ledger is the enumeration an erasure destroys
	/// from, so a store that cannot answer it leaves every handle beyond the data subject's own one
	/// undiscoverable — a key nothing looks for, under a certificate reporting the subject erased.
	/// </para>
	/// <para>
	/// It returns null rather than throwing so the arms can record a VISIBLE SKIP against a store that does
	/// not yet supply one. That is deliberately not the same as passing: a skip says the property is
	/// UNVERIFIED for that store and keeps the gap in the suite's own output, where a throw would make the
	/// whole suite red for a capability the store has not claimed yet and a silent pass would hide it.
	/// </para>
	/// </remarks>
	private static IKeyMintLedger? TryGetMintLedger(IErasureStore store) =>
		store as IKeyMintLedger
		?? store.GetService(typeof(IKeyMintLedger)) as IKeyMintLedger;

	/// <summary>
	/// Verifies that every handle recorded for one data subject is enumerated for them — all of them, not
	/// just the first.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted the enumeration.</returns>
	/// <remarks>
	/// <para>
	/// <b>LIVENESS, and it is the arm the erasure's correctness rests on.</b> A data subject's fields are
	/// placed under one handle per obligation class they appear in, so a subject in two classes has two
	/// handles. The erasure destroys what this enumeration returns; a store returning one row where two
	/// exist leaves a live key for a subject reported erased, and nothing downstream learns.
	/// </para>
	/// <para>
	/// RED input: return only the first matching row, or cap the result. The count assertion fails.
	/// </para>
	/// <para>
	/// <b>Scope — this arm does NOT establish that a paging store fails.</b> It records two handles, and any
	/// realistic page size exceeds two, so a provider that pages internally passes here. The contract requires
	/// every continuation to be drained; binding that would need an arm large enough to cross a real page
	/// boundary, and there is none. Paging behaviour is UNVERIFIED by this suite.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_ShouldEnumerateEveryHandleRecordedForTheSubject()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_ShouldEnumerateEveryHandleRecordedForTheSubject),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so the complete enumeration of one subject's handles is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_ShouldEnumerateEveryHandleRecordedForTheSubject));

		var subject = NewSubjectHash();
		var plainHandle = NewHandle();
		var classHandle = NewHandle();

		await ledger.RecordMintIntentAsync(plainHandle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordMintIntentAsync(classHandle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		// CONFIRMED, exactly as the write path confirms after the key is created. Without this the rows
		// stay PENDING, which is a different property with its own arm below -- this arm is about whether
		// the enumeration is complete.
		await ledger.MarkMintedAsync(plainHandle, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(classHandle, CancellationToken.None).ConfigureAwait(false);

		var handles = await ledger.ListHandlesAsync(FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (handles is null || handles.Minted.Count != 2 || !handles.Minted.Contains(plainHandle) || !handles.Minted.Contains(classHandle))
		{
			throw new TestFixtureAssertionException(
				$"Two handles were recorded for one data subject and the enumeration returned "
				+ $"{handles?.Minted.Count.ToString(CultureInfo.InvariantCulture) ?? "null"}. A subject appearing "
				+ "in two obligation classes has two handles, and the erasure destroys exactly what this "
				+ "returns -- so one row where two exist is a live key for a subject whose certificate "
				+ "reports them erased, and nothing downstream can tell.");
		}
	}

	/// <summary>
	/// Verifies that a handle recorded for one owner is never enumerated for another.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted both halves.</returns>
	/// <remarks>
	/// <para>
	/// <b>SAFETY, and it needs two owners to be expressible at all.</b> This enumeration is addressed by
	/// (owner, subject) rather than by the ledger's own key, so unlike every other read on this store the
	/// owner term is load-bearing rather than redundant: without it the enumeration returns every subject's
	/// handles, and one owner's erasure would destroy another owner's keys.
	/// </para>
	/// <para>
	/// The two owners are chosen so the first is a strict PREFIX of the second, so a store matching by
	/// prefix or "starts with" fails here rather than looking correct.
	/// </para>
	/// <para>
	/// <b>Both halves, because the safety half alone is satisfied by a store that enumerates nothing.</b>
	/// The liveness assertion is what fails if the owner term is tightened into a filter that can never
	/// match.
	/// </para>
	/// <para>
	/// RED input: drop the owner term from the enumeration, or match it by prefix. The cross-owner
	/// assertion fails.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_ShouldNotEnumerateAnotherOwnersHandle()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_ShouldNotEnumerateAnotherOwnersHandle),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so cross-owner isolation of the mint ledger is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_ShouldNotEnumerateAnotherOwnersHandle));

		// ONE subject token under TWO owners, which is the reachable case rather than a contrived one:
		// data-subject identifiers are consumer-supplied from their own entities, so a customer number or
		// an e-mail address repeats across tenants in ordinary multi-tenant deployments.
		var subject = NewSubjectHash();
		var firstOwnersHandle = NewHandle();
		var secondOwnersHandle = NewHandle();

		await ledger.RecordMintIntentAsync(firstOwnersHandle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordMintIntentAsync(secondOwnersHandle, SecondOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		await ledger.MarkMintedAsync(firstOwnersHandle, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(secondOwnersHandle, CancellationToken.None).ConfigureAwait(false);

		var firstOwnersHandles = await ledger.ListHandlesAsync(FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		// THE LIVENESS HALF FIRST, because the isolation assertion below is satisfied by a store that
		// enumerates nothing at all -- which is safe, useless, and indistinguishable from correct.
		if (firstOwnersHandles is null || !firstOwnersHandles.Minted.Contains(firstOwnersHandle))
		{
			throw new TestFixtureAssertionException(
				"An owner's enumeration did not return their OWN handle. A store that enumerates nothing "
				+ "satisfies every isolation assertion while leaving every key alive, so this half is what "
				+ "distinguishes isolation from inaction.");
		}

		if (firstOwnersHandles.Minted.Contains(secondOwnersHandle))
		{
			throw new TestFixtureAssertionException(
				"An enumeration for one owner returned a handle recorded for ANOTHER owner. The owner term "
				+ "is what stops one owner's erasure destroying keys in a partition they do not hold. The "
				+ "first owner is a strict PREFIX of the second, so a store matching by prefix or "
				+ "'starts with' fails here rather than looking correct.");
		}
	}

	/// <summary>
	/// Verifies that the owner and subject terms are matched ORDINALLY — no case folding, no trimming, no
	/// prefix.
	/// </summary>
    /// <returns>A task that completes when the arm has asserted the ordinal match.</returns>
	/// <remarks>
	/// <para>
	/// <b>SAFETY.</b> A key-vault object name is case-insensitive, which is why the handle is a digest
	/// rather than a readable composition — but the OWNER recorded beside it is consumer-supplied text, and
	/// a database whose default collation folds case would let one owner's handles be enumerated for
	/// another. That is the cross-tenant direction this subsystem has already shipped once.
	/// </para>
	/// <para>
	/// RED input: compare the owner with a culture-aware or case-insensitive comparison, or declare the
	/// column without a binary collation. This arm reddens and the cross-owner arm above does not, which is
	/// what makes the two separable rather than one assertion written twice.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_ShouldMatchTheOwnerOrdinally()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_ShouldMatchTheOwnerOrdinally),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so ordinal matching of the owner and subject terms is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_ShouldMatchTheOwnerOrdinally));

		var subject = NewSubjectHash();
		var handle = NewHandle();

		await ledger.RecordMintIntentAsync(handle, MixedCaseOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(handle, CancellationToken.None).ConfigureAwait(false);

		var asRecorded = await ledger.ListHandlesAsync(
			MixedCaseOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (asRecorded is null || !asRecorded.Minted.Contains(handle))
		{
			throw new TestFixtureAssertionException(
				"The handle was not enumerable under the owner it was recorded with, so the two ordinal "
				+ "assertions below would hold vacuously against a store that enumerates nothing.");
		}

		var underADifferentCase = await ledger.ListHandlesAsync(
			new TenantId(MixedCaseOwner.ToUpperInvariant()), subject, CancellationToken.None).ConfigureAwait(false);

		if (underADifferentCase is not null && underADifferentCase.Minted.Contains(handle))
		{
			throw new TestFixtureAssertionException(
				"A handle recorded for one owner was enumerated for an owner differing only in CASE. An "
				+ "owner is consumer-supplied text and two spellings are two owners; folding the "
				+ "distinction lets one owner's erasure enumerate -- and destroy -- another's keys. A "
				+ "column declared without a binary collation fails here.");
		}

		// THE PADDED OWNER, which is the case the earlier revision of this arm left untested while its name
		// and its two preceding assertions were all about the owner. It is the one that matters most on SQL
		// Server: the ANSI trailing-blank rule applies to `=` on NVARCHAR, so whether a binary collation
		// suppresses it is a behavioural question no schema can answer by inspection.
		var underAPaddedOwner = await ledger.ListHandlesAsync(
			new TenantId(MixedCaseOwner + " "), subject, CancellationToken.None).ConfigureAwait(false);

		if (underAPaddedOwner is not null && underAPaddedOwner.Minted.Contains(handle))
		{
			throw new TestFixtureAssertionException(
				"A handle was enumerated for an OWNER with a trailing space. A padded owner is a different "
				+ "owner, and on several collations a padded comparison is the DEFAULT -- so a store that "
				+ "pads enumerates, and an erasure then destroys, handles belonging to somebody else.");
		}

		var underAPaddedToken = await ledger.ListHandlesAsync(
			MixedCaseOwnerId, subject + " ", CancellationToken.None).ConfigureAwait(false);

		if (underAPaddedToken is not null && underAPaddedToken.Minted.Contains(handle))
		{
			throw new TestFixtureAssertionException(
				"A handle was enumerated for a subject token with a trailing space. A padded token is a "
				+ "different token, and on several collations a padded comparison is the DEFAULT -- so a "
				+ "store that trims enumerates handles for a subject nobody asked about.");
		}
	}

	/// <summary>
	/// Verifies that recording a handle already on file is a no-op rather than an error or a duplicate.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted the idempotence.</returns>
	/// <remarks>
	/// <para>
	/// <b>The first write per subject and obligation class races with every other, so this is the ordinary
	/// path rather than an edge case.</b> The row carries nothing a later call could legitimately change,
	/// so a second record must insert nothing and must not throw: a throw here would fail a perfectly good
	/// personal-data write, and this ledger is on the write path and fails closed.
	/// </para>
	/// <para>
	/// RED input: insert unconditionally, or raise a duplicate-key error. The count assertion fails on the
	/// first, the call throws on the second.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_RecordingTheSameHandleTwice_ShouldBeANoOp()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_RecordingTheSameHandleTwice_ShouldBeANoOp),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so idempotent recording is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_RecordingTheSameHandleTwice_ShouldBeANoOp));

		var subject = NewSubjectHash();
		var handle = NewHandle();

		await ledger.RecordMintIntentAsync(handle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordMintIntentAsync(handle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(handle, CancellationToken.None).ConfigureAwait(false);

		var handles = await ledger.ListHandlesAsync(FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (handles is null || handles.Minted.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Recording one handle twice left {handles?.Minted.Count.ToString(CultureInfo.InvariantCulture) ?? "null"} "
				+ "rows. The entry is keyed by the handle and is immutable once written, so a second record "
				+ "inserts nothing. A duplicate makes the erasure destroy one handle twice and report a "
				+ "coverage count it did not achieve; a throw would fail a perfectly good personal-data "
				+ "write, and the first write per subject and obligation class races with every other.");
		}
	}

	/// <summary>
	/// Verifies that re-recording one handle under a DIFFERENT owner is refused loudly, rather than silently
	/// keeping the first spelling.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted the refusal and the survivor.</returns>
	/// <remarks>
	/// <para>
	/// <b>SAFETY, and it is the arm that binds the row to the HANDLE ALONE.</b> The idempotence arm records
	/// one handle under one owner twice, so it is satisfied by a store keyed on the whole triple — which
	/// would admit two rows for one handle under two owners, exactly what the handle-only primary key exists
	/// to forbid. This arm is what makes that keying observable through the interface.
	/// </para>
	/// <para>
	/// <b>Silently keeping the first spelling is worse than having no row.</b> The handle would be recorded
	/// while the enumeration for the owner whose data it actually holds returned nothing — so the erasure
	/// destroys what it found and certifies. A handle is derived from the owner and the subject, so a
	/// collision between two of them is a derivation defect or a hash collision, and both must be loud.
	/// </para>
	/// <para>
	/// RED input: use an unconditional insert-if-absent and drop the comparison. The refusal never happens.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_RerecordingAHandleUnderAnotherOwner_ShouldBeRefused()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_RerecordingAHandleUnderAnotherOwner_ShouldBeRefused),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so the disagreeing-enumeration-key refusal is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_RerecordingAHandleUnderAnotherOwner_ShouldBeRefused));

		var subject = NewSubjectHash();
		var handle = NewHandle();

		await ledger.RecordMintIntentAsync(handle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(handle, CancellationToken.None).ConfigureAwait(false);

		var refused = false;
		try
		{
			await ledger.RecordMintIntentAsync(handle, SecondOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			refused = true;
		}

		if (!refused)
		{
			throw new TestFixtureAssertionException(
				"Re-recording one handle under a DIFFERENT owner was accepted. Either the store kept the "
				+ "first spelling silently -- leaving the handle unenumerable for the owner whose data it "
				+ "holds, so that owner's erasure destroys what it found and certifies -- or it admitted a "
				+ "second row for one handle, which the handle-only primary key forbids.");
		}

		// THE LIVENESS HALF: the refusal must not have destroyed the row that was already correct.
		var firstOwnersHandles = await ledger.ListHandlesAsync(FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (firstOwnersHandles is null || !firstOwnersHandles.Minted.Contains(handle))
		{
			throw new TestFixtureAssertionException(
				"The refused second record removed or altered the first owner's row. Rows are insert-only "
				+ "and permanent, so a refusal must leave the existing row exactly as it was -- otherwise "
				+ "the refusal itself is the omission from the enumeration.");
		}
	}

	/// <summary>
	/// Verifies that a subject with no recorded handles enumerates empty rather than failing.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted the empty enumeration.</returns>
	/// <remarks>
	/// <para>
	/// <b>LIVENESS for the commonest case there is.</b> Most erasure requests name a subject who never had
	/// an annotated field written, and such a request must still reach a terminal state. An enumeration
	/// that threw on an unknown subject would make every one of those requests fail.
	/// </para>
	/// <para>
	/// The empty answer is NOT evidence that the subject's data was covered — it also describes a subject
	/// whose fields predate this ledger — and the contract says so. This arm binds only that the answer is
	/// empty rather than an error.
	/// </para>
	/// <para>
	/// RED input: throw, or return null, when no row matches.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_UnknownSubject_ShouldEnumerateEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_UnknownSubject_ShouldEnumerateEmpty),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so the empty enumeration for an unknown subject is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot enumerate the handles holding "
				+ "a subject's personal fields.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_UnknownSubject_ShouldEnumerateEmpty));

		var handles = await ledger.ListHandlesAsync(
			FirstOwnerId, NewSubjectHash(), CancellationToken.None).ConfigureAwait(false);

		if (handles is null || handles.Minted.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"A subject with no recorded handles did not enumerate empty. Most erasure requests name a "
				+ "subject who never had an annotated field written, and every one of those must still "
				+ "reach a terminal state -- so an unknown subject answers an empty list, never null and "
				+ "never an error.");
		}
	}

	/// <summary>
	/// Verifies that a handle whose key creation has NOT been confirmed is reported as PENDING and not as
	/// minted.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted the pending state.</returns>
	/// <remarks>
	/// <para>
	/// <b>SAFETY, and it is the arm the concurrent-write property rests on.</b> A mint-ledger row is
	/// written BEFORE the key is created, so between those two moments a write is invisible to both
	/// observables an erasure has: the handle set has not changed since the erasure read it, and the key
	/// material reads absent because it genuinely is not there yet. That state is observationally
	/// identical to a writer that crashed between its row and its mint -- which is SAFE and correctly
	/// ignored -- and the two diverge only after the certificate is signed.
	/// </para>
	/// <para>
	/// So no re-reading of the handle set and no number of material probes can separate them. The row has
	/// to say which side of the mint it is on, and this arm is what binds that it does. A store that
	/// reports a pending row as minted lets an erasure sign a clean completion over personal data that is
	/// about to become readable.
	/// </para>
	/// <para>
	/// RED input: mark a row minted on insert, or report the pending set as empty, or fold both sets into
	/// one. Any of the three makes a pending row indistinguishable from a confirmed one, which is the
	/// whole defect.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_AnUnconfirmedHandle_ShouldBeReportedAsPending()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_AnUnconfirmedHandle_ShouldBeReportedAsPending),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so the pending state of an unconfirmed mint is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one: without it an erasure cannot tell a write in flight from a write "
				+ "that was abandoned.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_AnUnconfirmedHandle_ShouldBeReportedAsPending));

		var subject = NewSubjectHash();
		var pendingHandle = NewHandle();
		var confirmedHandle = NewHandle();

		await ledger.RecordMintIntentAsync(pendingHandle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordMintIntentAsync(confirmedHandle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		// Only one of the two is confirmed, so the arm binds the SPLIT rather than merely that both sets
		// are populated -- a store that put every row in one set would otherwise pass.
		await ledger.MarkMintedAsync(confirmedHandle, CancellationToken.None).ConfigureAwait(false);

		var snapshot = await ledger.ListHandlesAsync(
			FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (snapshot is null)
		{
			throw new TestFixtureAssertionException(
				"The enumeration returned null. A conforming store answers with a snapshot, always.");
		}

		if (!snapshot.PendingMint.Contains(pendingHandle, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A handle recorded but never confirmed was NOT reported as pending. The row is written "
				+ "before the key is created, so an unconfirmed row is the only evidence that a write is "
				+ "in flight -- and an erasure that cannot see it signs a clean completion over personal "
				+ "data that is about to become readable.");
		}

		if (snapshot.Minted.Contains(pendingHandle, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A handle recorded but never confirmed was reported as MINTED. That is the catastrophic "
				+ "direction: an erasure reads the minted set as the set it must cover, destroys it, and "
				+ "certifies -- while the key behind this handle is still about to be created.");
		}

		if (!snapshot.Minted.Contains(confirmedHandle, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A handle whose mint WAS confirmed is missing from the minted set. Without it the erasure "
				+ "does not destroy a handle that genuinely holds this subject's personal data.");
		}

		if (snapshot.PendingMint.Contains(confirmedHandle, StringComparer.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"A confirmed handle is still reported as pending. Confirming a mint must move the row, or "
				+ "every erasure for this subject reports a concurrent write forever and never completes.");
		}
	}

	/// <summary>
	/// Verifies that confirming a mint is idempotent and that confirming a handle with no row is refused.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted both.</returns>
	/// <remarks>
	/// <para>
	/// <b>The idempotence half is LIVENESS:</b> the confirmation is fail-open and therefore retried, so a
	/// second confirmation must be a no-op rather than an error. The refusal half is SAFETY: the row is
	/// written before the key, always, so confirming a handle with NO row means the caller skipped the
	/// intent step -- and a key may then exist at a handle no erasure can enumerate. Inserting a row at
	/// that point would paper over it and would have no owner or subject to record.
	/// </para>
	/// <para>
	/// RED input: throw on the second confirmation, or silently insert a row when none exists.
	/// </para>
	/// </remarks>
	public virtual async Task MintLedger_ConfirmingAMint_ShouldBeIdempotentAndRefuseAnAbsentRow()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var ledger = TryGetMintLedger(store);
		if (ledger is null)
		{
			SkipArm(
				nameof(MintLedger_ConfirmingAMint_ShouldBeIdempotentAndRefuseAnAbsentRow),
				capability: typeof(IKeyMintLedger),
				"this store does not supply a mint ledger, so the idempotence of a mint confirmation is UNVERIFIED for this store. A conforming erasure "
				+ "store must supply one.");
			return;
		}

		RecordArmExecuted(nameof(MintLedger_ConfirmingAMint_ShouldBeIdempotentAndRefuseAnAbsentRow));

		var subject = NewSubjectHash();
		var handle = NewHandle();

		await ledger.RecordMintIntentAsync(handle, FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(handle, CancellationToken.None).ConfigureAwait(false);
		await ledger.MarkMintedAsync(handle, CancellationToken.None).ConfigureAwait(false);

		var snapshot = await ledger.ListHandlesAsync(
			FirstOwnerId, subject, CancellationToken.None).ConfigureAwait(false);

		if (snapshot.Minted.Count != 1 || snapshot.PendingMint.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Confirming one mint twice did not leave exactly one minted row and no pending row. The "
				+ "confirmation is fail-open and therefore retried, so a second confirmation must be a "
				+ "no-op.");
		}

		var refused = false;

		try
		{
			await ledger.MarkMintedAsync(NewHandle(), CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			refused = true;
		}

		if (!refused)
		{
			throw new TestFixtureAssertionException(
				"Confirming a mint for a handle with NO row was accepted. The row is written before the "
				+ "key, always, so its absence means the intent step was skipped -- and a key may exist at "
				+ "a handle no erasure can enumerate. Accepting it silently loses the only signal that "
				+ "happened.");
		}
	}

	/// <summary>
	/// Produces a handle value unique to this arm, shaped like the digest the derivation actually produces.
	/// </summary>
	/// <remarks>
	/// Unique per call so the arms share a store without colliding, and <b>81 characters</b> because that is
	/// the real maximum the shipped derivation produces — a 64-character digest, a separator, and a
	/// 16-character obligation-class discriminator. A 64-character fixture would leave a store whose column
	/// is too narrow for a RETAINED handle passing here and failing in a consumer's deployment, on the write
	/// path, fail-closed.
	/// </remarks>
	private static string NewHandle() =>
		Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") + "-" + Guid.NewGuid().ToString("N")[..16];

	/// <summary>
	/// Produces a data-subject pseudonymisation token unique to this arm.
	/// </summary>
	private static string NewSubjectHash() => "conformance-subject-" + Guid.NewGuid().ToString("N");

	/// <inheritdoc cref="RecordCompletionAsync_ShouldMarkCompleted"/>
	public virtual async Task RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();
		var certificateId = Guid.NewGuid();

		try
		{
			await store.RecordCompletionAsync(
				nonExistentId,
				keysDeleted: 0,
				recordsAffected: 0,
				certificateId,
				// Any identity: there is no row to carry one, and "no such request" must be reported
				// ahead of any claim comparison -- a caller has to tell it from a lost claim.
				"an-identity-no-request-carries",
				CancellationToken.None).ConfigureAwait(false);

			throw new TestFixtureAssertionException(
				"Expected KeyNotFoundException for non-existent RequestId but no exception was thrown");
		}
		catch (KeyNotFoundException)
		{
			// Expected
		}
	}

	#endregion

	#region Cancellation Tests (STATE MACHINE)

	/// <summary>
	/// Verifies that RecordCancellationAsync succeeds for Scheduled status.
	/// </summary>
	public virtual async Task RecordCancellationAsync_Scheduled_ShouldCancel()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		// Initial status is Scheduled
		var cancelled = await store.RecordCancellationAsync(
			request.RequestId,
			reason: "User requested cancellation",
			cancelledBy: "admin",
			CancellationToken.None).ConfigureAwait(false);

		if (!cancelled)
		{
			throw new TestFixtureAssertionException(
				"RecordCancellationAsync should return true for Scheduled request");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after cancellation");
		}

		if (status.Status != ErasureRequestStatus.Cancelled)
		{
			throw new TestFixtureAssertionException(
				$"Status should be Cancelled. Actual: {status.Status}");
		}

		if (status.CancellationReason != "User requested cancellation")
		{
			throw new TestFixtureAssertionException(
				$"CancellationReason mismatch. Expected: 'User requested cancellation', Actual: '{status.CancellationReason}'");
		}

		if (status.CancelledBy != "admin")
		{
			throw new TestFixtureAssertionException(
				$"CancelledBy mismatch. Expected: 'admin', Actual: '{status.CancelledBy}'");
		}

		if (status.CancelledAt is null)
		{
			throw new TestFixtureAssertionException(
				"CancelledAt should be set after cancellation");
		}
	}

	/// <summary>
	/// Verifies that RecordCancellationAsync succeeds for Pending status.
	/// </summary>
	public virtual async Task RecordCancellationAsync_Pending_ShouldCancel()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		// Change status to Pending first (SaveRequest sets Scheduled), and ASSERT it: a refused
		// precondition leaves the request Scheduled, which is also cancellable, so the arm would pass
		// while testing a state it did not mean to construct.
		if (await store.UpdateStatusAsync(
				request.RequestId,
				ErasureRequestStatus.Pending,
				expectedStatus: null,
				null,
				claimIdentity: null,
				CancellationToken.None).ConfigureAwait(false) != ErasureWriteOutcome.Applied)
		{
			throw new TestFixtureAssertionException(
				"Moving a freshly saved request to Pending must be applied; this arm tests cancellation "
				+ "FROM Pending and cannot construct that state otherwise.");
		}

		var cancelled = await store.RecordCancellationAsync(
			request.RequestId,
			reason: "Pending cancellation",
			cancelledBy: "admin",
			CancellationToken.None).ConfigureAwait(false);

		if (!cancelled)
		{
			throw new TestFixtureAssertionException(
				"RecordCancellationAsync should return true for Pending request");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null || status.Status != ErasureRequestStatus.Cancelled)
		{
			throw new TestFixtureAssertionException(
				"Status should be Cancelled for Pending request after cancellation");
		}
	}

	/// <summary>
	/// Verifies that RecordCancellationAsync returns false for InProgress status (STATE MACHINE).
	/// </summary>
	public virtual async Task RecordCancellationAsync_InProgress_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		// Take execution custody, which is the only way production reaches the claimed state
		_ = await ClaimForArmAsync(store, request.RequestId).ConfigureAwait(false);

		// Attempt to cancel - should fail (state machine)
		var cancelled = await store.RecordCancellationAsync(
			request.RequestId,
			reason: "Should fail",
			cancelledBy: "admin",
			CancellationToken.None).ConfigureAwait(false);

		if (cancelled)
		{
			throw new TestFixtureAssertionException(
				"RecordCancellationAsync should return false for InProgress request (STATE MACHINE)");
		}

		// Status should remain InProgress
		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null || status.Status != ErasureRequestStatus.InProgress)
		{
			throw new TestFixtureAssertionException(
				"Status should remain InProgress after failed cancellation attempt");
		}
	}

	/// <summary>
	/// Verifies that RecordCancellationAsync returns false for non-existent request.
	/// </summary>
	public virtual async Task RecordCancellationAsync_NonExistent_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();

		var cancelled = await store.RecordCancellationAsync(
			nonExistentId,
			reason: "Test",
			cancelledBy: "admin",
			CancellationToken.None).ConfigureAwait(false);

		if (cancelled)
		{
			throw new TestFixtureAssertionException(
				"RecordCancellationAsync should return false for non-existent RequestId");
		}
	}

	#endregion

	#region Scheduled Query Tests

	/// <summary>
	/// Verifies that GetScheduledRequestsAsync returns due requests.
	/// </summary>
	public virtual async Task GetScheduledRequestsAsync_ShouldReturnDueRequests()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Request that is due (scheduled time in the past)
		var dueRequest = CreateErasureRequest();
		await store.SaveRequestAsync(dueRequest, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None).ConfigureAwait(false);

		// Request that is not due (scheduled time in the future)
		var futureRequest = CreateErasureRequest();
		await store.SaveRequestAsync(futureRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var scheduled = await GetQueryStore(store).GetScheduledRequestsAsync(100, CancellationToken.None).ConfigureAwait(false);

		if (!scheduled.Any(r => r.RequestId == dueRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Due request should be returned by GetScheduledRequestsAsync");
		}

		if (scheduled.Any(r => r.RequestId == futureRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Future request should NOT be returned by GetScheduledRequestsAsync");
		}
	}

	/// <summary>
	/// Verifies that GetScheduledRequestsAsync orders by scheduled time.
	/// </summary>
	public virtual async Task GetScheduledRequestsAsync_ShouldOrderByScheduledTime()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create requests with different scheduled times (all in the past)
		var laterRequest = CreateErasureRequest();
		await store.SaveRequestAsync(laterRequest, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None).ConfigureAwait(false);

		var earlierRequest = CreateErasureRequest();
		await store.SaveRequestAsync(earlierRequest, DateTimeOffset.UtcNow.AddMinutes(-10), CancellationToken.None).ConfigureAwait(false);

		var scheduled = await GetQueryStore(store).GetScheduledRequestsAsync(100, CancellationToken.None).ConfigureAwait(false);

		if (scheduled.Count < 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 2 scheduled requests, got {scheduled.Count}");
		}

		// Earlier request should appear before later request (ordered by ScheduledExecutionAt ascending)
		var earlierIndex = -1;
		var laterIndex = -1;

		for (var i = 0; i < scheduled.Count; i++)
		{
			if (scheduled[i].RequestId == earlierRequest.RequestId)
			{
				earlierIndex = i;
			}

			if (scheduled[i].RequestId == laterRequest.RequestId)
			{
				laterIndex = i;
			}
		}

		if (earlierIndex < 0 || laterIndex < 0)
		{
			throw new TestFixtureAssertionException(
				"Both requests should be in the result");
		}

		if (earlierIndex > laterIndex)
		{
			throw new TestFixtureAssertionException(
				"Earlier request should appear before later request (ordered by ScheduledExecutionAt)");
		}
	}

	#endregion

	#region List Query Tests

	/// <summary>
	/// Verifies that ListRequestsAsync filters by status.
	/// </summary>
	public virtual async Task ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var scheduledRequest = CreateErasureRequest();
		await store.SaveRequestAsync(scheduledRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var completedRequest = CreateErasureRequest();
		await store.SaveRequestAsync(completedRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var completedClaim = await store.ClaimForExecutionAsync(
				completedRequest.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		await store.RecordCompletionAsync(completedRequest.RequestId, 0, 0, Guid.NewGuid(), completedClaim.ClaimIdentity!, CancellationToken.None).ConfigureAwait(false);

		var results = await GetQueryStore(store).ListRequestsAsync(
			status: ErasureRequestStatus.Completed,
			tenantId: null,
			fromDate: null,
			toDate: null,
			pageNumber: 1,
			pageSize: 100,
			CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(r => r.RequestId == completedRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Completed request should be returned when filtering by Completed status");
		}

		if (results.Any(r => r.RequestId == scheduledRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Scheduled request should NOT be returned when filtering by Completed status");
		}
	}

	/// <summary>
	/// Verifies that a supplied tenant filter narrows the result: it is neither ignored nor inverted.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted both halves.</returns>
	/// <remarks>
	/// <para>
	/// <b>The request names no tenant, deliberately, and this arm previously named two.</b> A request's
	/// owning tenant is resolved by the host and a request naming a different one is refused, so the
	/// earlier shape of this arm — two requests carrying two caller-supplied tenants — asserted a
	/// behaviour that no correct store can produce. It certified, as a published requirement, the very
	/// defect that let an erasure destroy a key the write path never minted and still report completion.
	/// A store that passes the old shape has that defect; a correct store failed our own kit.
	/// </para>
	/// <para>
	/// So the owner is read back from the store rather than assumed. This arm then asserts the two halves
	/// that make the argument load-bearing: naming the recorded owner still returns the row, and naming
	/// any other tenant returns nothing. The first half is what fails against a store that returns the
	/// empty set to everybody; the second is what fails against a store that drops the argument, or
	/// inverts it. Neither half can be satisfied by the other.
	/// </para>
	/// <para>
	/// <b>Scope — what this arm does NOT establish.</b> It uses one row, so it cannot see a filter that
	/// matches too widely across rows: prefix matching, or a comparison that folds case. Those are
	/// covered by <see cref="ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest"/> and
	/// <see cref="ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding"/>, both of which
	/// need <see cref="BeginOwnerScope"/> and report a skip when a suite cannot supply it.
	/// </para>
	/// </remarks>
	public virtual async Task ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var request = CreateErasureRequest();
		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var owner = await RecordedOwnerAsync(store, request.RequestId).ConfigureAwait(false);

		if (string.Equals(owner, ForeignTenant, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"This suite's store resolved '{owner}' as its owner, which is the value this arm uses as the "
				+ "tenant it must NOT match. Override CreateStore to resolve a different tenant; with these "
				+ "two equal, the arm's safety half would be asserting that a tenant does not match itself.");
		}

		// LIVENESS. Without this half the safety assertion below is satisfied by a store that returns the
		// empty set to every caller, which is the cheapest way to never return another tenant's row.
		var byOwner = await ListByTenantAsync(store, owner).ConfigureAwait(false);
		if (!byOwner.Any(r => r.RequestId == request.RequestId))
		{
			throw new TestFixtureAssertionException(
				$"A request whose recorded owner is '{owner}' must be returned when listing is filtered by "
				+ $"'{owner}'. A supplied tenant filter narrows the result; it must not exclude the rows that "
				+ "own it, or no caller can list their own erasure requests.");
		}

		// SAFETY. Fails against a store that ignores the argument (the row comes back for any tenant) and
		// against one that inverts it (the row comes back for every tenant except its owner).
		var byForeignTenant = await ListByTenantAsync(store, ForeignTenant).ConfigureAwait(false);
		if (byForeignTenant.Any(r => r.RequestId == request.RequestId))
		{
			throw new TestFixtureAssertionException(
				$"A request whose recorded owner is '{owner}' must NOT be returned when listing is filtered "
				+ $"by '{ForeignTenant}'. A supplied tenant filter that is ignored, or applied as an "
				+ "inequality, hands one tenant's erasure requests to another.");
		}
	}

	/// <summary>
	/// Verifies that a request owned by one tenant never reaches a listing made for another.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted both halves, or reported a skip.</returns>
	/// <remarks>
	/// <para>
	/// <b>This is the isolation property the tenant filter exists for, and it needs two owners.</b> The
	/// owner decides which per-subject key protects a data subject's fields, so a listing that returned
	/// another owner's request would offer an operator a request whose execution destroys a key in a
	/// partition they do not hold. One row cannot establish it: a filter that matches too widely — by
	/// prefix, or by folding a distinction — looks correct until a second owner exists to be matched
	/// wrongly.
	/// </para>
	/// <para>
	/// The two owners are chosen so that the first is a strict PREFIX of the second. Filtering by the
	/// shorter one then fails against a store that matches by prefix or by "starts with", which an
	/// equality comparison and this arm agree must return one row rather than two.
	/// </para>
	/// <para>
	/// <b>Scope — what this arm does NOT establish.</b> It lists from inside the first owner's own scope,
	/// so that it works against a store that applies an ambient tenant term as well as the argument. On
	/// such a store the exclusion may be the ambient term's work rather than the argument's; the argument
	/// itself is bound by <see cref="ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant"/>, which
	/// runs for every suite. Read the two together: this arm establishes the isolation property, that one
	/// establishes that the argument is what carries it.
	/// </para>
	/// </remarks>
	public virtual async Task ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		using var firstScope = BeginOwnerScope(FirstOwner);
		if (firstScope is null)
		{
			SkipArm(
				nameof(ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest),
				capability: null,
				"the suite does not override BeginOwnerScope, so this store resolves no named tenant and two "
				+ "requests under two owners cannot be created. Cross-owner list isolation is UNVERIFIED for "
				+ "this store.");
			return;
		}

		RecordArmExecuted(nameof(ListRequestsAsync_AcrossOwners_ShouldNotReturnAnotherOwnersRequest));

		var firstRequest = CreateErasureRequest();
		await store.SaveRequestAsync(firstRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);
		var firstOwner = await RecordedOwnerAsync(store, firstRequest.RequestId).ConfigureAwait(false);

		Guid secondRequestId;
		string secondOwner;
		using (var secondScope = BeginOwnerScope(SecondOwner))
		{
			if (secondScope is null)
			{
				throw new TestFixtureAssertionException(
					"BeginOwnerScope returned a scope for one tenant and null for another. It must either "
					+ "support every tenant it is asked for or none, or the arms cannot tell an unsupported "
					+ "suite from a partially wired one.");
			}

			var secondRequest = CreateErasureRequest();
			await store.SaveRequestAsync(secondRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);
			secondRequestId = secondRequest.RequestId;
			secondOwner = await RecordedOwnerAsync(store, secondRequest.RequestId).ConfigureAwait(false);
		}

		// The fixture's own precondition, asserted rather than assumed: a scope that did not change the
		// recorded owner would make both assertions below pass while measuring one tenant twice.
		if (string.Equals(firstOwner, secondOwner, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Two saves made under two different owner scopes both recorded '{firstOwner}'. Either the "
				+ "store does not resolve its owner from the scope BeginOwnerScope establishes, or it takes "
				+ "the owner from somewhere the scope does not reach. Until they differ this arm compares a "
				+ "tenant with itself.");
		}

		var forFirstOwner = await ListByTenantAsync(store, firstOwner).ConfigureAwait(false);

		// LIVENESS: the owner's own request is still reachable. A store that returned nothing would satisfy
		// the isolation assertion below and serve no caller at all.
		if (!forFirstOwner.Any(r => r.RequestId == firstRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				$"A listing filtered by '{firstOwner}' must return that owner's own request.");
		}

		// SAFETY: the other owner's request is not reachable. Fails against a prefix or "starts with" match,
		// and against any comparison that treats the two owners as one.
		if (forFirstOwner.Any(r => r.RequestId == secondRequestId))
		{
			throw new TestFixtureAssertionException(
				$"A listing filtered by '{firstOwner}' returned a request owned by '{secondOwner}'. The owner "
				+ "decides which per-subject key an erasure destroys, so a cross-owner listing offers an "
				+ "operator a request whose execution reaches a partition they do not hold. Note that "
				+ $"'{firstOwner}' is a prefix of '{secondOwner}': a filter applied as a prefix or "
				+ "\"starts with\" match, rather than as equality, fails here and passes a single-row check.");
		}
	}

	/// <summary>
	/// Verifies that the tenant filter compares ordinally: it folds neither case nor surrounding padding.
	/// </summary>
	/// <returns>A task that completes when the arm has asserted every probe, or reported a skip.</returns>
	/// <remarks>
	/// <para>
	/// <b>Two tenant identifiers that differ only in case are two tenants.</b> The per-subject key handle
	/// is derived from the owner term, so a comparison that folded case would let a listing for one tenant
	/// return a request owned by another — and the key that request's execution destroys belongs to the
	/// other. The same holds for surrounding whitespace: a padded identifier is not the identifier.
	/// </para>
	/// <para>
	/// <b>This is a database-configuration trap as much as a code one, which is why it is a conformance
	/// arm rather than a unit test.</b> A store whose comparison is delegated to a database compares under
	/// that column's collation. A case-insensitive or otherwise non-deterministic collation folds the two
	/// identifiers inside the engine, with correct-looking code above it, so the divergence appears only
	/// on the deployment that configured it that way. Pin a binary or deterministic collation on the
	/// column that holds the owner, and this arm will tell you whether you did.
	/// </para>
	/// </remarks>
	public virtual async Task ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		using var scope = BeginOwnerScope(MixedCaseOwner);
		if (scope is null)
		{
			SkipArm(
				nameof(ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding),
				capability: null,
				"the suite does not override BeginOwnerScope, so this store resolves no named tenant and no "
				+ "owner with a case distinction can be recorded. Ordinal comparison of the tenant filter is "
				+ "UNVERIFIED for this store.");
			return;
		}

		RecordArmExecuted(nameof(ListRequestsAsync_TenantFilterIsOrdinal_ShouldNotFoldCaseOrPadding));

		var request = CreateErasureRequest();
		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);
		var owner = await RecordedOwnerAsync(store, request.RequestId).ConfigureAwait(false);

		if (string.Equals(owner, owner.ToUpperInvariant(), StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"The store recorded '{owner}' as the owner, which carries no case distinction, so the case "
				+ "probe below cannot discriminate. BeginOwnerScope must establish the tenant it is given "
				+ "rather than canonicalising it.");
		}

		// LIVENESS first: the exact term matches. Every probe below asserts a NON-match, and all of them are
		// satisfied by a store that matches nothing.
		var exact = await ListByTenantAsync(store, owner).ConfigureAwait(false);
		if (!exact.Any(r => r.RequestId == request.RequestId))
		{
			throw new TestFixtureAssertionException(
				$"A listing filtered by the exact recorded owner '{owner}' must return its request.");
		}

		// TRAILING-ONLY PADDING IS A SEPARATE PROBE AND IT IS THE ONE THAT BITES. SQL Server follows the
		// ANSI rule that TRAILING blanks are insignificant in a character comparison, and says nothing
		// about leading ones -- so the both-sides probe below differs for a reason the server honours
		// anyway (the leading space) and passes without ever testing the dangerous case. A store can
		// therefore fold a trailing space and satisfy this arm. Measured on real SQL Server.
		foreach (var probe in new[] { owner.ToUpperInvariant(), $" {owner} ", $"{owner} " })
		{
			if (string.Equals(probe, owner, StringComparison.Ordinal))
			{
				continue;
			}

			var results = await ListByTenantAsync(store, probe).ConfigureAwait(false);
			if (results.Any(r => r.RequestId == request.RequestId))
			{
				throw new TestFixtureAssertionException(
					$"A listing filtered by '{probe}' returned a request owned by '{owner}'. These are two "
					+ "different tenants: the owner term is composed into the per-subject key handle exactly "
					+ "as stored, so folding case or padding here hands one tenant's erasure requests to "
					+ "another. If the comparison runs in a database, check the collation on the column "
					+ "holding the owner.");
			}
		}
	}

	/// <summary>
	/// Verifies that ListRequestsAsync filters by date range.
	/// </summary>
	public virtual async Task ListRequestsAsync_WithDateRange_ShouldFilterByDates()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		var oldRequest = CreateErasureRequest();
		// Create request with older RequestedAt by manipulating the test data
		var oldRequestData = new ErasureRequest
		{
			RequestId = GenerateRequestId(),
			DataSubjectId = $"user-{Guid.NewGuid():N}",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test-admin",
			RequestedAt = now.AddDays(-10)
		};
		await store.SaveRequestAsync(oldRequestData, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var recentRequest = CreateErasureRequest();
		await store.SaveRequestAsync(recentRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var results = await GetQueryStore(store).ListRequestsAsync(
			status: null,
			tenantId: null,
			fromDate: now.AddDays(-1),
			toDate: now.AddDays(1),
			pageNumber: 1,
			pageSize: 100,
			CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(r => r.RequestId == recentRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Recent request should be returned within date range");
		}

		if (results.Any(r => r.RequestId == oldRequestData.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Old request should NOT be returned outside date range");
		}
	}

	#endregion

	#region Certificate Tests

	/// <summary>
	/// Verifies that SaveCertificateAsync persists the certificate.
	/// </summary>
	public virtual async Task SaveCertificateAsync_ShouldPersistCertificate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var certificate = CreateErasureCertificate();

		await GetCertificateStore(store).SaveCertificateAsync(certificate, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await GetCertificateStore(store).GetCertificateByIdAsync(certificate.Payload.CertificateId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate with CertificateId {certificate.Payload.CertificateId} was not found after SaveCertificateAsync");
		}

		if (retrieved.Payload.CertificateId != certificate.Payload.CertificateId)
		{
			throw new TestFixtureAssertionException(
				$"CertificateId mismatch. Expected: {certificate.Payload.CertificateId}, Actual: {retrieved.Payload.CertificateId}");
		}

		if (retrieved.Payload.RequestId != certificate.Payload.RequestId)
		{
			throw new TestFixtureAssertionException(
				$"RequestId mismatch. Expected: {certificate.Payload.RequestId}, Actual: {retrieved.Payload.RequestId}");
		}
	}

	/// <summary>
	/// Verifies that SaveCertificateAsync throws <see cref="DuplicateErasureCertificateException"/> for a
	/// duplicate certificate ID — and that a first save of a fresh ID still stores a retrievable certificate.
	/// </summary>
	/// <remarks>
	/// Paired for the same reason as the request arm: a store that refuses every certificate satisfies
	/// "a duplicate throws" and issues no attestation at all.
	/// </remarks>
	public virtual async Task SaveCertificateAsync_DuplicateId_ShouldThrowDuplicateErasureCertificateException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var certificateId = Guid.NewGuid();
		var cert1 = CreateErasureCertificate(certificateId: certificateId);
		var cert2 = CreateErasureCertificate(certificateId: certificateId, requestId: Guid.NewGuid());

		// LIVENESS: the first certificate must persist and be readable back by its own id.
		await GetCertificateStore(store).SaveCertificateAsync(cert1, CancellationToken.None).ConfigureAwait(false);

		var stored = await GetCertificateStore(store)
			.GetCertificateByIdAsync(certificateId, CancellationToken.None).ConfigureAwait(false);
		if (stored is null)
		{
			throw new TestFixtureAssertionException(
				$"A first save of a fresh CertificateId {certificateId} must store a retrievable "
				+ "certificate. The duplicate assertion below is vacuous against a store that accepts nothing.");
		}

		// SAFETY: the re-issue must fail with the type that means "already issued" and nothing else.
		try
		{
			await GetCertificateStore(store).SaveCertificateAsync(cert2, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected DuplicateErasureCertificateException for duplicate CertificateId but no exception was thrown");
		}
		catch (DuplicateErasureCertificateException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that a request holding TWO certificates still resolves by request id, and resolves to the
	/// newest one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm exists because its absence let a P0 ship.</b> The certificates table keys uniqueness on
	/// CertificateId and indexes RequestId NON-uniquely, while both SQL providers read the per-request
	/// certificate with a SINGLE-row query. The moment a request held two certificates, that read threw
	/// InvalidOperationException on every subsequent call, permanently — two signed documents stored and
	/// neither retrievable by the lookup a consumer uses.
	/// </para>
	/// <para>
	/// <b>Two certificates for one request is reachable and legitimate</b>, which is why the fix is a total
	/// ordered read rather than a unique constraint: the partial-completion branch issues one certificate and
	/// the completion branch issues another, so a request that partially completes and later completes holds
	/// both. Forbidding the second would destroy evidence; the lookup simply has to say which is current.
	/// </para>
	/// <para>
	/// <b>RED inputs, both of which this arm detects.</b> Change the per-request read back to a single-row
	/// query and it fails with InvalidOperationException rather than an assertion. Remove the ORDER BY and it
	/// fails the newest-wins assertion — which is why the OLDER certificate is saved FIRST here: an
	/// unordered scan tends to return insertion order, so saving the newer one first would let a store with
	/// no ordering pass by luck.
	/// </para>
	/// </remarks>
	public virtual async Task GetCertificateAsync_WhenRequestHasTwoCertificates_ShouldReturnTheNewest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var requestId = Guid.NewGuid();

		var olderCertificate = CreateErasureCertificate(requestId: requestId);
		olderCertificate = olderCertificate with
		{
			Payload = olderCertificate.Payload with { GeneratedAt = DateTimeOffset.UtcNow.AddHours(-2) },
		};

		var newerCertificate = CreateErasureCertificate(requestId: requestId);
		newerCertificate = newerCertificate with
		{
			Payload = newerCertificate.Payload with { GeneratedAt = DateTimeOffset.UtcNow },
		};

		// Older FIRST, deliberately. See the RED inputs in the remarks.
		await GetCertificateStore(store).SaveCertificateAsync(olderCertificate, CancellationToken.None)
			.ConfigureAwait(false);
		await GetCertificateStore(store).SaveCertificateAsync(newerCertificate, CancellationToken.None)
			.ConfigureAwait(false);

		// TOTALITY: the lookup must ANSWER. Before the fix this threw, and a throw here is the P0 itself
		// rather than a test failure, so it is not caught and reshaped into a friendlier message.
		var resolved = await GetCertificateStore(store)
			.GetCertificateAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		if (resolved is null)
		{
			throw new TestFixtureAssertionException(
				$"A request holding two certificates resolved to NONE. Request {requestId} has certificates "
				+ $"{olderCertificate.Payload.CertificateId} and {newerCertificate.Payload.CertificateId}.");
		}

		// DETERMINISM: and it must be the current one, not whichever the engine happened to scan first.
		if (resolved.Payload.CertificateId != newerCertificate.Payload.CertificateId)
		{
			throw new TestFixtureAssertionException(
				"A request holding two certificates must resolve to the NEWEST. Expected "
				+ $"{newerCertificate.Payload.CertificateId} (generated {newerCertificate.Payload.GeneratedAt:O}) "
				+ $"but got {resolved.Payload.CertificateId} (generated {resolved.Payload.GeneratedAt:O}). "
				+ "A completion certificate is generated after the partial one it supersedes, so returning the "
				+ "older document reports an erasure as less complete than it is.");
		}

		// LIVENESS: both documents must still exist. A store that satisfied the two assertions above by
		// discarding one certificate on write would be destroying signed evidence, which is worse than the
		// defect this arm was written for.
		foreach (var persisted in new[] { olderCertificate, newerCertificate })
		{
			var byId = await GetCertificateStore(store)
				.GetCertificateByIdAsync(persisted.Payload.CertificateId, CancellationToken.None)
				.ConfigureAwait(false);

			if (byId is null)
			{
				throw new TestFixtureAssertionException(
					$"Certificate {persisted.Payload.CertificateId} is no longer retrievable by its own id. "
					+ "Both certificates are signed evidence and neither may be dropped to make the "
					+ "per-request lookup unambiguous.");
			}
		}
	}

	/// <summary>
	/// Verifies that GetCertificateAsync returns certificate by request ID.
	/// </summary>
	public virtual async Task GetCertificateAsync_ByRequestId_ShouldReturnCertificate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var requestId = Guid.NewGuid();
		var certificate = CreateErasureCertificate(requestId: requestId);

		await GetCertificateStore(store).SaveCertificateAsync(certificate, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await GetCertificateStore(store).GetCertificateAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate should be found by RequestId {requestId}");
		}

		if (retrieved.Payload.RequestId != requestId)
		{
			throw new TestFixtureAssertionException(
				$"RequestId mismatch. Expected: {requestId}, Actual: {retrieved.Payload.RequestId}");
		}
	}

	/// <summary>
	/// Verifies that GetCertificateByIdAsync returns certificate by certificate ID.
	/// </summary>
	public virtual async Task GetCertificateByIdAsync_ShouldReturnCertificate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var certificate = CreateErasureCertificate();

		await GetCertificateStore(store).SaveCertificateAsync(certificate, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await GetCertificateStore(store).GetCertificateByIdAsync(certificate.Payload.CertificateId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate should be found by CertificateId {certificate.Payload.CertificateId}");
		}

		if (retrieved.Payload.CertificateId != certificate.Payload.CertificateId)
		{
			throw new TestFixtureAssertionException(
				$"CertificateId mismatch. Expected: {certificate.Payload.CertificateId}, Actual: {retrieved.Payload.CertificateId}");
		}
	}

	/// <summary>
	/// Verifies that a certificate signed before it is stored still verifies after it is read back.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the arm the other certificate arms cannot replace.</b> They compare identifiers, so a store
	/// that dropped every claim on the document — the exemptions, the counts, what was actually erased —
	/// would satisfy all of them. The signature covers the payload whole, so the only check that can tell
	/// you a store returned the certificate it was given is to ask whether the returned certificate still
	/// authenticates.
	/// </para>
	/// <para>
	/// <b>A failure here is not cosmetic.</b> The consumer-visible symptom of a store that loses, truncates
	/// or reshapes any claim is not a missing field — it is
	/// <see cref="ErasureCertificateVerificationResult.SignatureMismatch"/> on a compliance record, which
	/// reads to whoever runs it as though their evidence was interfered with. A store that cannot round-trip
	/// a payload byte-for-byte cannot hold signed certificates at all.
	/// </para>
	/// <para>
	/// The timestamps in the fixture carry sub-microsecond precision on purpose: a column type that cannot
	/// represent a .NET <see cref="DateTimeOffset"/> exactly is exactly the kind of quiet loss this arm is
	/// here to find, and rounding the fixture would hide it.
	/// </para>
	/// </remarks>
	public virtual async Task SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var certificate = CreateSignedErasureCertificate();

		await GetCertificateStore(store).SaveCertificateAsync(certificate, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await GetCertificateStore(store)
			.GetCertificateByIdAsync(certificate.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate with CertificateId {certificate.Payload.CertificateId} was not found after SaveCertificateAsync");
		}

		var outcome = ErasureCertificateVerifier.Verify(retrieved, ConformanceSigningKey);

		if (outcome != ErasureCertificateVerificationResult.Verified)
		{
			throw new TestFixtureAssertionException(
				$"A certificate read back from this store no longer verifies: {outcome}. The store did not return "
				+ "the payload it was given, so the signature cannot be recomputed over it. Whichever claim the "
				+ "store drops, truncates or reshapes, the consumer sees a compliance record reporting as altered. "
				+ "Compare the stored columns with every property on ErasureCertificatePayload -- including the "
				+ "precision of the timestamp columns, which must hold a .NET DateTimeOffset exactly.");
		}
	}

	/// <summary>
	/// Verifies that the arm above can fail: an altered payload must NOT verify.
	/// </summary>
	/// <remarks>
	/// Without this, a verifier that returned <see cref="ErasureCertificateVerificationResult.Verified"/>
	/// unconditionally would satisfy the round-trip arm while establishing nothing. This arm runs entirely
	/// against the store's own output, so it also confirms the key and scheme the round-trip arm relies on
	/// are the ones actually in play.
	/// </remarks>
	public virtual async Task SaveCertificateAsync_ShouldNotVerifyACertificateWhoseClaimsWereAltered()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var certificate = CreateSignedErasureCertificate();

		await GetCertificateStore(store).SaveCertificateAsync(certificate, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await GetCertificateStore(store)
			.GetCertificateByIdAsync(certificate.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate with CertificateId {certificate.Payload.CertificateId} was not found after SaveCertificateAsync");
		}

		// One claim, changed the way an alteration after issue would change it.
		var altered = retrieved with
		{
			Payload = retrieved.Payload with
			{
				Summary = retrieved.Payload.Summary with { RecordsAffected = retrieved.Payload.Summary.RecordsAffected + 1 },
			},
		};

		var outcome = ErasureCertificateVerifier.Verify(altered, ConformanceSigningKey);

		if (outcome != ErasureCertificateVerificationResult.SignatureMismatch)
		{
			throw new TestFixtureAssertionException(
				$"An altered certificate reported {outcome} rather than SignatureMismatch. The round-trip arm "
				+ "beside this one is therefore vacuous: it cannot distinguish a store that returns what it was "
				+ "given from one that returns anything at all.");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupExpiredCertificatesAsync removes expired certificates.
	/// </summary>
	public virtual async Task CleanupExpiredCertificatesAsync_ShouldRemoveExpired()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create expired certificate (RetainUntil in the past)
		var expiredCertificate = CreateErasureCertificate(retainUntil: DateTimeOffset.UtcNow.AddMinutes(-5));
		await GetCertificateStore(store).SaveCertificateAsync(expiredCertificate, CancellationToken.None).ConfigureAwait(false);

		// Verify certificate exists
		var beforeCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(expiredCertificate.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);
		if (beforeCleanup is null)
		{
			throw new TestFixtureAssertionException(
				"Certificate should exist before cleanup");
		}

		var removedCount = await GetCertificateStore(store).CleanupExpiredCertificatesAsync(CancellationToken.None).ConfigureAwait(false);

		if (removedCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 expired certificate to be removed, got {removedCount}");
		}

		// Verify certificate was removed
		var afterCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(expiredCertificate.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);
		if (afterCleanup is not null)
		{
			throw new TestFixtureAssertionException(
				"Expired certificate should be removed after cleanup");
		}
	}

	/// <summary>
	/// Verifies that CleanupExpiredCertificatesAsync keeps valid certificates.
	/// </summary>
	public virtual async Task CleanupExpiredCertificatesAsync_ShouldKeepValid()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create valid certificate (RetainUntil in the future)
		var validCertificate = CreateErasureCertificate(retainUntil: DateTimeOffset.UtcNow.AddYears(7));
		await GetCertificateStore(store).SaveCertificateAsync(validCertificate, CancellationToken.None).ConfigureAwait(false);

		_ = await GetCertificateStore(store).CleanupExpiredCertificatesAsync(CancellationToken.None).ConfigureAwait(false);

		// Verify certificate still exists
		var afterCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(validCertificate.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);
		if (afterCleanup is null)
		{
			throw new TestFixtureAssertionException(
				"Valid certificate should be kept after cleanup");
		}
	}

	#endregion

	#region Duplicate Translation Narrowness

	/// <summary>
	/// Gets a <see cref="ErasureRequest.RequestedBy"/> value long enough that a store backed by a
	/// width-constrained column rejects it, producing a failure that is <b>not</b> a duplicate.
	/// </summary>
	/// <value>
	/// A string far longer than any plausible column width. Override to supply a different non-duplicate
	/// trigger, or to shorten it if a store rejects at a lower bound for an unrelated reason.
	/// </value>
	/// <remarks>
	/// Deliberately a property rather than an abstract method. An abstract member would oblige every
	/// consumer already deriving this kit to implement it before their suite compiles again, and the arm
	/// below is written so that a store which simply <i>accepts</i> this value is treated as conforming.
	/// A store with no width constraint therefore needs no override and loses nothing.
	/// </remarks>
	protected virtual string NonDuplicateFailureRequestedBy => new('x', 100_000);

	/// <summary>
	/// Verifies that the store translates <b>only</b> the duplicate-key condition, and that a failure which
	/// is not a duplicate reaches the caller as something other than
	/// <see cref="DuplicateErasureRequestException"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other duplicate assertion in this kit is satisfied perfectly by a store whose save is wrapped
	/// in a blanket catch that reports <i>every</i> failure as a duplicate. That store is not merely
	/// imprecise: a caller told "the request is already on file" when the real fault was a dropped
	/// connection, a timeout, or a value too long for its column treats a write that never happened as
	/// already done and never re-files it. An erasure request is a data subject's exercise of a statutory
	/// right, so a request dropped that way is not recoverable by anything the caller does later.
	/// </para>
	/// <para>
	/// The trigger is provider-neutral by construction rather than by naming an engine. A store backed by
	/// a width-constrained column rejects an over-long value with its own native failure, and this arm
	/// requires that failure to arrive as anything other than a duplicate. A store with no such
	/// constraint accepts the value instead, and that branch is asserted too — the request must be
	/// readable back — so neither outcome is a silent pass. What the arm forbids is the third outcome: a
	/// store that answers "already on file" for a condition that is nothing of the sort.
	/// </para>
	/// <para>
	/// The final step is the liveness half, and it is what stops the fix from being "never translate
	/// anything". A filter narrowed until it no longer fires would satisfy everything above; requiring a
	/// genuine duplicate to still raise <see cref="DuplicateErasureRequestException"/> after the
	/// non-duplicate trigger has run is what makes the safety half mean something.
	/// </para>
	/// </remarks>
	public virtual async Task SaveRequestAsync_NonDuplicateFailure_ShouldNotTranslateToDuplicate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		var triggerId = GenerateRequestId();
		var trigger = CreateErasureRequest(requestId: triggerId) with
		{
			RequestedBy = NonDuplicateFailureRequestedBy,
		};

		var accepted = false;
		try
		{
			await store.SaveRequestAsync(trigger, scheduledTime, CancellationToken.None).ConfigureAwait(false);
			accepted = true;
		}
		catch (DuplicateErasureRequestException ex)
		{
			// SAFETY. This identifier has never been saved, so "already on file" cannot be true. The store
			// is reporting a non-duplicate failure as a duplicate, which is the condition this arm exists
			// to catch — a caller branching on the type would abandon a write that never landed.
			throw new TestFixtureAssertionException(
				$"SaveRequestAsync reported DuplicateErasureRequestException for RequestId {triggerId}, "
				+ "which had never been saved. Only the duplicate-key condition may be translated; every "
				+ "other failure must reach the caller unchanged. A blanket catch around the save, or an "
				+ "exception filter that matches more than the unique-constraint violation, produces "
				+ "exactly this. The store's own failure was: "
				+ (ex.InnerException?.ToString() ?? "(not preserved as InnerException)"),
				ex);
		}
#pragma warning disable CA1031 // Any other exception is the conforming outcome: the failure surfaced unchanged.
		catch (Exception)
#pragma warning restore CA1031
		{
			// CONFORMING. The store rejected the value and said so in its own terms rather than
			// mistranslating it. Nothing further to assert about the type: this kit deliberately does not
			// name any engine's exception, because doing so would make it underivable for the next one.
		}

		if (accepted)
		{
			// The store has no width constraint, so no failure was induced and nothing could have been
			// mistranslated. Assert the accepted path rather than returning quietly — a store that
			// swallowed the save would otherwise reach the end of this arm looking conforming.
			var storedTrigger = await store.GetStatusAsync(triggerId, CancellationToken.None).ConfigureAwait(false);
			if (storedTrigger is null)
			{
				throw new TestFixtureAssertionException(
					$"SaveRequestAsync accepted RequestId {triggerId} without error, so it must be "
					+ "retrievable. A save that neither throws nor stores is a silent data loss on the "
					+ "erasure path.");
			}
		}

		// LIVENESS. A store that satisfies everything above by never translating anything is not
		// conforming either, so a genuine duplicate must still be reported as one.
		var duplicateId = GenerateRequestId();
		var first = CreateErasureRequest(requestId: duplicateId);
		var second = CreateErasureRequest(requestId: duplicateId);

		await store.SaveRequestAsync(first, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		try
		{
			await store.SaveRequestAsync(second, scheduledTime, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				$"A second save of RequestId {duplicateId} must raise DuplicateErasureRequestException. "
				+ "Nothing was thrown, so the duplicate condition is not being reported at all.");
		}
		catch (DuplicateErasureRequestException)
		{
			// Expected: the narrowing above did not cost the store its duplicate signal.
		}
	}

	#endregion

}
