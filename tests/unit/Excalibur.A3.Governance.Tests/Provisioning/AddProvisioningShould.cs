// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.Provisioning;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance.Tests.Provisioning;

/// <summary>
/// Unit tests for <see cref="ProvisioningGovernanceBuilderExtensions.AddProvisioning"/>:
/// DI registration for store, workflow, risk assessor, and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddProvisioningShould : UnitTestBase
{
	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning());
		return services.BuildServiceProvider();
	}

	#region Store Registration

	[Fact]
	public void RegisterInMemoryProvisioningStore_AsFallback()
	{
		using var provider = BuildProvider();
		provider.GetService<IProvisioningStore>().ShouldBeOfType<InMemoryProvisioningStore>();
	}

	[Fact]
	public void PreserveExistingStore_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IProvisioningStore, StubProvisioningStore>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IProvisioningStore>().ShouldBeOfType<StubProvisioningStore>();
	}

	#endregion

	#region Workflow Registration

	[Fact]
	public void RegisterDefaultSingleApproverWorkflow_AsFallback()
	{
		using var provider = BuildProvider();
		var workflow = provider.GetService<IProvisioningWorkflowConfiguration>();
		workflow.ShouldNotBeNull();
		workflow.ShouldBeOfType<DefaultSingleApproverWorkflow>();
	}

	#endregion

	#region Risk Assessor Registration

	[Fact]
	public void RegisterDefaultGrantRiskAssessor_AsFallback()
	{
		using var provider = BuildProvider();
		var assessor = provider.GetService<IGrantRiskAssessor>();
		assessor.ShouldNotBeNull();
		assessor.ShouldBeOfType<DefaultGrantRiskAssessor>();
	}

	#endregion

	#region Options Configuration (Sprint 712)

	[Fact]
	public void UseDefaultProvisioningOptions()
	{
		using var provider = BuildProvider();
		var opts = provider.GetRequiredService<IOptions<ProvisioningOptions>>().Value;

		opts.DefaultApprovalTimeout.ShouldBe(TimeSpan.FromHours(72));
		opts.RequireRiskAssessment.ShouldBeTrue();
		opts.EnableJitAccess.ShouldBeFalse();
	}

	[Fact]
	public void ApplyCustomProvisioningOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning(
				provisioning =>
				{
					provisioning.DefaultApprovalTimeout = TimeSpan.FromHours(24);
					provisioning.RequireRiskAssessment = false;
					provisioning.EnableJitAccess = true;
				}));

		using var provider = services.BuildServiceProvider();
		var opts = provider.GetRequiredService<IOptions<ProvisioningOptions>>().Value;
		opts.DefaultApprovalTimeout.ShouldBe(TimeSpan.FromHours(24));
		opts.RequireRiskAssessment.ShouldBeFalse();
		opts.EnableJitAccess.ShouldBeTrue();
	}

	[Fact]
	public void UseDefaultJitAccessOptions()
	{
		using var provider = BuildProvider();
		var opts = provider.GetRequiredService<IOptions<JitAccessOptions>>().Value;

		opts.DefaultJitDuration.ShouldBe(TimeSpan.FromHours(4));
		opts.MaxJitDuration.ShouldBe(TimeSpan.FromHours(24));
		opts.ExpiryCheckInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ApplyCustomJitAccessOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning(
				configureJit: jit =>
				{
					jit.DefaultJitDuration = TimeSpan.FromHours(8);
					jit.MaxJitDuration = TimeSpan.FromHours(48);
				}));

		using var provider = services.BuildServiceProvider();
		var opts = provider.GetRequiredService<IOptions<JitAccessOptions>>().Value;
		opts.DefaultJitDuration.ShouldBe(TimeSpan.FromHours(8));
		opts.MaxJitDuration.ShouldBe(TimeSpan.FromHours(48));
	}

	#endregion

	#region Background Service + Completion Service Registration

	[Fact]
	public void RegisterJitExpiryBackgroundService()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning());

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "JitAccessExpiryService");
	}

	[Fact]
	public void RegisterProvisioningCompletionService()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddProvisioning());

		services.ShouldContain(sd =>
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "ProvisioningCompletionService");
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
				var result = g.AddProvisioning();
				capturedBuilder = result;
			});

		capturedBuilder.ShouldNotBeNull();
		capturedBuilder.ShouldBeAssignableTo<IGovernanceBuilder>();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddProvisioning());
	}

	#endregion

	#region Test Doubles

	private sealed class StubProvisioningStore : IProvisioningStore
	{
		public Task<ProvisioningRequestSummary?> GetRequestAsync(string requestId, CancellationToken cancellationToken) =>
			Task.FromResult<ProvisioningRequestSummary?>(null);

		public Task<IReadOnlyList<ProvisioningRequestSummary>> GetRequestsByStatusAsync(
			ProvisioningRequestStatus? status, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<ProvisioningRequestSummary>>([]);

		public Task SaveRequestAsync(ProvisioningRequestSummary request, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> DeleteRequestAsync(string requestId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}

	#endregion
}
