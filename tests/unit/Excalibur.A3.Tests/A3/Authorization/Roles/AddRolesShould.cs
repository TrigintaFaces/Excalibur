// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Roles;

/// <summary>
/// Unit tests for <see cref="RoleServiceCollectionExtensions.AddRoles"/> DI registration
/// and <see cref="RoleOptions"/> configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AddRolesShould : UnitTestBase
{
	[Fact]
	public void RegisterInMemoryRoleStore_AsFallback()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddExcaliburA3Core().AddRoles();

		var provider = services.BuildServiceProvider();
		provider.GetService<IRoleStore>().ShouldBeOfType<InMemoryRoleStore>();
	}

	[Fact]
	public void PreserveExistingRoleStore_WhenRegisteredBefore()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddSingleton<IRoleStore, StubRoleStore>();

		services.AddExcaliburA3Core().AddRoles();

		var provider = services.BuildServiceProvider();
		provider.GetService<IRoleStore>().ShouldBeOfType<StubRoleStore>();
	}

	[Fact]
	public void ConfigureRoleOptions()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddExcaliburA3Core().AddRoles(opts =>
		{
			opts.MaxHierarchyDepth = 3;
			opts.EnforceUniqueNames = false;
		});

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RoleOptions>>().Value;
		options.MaxHierarchyDepth.ShouldBe(3);
		options.EnforceUniqueNames.ShouldBeFalse();
	}

	[Fact]
	public void UseDefaultRoleOptions_WhenNoConfigureDelegate()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddExcaliburA3Core().AddRoles();

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RoleOptions>>().Value;
		options.MaxHierarchyDepth.ShouldBe(5);
		options.EnforceUniqueNames.ShouldBeTrue();
	}

	[Fact]
	public void ReturnIA3Builder_ForFluentChaining()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		var result = services.AddExcaliburA3Core().AddRoles();

		result.ShouldNotBeNull();
		result.ShouldBeAssignableTo<IA3Builder>();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IA3Builder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddRoles());
	}

	[Fact]
	public void SupportFullFluentChain()
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		var result = services.AddExcaliburA3Core()
			.AddRoles(opts => opts.MaxHierarchyDepth = 2);

		result.ShouldNotBeNull();
		result.ShouldBeAssignableTo<IA3Builder>();
	}

	#region Test Double

	private sealed class StubRoleStore : IRoleStore
	{
		public Task<RoleSummary?> GetRoleAsync(string roleId, CancellationToken cancellationToken) =>
			Task.FromResult<RoleSummary?>(null);

		public Task<IReadOnlyList<RoleSummary>> GetRolesAsync(string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<RoleSummary>>(Array.Empty<RoleSummary>());

		public Task SaveRoleAsync(RoleSummary role, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
