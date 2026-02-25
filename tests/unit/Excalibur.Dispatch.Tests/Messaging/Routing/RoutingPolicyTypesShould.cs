using Excalibur.Dispatch.Routing.Policies;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingPolicyTypesShould
{
	// --- RoutingPolicyOptions ---

	[Fact]
	public void RoutingPolicyOptions_HaveCorrectDefaults()
	{
		var options = new RoutingPolicyOptions();

		options.PolicyFilePath.ShouldBeNull();
		options.WatchForChanges.ShouldBeFalse();
		options.ThrowOnMissingFile.ShouldBeFalse();
	}

	[Fact]
	public void RoutingPolicyOptions_SetAllProperties()
	{
		var options = new RoutingPolicyOptions
		{
			PolicyFilePath = "/etc/routing/policies.json",
			WatchForChanges = true,
			ThrowOnMissingFile = true,
		};

		options.PolicyFilePath.ShouldBe("/etc/routing/policies.json");
		options.WatchForChanges.ShouldBeTrue();
		options.ThrowOnMissingFile.ShouldBeTrue();
	}

	// --- RoutingRule ---

	[Fact]
	public void RoutingRule_HaveCorrectDefaults()
	{
		var rule = new RoutingRule();

		rule.MessageTypePattern.ShouldBe(string.Empty);
		rule.Transport.ShouldBeNull();
		rule.Endpoint.ShouldBeNull();
		rule.Priority.ShouldBe(100);
		rule.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void RoutingRule_SetAllProperties()
	{
		var rule = new RoutingRule
		{
			MessageTypePattern = "Order*",
			Transport = "kafka",
			Endpoint = "orders-topic",
			Priority = 10,
			Enabled = false,
		};

		rule.MessageTypePattern.ShouldBe("Order*");
		rule.Transport.ShouldBe("kafka");
		rule.Endpoint.ShouldBe("orders-topic");
		rule.Priority.ShouldBe(10);
		rule.Enabled.ShouldBeFalse();
	}

	// --- RoutingPolicyFile ---

	[Fact]
	public void RoutingPolicyFile_HaveEmptyRulesByDefault()
	{
		var file = new RoutingPolicyFile();

		file.Rules.ShouldNotBeNull();
		file.Rules.ShouldBeEmpty();
	}

	[Fact]
	public void RoutingPolicyFile_StoreRules()
	{
		var file = new RoutingPolicyFile
		{
			Rules =
			[
				new RoutingRule { MessageTypePattern = "Order*", Transport = "kafka" },
				new RoutingRule { MessageTypePattern = "User*", Transport = "rabbitmq" },
			],
		};

		file.Rules.Count.ShouldBe(2);
		file.Rules[0].MessageTypePattern.ShouldBe("Order*");
		file.Rules[1].Transport.ShouldBe("rabbitmq");
	}
}
