// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Publisher;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqPublisherOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqPublisherOptions();

		// Assert
		options.EnableConfirms.ShouldBeTrue();
		options.ConfirmTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.MandatoryPublishing.ShouldBeTrue();
		options.Persistence.ShouldBe(RabbitMqPersistence.Persistent);
		options.MessageTtl.ShouldBe(TimeSpan.FromDays(7));
		options.MaxMessageSizeBytes.ShouldBe(128 * 1024 * 1024);
		options.EnableBatchPublishing.ShouldBeFalse();
		options.MaxBatchSize.ShouldBe(100);
		options.BatchFlushInterval.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqPublisherOptions
		{
			EnableConfirms = false,
			ConfirmTimeout = TimeSpan.FromSeconds(10),
			MandatoryPublishing = false,
			Persistence = RabbitMqPersistence.Transient,
			MessageTtl = TimeSpan.FromHours(1),
			MaxMessageSizeBytes = 64 * 1024 * 1024,
			EnableBatchPublishing = true,
			MaxBatchSize = 500,
			BatchFlushInterval = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.EnableConfirms.ShouldBeFalse();
		options.ConfirmTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.MandatoryPublishing.ShouldBeFalse();
		options.Persistence.ShouldBe(RabbitMqPersistence.Transient);
		options.MessageTtl.ShouldBe(TimeSpan.FromHours(1));
		options.MaxMessageSizeBytes.ShouldBe(64 * 1024 * 1024);
		options.EnableBatchPublishing.ShouldBeTrue();
		options.MaxBatchSize.ShouldBe(500);
		options.BatchFlushInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void AllowNullMessageTtl()
	{
		// Arrange & Act
		var options = new RabbitMqPublisherOptions { MessageTtl = null };

		// Assert
		options.MessageTtl.ShouldBeNull();
	}
}
