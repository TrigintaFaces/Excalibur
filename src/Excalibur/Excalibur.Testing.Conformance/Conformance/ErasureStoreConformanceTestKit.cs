// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Compliance;

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
/// <item><description>Erasure certificates for compliance proof with retention periods</description></item>
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
			Signature = $"sig-{Guid.NewGuid():N}",
			RetainUntil = retainUntil ?? DateTimeOffset.UtcNow.AddYears(7)
		};

	/// <summary>
	/// Generates a unique request ID for test isolation.
	/// </summary>
	/// <returns>A unique request identifier.</returns>
	protected virtual Guid GenerateRequestId() => Guid.NewGuid();

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
	/// Verifies that UpdateStatusAsync changes the status.
	/// </summary>
	public virtual async Task UpdateStatusAsync_ShouldUpdateStatus()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var request = CreateErasureRequest();
		var scheduledTime = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduledTime, CancellationToken.None).ConfigureAwait(false);

		var updated = await store.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.InProgress,
			null,
			CancellationToken.None).ConfigureAwait(false);

		if (!updated)
		{
			throw new TestFixtureAssertionException(
				"UpdateStatusAsync should return true for existing request");
		}

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after update");
		}

		if (status.Status != ErasureRequestStatus.InProgress)
		{
			throw new TestFixtureAssertionException(
				$"Status should be InProgress after update. Actual: {status.Status}");
		}
	}

	/// <summary>
	/// Verifies that UpdateStatusAsync sets ExecutedAt when status changes to InProgress.
	/// </summary>
	public virtual async Task UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt()
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
				"ExecutedAt should be null before status update to InProgress");
		}

		_ = await store.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.InProgress,
			null,
			CancellationToken.None).ConfigureAwait(false);

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		if (status is null)
		{
			throw new TestFixtureAssertionException(
				"Request should be found after update");
		}

		if (status.ExecutedAt is null)
		{
			throw new TestFixtureAssertionException(
				"ExecutedAt should be set when status changes to InProgress");
		}
	}

	/// <summary>
	/// Verifies that UpdateStatusAsync returns false for non-existent request.
	/// </summary>
	public virtual async Task UpdateStatusAsync_NonExistent_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateRequestId();

		var updated = await store.UpdateStatusAsync(
			nonExistentId,
			ErasureRequestStatus.InProgress,
			null,
			CancellationToken.None).ConfigureAwait(false);

		if (updated)
		{
			throw new TestFixtureAssertionException(
				"UpdateStatusAsync should return false for non-existent RequestId");
		}
	}

	#endregion

	#region Completion Tests

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

		await store.RecordCompletionAsync(
			request.RequestId,
			keysDeleted: 10,
			recordsAffected: 500,
			certificateId,
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
	/// Verifies that RecordCompletionAsync throws KeyNotFoundException for non-existent request.
	/// </summary>
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

		// Change status to Pending first (SaveRequest sets Scheduled)
		_ = await store.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.Pending,
			null,
			CancellationToken.None).ConfigureAwait(false);

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

		// Change status to InProgress
		_ = await store.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.InProgress,
			null,
			CancellationToken.None).ConfigureAwait(false);

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
		await store.RecordCompletionAsync(completedRequest.RequestId, 0, 0, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

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
	/// Verifies that ListRequestsAsync filters by tenant.
	/// </summary>
	public virtual async Task ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var tenantARequest = CreateErasureRequest(tenantId: "tenant-A");
		await store.SaveRequestAsync(tenantARequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var tenantBRequest = CreateErasureRequest(tenantId: "tenant-B");
		await store.SaveRequestAsync(tenantBRequest, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		var results = await GetQueryStore(store).ListRequestsAsync(
			status: null,
			tenantId: "tenant-A",
			fromDate: null,
			toDate: null,
			pageNumber: 1,
			pageSize: 100,
			CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(r => r.RequestId == tenantARequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-A request should be returned when filtering by tenant-A");
		}

		if (results.Any(r => r.RequestId == tenantBRequest.RequestId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-B request should NOT be returned when filtering by tenant-A");
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

		var retrieved = await GetCertificateStore(store).GetCertificateByIdAsync(certificate.CertificateId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate with CertificateId {certificate.CertificateId} was not found after SaveCertificateAsync");
		}

		if (retrieved.CertificateId != certificate.CertificateId)
		{
			throw new TestFixtureAssertionException(
				$"CertificateId mismatch. Expected: {certificate.CertificateId}, Actual: {retrieved.CertificateId}");
		}

		if (retrieved.RequestId != certificate.RequestId)
		{
			throw new TestFixtureAssertionException(
				$"RequestId mismatch. Expected: {certificate.RequestId}, Actual: {retrieved.RequestId}");
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

		if (retrieved.RequestId != requestId)
		{
			throw new TestFixtureAssertionException(
				$"RequestId mismatch. Expected: {requestId}, Actual: {retrieved.RequestId}");
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

		var retrieved = await GetCertificateStore(store).GetCertificateByIdAsync(certificate.CertificateId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Certificate should be found by CertificateId {certificate.CertificateId}");
		}

		if (retrieved.CertificateId != certificate.CertificateId)
		{
			throw new TestFixtureAssertionException(
				$"CertificateId mismatch. Expected: {certificate.CertificateId}, Actual: {retrieved.CertificateId}");
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
		var beforeCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(expiredCertificate.CertificateId, CancellationToken.None)
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
		var afterCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(expiredCertificate.CertificateId, CancellationToken.None)
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
		var afterCleanup = await GetCertificateStore(store).GetCertificateByIdAsync(validCertificate.CertificateId, CancellationToken.None)
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
