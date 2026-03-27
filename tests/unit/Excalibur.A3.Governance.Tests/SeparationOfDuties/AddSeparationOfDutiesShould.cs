// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.SeparationOfDuties;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Hosting;

namespace Excalibur.A3.Governance.Tests.SeparationOfDuties;

/// <summary>
/// Unit tests for <see cref="SoDGovernanceBuilderExtensions.AddSeparationOfDuties"/>
/// DI registration, options configuration, and ValidateOnStart behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddSeparationOfDutiesShould : UnitTestBase
{
	#region Store Registration

	[Fact]
	public void RegisterInMemorySoDPolicyStore_AsFallback()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		using var provider = services.BuildServiceProvider();
		provider.GetService<ISoDPolicyStore>().ShouldBeOfType<InMemorySoDPolicyStore>();
	}

	[Fact]
	public void PreserveExistingStore_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ISoDPolicyStore, StubSoDPolicyStore>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		using var provider = services.BuildServiceProvider();
		provider.GetService<ISoDPolicyStore>().ShouldBeOfType<StubSoDPolicyStore>();
	}

	#endregion

	#region Evaluator Registration

	[Fact]
	public void RegisterDefaultSoDEvaluator()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		// Check descriptor (resolving needs IGrantStore which isn't registered by AddSeparationOfDuties alone)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISoDEvaluator) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "DefaultSoDEvaluator");
	}

	#endregion

	#region Options Configuration

	[Fact]
	public void UseDefaultOptions()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SoDOptions>>().Value;

		options.EnablePreventiveEnforcement.ShouldBeTrue();
		options.EnableDetectiveScanning.ShouldBeTrue();
		options.DetectiveScanInterval.ShouldBe(TimeSpan.FromHours(24));
		options.MinimumEnforcementSeverity.ShouldBe(SoDSeverity.Violation);
	}

	[Fact]
	public void ApplyCustomOptions()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties(opts =>
			{
				opts.EnablePreventiveEnforcement = false;
				opts.EnableDetectiveScanning = false;
				opts.DetectiveScanInterval = TimeSpan.FromHours(12);
				opts.MinimumEnforcementSeverity = SoDSeverity.Critical;
			}));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SoDOptions>>().Value;

		options.EnablePreventiveEnforcement.ShouldBeFalse();
		options.EnableDetectiveScanning.ShouldBeFalse();
		options.DetectiveScanInterval.ShouldBe(TimeSpan.FromHours(12));
		options.MinimumEnforcementSeverity.ShouldBe(SoDSeverity.Critical);
	}

	#endregion

	#region ValidateOnStart

	[Fact]
	public void ThrowOnStart_WhenScanIntervalOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties(opts =>
			{
				opts.DetectiveScanInterval = TimeSpan.FromSeconds(1); // Below 1 minute minimum
			}));

		using var provider = services.BuildServiceProvider();
		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<SoDOptions>>().Value);
	}

	#endregion

	#region Service Registrations

	[Fact]
	public void RegisterPreventiveMiddleware()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Excalibur.Dispatch.Abstractions.IDispatchMiddleware) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "SoDPreventiveMiddleware");
	}

	[Fact]
	public void RegisterDetectiveScanService()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddSeparationOfDuties());

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "SoDDetectiveScanService");
	}

	#endregion

	#region Fluent Chaining

	[Fact]
	public void ReturnIGovernanceBuilder_ForFluentChaining()
	{
		var services = new ServiceCollection();
		IGovernanceBuilder? capturedBuilder = null;

		services.AddExcaliburA3Core()
			.AddGovernance(g =>
			{
				capturedBuilder = g.AddSeparationOfDuties();
			});

		capturedBuilder.ShouldNotBeNull();
		capturedBuilder.ShouldBeAssignableTo<IGovernanceBuilder>();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddSeparationOfDuties());
	}

	#endregion

	#region Test Doubles

	private sealed class StubSoDPolicyStore : ISoDPolicyStore
	{
		public Task<SoDPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken) =>
			Task.FromResult<SoDPolicy?>(null);

		public Task<IReadOnlyList<SoDPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SoDPolicy>>(Array.Empty<SoDPolicy>());

		public Task SavePolicyAsync(SoDPolicy policy, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
