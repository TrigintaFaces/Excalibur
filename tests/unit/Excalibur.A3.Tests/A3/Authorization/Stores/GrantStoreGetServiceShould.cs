// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Authorization.Stores;

/// <summary>
/// Unit tests for the <see cref="IGrantStore.GetService(Type)"/> ISP extension point
/// across store implementations that also implement sub-interfaces.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantStoreGetServiceShould
{
	[Fact]
	public void ReturnSelf_WhenStoreImplementsIGrantQueryStore()
	{
		// Arrange
		IGrantStore store = new CompositeGrantStore();

		// Act
		var queryStore = store.GetService(typeof(IGrantQueryStore));

		// Assert
		queryStore.ShouldNotBeNull();
		queryStore.ShouldBeAssignableTo<IGrantQueryStore>();
		queryStore.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnSelf_WhenStoreImplementsIActivityGroupGrantStore()
	{
		// Arrange
		IGrantStore store = new CompositeGrantStore();

		// Act
		var activityGroupGrantStore = store.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		activityGroupGrantStore.ShouldNotBeNull();
		activityGroupGrantStore.ShouldBeAssignableTo<IActivityGroupGrantStore>();
		activityGroupGrantStore.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnNull_WhenServiceTypeIsNotSupported()
	{
		// Arrange
		IGrantStore store = new CompositeGrantStore();

		// Act
		var result = store.GetService(typeof(IDisposable));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenMinimalStoreDoesNotImplementSubInterfaces()
	{
		// Arrange
		IGrantStore store = new MinimalGrantStore();

		// Act
		var queryStore = store.GetService(typeof(IGrantQueryStore));
		var activityGroupGrantStore = store.GetService(typeof(IActivityGroupGrantStore));

		// Assert
		queryStore.ShouldBeNull();
		activityGroupGrantStore.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServiceTypeIsNull()
	{
		// Arrange
		IGrantStore store = new CompositeGrantStore();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}

	// Composite store that implements all sub-interfaces (like SqlServerGrantStore)
	private sealed class CompositeGrantStore : IGrantStore, IGrantQueryStore, IActivityGroupGrantStore
	{
		public Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) =>
			Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>(Array.Empty<Grant>());

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType)
		{
			ArgumentNullException.ThrowIfNull(serviceType);

			if (serviceType == typeof(IGrantQueryStore))
				return this;

			if (serviceType == typeof(IActivityGroupGrantStore))
				return this;

			return null;
		}

		// IGrantQueryStore
		public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string? userId, string tenantId,
			string grantType, string qualifier, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>(Array.Empty<Grant>());

		public Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

		// IActivityGroupGrantStore
		public Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> DeleteAllActivityGroupGrantsAsync(string grantType,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
			string? tenantId, string grantType, string qualifier,
			DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
			string grantType, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
	}

	// Minimal store that only implements IGrantStore (no sub-interfaces)
	private sealed class MinimalGrantStore : IGrantStore
	{
		public Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) =>
			Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Grant>>(Array.Empty<Grant>());

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
			string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
			string qualifier, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}
}
