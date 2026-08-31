// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Hosting;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// The AWS SNS transport registered its options UNNAMED, so a host adding two named SNS transports
/// wrote both configurations into one instance and the second silently won. The XML example on
/// <c>AddAwsSnsTransport</c> demonstrates exactly that two-transport scenario, so the defect was
/// reachable from the documented usage.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AwsSnsNamedOptionsShould
{
	private static AwsSnsOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get(name);

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("orders", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789012:orders-topic")
			.Region("us-east-1"));

		_ = services.AddAwsSnsTransport("payments", sns => sns
			.TopicArn("arn:aws:sns:us-west-2:123456789012:payments-topic")
			.Region("us-west-2"));

		using var provider = services.BuildServiceProvider();

		Resolve(provider, "orders").TopicArn.ShouldBe("arn:aws:sns:us-east-1:123456789012:orders-topic");
		Resolve(provider, "orders").Connection.RegionEndpoint.ShouldBe("us-east-1");
		Resolve(provider, "payments").TopicArn.ShouldBe("arn:aws:sns:us-west-2:123456789012:payments-topic");
		Resolve(provider, "payments").Connection.RegionEndpoint.ShouldBe("us-west-2");
	}

	[Fact]
	public void GiveEachNamedTransportItsOwnTopicEncryptionApplier()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsSnsTransport("plain", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789012:plain-topic")
			.Region("us-east-1"));

		_ = services.AddAwsSnsTransport("encrypted", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789012:encrypted-topic")
			.Region("us-east-1")
			.EnableEncryption("alias/orders-key"));

		using var provider = services.BuildServiceProvider();

		// The applier was registered by TYPE through TryAddEnumerable, which de-duplicates, so two
		// named transports shared ONE applier reading ONE unnamed configuration. Each named transport
		// now contributes its own, or a per-transport KMS key cannot be applied to its own topic.
		var appliers = provider.GetServices<IHostedService>()
			.Where(static service => service.GetType().Name == "AwsSnsTopicEncryptionService")
			.ToList();

		appliers.Count.ShouldBe(2);
	}

	[Fact]
	public void RejectAConfigurationWithNoTopic()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("nameless", sns => sns.Region("us-east-1"));

		using var provider = services.BuildServiceProvider();

		// Liveness's counterpart: naming the options must not stop them being validated. A registration
		// that quietly bypassed validation would pass every "the value arrives" assertion above.
		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider, "nameless"));
	}
}
