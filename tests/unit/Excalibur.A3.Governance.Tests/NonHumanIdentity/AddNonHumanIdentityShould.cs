// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.NonHumanIdentity;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance.Tests.NonHumanIdentity;

/// <summary>
/// Unit tests for <see cref="NonHumanIdentityGovernanceBuilderExtensions"/>:
/// AddNonHumanIdentity (PrincipalTypeProvider) and AddApiKeyManagement (IApiKeyManager + options).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddNonHumanIdentityShould : UnitTestBase
{
	#region AddNonHumanIdentity

	[Fact]
	public void RegisterDefaultPrincipalTypeProvider()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddNonHumanIdentity());

		using var provider = services.BuildServiceProvider();
		var typeProvider = provider.GetService<IPrincipalTypeProvider>();
		typeProvider.ShouldNotBeNull();
		typeProvider.ShouldBeOfType<DefaultPrincipalTypeProvider>();
	}

	[Fact]
	public async Task DefaultProvider_ReturnsHumanForAllPrincipals()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddNonHumanIdentity());

		using var provider = services.BuildServiceProvider();
		var typeProvider = provider.GetRequiredService<IPrincipalTypeProvider>();

		var result = await typeProvider.GetPrincipalTypeAsync("any-user", CancellationToken.None);
		result.ShouldBe(PrincipalType.Human);
	}

	[Fact]
	public void PreserveExistingProvider_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IPrincipalTypeProvider, StubPrincipalTypeProvider>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddNonHumanIdentity());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IPrincipalTypeProvider>().ShouldBeOfType<StubPrincipalTypeProvider>();
	}

	[Fact]
	public void ReturnIGovernanceBuilder_ForFluentChaining_NonHumanIdentity()
	{
		var services = new ServiceCollection();
		IGovernanceBuilder? captured = null;

		services.AddExcaliburA3Core()
			.AddGovernance(g => { captured = g.AddNonHumanIdentity(); });

		captured.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilder_AddNonHumanIdentity()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddNonHumanIdentity());
	}

	#endregion

	#region AddApiKeyManagement

	[Fact]
	public void RegisterInMemoryApiKeyManager()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement());

		using var provider = services.BuildServiceProvider();
		var manager = provider.GetService<IApiKeyManager>();
		manager.ShouldNotBeNull();
		manager.ShouldBeOfType<InMemoryApiKeyManager>();
	}

	[Fact]
	public void UseDefaultApiKeyOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement());

		using var provider = services.BuildServiceProvider();
		var opts = provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;

		opts.MaxKeysPerPrincipal.ShouldBe(10);
		opts.DefaultExpirationDays.ShouldBe(90);
		opts.KeyLengthBytes.ShouldBe(32);
	}

	[Fact]
	public void ApplyCustomApiKeyOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement(opts =>
			{
				opts.MaxKeysPerPrincipal = 5;
				opts.DefaultExpirationDays = 30;
				opts.KeyLengthBytes = 64;
			}));

		using var provider = services.BuildServiceProvider();
		var opts = provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
		opts.MaxKeysPerPrincipal.ShouldBe(5);
		opts.DefaultExpirationDays.ShouldBe(30);
		opts.KeyLengthBytes.ShouldBe(64);
	}

	[Fact]
	public void AcceptMaxKeysValue_WhenConfigured()
	{
		// ValidateDataAnnotations removed in Sprint 750 AOT migration -- range validation no longer enforced via DI
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement(opts =>
			{
				opts.MaxKeysPerPrincipal = 0;
			}));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
		options.MaxKeysPerPrincipal.ShouldBe(0);
	}

	[Fact]
	public void ThrowOnStart_WhenKeyLengthOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement(opts =>
			{
				opts.KeyLengthBytes = 8; // Below minimum of 16
			}));

		using var provider = services.BuildServiceProvider();
		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value);
	}

	[Fact]
	public void AlsoRegisterPrincipalTypeProvider_WhenCallingAddApiKeyManagement()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddApiKeyManagement());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IPrincipalTypeProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void ReturnIGovernanceBuilder_ForFluentChaining_ApiKey()
	{
		var services = new ServiceCollection();
		IGovernanceBuilder? captured = null;

		services.AddExcaliburA3Core()
			.AddGovernance(g => { captured = g.AddApiKeyManagement(); });

		captured.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilder_AddApiKeyManagement()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddApiKeyManagement());
	}

	#endregion

	#region Test Doubles

	private sealed class StubPrincipalTypeProvider : IPrincipalTypeProvider
	{
		public Task<PrincipalType> GetPrincipalTypeAsync(string principalId, CancellationToken cancellationToken) =>
			Task.FromResult(PrincipalType.ServiceAccount);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
