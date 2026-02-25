// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationServiceShould : UnitTestBase
{
	private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
	private readonly IHostApplicationLifetime _appLifetime = A.Fake<IHostApplicationLifetime>();

	[Fact]
	public async Task SkipValidationWhenDisabled()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		var options = new ConfigurationValidationOptions { Enabled = false };
		var service = CreateService([validator], options);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Assert - validator should never be called
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RunAllValidatorsInPriorityOrder()
	{
		// Arrange
		var callOrder = new List<string>();

		var validator1 = CreatePassingValidator("High", priority: 10);
		A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callOrder.Add("High");
				return Task.FromResult(ConfigurationValidationResult.Success());
			});

		var validator2 = CreatePassingValidator("Low", priority: 1);
		A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callOrder.Add("Low");
				return Task.FromResult(ConfigurationValidationResult.Success());
			});

		var service = CreateService([validator1, validator2]);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Assert - Lower priority number should run first
		callOrder.Count.ShouldBe(2);
		callOrder[0].ShouldBe("Low");
		callOrder[1].ShouldBe("High");
	}

	[Fact]
	public async Task PassWhenAllValidatorsSucceed()
	{
		// Arrange
		var validator = CreatePassingValidator("TestValidator");
		var service = CreateService([validator]);

		// Act & Assert - should not throw
		await service.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ThrowWhenValidationFailsAndFailFastIsTrue()
	{
		// Arrange
		var validator = CreateFailingValidator("BadConfig", "Value is invalid");
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = CreateService([validator], options);

		// Act & Assert
		await Should.ThrowAsync<ConfigurationValidationException>(
			() => service.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task NotThrowWhenValidationFailsAndFailFastIsFalse()
	{
		// Arrange
		var validator = CreateFailingValidator("BadConfig", "Value is invalid");
		var options = new ConfigurationValidationOptions { FailFast = false };
		var service = CreateService([validator], options);

		// Act & Assert - should not throw
		await service.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopApplicationWhenFailFastAndValidationFails()
	{
		// Arrange
		var validator = CreateFailingValidator("BadConfig", "Value is invalid");
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = CreateService([validator], options);

		// Act
		try
		{
			await service.StartAsync(CancellationToken.None);
		}
		catch (ConfigurationValidationException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _appLifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task TreatValidatorExceptionsAsErrorsWhenConfigured()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		A.CallTo(() => validator.ConfigurationName).Returns("CrashyValidator");
		A.CallTo(() => validator.Priority).Returns(0);
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Validator crashed"));

		var options = new ConfigurationValidationOptions
		{
			FailFast = true,
			TreatValidatorExceptionsAsErrors = true,
		};
		var service = CreateService([validator], options);

		// Act & Assert
		await Should.ThrowAsync<ConfigurationValidationException>(
			() => service.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task IgnoreValidatorExceptionsWhenConfigured()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		A.CallTo(() => validator.ConfigurationName).Returns("CrashyValidator");
		A.CallTo(() => validator.Priority).Returns(0);
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Validator crashed"));

		var options = new ConfigurationValidationOptions
		{
			TreatValidatorExceptionsAsErrors = false,
		};
		var service = CreateService([validator], options);

		// Act & Assert - should not throw
		await service.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ReturnCompletedTaskOnStopAsync()
	{
		// Arrange
		var service = CreateService([]);

		// Act
		await service.StopAsync(CancellationToken.None);

		// Assert - should complete without error
	}

	[Fact]
	public async Task HandleMultipleValidatorsWithMixedResults()
	{
		// Arrange
		var passingValidator = CreatePassingValidator("GoodConfig");
		var failingValidator = CreateFailingValidator("BadConfig", "Invalid value");
		var options = new ConfigurationValidationOptions { FailFast = false };
		var service = CreateService([passingValidator, failingValidator], options);

		// Act & Assert - should not throw when fail fast is false
		await service.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task IncludeAllErrorsInException()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		A.CallTo(() => validator.ConfigurationName).Returns("MultiError");
		A.CallTo(() => validator.Priority).Returns(0);
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var errors = new List<ConfigurationValidationError>
				{
					new("Error 1", "Path1"),
					new("Error 2", "Path2"),
				};
				return Task.FromResult(ConfigurationValidationResult.Failure(errors));
			});

		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = CreateService([validator], options);

		// Act
		var ex = await Should.ThrowAsync<ConfigurationValidationException>(
			() => service.StartAsync(CancellationToken.None));

		// Assert
		ex.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ConfigurationValidationService(
			null!, [], _appLifetime, NullLogger<ConfigurationValidationService>.Instance));
	}

	[Fact]
	public void ThrowWhenValidatorsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ConfigurationValidationService(
			_configuration, null!, _appLifetime, NullLogger<ConfigurationValidationService>.Instance));
	}

	[Fact]
	public void ThrowWhenLifetimeIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ConfigurationValidationService(
			_configuration, [], null!, NullLogger<ConfigurationValidationService>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ConfigurationValidationService(
			_configuration, [], _appLifetime, null!));
	}

	[Fact]
	public void UseDefaultOptionsWhenNullProvided()
	{
		// Act
		var service = new ConfigurationValidationService(
			_configuration, [], _appLifetime,
			NullLogger<ConfigurationValidationService>.Instance,
			options: null);

		// Assert - should not throw, uses defaults
		service.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleEmptyValidatorCollection()
	{
		// Arrange
		var service = CreateService([]);

		// Act & Assert - should pass with no validators
		await service.StartAsync(CancellationToken.None);
	}

	private ConfigurationValidationService CreateService(
		IEnumerable<IConfigurationValidator> validators,
		ConfigurationValidationOptions? options = null)
	{
		return new ConfigurationValidationService(
			_configuration,
			validators,
			_appLifetime,
			NullLogger<ConfigurationValidationService>.Instance,
			options);
	}

	private static IConfigurationValidator CreatePassingValidator(string name, int priority = 0)
	{
		var validator = A.Fake<IConfigurationValidator>();
		A.CallTo(() => validator.ConfigurationName).Returns(name);
		A.CallTo(() => validator.Priority).Returns(priority);
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(Task.FromResult(ConfigurationValidationResult.Success()));
		return validator;
	}

	private static IConfigurationValidator CreateFailingValidator(string name, string errorMessage)
	{
		var validator = A.Fake<IConfigurationValidator>();
		A.CallTo(() => validator.ConfigurationName).Returns(name);
		A.CallTo(() => validator.Priority).Returns(0);
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(ConfigurationValidationResult.Failure(errorMessage, name)));
		return validator;
	}
}
