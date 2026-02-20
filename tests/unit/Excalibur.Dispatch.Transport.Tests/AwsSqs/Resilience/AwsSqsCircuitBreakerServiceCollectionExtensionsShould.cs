// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsCircuitBreakerServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsSqsCircuitBreakerServiceCollectionExtensions.AddAwsSqsCircuitBreaker(
				null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsSqsCircuitBreaker(null!));
	}

	[Fact]
	public void RegisterCircuitBreakerOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsSqsCircuitBreaker(opts =>
		{
			opts.FailureThreshold = 5;
			opts.BreakDuration = TimeSpan.FromSeconds(30);
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}
}
