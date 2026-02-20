// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConfluentConsumerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ConfluentConsumerOptions();

		// Assert
		options.AutoAcknowledge.ShouldBeTrue();
		options.ErrorHandling.ShouldBe(DeserializationErrorHandling.DeadLetter);
		options.DeadLetterTopic.ShouldBeNull();
		options.EnableUpcasting.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ConfluentConsumerOptions
		{
			AutoAcknowledge = false,
			ErrorHandling = DeserializationErrorHandling.Skip,
			DeadLetterTopic = "my-dlq",
			EnableUpcasting = false,
		};

		// Assert
		options.AutoAcknowledge.ShouldBeFalse();
		options.ErrorHandling.ShouldBe(DeserializationErrorHandling.Skip);
		options.DeadLetterTopic.ShouldBe("my-dlq");
		options.EnableUpcasting.ShouldBeFalse();
	}
}
