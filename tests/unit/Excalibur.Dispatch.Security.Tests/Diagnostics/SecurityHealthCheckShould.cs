// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Diagnostics;

/// <summary>
/// Tests for SecurityHealthCheck (Sprint 696 T.21).
/// Verifies health check correctly reports security configuration status.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class SecurityHealthCheckShould
{
	[Fact]
	public async Task ReturnHealthyWhenAuthenticationNotRequired()
	{
		// Arrange
		var options = CreateOptions(requireAuth: false, signingKey: null);
		var sut = new SecurityHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("properly configured");
	}

	[Fact]
	public async Task ReturnHealthyWhenAuthRequiredAndKeyConfigured()
	{
		// Arrange
		var options = CreateOptions(requireAuth: true, signingKey: "valid-signing-key-12345");
		var sut = new SecurityHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task ReturnDegradedWhenAuthRequiredButKeyMissing()
	{
		// Arrange
		var options = CreateOptions(requireAuth: true, signingKey: null);
		var sut = new SecurityHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("JwtSigningKey");
	}

	[Fact]
	public async Task ReturnDegradedWhenAuthRequiredButKeyEmpty()
	{
		// Arrange
		var options = CreateOptions(requireAuth: true, signingKey: "");
		var sut = new SecurityHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public async Task ReturnHealthyWhenAuthDisabledRegardlessOfKey()
	{
		// Arrange -- auth not required, no key
		var options = CreateOptions(requireAuth: false, signingKey: null);
		var sut = new SecurityHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	private static IOptions<SecurityOptions> CreateOptions(bool requireAuth, string? signingKey)
	{
		var opts = new SecurityOptions
		{
			Authentication = new SecurityAuthenticationOptions
			{
				RequireAuthentication = requireAuth,
				JwtSigningKey = signingKey
			}
		};
		return Microsoft.Extensions.Options.Options.Create(opts);
	}
}
