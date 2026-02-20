// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportConfigurationValidator"/> (Sprint 211).
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportConfigurationValidatorShould : UnitTestBase
{
	private readonly TransportConfigurationValidator _validator = new();

	#region Constructor Tests

	[Fact]
	public void CreateInstance_Successfully()
	{
		// Arrange & Act
		var validator = new TransportConfigurationValidator();

		// Assert
		_ = validator.ShouldNotBeNull();
		_ = validator.ShouldBeAssignableTo<ITransportConfigurationValidator>();
	}

	#endregion Constructor Tests

	#region Null Argument Tests

	[Fact]
	public void Validate_ThrowArgumentNullException_WhenRegistrationsIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _validator.Validate(null!));
	}

	#endregion Null Argument Tests

	#region Empty Registrations Tests

	[Fact]
	public void Validate_ReturnSuccess_WhenRegistrationsIsEmpty()
	{
		// Arrange
		var registrations = Enumerable.Empty<TransportRegistrationInfo>();

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion Empty Registrations Tests

	#region Valid Registration Tests

	[Fact]
	public void Validate_ReturnSuccess_ForValidKafkaRegistration()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-orders", "kafka", new Dictionary<string, object>
			{
				["BootstrapServers"] = "localhost:9092"
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidRabbitMQRegistration()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("rabbitmq-events", "rabbitmq", new Dictionary<string, object>
			{
				["ConnectionString"] = "amqp://localhost"
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidAzureServiceBusRegistration()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("asb-notifications", "azure-servicebus", new Dictionary<string, object>
			{
				["ConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/"
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidCronTimerRegistration()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("hourly-job", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "0 * * * *"
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidInMemoryRegistration()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("in-memory-test", "inmemory")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidRegistrationWithoutOptions()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-orders", "kafka")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion Valid Registration Tests

	#region Duplicate Name Tests

	[Fact]
	public void Validate_ReturnFailure_ForDuplicateTransportNames()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("events", "kafka"),
			new TransportRegistrationInfo("events", "rabbitmq") // Duplicate name
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.TransportName == "events" && e.Property == "Name");
	}

	[Fact]
	public void Validate_DetectDuplicates_CaseInsensitively()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("Events", "kafka"),
			new TransportRegistrationInfo("EVENTS", "rabbitmq"), // Case-insensitive duplicate
			new TransportRegistrationInfo("events", "crontimer")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1); // One duplicate group
	}

	#endregion Duplicate Name Tests

	#region Empty/Null Name Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyTransportName()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("", "kafka")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "Name");
	}

	[Fact]
	public void Validate_ReturnFailure_ForWhitespaceTransportName()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("   ", "kafka")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "Name");
	}

	#endregion Empty/Null Name Tests

	#region Empty/Null TransportType Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyTransportType()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("test", "")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "TransportType");
	}

	[Fact]
	public void Validate_ReturnFailure_ForWhitespaceTransportType()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("test", "   ")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "TransportType");
	}

	#endregion Empty/Null TransportType Tests

	#region Kafka Validation Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyKafkaBootstrapServers()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-test", "kafka", new Dictionary<string, object>
			{
				["BootstrapServers"] = ""
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.TransportName == "kafka-test" && e.Property == "BootstrapServers");
	}

	[Fact]
	public void Validate_ReturnFailure_ForWhitespaceKafkaBootstrapServers()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-test", "kafka", new Dictionary<string, object>
			{
				["BootstrapServers"] = "   "
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "BootstrapServers");
	}

	#endregion Kafka Validation Tests

	#region RabbitMQ Validation Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyRabbitMQConnectionString()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("rabbit-test", "rabbitmq", new Dictionary<string, object>
			{
				["ConnectionString"] = ""
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.TransportName == "rabbit-test" && e.Property == "ConnectionString");
	}

	#endregion RabbitMQ Validation Tests

	#region Azure Service Bus Validation Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyAzureServiceBusConnectionString()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("asb-test", "azure-servicebus", new Dictionary<string, object>
			{
				["ConnectionString"] = ""
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.TransportName == "asb-test" && e.Property == "ConnectionString");
	}

	#endregion Azure Service Bus Validation Tests

	#region CronTimer Validation Tests

	[Fact]
	public void Validate_ReturnFailure_ForEmptyCronExpression()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("cron-test", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = ""
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.TransportName == "cron-test" && e.Property == "CronExpression");
	}

	[Fact]
	public void Validate_ReturnFailure_ForInvalidCronExpression_TooFewFields()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("cron-test", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "* * *" // Only 3 fields
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "CronExpression" && e.Message.Contains("5 or 6"));
	}

	[Fact]
	public void Validate_ReturnFailure_ForInvalidCronExpression_TooManyFields()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("cron-test", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "* * * * * * *" // 7 fields
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Property == "CronExpression");
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidCronExpression_5Fields()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("cron-test", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "0 */5 * * *" // 5 fields (minute-level)
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnSuccess_ForValidCronExpression_6Fields()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("cron-test", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "0 0 */5 * * *" // 6 fields (second-level)
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion CronTimer Validation Tests

	#region Multiple Registrations Tests

	[Fact]
	public void Validate_ReturnSuccess_ForMultipleValidRegistrations()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-orders", "kafka", new Dictionary<string, object>
			{
				["BootstrapServers"] = "localhost:9092"
			}),
			new TransportRegistrationInfo("rabbit-events", "rabbitmq", new Dictionary<string, object>
			{
				["ConnectionString"] = "amqp://localhost"
			}),
			new TransportRegistrationInfo("cron-hourly", "crontimer", new Dictionary<string, object>
			{
				["CronExpression"] = "0 * * * *"
			}),
			new TransportRegistrationInfo("in-memory-test", "inmemory")
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Validate_CollectAllErrors_FromMultipleInvalidRegistrations()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("", "kafka"), // Empty name
			new TransportRegistrationInfo("valid", ""), // Empty type
			new TransportRegistrationInfo("kafka-empty", "kafka", new Dictionary<string, object>
			{
				["BootstrapServers"] = ""
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	#endregion Multiple Registrations Tests

	#region Case Insensitivity Tests

	[Fact]
	public void Validate_HandleTransportType_CaseInsensitively()
	{
		// Arrange
		var registrations = new[]
		{
			new TransportRegistrationInfo("kafka-upper", "KAFKA", new Dictionary<string, object>
			{
				["BootstrapServers"] = "" // Empty - should still trigger validation
			}),
			new TransportRegistrationInfo("rabbit-mixed", "RabbitMQ", new Dictionary<string, object>
			{
				["ConnectionString"] = "" // Empty - should still trigger validation
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	#endregion Case Insensitivity Tests

	#region Unknown Transport Type Tests

	[Fact]
	public void Validate_ReturnSuccess_ForUnknownTransportType()
	{
		// Arrange - Unknown transport types have no required options
		var registrations = new[]
		{
			new TransportRegistrationInfo("custom-transport", "custom-type", new Dictionary<string, object>
			{
				["SomeOption"] = "some-value"
			})
		};

		// Act
		var result = _validator.Validate(registrations);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion Unknown Transport Type Tests

	#region TransportValidationResult Factory Tests

	[Fact]
	public void TransportValidationResult_Success_ReturnsValidResult()
	{
		// Act
		var result = TransportValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void TransportValidationResult_FailureWithSingleError_ReturnsInvalidResult()
	{
		// Act
		var result = TransportValidationResult.Failure("test-transport", "Property", "Error message");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].TransportName.ShouldBe("test-transport");
		result.Errors[0].Property.ShouldBe("Property");
		result.Errors[0].Message.ShouldBe("Error message");
	}

	[Fact]
	public void TransportValidationResult_FailureWithMultipleErrors_ReturnsInvalidResult()
	{
		// Arrange
		var errors = new[]
		{
			new TransportValidationError("transport1", "Prop1", "Error1"),
			new TransportValidationError("transport2", "Prop2", "Error2")
		};

		// Act
		var result = TransportValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	#endregion TransportValidationResult Factory Tests
}
