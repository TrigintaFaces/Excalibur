using Excalibur.Dispatch;
using Excalibur.Compliance.Erasure;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="InMemoryErasureStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class InMemoryErasureStoreShould
{
	private readonly InMemoryErasureStore _sut = new(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }), TimeProvider.System);

	#region SaveRequestAsync Tests

	[Fact]
	public async Task SaveRequestAsync_StoresRequest()
	{
		// Arrange
		var request = CreateRequest();

		// Act
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.RequestCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveRequestAsync_ThrowsDuplicateErasureRequestException_WhenRequestAlreadyExists()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the first save must be visible. A store that accepted nothing would pass the assertion
		// below while storing no erasure request at all.
		var stored = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull(
			"a first save of a fresh RequestId must store a retrievable request, or the duplicate "
			+ "assertion below proves nothing");

		// SAFETY: the specific type, not the base. An unprovisioned schema, a disposed store and an
		// unresolved tenant all surface as InvalidOperationException, so a caller branching on the base
		// type would treat a request that was never stored as already on file and never re-file it.
		var thrown = await Should.ThrowAsync<DuplicateErasureRequestException>(
			() => _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None)).ConfigureAwait(false);

		thrown.RequestId.ShouldBe(
			request.RequestId,
			"the exception must name the request that was re-filed so a caller can act on it");
	}

	[Fact]
	public async Task SaveRequestAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// A missing argument is the caller's mistake, reported as such — matching what both SQL stores
		// already do, rather than dereferencing null and surfacing a NullReferenceException.
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveRequestAsync(null!, DateTimeOffset.UtcNow, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region GetStatusAsync Tests

	[Fact]
	public async Task GetStatusAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetStatusAsync_ReturnsStatus_WhenRequestExists()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.RequestId.ShouldBe(request.RequestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
	}

	#endregion

	#region UpdateStatusAsync Tests

	[Fact]
	public async Task UpdateStatusAsync_ReturnsFalse_WhenNotFound()
	{
		var result = await _sut.ClaimForExecutionAsync(
			Guid.NewGuid(), ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		// A VALUE, never an exception, and named: a caller must be able to tell "no such request" from
		// "this store cannot answer", and only one of those is worth reporting as a defect.
		result.Outcome.ShouldBe(ErasureClaimOutcome.NotFound);
	}

	[Fact]
	public async Task UpdateStatusAsync_UpdatesStatus()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ClaimForExecutionAsync(
			request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Outcome.ShouldBe(ErasureClaimOutcome.Granted);
		result.ClaimIdentity.ShouldNotBeNullOrWhiteSpace(
			"a granted claim must carry the identity its holder presents on every fenced write");
		var status = await _sut.GetStatusAsync(request.RequestId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.InProgress);
	}

	[Fact]
	public async Task UpdateStatusAsync_SetsExecutedAt_WhenStatusIsInProgress()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act
		// ExecutedAt is stamped by TAKING CUSTODY, which is the step that advances the status. Writing
		// the claimed status through the general member is refused outright: custody has one door.
		_ = await _sut.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var status = await _sut.GetStatusAsync(request.RequestId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
		status.ExecutedAt.ShouldNotBeNull();
	}

	#endregion

	#region RecordCompletionAsync Tests

	[Fact]
	public async Task RecordCompletionAsync_ThrowsKeyNotFoundException_WhenNotFound()
	{
		await Should.ThrowAsync<KeyNotFoundException>(
			() => _sut.RecordCompletionAsync(Guid.NewGuid(), 5, 10, Guid.NewGuid(), "test-claim-identity", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordCompletionAsync_UpdatesRequestWithCompletionData()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		var certId = Guid.NewGuid();

		// Act
		var claim = await _sut.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		claim.Outcome.ShouldBe(ErasureClaimOutcome.Granted);
		// Presents the identity the store granted, which is what makes this a fenced write
		// rather than an unconditional one.
		_ = await _sut.RecordCompletionAsync(request.RequestId, 3, 100, certId, claim.ClaimIdentity!, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.Completed);
		status.KeysDeleted.ShouldBe(3);
		status.RecordsAffected.ShouldBe(100);
		status.CertificateId.ShouldBe(certId);
		status.CompletedAt.ShouldNotBeNull();
	}

	#endregion

	#region RecordCancellationAsync Tests

	[Fact]
	public async Task RecordCancellationAsync_ReturnsFalse_WhenNotFound()
	{
		var result = await _sut.RecordCancellationAsync(Guid.NewGuid(), "reason", "admin", CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RecordCancellationAsync_ReturnsFalse_WhenStatusIsNotCancellable()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		var claim = await _sut.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		claim.Outcome.ShouldBe(ErasureClaimOutcome.Granted);
		// Presents the identity the store granted, which is what makes this a fenced write
		// rather than an unconditional one.
		_ = await _sut.RecordCompletionAsync(request.RequestId, 0, 0, Guid.NewGuid(), claim.ClaimIdentity!, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RecordCancellationAsync(request.RequestId, "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RecordCancellationAsync_ReturnsTrue_WhenScheduled()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RecordCancellationAsync(request.RequestId, "reason", "admin", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		status.Status.ShouldBe(ErasureRequestStatus.Cancelled);
		status.CancellationReason.ShouldBe("reason");
		status.CancelledBy.ShouldBe("admin");
	}

	#endregion

	#region ListRequestsAsync Tests

	[Fact]
	public async Task ListRequestsAsync_ReturnsEmpty_WhenNoRequests()
	{
		var results = await _sut.ListRequestsAsync(null, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListRequestsAsync_FiltersByStatus()
	{
		// Arrange
		var request1 = CreateRequest();
		var request2 = CreateRequest();
		await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		var claim = await _sut.ClaimForExecutionAsync(
				request1.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		claim.Outcome.ShouldBe(ErasureClaimOutcome.Granted);
		// Presents the identity the store granted, which is what makes this a fenced write
		// rather than an unconditional one.
		_ = await _sut.RecordCompletionAsync(request1.RequestId, 0, 0, Guid.NewGuid(), claim.ClaimIdentity!, CancellationToken.None).ConfigureAwait(false);

		// Act
		var scheduled = await _sut.ListRequestsAsync(ErasureRequestStatus.Scheduled, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);
		var completed = await _sut.ListRequestsAsync(ErasureRequestStatus.Completed, null, null, null, 1, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.Count.ShouldBe(1);
		completed.Count.ShouldBe(1);
	}

	/// <summary>
	/// The tenant filter discriminates between two requests whose owners the HOST resolved.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm used to seed its two tenants by naming them on the requests, and that is the defect it
	/// now guards against.</b> A request's owning tenant is resolved by the deployment, never taken from
	/// the request, because the owner is composed into the per-subject key handle the erasure destroys: an
	/// owner the write path never used names a key that was never minted, the provider reports it absent,
	/// the erasure treats that as an idempotent no-op, and the request reaches a completed state with a
	/// signed certificate over data that still decrypts. Seeding by request property asserted exactly the
	/// behaviour that produces that outcome.
	/// </para>
	/// <para>
	/// So the two owners come from the ambient context instead, which is how a multi-tenant host produces
	/// them, and the filter is asserted against the owners the store actually recorded rather than against
	/// the constants this arm asked for.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ListRequestsAsync_FiltersByTheOwnerTheHostResolved()
	{
		var store = new InMemoryErasureStore(
			TestDataSubjectHasher.Instance,
			TenantContextHolder.AmbientContext,
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }),
			TimeProvider.System);

		var first = CreateRequest();
		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			await store.SaveRequestAsync(first, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		}

		var second = CreateRequest();
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveRequestAsync(second, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		}

		// The fixture's own precondition. If the ambient scope did not reach the owner the store recorded,
		// both assertions below would compare one tenant against itself and pass.
		var firstStatus = await store.GetStatusAsync(first.RequestId, CancellationToken.None).ConfigureAwait(false);
		firstStatus.ShouldNotBeNull();
		firstStatus.TenantId.ShouldBe("tenant-a", "the host's ambient term is the owner, not the request's");

		var results = await store
			.ListRequestsAsync(null, "tenant-a", null, null, 1, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS: the owner's own request is returned. A store returning nothing satisfies the safety
		// assertion below and serves no caller.
		results.Select(r => r.RequestId).ShouldContain(first.RequestId);

		// SAFETY: the other owner's request is not. Fails against a store that drops the argument, and
		// against one that applies it as an inequality.
		results.Select(r => r.RequestId).ShouldNotContain(second.RequestId);
	}

	#endregion

	#region Certificate Tests

	[Fact]
	public async Task SaveCertificateAsync_ThrowsDuplicateErasureCertificateException_WhenAlreadyExists()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the first certificate must persist and be readable back by its own id.
		var stored = await _sut.GetCertificateByIdAsync(cert.Payload.CertificateId, CancellationToken.None)
			.ConfigureAwait(false);
		stored.ShouldNotBeNull(
			"a first save of a fresh CertificateId must store a retrievable certificate, or the duplicate "
			+ "assertion below proves nothing");

		// SAFETY: the re-issue must fail with the type that means "already issued" and nothing else.
		var thrown = await Should.ThrowAsync<DuplicateErasureCertificateException>(
			() => _sut.SaveCertificateAsync(cert, CancellationToken.None)).ConfigureAwait(false);

		thrown.CertificateId.ShouldBe(cert.Payload.CertificateId);
	}

	[Fact]
	public async Task SaveCertificateAsync_ThrowsArgumentNullException_WhenCertificateIsNull()
	{
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveCertificateAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GetCertificateAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetCertificateAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCertificateAsync_ReturnsCertificate_WhenMappingExists()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetCertificateAsync(cert.Payload.RequestId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Payload.CertificateId.ShouldBe(cert.Payload.CertificateId);
	}

	[Fact]
	public async Task GetCertificateByIdAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetCertificateByIdAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetCertificateByIdAsync_ReturnsCertificate()
	{
		// Arrange
		var cert = CreateCertificate();
		await _sut.SaveCertificateAsync(cert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetCertificateByIdAsync(cert.Payload.CertificateId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Payload.CertificateId.ShouldBe(cert.Payload.CertificateId);
	}

	#endregion

	#region CleanupExpiredCertificatesAsync Tests

	[Fact]
	public async Task CleanupExpiredCertificatesAsync_RemovesExpiredCertificates()
	{
		// Arrange
		var expiredCert = CreateCertificate(retainUntil: DateTimeOffset.UtcNow.AddDays(-1));
		var validCert = CreateCertificate(retainUntil: DateTimeOffset.UtcNow.AddDays(30));
		await _sut.SaveCertificateAsync(expiredCert, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveCertificateAsync(validCert, CancellationToken.None).ConfigureAwait(false);

		// Act
		var count = await _sut.CleanupExpiredCertificatesAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		count.ShouldBe(1);
		_sut.CertificateCount.ShouldBe(1);
	}

	#endregion

	#region GetScheduledRequestsAsync Tests

	[Fact]
	public async Task GetScheduledRequestsAsync_ReturnsOnlyDueScheduledRequests()
	{
		// Arrange
		var request1 = CreateRequest();
		var request2 = CreateRequest();
		await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetScheduledRequestsAsync(100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].RequestId.ShouldBe(request1.RequestId);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllData()
	{
		// Arrange
		var request = CreateRequest();
		await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveCertificateAsync(CreateCertificate(), CancellationToken.None).ConfigureAwait(false);

		// Act
		_sut.Clear();

		// Assert
		_sut.RequestCount.ShouldBe(0);
		_sut.CertificateCount.ShouldBe(0);
	}

	#endregion

	#region Helpers

	private static ErasureRequest CreateRequest(string? tenantId = null) =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = $"user-{Guid.NewGuid():N}",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			TenantId = tenantId,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

	private static ErasureCertificate CreateCertificate(DateTimeOffset? retainUntil = null) =>
		new()
		{
			// Every claim lives in the payload; the envelope carries only the payload and its signature.
			// A store test stores and reloads the envelope, so a fixed signature string is adequate here --
			// a store's guarantee is FIDELITY (what was saved comes back), not signature validity.
			Payload = new ErasureCertificatePayload
			{
				CertificateId = Guid.NewGuid(),
				RequestId = Guid.NewGuid(),
				DataSubjectReference = "hash-value",
				RequestReceivedAt = DateTimeOffset.UtcNow.AddDays(-1),
				CompletedAt = DateTimeOffset.UtcNow,
				Method = ErasureMethod.CryptographicErasure,
				Summary = new ErasureSummary(),
				Verification = new VerificationSummary
				{
					Verified = true,
					Methods = VerificationMethod.KeyManagementSystem,
					VerifiedAt = DateTimeOffset.UtcNow
				},
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				RetainUntil = retainUntil ?? DateTimeOffset.UtcNow.AddDays(365)
			},
			Signature = "test-sig"
		};

	#endregion
}
