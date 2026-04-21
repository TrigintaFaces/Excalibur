// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.Reporting;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance.Tests.Reporting;

/// <summary>
/// Unit tests for <see cref="EntitlementReportingGovernanceBuilderExtensions.AddEntitlementReporting"/>:
/// DI registration for IEntitlementReportProvider, IReportFormatter, and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AddEntitlementReportingShould : UnitTestBase
{
	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddEntitlementReporting());
		return services.BuildServiceProvider();
	}

	[Fact]
	public void RegisterDefaultEntitlementReportProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddEntitlementReporting());

		// Verify descriptor (resolving would require IGrantStore which is scenario-specific)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IEntitlementReportProvider) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "DefaultEntitlementReportProvider");
	}

	[Fact]
	public void RegisterJsonReportFormatter()
	{
		using var provider = BuildProvider();
		var formatter = provider.GetService<IReportFormatter>();
		formatter.ShouldNotBeNull();
		formatter.ShouldBeOfType<JsonReportFormatter>();
		formatter.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void PreserveExistingFormatter_WhenRegisteredBefore()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IReportFormatter, StubReportFormatter>();
		services.AddExcaliburA3Core()
			.AddGovernance(g => g.AddEntitlementReporting());

		using var provider = services.BuildServiceProvider();
		provider.GetService<IReportFormatter>().ShouldBeOfType<StubReportFormatter>();
	}

	[Fact]
	public void ReturnIGovernanceBuilder_ForFluentChaining()
	{
		var services = new ServiceCollection();
		IGovernanceBuilder? captured = null;

		services.AddExcaliburA3Core()
			.AddGovernance(g => { captured = g.AddEntitlementReporting(); });

		captured.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilder()
	{
		IGovernanceBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddEntitlementReporting());
	}

	private sealed class StubReportFormatter : IReportFormatter
	{
		public string ContentType => "text/csv";
		public Task<byte[]> FormatAsync(EntitlementSnapshot snapshot, CancellationToken cancellationToken) =>
			Task.FromResult(Array.Empty<byte>());
	}
}
