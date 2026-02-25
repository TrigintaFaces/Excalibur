// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class HealthChecksBuilderExtensionsShould
{
	[Fact]
	public void ThrowWhenHealthChecksBuilderIsNull()
	{
		IHealthChecksBuilder builder = null!;
		Should.Throw<ArgumentNullException>(
			() => builder.AddElasticHealthCheck("test", TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void ThrowWhenNameIsNull()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		Should.Throw<ArgumentException>(
			() => builder.AddElasticHealthCheck(null!, TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void ThrowWhenNameIsEmpty()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		Should.Throw<ArgumentException>(
			() => builder.AddElasticHealthCheck("", TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void ThrowWhenNameIsWhitespace()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		Should.Throw<ArgumentException>(
			() => builder.AddElasticHealthCheck("   ", TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void ThrowWhenTimeoutIsZero()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AddElasticHealthCheck("test", TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenTimeoutIsNegative()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		Should.Throw<ArgumentOutOfRangeException>(
			() => builder.AddElasticHealthCheck("test", TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void RegisterHealthCheckSuccessfully()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		var result = builder.AddElasticHealthCheck("elasticsearch", TimeSpan.FromSeconds(30));

		result.ShouldNotBeNull();

		// Verify the health check registration exists
		using var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetService<IOptions<HealthCheckServiceOptions>>();
		healthCheckOptions.ShouldNotBeNull();
		healthCheckOptions.Value.Registrations.ShouldContain(
			r => r.Name == "elasticsearch");
	}

	[Fact]
	public void ReturnBuilderForMethodChaining()
	{
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		var result = builder.AddElasticHealthCheck("test", TimeSpan.FromSeconds(10));

		result.ShouldBe(builder);
	}
}
