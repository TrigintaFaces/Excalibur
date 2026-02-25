// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Depth coverage tests for <see cref="RoutingDecision"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingDecisionDepthShould
{
	[Fact]
	public void Success_SetsAllProperties()
	{
		var endpoints = new List<string> { "billing", "inventory" };
		var rules = new List<string> { "rule-1", "rule-2" };

		var decision = RoutingDecision.Success("rabbitmq", endpoints, rules);

		decision.Transport.ShouldBe("rabbitmq");
		decision.Endpoints.ShouldBe(endpoints);
		decision.MatchedRules.ShouldBe(rules);
		decision.IsSuccess.ShouldBeTrue();
		decision.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Success_WithoutMatchedRules_DefaultsToEmpty()
	{
		var decision = RoutingDecision.Success("kafka", []);
		decision.MatchedRules.ShouldBeEmpty();
	}

	[Fact]
	public void Success_ThrowsArgumentException_WhenTransportIsNull()
	{
		Should.Throw<ArgumentException>(() => RoutingDecision.Success(null!, []));
	}

	[Fact]
	public void Success_ThrowsArgumentException_WhenTransportIsEmpty()
	{
		Should.Throw<ArgumentException>(() => RoutingDecision.Success("", []));
	}

	[Fact]
	public void Success_ThrowsArgumentException_WhenTransportIsWhitespace()
	{
		Should.Throw<ArgumentException>(() => RoutingDecision.Success("  ", []));
	}

	[Fact]
	public void Success_ThrowsArgumentNullException_WhenEndpointsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => RoutingDecision.Success("transport", null!));
	}

	[Fact]
	public void Failure_SetsFailureReasonAndEmptyTransport()
	{
		var decision = RoutingDecision.Failure("No matching rules");
		decision.Transport.ShouldBe(string.Empty);
		decision.IsSuccess.ShouldBeFalse();
		decision.FailureReason.ShouldBe("No matching rules");
		decision.Endpoints.ShouldBeEmpty();
	}

	[Fact]
	public void Failure_ThrowsArgumentException_WhenReasonIsNull()
	{
		Should.Throw<ArgumentException>(() => RoutingDecision.Failure(null!));
	}

	[Fact]
	public void Failure_ThrowsArgumentException_WhenReasonIsEmpty()
	{
		Should.Throw<ArgumentException>(() => RoutingDecision.Failure(""));
	}

	[Fact]
	public void IsSuccess_ReturnsFalse_WhenTransportIsEmpty()
	{
		var decision = new RoutingDecision { Transport = "", Endpoints = [] };
		decision.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void IsSuccess_ReturnsTrue_WhenTransportHasValue()
	{
		var decision = new RoutingDecision { Transport = "local", Endpoints = [] };
		decision.IsSuccess.ShouldBeTrue();
	}
}
