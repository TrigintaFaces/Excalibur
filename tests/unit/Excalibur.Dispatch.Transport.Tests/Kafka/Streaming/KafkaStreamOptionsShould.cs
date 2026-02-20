// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Streaming;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaStreamOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new KafkaStreamOptions();

		// Assert
		options.InputTopic.ShouldBe(string.Empty);
		options.OutputTopic.ShouldBe(string.Empty);
		options.ApplicationId.ShouldBe(string.Empty);
		options.ProcessingGuarantee.ShouldBe(ProcessingGuarantee.AtLeastOnce);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new KafkaStreamOptions
		{
			InputTopic = "raw-events",
			OutputTopic = "processed-events",
			ApplicationId = "event-processor",
			ProcessingGuarantee = ProcessingGuarantee.ExactlyOnce,
		};

		// Assert
		options.InputTopic.ShouldBe("raw-events");
		options.OutputTopic.ShouldBe("processed-events");
		options.ApplicationId.ShouldBe("event-processor");
		options.ProcessingGuarantee.ShouldBe(ProcessingGuarantee.ExactlyOnce);
	}
}
