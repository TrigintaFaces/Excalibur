// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Unit tests for <see cref="RoutingDecision"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingDecisionShould
{
	private static readonly IReadOnlyList<string> TwoEndpoints = ["billing-service", "inventory-service"];
	private static readonly IReadOnlyList<string> SingleEndpoint = ["ep1"];
	private static readonly IReadOnlyList<string> AnalyticsEndpoint = ["analytics"];
	private static readonly IReadOnlyList<string> MatchedRulesKafka = ["transport:kafka", "endpoint:analytics"];
	private static readonly IReadOnlyList<string> SingleRule = ["rule1"];

	#region Success factory method

	[Fact]
	public void CreateSuccessDecisionWithTransportAndEndpoints()
	{
		// Arrange & Act
		var decision = RoutingDecision.Success("rabbitmq", TwoEndpoints);

		// Assert
		decision.Transport.ShouldBe("rabbitmq");
		decision.Endpoints.Count.ShouldBe(2);
		decision.Endpoints.ShouldContain("billing-service");
		decision.Endpoints.ShouldContain("inventory-service");
		decision.IsSuccess.ShouldBeTrue();
		decision.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessDecisionWithMatchedRules()
	{
		// Arrange & Act
		var decision = RoutingDecision.Success("kafka", AnalyticsEndpoint, MatchedRulesKafka);

		// Assert
		decision.MatchedRules.Count.ShouldBe(2);
		decision.MatchedRules.ShouldContain("transport:kafka");
		decision.MatchedRules.ShouldContain("endpoint:analytics");
	}

	[Fact]
	public void CreateSuccessDecisionWithEmptyMatchedRulesWhenNull()
	{
		// Arrange & Act
		var decision = RoutingDecision.Success("local", Array.Empty<string>());

		// Assert
		decision.MatchedRules.ShouldBeEmpty();
	}

	[Fact]
	public void CreateSuccessDecisionWithEmptyEndpoints()
	{
		// Arrange & Act
		var decision = RoutingDecision.Success("local", Array.Empty<string>());

		// Assert
		decision.Endpoints.ShouldBeEmpty();
		decision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullTransportForSuccess()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Success(null!, SingleEndpoint));
	}

	[Fact]
	public void ThrowOnEmptyTransportForSuccess()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Success("", SingleEndpoint));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportForSuccess()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Success("   ", SingleEndpoint));
	}

	[Fact]
	public void ThrowOnNullEndpointsForSuccess()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => RoutingDecision.Success("local", null!));
	}

	#endregion

	#region Failure factory method

	[Fact]
	public void CreateFailureDecisionWithReason()
	{
		// Arrange & Act
		var decision = RoutingDecision.Failure("No matching routing rules found");

		// Assert
		decision.Transport.ShouldBe(string.Empty);
		decision.Endpoints.ShouldBeEmpty();
		decision.IsSuccess.ShouldBeFalse();
		decision.FailureReason.ShouldBe("No matching routing rules found");
	}

	[Fact]
	public void ThrowOnNullReasonForFailure()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Failure(null!));
	}

	[Fact]
	public void ThrowOnEmptyReasonForFailure()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Failure(""));
	}

	[Fact]
	public void ThrowOnWhitespaceReasonForFailure()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => RoutingDecision.Failure("   "));
	}

	[Fact]
	public void HaveEmptyMatchedRulesOnFailure()
	{
		// Arrange & Act
		var decision = RoutingDecision.Failure("Some error");

		// Assert
		decision.MatchedRules.ShouldBeEmpty();
	}

	#endregion

	#region IsSuccess property

	[Fact]
	public void ReturnTrueForIsSuccessWhenTransportIsSet()
	{
		// Arrange & Act
		var decision = RoutingDecision.Success("rabbitmq", Array.Empty<string>());

		// Assert
		decision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForIsSuccessWhenTransportIsEmpty()
	{
		// Arrange & Act
		var decision = RoutingDecision.Failure("reason");

		// Assert
		decision.IsSuccess.ShouldBeFalse();
	}

	#endregion

	#region Record equality

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var endpoints = new List<string> { "ep1" }.AsReadOnly();
		var rules = new List<string> { "rule1" }.AsReadOnly();

		var decision1 = RoutingDecision.Success("local", endpoints, rules);
		var decision2 = RoutingDecision.Success("local", endpoints, rules);

		// Assert
		decision1.ShouldBe(decision2);
	}

	[Fact]
	public void DetectInequalityWithDifferentTransport()
	{
		// Arrange
		var endpoints = new List<string> { "ep1" }.AsReadOnly();

		var decision1 = RoutingDecision.Success("local", endpoints);
		var decision2 = RoutingDecision.Success("rabbitmq", endpoints);

		// Assert
		decision1.ShouldNotBe(decision2);
	}

	#endregion

	#region With expression (record copying)

	[Fact]
	public void SupportWithExpression()
	{
		// Arrange
		var original = RoutingDecision.Success("local", SingleEndpoint);

		// Act
		var modified = original with { Transport = "rabbitmq" };

		// Assert
		modified.Transport.ShouldBe("rabbitmq");
		modified.Endpoints.ShouldContain("ep1");
		original.Transport.ShouldBe("local");
	}

	#endregion
}
