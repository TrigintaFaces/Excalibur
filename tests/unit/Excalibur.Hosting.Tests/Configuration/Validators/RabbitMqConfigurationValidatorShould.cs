// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="RabbitMqConfigurationValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class RabbitMqConfigurationValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void SetDefaultConfigurationName()
	{
		// Act
		var validator = new RabbitMqConfigurationValidator();

		// Assert
		validator.ConfigurationName.ShouldBe("RabbitMQ:RabbitMQ");
	}

	[Fact]
	public void SetCustomConfigurationName()
	{
		// Act
		var validator = new RabbitMqConfigurationValidator("CustomSection");

		// Assert
		validator.ConfigurationName.ShouldBe("RabbitMQ:CustomSection");
	}

	[Fact]
	public void SetPriorityTo30()
	{
		// Act
		var validator = new RabbitMqConfigurationValidator();

		// Assert
		validator.Priority.ShouldBe(30);
	}

	#endregion

	#region Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:ConnectionString"] = "amqp://guest:guest@localhost:5672"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenConnectionStringHasWrongScheme()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:ConnectionString"] = "http://localhost:5672"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid URL scheme"));
	}

	#endregion

	#region Host Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenHostIsProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenHostNameIsProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:HostName"] = "localhost"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenHostIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Port"] = "5672"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("host is missing"));
	}

	#endregion

	#region Port Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenPortIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Port"] = "5672"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenPortIsOutOfRange()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Port"] = "99999"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("outside the valid range"));
	}

	[Fact]
	public async Task ReturnFailure_WhenPortIsNotANumber()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Port"] = "abc"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("not a valid integer"));
	}

	#endregion

	#region Virtual Host Tests

	[Fact]
	public async Task ReturnSuccess_WhenVirtualHostIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:VirtualHost"] = "my-vhost"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenVirtualHostContainsSpaces()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:VirtualHost"] = "my vhost"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("cannot contain spaces"));
	}

	#endregion

	#region Credentials Tests

	[Fact]
	public async Task ReturnSuccess_WhenCredentialsAreComplete()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Username"] = "guest",
				["RabbitMQ:Password"] = "guest"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenUsingUserName()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:UserName"] = "guest",
				["RabbitMQ:Password"] = "guest"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenUsernameIsProvidedWithoutPassword()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Username"] = "guest"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("password is required"));
	}

	#endregion

	#region Exchange Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenExchangeConfigurationIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Name"] = "my-exchange",
				["RabbitMQ:Exchange:Type"] = "direct"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenExchangeNameStartsWithAmq()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Name"] = "amq.my-exchange"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("reserved"));
	}

	[Theory]
	[InlineData("direct")]
	[InlineData("topic")]
	[InlineData("fanout")]
	[InlineData("headers")]
	[InlineData("DIRECT")]
	[InlineData("Topic")]
	public async Task ReturnSuccess_WhenExchangeTypeIsValid(string exchangeType)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Type"] = exchangeType
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenExchangeTypeIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Type"] = "invalid-type"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("not valid"));
	}

	[Fact]
	public async Task ReturnFailure_WhenExchangeDurableIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Durable"] = "yes"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid durability setting"));
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData("True")]
	[InlineData("False")]
	public async Task ReturnSuccess_WhenExchangeDurableIsValid(string durableValue)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Exchange:Durable"] = durableValue
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Queue Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenQueueConfigurationIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:Name"] = "my-queue"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenQueueNameStartsWithAmq()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:Name"] = "amq.my-queue"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("reserved"));
	}

	[Fact]
	public async Task ReturnFailure_WhenQueueNameExceedsMaxLength()
	{
		// Arrange
		var longName = new string('a', 256);
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:Name"] = longName
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("exceeds maximum length"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenMessageTtlIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MessageTtl"] = "60000"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenMessageTtlIsZero()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MessageTtl"] = "0"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMessageTtlIsNegative()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MessageTtl"] = "-1"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("cannot be negative"));
	}

	[Fact]
	public async Task ReturnFailure_WhenMessageTtlIsNotANumber()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MessageTtl"] = "abc"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid message TTL format"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenMaxLengthIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MaxLength"] = "10000"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMaxLengthIsZero()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Queue:MaxLength"] = "0"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("outside the valid range"));
	}

	#endregion

	#region Connection Pool Tests

	[Fact]
	public async Task ReturnSuccess_WhenMaxConnectionsIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:MaxConnections"] = "50"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMaxConnectionsExceedsLimit()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:MaxConnections"] = "1001"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("outside the valid range"));
	}

	#endregion

	#region Heartbeat Tests

	[Fact]
	public async Task ReturnSuccess_WhenHeartbeatIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Heartbeat"] = "00:01:00"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenHeartbeatIsTooShort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Heartbeat"] = "00:00:01"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("less than the minimum"));
	}

	[Fact]
	public async Task ReturnFailure_WhenHeartbeatIsTooLong()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RabbitMQ:Host"] = "localhost",
				["RabbitMQ:Heartbeat"] = "00:15:00"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("exceeds the maximum"));
	}

	#endregion

	#region Custom Config Section Tests

	[Fact]
	public async Task ValidateCustomConfigSection()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["CustomRabbit:Host"] = "localhost"
			})
			.Build();

		var validator = new RabbitMqConfigurationValidator("CustomRabbit");

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion
}
