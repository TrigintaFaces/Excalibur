// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.OrphanedAccess;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance.Tests.OrphanedAccess;

/// <summary>
/// Unit tests for <see cref="OrphanedAccessGovernanceBuilderExtensions.AddOrphanedAccessDetection"/>:
/// DI registration, options configuration, ValidateOnStart, and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddOrphanedAccessDetectionShould : UnitTestBase
{
	private static ServiceProvider BuildProvider(Action<OrphanedAccessOptions>? configure = null)
	{
		var services = new ServiceCollection();
		// IUserStatusProvider must be registered by consumers (no default provided)
		services.AddLogging();
		services.AddSingleton<IUserStatusProvider, StubUserStatusProvider>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddOrphanedAccessDetection(configure));
		return services.BuildServiceProvider();
	}

	#region Detector Registration

	[Fact]
	public void RegisterDefaultOrphanedAccessDetector()
	{
		using var provider = BuildProvider();
		var detector = provider.GetService<IOrphanedAccessDetector>();
		detector.ShouldNotBeNull();
		detector.ShouldBeOfType<DefaultOrphanedAccessDetector>();
	}

	[Fact]
	public void PreserveExistingDetector_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IOrphanedAccessDetector, StubOrphanedAccessDetector>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddOrphanedAccessDetection());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IOrphanedAccessDetector>().ShouldBeOfType<StubOrphanedAccessDetector>();
	}

	#endregion

	#region Options Configuration

	[Fact]
	public void UseDefaultOptions_WhenNoConfigureDelegate()
	{
		using var provider = BuildProvider();
		var options = provider.GetRequiredService<IOptions<OrphanedAccessOptions>>().Value;

		options.ScanIntervalHours.ShouldBe(24);
		options.InactiveGracePeriodDays.ShouldBe(30);
		options.AutoRevokeDeparted.ShouldBeFalse();
		options.AutoRevokeAfterGracePeriod.ShouldBeFalse();
	}

	[Fact]
	public void ApplyCustomOptions()
	{
		using var provider = BuildProvider(opts =>
		{
			opts.ScanIntervalHours = 12;
			opts.InactiveGracePeriodDays = 7;
			opts.AutoRevokeDeparted = true;
			opts.AutoRevokeAfterGracePeriod = true;
		});

		var options = provider.GetRequiredService<IOptions<OrphanedAccessOptions>>().Value;
		options.ScanIntervalHours.ShouldBe(12);
		options.InactiveGracePeriodDays.ShouldBe(7);
		options.AutoRevokeDeparted.ShouldBeTrue();
		options.AutoRevokeAfterGracePeriod.ShouldBeTrue();
	}

	#endregion

	#region ValidateOnStart

	[Fact]
	public void ThrowOnStart_WhenScanIntervalOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddOrphanedAccessDetection(opts =>
			{
				opts.ScanIntervalHours = 0; // Below minimum of 1
			}));

		using var provider = services.BuildServiceProvider();

		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<OrphanedAccessOptions>>().Value);
	}

	[Fact]
	public void ThrowOnStart_WhenGracePeriodOutOfRange()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddOrphanedAccessDetection(opts =>
			{
				opts.InactiveGracePeriodDays = 0; // Below minimum of 1
			}));

		using var provider = services.BuildServiceProvider();

		Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptions<OrphanedAccessOptions>>().Value);
	}

	#endregion

	#region Background Service Registration

	[Fact]
	public void RegisterScanBackgroundService()
	{
		var services = new ServiceCollection();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddOrphanedAccessDetection());

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "OrphanedAccessScanService");
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
				var result = g.AddOrphanedAccessDetection();
				capturedBuilder = result;
			});

		capturedBuilder.ShouldNotBeNull();
		capturedBuilder.ShouldBeAssignableTo<IGovernanceBuilder>();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddOrphanedAccessDetection());
	}

	#endregion

	#region Test Doubles

	private sealed class StubOrphanedAccessDetector : IOrphanedAccessDetector
	{
		public Task<OrphanedAccessReport> DetectAsync(string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult(new OrphanedAccessReport(DateTimeOffset.UtcNow, tenantId, [], 0));

		public object? GetService(Type serviceType) => null;
	}

	private sealed class StubUserStatusProvider : IUserStatusProvider
	{
		public Task<PrincipalStatusResult> GetStatusAsync(string principalId, CancellationToken cancellationToken) =>
			Task.FromResult(new PrincipalStatusResult(PrincipalStatus.Active, null));
	}

	#endregion
}
