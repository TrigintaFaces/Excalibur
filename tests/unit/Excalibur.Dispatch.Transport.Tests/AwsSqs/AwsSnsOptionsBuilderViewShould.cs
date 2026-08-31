// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Liveness lock for the AWS SNS options collapse. The registration used to translate a parallel
/// builder-facing options model into the canonical <see cref="AwsSnsOptions"/> field by field; the
/// builder now writes straight into the instance the options system owns. These assert the property
/// that matters — a value set through the public fluent API is observable at the runtime seam that
/// consumes it — including the two fields whose loss would be silent rather than loud.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AwsSnsOptionsBuilderViewShould
{
	[Fact]
	public void CarryEveryFluentlyConfiguredValueToTheResolvedOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("notifications", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789:my-topic")
			.Region("us-west-2"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get("notifications");

		options.TopicArn.ShouldBe("arn:aws:sns:us-east-1:123456789:my-topic");

		// Region is re-shaped on the way in: the fluent call is flat, the canonical model nests it
		// under the connection group. That re-shaping is what the deleted copy list used to perform.
		options.Connection.RegionEndpoint.ShouldBe("us-west-2");
	}

	[Fact]
	public void CarryBothFieldsTheEncryptionHostedServiceRequires()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("secure", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789:secure-topic")
			.EnableEncryption("alias/my-kms-key"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get("secure");

		// The topic-encryption hosted service returns early unless EnableEncryption is set and only
		// then reads the key, so carrying the key without the flag disables encryption in silence —
		// no exception, no log, just an unencrypted topic. Both arms are asserted deliberately.
		options.EnableEncryption.ShouldBeTrue();
		options.KmsMasterKeyId.ShouldBe("alias/my-kms-key");
	}

	[Fact]
	public void LetConfigureOptionsReachFieldsWithNoFluentMethod()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("raw", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789:raw-topic")
			.ConfigureOptions(o =>
			{
				o.Connection.MaxErrorRetry = 9;
				o.Connection.Timeout = TimeSpan.FromSeconds(11);
			}));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get("raw");

		// ConfigureOptions now hands the consumer the canonical model, so fields the fluent surface
		// does not cover are reachable without a second options type existing to hold them.
		options.Connection.MaxErrorRetry.ShouldBe(9);
		options.Connection.Timeout.ShouldBe(TimeSpan.FromSeconds(11));
	}
}
