// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="LegalHoldService"/>.
/// Tests GDPR Article 17(3) legal hold processing per ADR-054.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LegalHoldServiceShould
{
	private readonly ILegalHoldStore _store;
	private readonly ILegalHoldQueryStore _queryStore;
	private readonly LegalHoldService _sut;

	public LegalHoldServiceShould()
	{
		_store = A.Fake<ILegalHoldStore>();
		_queryStore = A.Fake<ILegalHoldQueryStore>();

		// Wire up GetService to return the query sub-store (required before SUT construction)
		_ = A.CallTo(() => _store.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(_queryStore);

		_sut = new LegalHoldService(_store, NullLogger<LegalHoldService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LegalHoldService(
			null!,
			NullLogger<LegalHoldService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new LegalHoldService(
			_store,
			null!));
	}

	#endregion Constructor Tests

	#region CreateHoldAsync Tests

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.CreateHoldAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentException_WhenNeitherDataSubjectIdNorTenantId()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = null,
			TenantId = null,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test hold",
			CreatedBy = "legal@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentException_WhenCaseReferenceIsEmpty()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = string.Empty,
			Description = "Test hold",
			CreatedBy = "legal@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentException_WhenDescriptionIsEmpty()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = string.Empty,
			CreatedBy = "legal@test.com"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentException_WhenCreatedByIsEmpty()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test hold",
			CreatedBy = string.Empty
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_ThrowsArgumentException_WhenExpiresAtIsInPast()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test hold",
			CreatedBy = "legal@test.com",
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task CreateHoldAsync_CreatesHold_WithDataSubjectIdOnly()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test hold",
			CreatedBy = "legal@test.com"
		};

		// Act
		var result = await _sut.CreateHoldAsync(request, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.HoldId.ShouldNotBe(Guid.Empty);
		result.Basis.ShouldBe(LegalHoldBasis.LitigationHold);
		result.CaseReference.ShouldBe("CASE-001");
		result.IsActive.ShouldBeTrue();
		result.DataSubjectIdHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task CreateHoldAsync_CreatesHold_WithTenantIdOnly()
	{
		// Arrange
		var request = new LegalHoldRequest
		{
			TenantId = "tenant-123",
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "REG-001",
			Description = "Regulatory investigation",
			CreatedBy = "legal@test.com"
		};

		// Act
		var result = await _sut.CreateHoldAsync(request, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.TenantId.ShouldBe("tenant-123");
		result.DataSubjectIdHash.ShouldBeNull();
	}

	[Fact]
	public async Task CreateHoldAsync_HashesDataSubjectId()
	{
		// Arrange
		var request = CreateValidHoldRequest();

		// Act
		var result = await _sut.CreateHoldAsync(request, CancellationToken.None);

		// Assert
		// Hash should be SHA-256 hex string (64 chars)
		_ = result.DataSubjectIdHash.ShouldNotBeNull();
		result.DataSubjectIdHash.Length.ShouldBe(64);
	}

	[Fact]
	public async Task CreateHoldAsync_SavesHoldToStore()
	{
		// Arrange
		var request = CreateValidHoldRequest();

		// Act
		_ = await _sut.CreateHoldAsync(request, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.SaveHoldAsync(
			A<LegalHold>.That.Matches(h =>
				h.CaseReference == request.CaseReference &&
				h.IsActive == true),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateHoldAsync_SetsCorrectTimestamps()
	{
		// Arrange
		var request = CreateValidHoldRequest();
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.CreateHoldAsync(request, CancellationToken.None);
		var after = DateTimeOffset.UtcNow;

		// Assert
		result.CreatedAt.ShouldBeInRange(before, after);
	}

	[Theory]
	[InlineData(LegalHoldBasis.LitigationHold)]
	[InlineData(LegalHoldBasis.RegulatoryInvestigation)]
	[InlineData(LegalHoldBasis.LegalClaims)]
	[InlineData(LegalHoldBasis.LegalObligation)]
	public async Task CreateHoldAsync_PreservesAllBasisTypes(LegalHoldBasis basis)
	{
		// Arrange
		var request = CreateValidHoldRequest() with { Basis = basis };

		// Act
		var result = await _sut.CreateHoldAsync(request, CancellationToken.None);

		// Assert
		result.Basis.ShouldBe(basis);
	}

	#endregion CreateHoldAsync Tests

	#region ReleaseHoldAsync Tests

	[Fact]
	public async Task ReleaseHoldAsync_ThrowsArgumentException_WhenReasonIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ReleaseHoldAsync(Guid.NewGuid(), string.Empty, "admin", CancellationToken.None));
	}

	[Fact]
	public async Task ReleaseHoldAsync_ThrowsArgumentException_WhenReleasedByIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ReleaseHoldAsync(Guid.NewGuid(), "reason", string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task ReleaseHoldAsync_ThrowsKeyNotFoundException_WhenHoldNotFound()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns((LegalHold?)null);

		// Act & Assert
		_ = await Should.ThrowAsync<KeyNotFoundException>(() =>
			_sut.ReleaseHoldAsync(holdId, "Litigation concluded", "legal@test.com", CancellationToken.None));
	}

	[Fact]
	public async Task ReleaseHoldAsync_ThrowsInvalidOperation_WhenAlreadyReleased()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = false,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
		};

		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns(hold);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ReleaseHoldAsync(holdId, "Litigation concluded", "legal@test.com", CancellationToken.None));
	}

	[Fact]
	public async Task ReleaseHoldAsync_ReleasesHold_WhenActive()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
		};

		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns(hold);

		// Act
		await _sut.ReleaseHoldAsync(holdId, "Litigation concluded", "admin@test.com", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.UpdateHoldAsync(
			A<LegalHold>.That.Matches(h =>
				h.HoldId == holdId &&
				h.IsActive == false &&
				h.ReleasedBy == "admin@test.com" &&
				h.ReleaseReason == "Litigation concluded"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReleaseHoldAsync_SetsReleasedAt_ToCurrentTime()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
		};

		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns(hold);

		LegalHold? capturedHold = null;
		_ = A.CallTo(() => _store.UpdateHoldAsync(A<LegalHold>._, A<CancellationToken>._))
			.Invokes(call => capturedHold = call.GetArgument<LegalHold>(0));

		var before = DateTimeOffset.UtcNow;

		// Act
		await _sut.ReleaseHoldAsync(holdId, "Reason", "admin@test.com", CancellationToken.None);

		var after = DateTimeOffset.UtcNow;

		// Assert
		_ = capturedHold.ReleasedAt.ShouldNotBeNull();
		capturedHold.ReleasedAt.Value.ShouldBeInRange(before, after);
	}

	#endregion ReleaseHoldAsync Tests

	#region CheckHoldsAsync Tests

	[Fact]
	public async Task CheckHoldsAsync_ThrowsArgumentException_WhenDataSubjectIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CheckHoldsAsync(string.Empty, DataSubjectIdType.UserId, null, CancellationToken.None));
	}

	[Fact]
	public async Task CheckHoldsAsync_ReturnsNoHolds_WhenNoneExist()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<LegalHold>());

		// Act
		var result = await _sut.CheckHoldsAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.HasActiveHolds.ShouldBeFalse();
		result.ErasureBlocked.ShouldBeFalse();
		result.ActiveHolds.ShouldBeEmpty();
	}

	[Fact]
	public async Task CheckHoldsAsync_ReturnsHolds_WhenDataSubjectHoldsExist()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			DataSubjectIdHash = "hash",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(new[] { hold });

		// Act
		var result = await _sut.CheckHoldsAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.HasActiveHolds.ShouldBeTrue();
		result.ErasureBlocked.ShouldBeTrue();
		result.ActiveHolds.Count.ShouldBe(1);
		result.ActiveHolds[0].HoldId.ShouldBe(holdId);
	}

	[Fact]
	public async Task CheckHoldsAsync_IncludesTenantWideHolds()
	{
		// Arrange
		var subjectHold = new LegalHold
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			TenantId = "tenant-1",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Subject hold",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow
		};

		var tenantHold = new LegalHold
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = null, // Tenant-wide
			TenantId = "tenant-1",
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "REG-001",
			Description = "Tenant hold",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._,
			"tenant-1",
			A<CancellationToken>._))
			.Returns(new[] { subjectHold });

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForTenantAsync(
			"tenant-1",
			A<CancellationToken>._))
			.Returns(new[] { subjectHold, tenantHold });

		// Act
		var result = await _sut.CheckHoldsAsync("user-123", DataSubjectIdType.UserId, "tenant-1", CancellationToken.None);

		// Assert
		result.ActiveHolds.Count.ShouldBe(2);
	}

	[Fact]
	public async Task CheckHoldsAsync_DoesNotDuplicateHolds()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			DataSubjectIdHash = "hash",
			TenantId = "tenant-1",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(new[] { hold });

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForTenantAsync(
			"tenant-1",
			A<CancellationToken>._))
			.Returns(new[] { hold }); // Same hold returned

		// Act
		var result = await _sut.CheckHoldsAsync("user-123", DataSubjectIdType.UserId, "tenant-1", CancellationToken.None);

		// Assert
		result.ActiveHolds.Count.ShouldBe(1); // No duplicates
	}

	[Fact]
	public async Task CheckHoldsAsync_MapsToLegalHoldInfo()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
		var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
		var hold = new LegalHold
		{
			HoldId = holdId,
			DataSubjectIdHash = "hash",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			ExpiresAt = expiresAt,
			CreatedBy = "legal@test.com",
			CreatedAt = createdAt
		};

		_ = A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(new[] { hold });

		// Act
		var result = await _sut.CheckHoldsAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		var holdInfo = result.ActiveHolds[0];
		holdInfo.HoldId.ShouldBe(holdId);
		holdInfo.Basis.ShouldBe(LegalHoldBasis.LitigationHold);
		holdInfo.CaseReference.ShouldBe("CASE-001");
		holdInfo.CreatedAt.ShouldBe(createdAt);
		holdInfo.ExpiresAt.ShouldBe(expiresAt);
	}

	#endregion CheckHoldsAsync Tests

	#region GetHoldAsync Tests

	[Fact]
	public async Task GetHoldAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns((LegalHold?)null);

		// Act
		var result = await _sut.GetHoldAsync(holdId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetHoldAsync_ReturnsHold_WhenFound()
	{
		// Arrange
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test",
			IsActive = true,
			CreatedBy = "legal@test.com",
			CreatedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _store.GetHoldAsync(holdId, A<CancellationToken>._))
			.Returns(hold);

		// Act
		var result = await _sut.GetHoldAsync(holdId, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.HoldId.ShouldBe(holdId);
	}

	#endregion GetHoldAsync Tests

	#region ListActiveHoldsAsync Tests

	[Fact]
	public async Task ListActiveHoldsAsync_ReturnsEmptyList_WhenNoHolds()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.ListActiveHoldsAsync(
			A<string?>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<LegalHold>());

		// Act
		var result = await _sut.ListActiveHoldsAsync(null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListActiveHoldsAsync_PassesTenantIdFilter()
	{
		// Arrange
		const string tenantId = "tenant-123";
		_ = A.CallTo(() => _queryStore.ListActiveHoldsAsync(
			A<string?>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<LegalHold>());

		// Act
		_ = await _sut.ListActiveHoldsAsync(tenantId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _queryStore.ListActiveHoldsAsync(
			tenantId,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ListActiveHoldsAsync_ReturnsAllActiveHolds()
	{
		// Arrange
		var holds = new[]
		{
			new LegalHold
			{
				HoldId = Guid.NewGuid(),
				Basis = LegalHoldBasis.LitigationHold,
				CaseReference = "CASE-001",
				Description = "Test 1",
				IsActive = true,
				CreatedBy = "legal@test.com",
				CreatedAt = DateTimeOffset.UtcNow
			},
			new LegalHold
			{
				HoldId = Guid.NewGuid(),
				Basis = LegalHoldBasis.RegulatoryInvestigation,
				CaseReference = "REG-001",
				Description = "Test 2",
				IsActive = true,
				CreatedBy = "legal@test.com",
				CreatedAt = DateTimeOffset.UtcNow
			}
		};

		_ = A.CallTo(() => _queryStore.ListActiveHoldsAsync(
			A<string?>._,
			A<CancellationToken>._))
			.Returns(holds);

		// Act
		var result = await _sut.ListActiveHoldsAsync(null, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
	}

	#endregion ListActiveHoldsAsync Tests

	#region Helper Methods

	private static LegalHoldRequest CreateValidHoldRequest()
	{
		return new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			Description = "Test legal hold for litigation",
			CreatedBy = "legal@test.com"
		};
	}

	#endregion Helper Methods
}
