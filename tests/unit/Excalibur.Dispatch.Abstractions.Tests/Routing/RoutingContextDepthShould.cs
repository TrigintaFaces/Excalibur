// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Depth coverage tests for <see cref="RoutingContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingContextDepthShould
{
	[Fact]
	public void DefaultConstructor_SetsTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var context = new RoutingContext();
		var after = DateTimeOffset.UtcNow;

		context.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		context.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Properties_AreMutable()
	{
		var context = new RoutingContext();
		context.Source = "test-source";
		context.MessageType = "OrderCreated";
		context.CorrelationId = "corr-123";

		context.Source.ShouldBe("test-source");
		context.MessageType.ShouldBe("OrderCreated");
		context.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SourceEndpoint_IsAliasForSource()
	{
		var context = new RoutingContext();
		context.SourceEndpoint = "my-endpoint";
		context.Source.ShouldBe("my-endpoint");

		context.Source = "other-source";
		context.SourceEndpoint.ShouldBe("other-source");
	}

	[Fact]
	public void Properties_Dictionary_AllowsCustomValues()
	{
		var context = new RoutingContext();
		context.Properties["region"] = "us-east-1";
		context.Properties["priority"] = 5;

		context.Properties.ShouldContainKey("region");
		context.Properties["region"].ShouldBe("us-east-1");
		context.Properties["priority"].ShouldBe(5);
	}

	[Fact]
	public void CancellationToken_DefaultsToNone()
	{
		var context = new RoutingContext();
		context.CancellationToken.ShouldBe(CancellationToken.None);
	}

	[Fact]
	public void CancellationToken_CanBeSet()
	{
		using var cts = new CancellationTokenSource();
		var context = new RoutingContext { CancellationToken = cts.Token };
		context.CancellationToken.ShouldBe(cts.Token);
	}
}
