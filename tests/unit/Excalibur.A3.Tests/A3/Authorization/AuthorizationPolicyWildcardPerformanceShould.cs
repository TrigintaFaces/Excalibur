// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Performance tests for AuthorizationPolicy wildcard evaluation (Sprint 726 T.5 j7y8fv).
/// Verifies that exact match remains O(1) and wildcard evaluation scales reasonably.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyWildcardPerformanceShould
{
	private const string Tenant = "tenant-1";

	private static ITenantId CreateTenantId()
	{
		var tid = A.Fake<ITenantId>();
		A.CallTo(() => tid.Value).Returns(Tenant);
		return tid;
	}

	[Fact]
	public void EvaluateExactMatchWithin100Microseconds()
	{
		// Arrange -- policy with 1000 exact grants + 50 wildcards
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < 1000; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Activity{i}"] = true;
		}

		for (var i = 0; i < 50; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Prefix{i}.*"] = true;
		}

		var policy = new AuthorizationPolicy(grants, new Dictionary<string, object>(), CreateTenantId(), "user-1");

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			policy.HasGrant("Activity500");
		}

		// Act
		var sw = Stopwatch.StartNew();
		const int iterations = 10_000;
		for (var i = 0; i < iterations; i++)
		{
			policy.HasGrant("Activity500");
		}

		sw.Stop();

		// Assert -- exact match should be O(1), well under 100us per call
		var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
		avgMicroseconds.ShouldBeLessThan(100, $"Exact match averaged {avgMicroseconds:F2}µs per call");
	}

	[Fact]
	public void EvaluateWildcardWith50GrantsWithin500Microseconds()
	{
		// Arrange -- policy with 50 wildcard grants, no exact match for target
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < 50; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Namespace{i}.*"] = true;
		}

		var policy = new AuthorizationPolicy(grants, new Dictionary<string, object>(), CreateTenantId(), "user-1");

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			policy.HasGrant("Namespace25.Action");
		}

		// Act
		var sw = Stopwatch.StartNew();
		const int iterations = 10_000;
		for (var i = 0; i < iterations; i++)
		{
			policy.HasGrant("Namespace25.Action");
		}

		sw.Stop();

		// Assert
		var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
		avgMicroseconds.ShouldBeLessThan(500, $"50-wildcard eval averaged {avgMicroseconds:F2}µs per call");
	}

	[Fact]
	public void EvaluateWildcardWith500GrantsWithin1Millisecond()
	{
		// Arrange -- policy with 500 wildcard grants
		var grants = new Dictionary<string, object>();
		for (var i = 0; i < 500; i++)
		{
			grants[$"{Tenant}:{GrantType.Activity}:Namespace{i}.*"] = true;
		}

		var policy = new AuthorizationPolicy(grants, new Dictionary<string, object>(), CreateTenantId(), "user-1");

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			policy.HasGrant("Namespace250.Action");
		}

		// Act
		var sw = Stopwatch.StartNew();
		const int iterations = 1_000;
		for (var i = 0; i < iterations; i++)
		{
			policy.HasGrant("Namespace250.Action");
		}

		sw.Stop();

		// Assert
		var avgMicroseconds = sw.Elapsed.TotalMicroseconds / iterations;
		avgMicroseconds.ShouldBeLessThan(1000, $"500-wildcard eval averaged {avgMicroseconds:F2}µs per call");
	}
}
