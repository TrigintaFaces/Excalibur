// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for <see cref="A3Builder"/> via the <see cref="IA3Builder"/> interface.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class A3BuilderShould
{
	[Fact]
	public void ReturnIA3Builder_WhenAddExcaliburA3IsCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = services.AddExcaliburA3();

		// Assert
		builder.ShouldNotBeNull();
		builder.ShouldBeAssignableTo<IA3Builder>();
	}

	[Fact]
	public void ExposeServicesProperty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = services.AddExcaliburA3();

		// Assert
		builder.Services.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		IServiceCollection nullServices = null!;
		Should.Throw<ArgumentNullException>(() =>
			nullServices.AddExcaliburA3());
	}

	[Fact]
	public void RegisterGrantStoreViaUseGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UseGrantStore<FakeGrantStore>();

		// Assert
		result.ShouldBeSameAs(builder);

		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IGrantStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<FakeGrantStore>();
	}

	[Fact]
	public void RegisterActivityGroupStoreViaUseActivityGroupStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UseActivityGroupStore<FakeActivityGroupStore>();

		// Assert
		result.ShouldBeSameAs(builder);

		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IActivityGroupStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<FakeActivityGroupStore>();
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburA3()
			.UseGrantStore<FakeGrantStore>()
			.UseActivityGroupStore<FakeActivityGroupStore>();

		// Assert
		result.ShouldNotBeNull();

		var provider = services.BuildServiceProvider();
		provider.GetService<IGrantStore>().ShouldBeOfType<FakeGrantStore>();
		provider.GetService<IActivityGroupStore>().ShouldBeOfType<FakeActivityGroupStore>();
	}

	[Fact]
	public void ReplaceExistingGrantStore_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act -- register first, then replace
		builder.UseGrantStore<FakeGrantStore>();
		builder.UseGrantStore<AnotherFakeGrantStore>();

		// Assert -- only the last registration wins
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IGrantStore>();
		store.ShouldBeOfType<AnotherFakeGrantStore>();
	}

	[Fact]
	public void ReplaceExistingActivityGroupStore_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act -- register first, then replace
		builder.UseActivityGroupStore<FakeActivityGroupStore>();
		builder.UseActivityGroupStore<AnotherFakeActivityGroupStore>();

		// Assert -- only the last registration wins
		var provider = services.BuildServiceProvider();
		var store = provider.GetService<IActivityGroupStore>();
		store.ShouldBeOfType<AnotherFakeActivityGroupStore>();
	}

	// Test doubles

	private sealed class FakeGrantStore : IGrantStore
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

	private sealed class AnotherFakeGrantStore : IGrantStore
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

	private sealed class FakeActivityGroupStore : IActivityGroupStore
	{
		public Task<bool> ActivityGroupExistsAsync(string activityGroupName, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

		public Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> CreateActivityGroupAsync(string? tenantId, string name,
			string activityName, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public object? GetService(Type serviceType) => null;
	}

	private sealed class AnotherFakeActivityGroupStore : IActivityGroupStore
	{
		public Task<bool> ActivityGroupExistsAsync(string activityGroupName, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

		public Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> CreateActivityGroupAsync(string? tenantId, string name,
			string activityName, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public object? GetService(Type serviceType) => null;
	}
}
