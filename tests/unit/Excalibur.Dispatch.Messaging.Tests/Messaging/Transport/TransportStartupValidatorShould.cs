// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportStartupValidator"/>.
/// Tests startup validation per Sprint 34 bd-4jek.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class TransportStartupValidatorShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowWhenRegistryIsNull()
	{
		// Arrange
		var options = new TransportValidationOptions();
		var logger = NullLogger<TransportStartupValidator>.Instance;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransportStartupValidator(null!, options, [], logger));
	}

	[Fact]
	public void Constructor_ThrowWhenOptionsIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();
		var logger = NullLogger<TransportStartupValidator>.Instance;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransportStartupValidator(registry, null!, [], logger));
	}

	[Fact]
	public void Constructor_ThrowWhenLoggerIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new TransportValidationOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransportStartupValidator(registry, options, [], null!));
	}

	#endregion Constructor Tests

	#region StartAsync Tests - Validation Disabled

	[Fact]
	public async Task StartAsync_SkipValidationWhenDisabled()
	{
		// Arrange
		var registry = new TransportRegistry(); // No transports
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = false,
			RequireAtLeastOneTransport = true // Would fail if validation ran
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	#endregion StartAsync Tests - Validation Disabled

	#region StartAsync Tests - RequireAtLeastOneTransport

	[Fact]
	public async Task StartAsync_PassWhenNoTransportsAndNotRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireAtLeastOneTransport = false
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_ThrowWhenNoTransportsAndRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireAtLeastOneTransport = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("No transports are registered");
		ex.Message.ShouldContain("AddRabbitMQTransport");
	}

	[Fact]
	public async Task StartAsync_PassWhenTransportExistsAndRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireAtLeastOneTransport = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	#endregion StartAsync Tests - RequireAtLeastOneTransport

	#region StartAsync Tests - RequireDefaultTransportWhenMultiple

	[Fact]
	public async Task StartAsync_PassWhenSingleTransportWithoutDefault()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireDefaultTransportWhenMultiple = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw (only 1 transport)
		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_ThrowWhenMultipleTransportsWithoutDefault()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireDefaultTransportWhenMultiple = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("2 transports are registered");
		ex.Message.ShouldContain("rabbitmq");
		ex.Message.ShouldContain("kafka");
		ex.Message.ShouldContain("no default transport is set");
		ex.Message.ShouldContain("SetDefaultTransport");
	}

	[Fact]
	public async Task StartAsync_PassWhenMultipleTransportsWithDefault()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		registry.SetDefaultTransport("rabbitmq");
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireDefaultTransportWhenMultiple = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_PassWhenMultipleTransportsWithoutDefaultButNotRequired()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireDefaultTransportWhenMultiple = false
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	#endregion StartAsync Tests - RequireDefaultTransportWhenMultiple

	#region StartAsync Tests - Combined Scenarios

	[Fact]
	public async Task StartAsync_ValidateAllRulesSuccessfully()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		registry.RegisterTransport("servicebus", CreateAdapter("3"), "ServiceBus");
		registry.SetDefaultTransport("kafka");

		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportWhenMultiple = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should not throw
		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_FailAtLeastOneTransportFirst()
	{
		// Arrange - No transports, both validations enabled
		var registry = new TransportRegistry();
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = true,
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportWhenMultiple = true
		};
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should fail on "no transports" first
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("No transports are registered");
	}

	#endregion StartAsync Tests - Combined Scenarios

	#region StopAsync Tests

	[Fact]
	public async Task StopAsync_CompleteSuccessfully()
	{
		// Arrange
		var registry = new TransportRegistry();
		var options = new TransportValidationOptions();
		var validator = CreateValidator(registry, options);

		// Act & Assert - Should complete without error
		await validator.StopAsync(CancellationToken.None);
	}

	#endregion StopAsync Tests

	#region TransportValidationOptions Tests

	[Fact]
	public void TransportValidationOptions_HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new TransportValidationOptions();

		// Assert
		options.ValidateOnStartup.ShouldBeTrue();
		options.RequireAtLeastOneTransport.ShouldBeFalse();
		options.RequireDefaultTransportWhenMultiple.ShouldBeTrue();
	}

	[Fact]
	public void TransportValidationOptions_AllowSettingValues()
	{
		// Arrange & Act
		var options = new TransportValidationOptions
		{
			ValidateOnStartup = false,
			RequireAtLeastOneTransport = true,
			RequireDefaultTransportWhenMultiple = false
		};

		// Assert
		options.ValidateOnStartup.ShouldBeFalse();
		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportWhenMultiple.ShouldBeFalse();
	}

	#endregion TransportValidationOptions Tests

	private static TransportStartupValidator CreateValidator(
		TransportRegistry registry,
		TransportValidationOptions options)
	{
		return new TransportStartupValidator(
			registry,
			options,
			[],
			NullLogger<TransportStartupValidator>.Instance);
	}

	private static ITransportAdapter CreateAdapter(string name)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.TransportType).Returns("Test");
		_ = A.CallTo(() => adapter.IsRunning).Returns(true);
		return adapter;
	}
}
