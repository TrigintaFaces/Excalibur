// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Background auto-commit is refused at startup, because a guarantee a consumer can switch off with a flag
/// is not a guarantee.
/// </summary>
/// <remarks>
/// The transport commits only offsets whose messages reached a terminal state. A timer that commits
/// independently of that can advance the group past messages still inside handlers, which are then never
/// delivered to any member again — silently.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaAutoCommitRefusalShould
{
	/// <summary>SAFETY. The flag that defeats the settlement guarantee fails validation.</summary>
	/// <remarks>RED against a validator that accepts it, which is what shipped before.</remarks>
	[Fact]
	public void RefuseConsumerAutoCommit()
	{
		var options = Valid();
		options.Consumer.EnableAutoCommit = true;

		var result = new KafkaOptionsValidator().Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("EnableAutoCommit");
	}

	/// <summary>
	/// LIVENESS. An otherwise valid configuration still passes, so the refusal is specific rather than a
	/// validator that rejects everything.
	/// </summary>
	[Fact]
	public void AcceptAConfigurationThatLeavesAutoCommitOff()
	{
		var result = new KafkaOptionsValidator().Validate(name: null, Valid());

		result.Succeeded.ShouldBeTrue(result.FailureMessage);
	}

	private static KafkaOptions Valid() =>
		new()
		{
			BootstrapServers = "localhost:9092",
			ConsumerGroup = "orders",
		};
}
