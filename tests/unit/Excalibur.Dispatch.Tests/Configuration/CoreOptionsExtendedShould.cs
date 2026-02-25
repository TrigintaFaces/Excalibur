// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CoreOptionsExtendedShould
{
	// --- DeadLetterOptions ---

	[Fact]
	public void DeadLetterOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DeadLetterOptions();

		// Assert
		options.MaxAttempts.ShouldBe(3);
		options.QueueName.ShouldBe("deadletter");
		options.PreserveMetadata.ShouldBeTrue();
		options.IncludeExceptionDetails.ShouldBeTrue();
		options.EnableRecovery.ShouldBeFalse();
		options.RecoveryInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void DeadLetterOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DeadLetterOptions
		{
			MaxAttempts = 5,
			QueueName = "custom-dlq",
			PreserveMetadata = false,
			IncludeExceptionDetails = false,
			EnableRecovery = true,
			RecoveryInterval = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.MaxAttempts.ShouldBe(5);
		options.QueueName.ShouldBe("custom-dlq");
		options.PreserveMetadata.ShouldBeFalse();
		options.IncludeExceptionDetails.ShouldBeFalse();
		options.EnableRecovery.ShouldBeTrue();
		options.RecoveryInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	// --- MessageRoutingOptions ---

	[Fact]
	public void MessageRoutingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessageRoutingOptions();

		// Assert
		options.DefaultRoutingPattern.ShouldBe("{MessageType}");
		options.UseMessageTypeAsRoutingKey.ShouldBeTrue();
		options.MessageTypeRouting.ShouldNotBeNull();
		options.MessageTypeRouting.ShouldBeEmpty();
		options.RoutingKeyGenerators.ShouldNotBeNull();
		options.RoutingKeyGenerators.ShouldBeEmpty();
	}

	[Fact]
	public void MessageRoutingOptions_MessageTypeRouting_CanAddEntries()
	{
		// Arrange
		var options = new MessageRoutingOptions();

		// Act
		options.MessageTypeRouting["OrderCreated"] = "orders-topic";
		options.MessageTypeRouting["PaymentProcessed"] = "payments-topic";

		// Assert
		options.MessageTypeRouting.Count.ShouldBe(2);
		options.MessageTypeRouting["OrderCreated"].ShouldBe("orders-topic");
	}

	[Fact]
	public void MessageRoutingOptions_RoutingKeyGenerators_CanAddEntries()
	{
		// Arrange
		var options = new MessageRoutingOptions();

		// Act
		options.RoutingKeyGenerators["OrderCreated"] = obj => $"order-{obj.GetHashCode()}";

		// Assert
		options.RoutingKeyGenerators.Count.ShouldBe(1);
		options.RoutingKeyGenerators["OrderCreated"].ShouldNotBeNull();
	}

	[Fact]
	public void MessageRoutingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MessageRoutingOptions
		{
			DefaultRoutingPattern = "{Namespace}.{MessageType}",
			UseMessageTypeAsRoutingKey = false,
		};

		// Assert
		options.DefaultRoutingPattern.ShouldBe("{Namespace}.{MessageType}");
		options.UseMessageTypeAsRoutingKey.ShouldBeFalse();
	}

	// --- SerializationOptions ---

	[Fact]
	public void SerializationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SerializationOptions();

		// Assert
		options.EmbedMessageType.ShouldBeTrue();
		options.IncludeAssemblyInfo.ShouldBeFalse();
		options.DefaultBufferSize.ShouldBe(4096);
		options.UseBufferPooling.ShouldBeTrue();
	}

	[Fact]
	public void SerializationOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SerializationOptions
		{
			EmbedMessageType = false,
			IncludeAssemblyInfo = true,
			DefaultBufferSize = 8192,
			UseBufferPooling = false,
		};

		// Assert
		options.EmbedMessageType.ShouldBeFalse();
		options.IncludeAssemblyInfo.ShouldBeTrue();
		options.DefaultBufferSize.ShouldBe(8192);
		options.UseBufferPooling.ShouldBeFalse();
	}

	// --- LoggingOptions ---

	[Fact]
	public void LoggingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new LoggingOptions();

		// Assert
		options.EnhancedLogging.ShouldBeFalse();
		options.IncludeCorrelationIds.ShouldBeTrue();
		options.IncludeExecutionContext.ShouldBeTrue();
	}

	[Fact]
	public void LoggingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new LoggingOptions
		{
			EnhancedLogging = true,
			IncludeCorrelationIds = false,
			IncludeExecutionContext = false,
		};

		// Assert
		options.EnhancedLogging.ShouldBeTrue();
		options.IncludeCorrelationIds.ShouldBeFalse();
		options.IncludeExecutionContext.ShouldBeFalse();
	}

	// --- MetricsOptions ---

	[Fact]
	public void MetricsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MetricsOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.CustomTags.ShouldNotBeNull();
		options.CustomTags.ShouldBeEmpty();
	}

	[Fact]
	public void MetricsOptions_CustomTags_CanAddEntries()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.CustomTags["environment"] = "production";
		options.CustomTags["service"] = "orders";

		// Assert
		options.CustomTags.Count.ShouldBe(2);
		options.CustomTags["environment"].ShouldBe("production");
	}

	[Fact]
	public void MetricsOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MetricsOptions
		{
			Enabled = true,
			ExportInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ExportInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
