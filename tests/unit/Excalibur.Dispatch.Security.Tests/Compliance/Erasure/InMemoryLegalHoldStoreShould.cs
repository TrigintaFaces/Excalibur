// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="InMemoryLegalHoldStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class InMemoryLegalHoldStoreShould
{
	private readonly InMemoryLegalHoldStore _sut = new();

	#region SaveHoldAsync Tests

	[Fact]
	public async Task SaveHoldAsync_ThrowsArgumentNullException_WhenHoldIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveHoldAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SaveHoldAsync_StoresHold()
	{
		// Arrange
		var hold = CreateHold();

		// Act
		await _sut.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.HoldCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveHoldAsync_ThrowsInvalidOperationException_WhenDuplicate()
	{
		// Arrange
		var hold = CreateHold();
		await _sut.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveHoldAsync(hold, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region GetHoldAsync Tests

	[Fact]
	public async Task GetHoldAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _sut.GetHoldAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetHoldAsync_ReturnsHold_WhenExists()
	{
		// Arrange
		var hold = CreateHold();
		await _sut.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.HoldId.ShouldBe(hold.HoldId);
	}

	#endregion

	#region UpdateHoldAsync Tests

	[Fact]
	public async Task UpdateHoldAsync_ThrowsArgumentNullException_WhenHoldIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.UpdateHoldAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task UpdateHoldAsync_ReturnsFalse_WhenNotFound()
	{
		var hold = CreateHold();
		var result = await _sut.UpdateHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task UpdateHoldAsync_ReturnsTrue_WhenUpdated()
	{
		// Arrange
		var hold = CreateHold();
		await _sut.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		var updated = hold with { Description = "updated description" };

		// Act
		var result = await _sut.UpdateHoldAsync(updated, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		var retrieved = await _sut.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false);
		retrieved.Description.ShouldBe("updated description");
	}

	#endregion

	#region GetActiveHoldsForDataSubjectAsync Tests

	[Fact]
	public async Task GetActiveHoldsForDataSubjectAsync_ReturnsActiveHolds()
	{
		// Arrange
		var hold1 = CreateHold(dataSubjectIdHash: "hash-1", isActive: true);
		var hold2 = CreateHold(dataSubjectIdHash: "hash-1", isActive: false);
		var hold3 = CreateHold(dataSubjectIdHash: "hash-2", isActive: true);
		await _sut.SaveHoldAsync(hold1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(hold2, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(hold3, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetActiveHoldsForDataSubjectAsync("hash-1", null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].HoldId.ShouldBe(hold1.HoldId);
	}

	[Fact]
	public async Task GetActiveHoldsForDataSubjectAsync_ExcludesExpiredHolds()
	{
		// Arrange
		var expiredHold = CreateHold(dataSubjectIdHash: "hash-1", isActive: true,
			expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
		var validHold = CreateHold(dataSubjectIdHash: "hash-1", isActive: true,
			expiresAt: DateTimeOffset.UtcNow.AddDays(30));
		await _sut.SaveHoldAsync(expiredHold, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(validHold, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetActiveHoldsForDataSubjectAsync("hash-1", null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].HoldId.ShouldBe(validHold.HoldId);
	}

	[Fact]
	public async Task GetActiveHoldsForDataSubjectAsync_FiltersByTenantId()
	{
		// Arrange
		var hold1 = CreateHold(dataSubjectIdHash: "hash-1", isActive: true, tenantId: "tenant-a");
		var hold2 = CreateHold(dataSubjectIdHash: "hash-1", isActive: true, tenantId: "tenant-b");
		await _sut.SaveHoldAsync(hold1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(hold2, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetActiveHoldsForDataSubjectAsync("hash-1", "tenant-a", CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].TenantId.ShouldBe("tenant-a");
	}

	#endregion

	#region GetActiveHoldsForTenantAsync Tests

	[Fact]
	public async Task GetActiveHoldsForTenantAsync_ReturnsActiveHoldsForTenant()
	{
		// Arrange
		var hold1 = CreateHold(tenantId: "tenant-a", isActive: true);
		var hold2 = CreateHold(tenantId: "tenant-b", isActive: true);
		await _sut.SaveHoldAsync(hold1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(hold2, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetActiveHoldsForTenantAsync("tenant-a", CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion

	#region ListActiveHoldsAsync Tests

	[Fact]
	public async Task ListActiveHoldsAsync_ReturnsAllActiveHolds()
	{
		// Arrange
		var active = CreateHold(isActive: true);
		var inactive = CreateHold(isActive: false);
		await _sut.SaveHoldAsync(active, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(inactive, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.ListActiveHoldsAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ListActiveHoldsAsync_FiltersByTenantId()
	{
		// Arrange
		var hold1 = CreateHold(tenantId: "tenant-a", isActive: true);
		var hold2 = CreateHold(tenantId: "tenant-b", isActive: true);
		await _sut.SaveHoldAsync(hold1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(hold2, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.ListActiveHoldsAsync("tenant-a", CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion

	#region ListAllHoldsAsync Tests

	[Fact]
	public async Task ListAllHoldsAsync_ReturnsAllHolds()
	{
		// Arrange
		await _sut.SaveHoldAsync(CreateHold(isActive: true), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(CreateHold(isActive: false), CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.ListAllHoldsAsync(null, null, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ListAllHoldsAsync_FiltersByDateRange()
	{
		// Arrange
		var oldHold = CreateHold(createdAt: DateTimeOffset.UtcNow.AddDays(-30));
		var newHold = CreateHold(createdAt: DateTimeOffset.UtcNow);
		await _sut.SaveHoldAsync(oldHold, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(newHold, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.ListAllHoldsAsync(
			null,
			DateTimeOffset.UtcNow.AddDays(-1),
			null,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion

	#region GetExpiredHoldsAsync Tests

	[Fact]
	public async Task GetExpiredHoldsAsync_ReturnsOnlyExpiredActiveHolds()
	{
		// Arrange
		var expired = CreateHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
		var valid = CreateHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddDays(30));
		var inactiveExpired = CreateHold(isActive: false, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
		await _sut.SaveHoldAsync(expired, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(valid, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(inactiveExpired, CancellationToken.None).ConfigureAwait(false);

		// Act
		var results = await _sut.GetExpiredHoldsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(1);
		results[0].HoldId.ShouldBe(expired.HoldId);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllHolds()
	{
		// Arrange
		await _sut.SaveHoldAsync(CreateHold(), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(CreateHold(), CancellationToken.None).ConfigureAwait(false);

		// Act
		_sut.Clear();

		// Assert
		_sut.HoldCount.ShouldBe(0);
	}

	#endregion

	#region ActiveHoldCount Tests

	[Fact]
	public async Task ActiveHoldCount_ReturnsCorrectCount()
	{
		// Arrange
		await _sut.SaveHoldAsync(CreateHold(isActive: true), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveHoldAsync(CreateHold(isActive: false), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.ActiveHoldCount.ShouldBe(1);
		_sut.HoldCount.ShouldBe(2);
	}

	#endregion

	#region Helpers

	private static LegalHold CreateHold(
		string? dataSubjectIdHash = null,
		bool isActive = true,
		string? tenantId = null,
		DateTimeOffset? expiresAt = null,
		DateTimeOffset? createdAt = null) =>
		new()
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = dataSubjectIdHash ?? $"hash-{Guid.NewGuid():N}",
			IsActive = isActive,
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = $"CASE-{Guid.NewGuid():N}",
			Description = "test hold",
			CreatedBy = "legal-team",
			TenantId = tenantId,
			ExpiresAt = expiresAt,
			CreatedAt = createdAt ?? DateTimeOffset.UtcNow
		};

	#endregion
}
