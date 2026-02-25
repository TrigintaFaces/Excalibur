// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

using Microsoft.Extensions.Hosting;

using ConfigurationValidationException = Excalibur.Hosting.Configuration.ConfigurationValidationException;
using ConfigurationValidationResult = Excalibur.Hosting.Configuration.ConfigurationValidationResult;

namespace Excalibur.Tests.Hosting.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigurationValidationServiceShould
{
	private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
	private readonly IHostApplicationLifetime _applicationLifetime = A.Fake<IHostApplicationLifetime>();
	private readonly ILogger<ConfigurationValidationService> _logger = A.Fake<ILogger<ConfigurationValidationService>>();

	[Fact]
	public async Task SkipValidationWhenDisabled()
	{
		// Arrange
		var options = new ConfigurationValidationOptions { Enabled = false };
		var validators = new List<IConfigurationValidator>();

		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAllValidatorsInPriorityOrder()
	{
		// Arrange
		var validator1 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator1.ConfigurationName).Returns("Validator1");
		_ = A.CallTo(() => validator1.Priority).Returns(100);
		_ = A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Success());

		var validator2 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator2.ConfigurationName).Returns("Validator2");
		_ = A.CallTo(() => validator2.Priority).Returns(50);
		_ = A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Success());

		var validators = new List<IConfigurationValidator> { validator1, validator2 };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly());
	}

	[Fact]
	public async Task PassWhenAllValidatorsSucceed()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Success());

		var validators = new List<IConfigurationValidator> { validator };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task FailFastWhenValidationFails()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Test error"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act & Assert
		_ = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		_ = A.CallTo(() => _applicationLifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ContinueWhenFailFastIsDisabled()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Test error"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { FailFast = false };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task HandleValidatorExceptions()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Validator error"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { TreatValidatorExceptionsAsErrors = true, FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act & Assert
		_ = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		_ = A.CallTo(() => _applicationLifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IgnoreValidatorExceptionsWhenConfigured()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Validator error"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { TreatValidatorExceptionsAsErrors = false, FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task CombineErrorsFromMultipleValidators()
	{
		// Arrange
		var validator1 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator1.ConfigurationName).Returns("Validator1");
		_ = A.CallTo(() => validator1.Priority).Returns(100);
		_ = A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Error 1"));

		var validator2 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator2.ConfigurationName).Returns("Validator2");
		_ = A.CallTo(() => validator2.Priority).Returns(200);
		_ = A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Error 2"));

		var validators = new List<IConfigurationValidator> { validator1, validator2 };
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act & Assert
		var exception = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		exception.Errors.Count.ShouldBe(2);
		_ = A.CallTo(() => _applicationLifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsyncShouldCompleteSuccessfully()
	{
		// Arrange
		var validators = new List<IConfigurationValidator>();
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger);

		// Act
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert No exceptions should be thrown
	}

	[Fact]
	public async Task PassWithEmptyValidatorCollection()
	{
		// Arrange
		var validators = new List<IConfigurationValidator>();
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - no validators means no errors, no stop
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task IncludeMultipleErrorsFromSingleValidator()
	{
		// Arrange
		var errors = new List<Excalibur.Hosting.Configuration.ConfigurationValidationError>
		{
			new("Missing required field", "Database:ConnectionString"),
			new("Invalid port number", "Database:Port", value: "-1", recommendation: "Use a port between 1 and 65535"),
			new("Timeout too low", "Database:Timeout", value: "0", recommendation: "Use at least 5 seconds")
		};
		var result = ConfigurationValidationResult.Failure(errors);

		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("DatabaseValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(result);

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act & Assert
		var exception = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		exception.Errors.Count.ShouldBe(3);
		exception.Errors[0].ConfigurationPath.ShouldBe("Database:ConnectionString");
		exception.Errors[1].Value.ShouldBe("-1");
		exception.Errors[2].Recommendation.ShouldBe("Use at least 5 seconds");
	}

	[Fact]
	public async Task RunAllValidatorsEvenWhenSomeFail()
	{
		// Arrange - first validator fails, second should still run
		var validator1 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator1.ConfigurationName).Returns("FailingValidator");
		_ = A.CallTo(() => validator1.Priority).Returns(100);
		_ = A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Error from validator 1"));

		var validator2 = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator2.ConfigurationName).Returns("PassingValidator");
		_ = A.CallTo(() => validator2.Priority).Returns(200);
		_ = A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Success());

		var validators = new List<IConfigurationValidator> { validator1, validator2 };
		var options = new ConfigurationValidationOptions { FailFast = true };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act & Assert
		_ = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		// Both validators should have been called despite the first one failing
		A.CallTo(() => validator1.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => validator2.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassConfigurationToValidators()
	{
		// Arrange - use real configuration with values
		var configData = new Dictionary<string, string?>
		{
			["ConnectionStrings:Default"] = "Server=localhost;Database=test"
		};
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		IConfiguration? capturedConfig = null;
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("ConnectionValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Invokes((IConfiguration config, CancellationToken _) => capturedConfig = config)
			.Returns(ConfigurationValidationResult.Success());

		var validators = new List<IConfigurationValidator> { validator };
		var service = new ConfigurationValidationService(
			configuration,
			validators,
			_applicationLifetime,
			_logger);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - the actual configuration was passed to the validator
		capturedConfig.ShouldNotBeNull();
		capturedConfig["ConnectionStrings:Default"].ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public async Task UseDefaultOptionsWhenNullPassed()
	{
		// Arrange - options is null, should use defaults (Enabled=true, FailFast=true)
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Test error"));

		var validators = new List<IConfigurationValidator> { validator };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options: null);

		// Act & Assert - default FailFast=true should throw
		_ = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		_ = A.CallTo(() => _applicationLifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ContinueAfterExceptionWhenNotTreatedAsError()
	{
		// Arrange - validator throws but TreatValidatorExceptionsAsErrors=false,
		// and a second passing validator should still execute
		var throwingValidator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => throwingValidator.ConfigurationName).Returns("ThrowingValidator");
		_ = A.CallTo(() => throwingValidator.Priority).Returns(100);
		_ = A.CallTo(() => throwingValidator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Validator crashed"));

		var passingValidator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => passingValidator.ConfigurationName).Returns("PassingValidator");
		_ = A.CallTo(() => passingValidator.Priority).Returns(200);
		_ = A.CallTo(() => passingValidator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Success());

		var validators = new List<IConfigurationValidator> { throwingValidator, passingValidator };
		var options = new ConfigurationValidationOptions
		{
			TreatValidatorExceptionsAsErrors = false,
			FailFast = true
		};
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act - should not throw since exception is ignored and other validator passes
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - both validators were invoked
		A.CallTo(() => throwingValidator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => passingValidator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public void ThrowOnNullConfiguration()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ConfigurationValidationService(
				null!,
				new List<IConfigurationValidator>(),
				_applicationLifetime,
				_logger));
	}

	[Fact]
	public void ThrowOnNullValidators()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ConfigurationValidationService(
				_configuration,
				null!,
				_applicationLifetime,
				_logger));
	}

	[Fact]
	public void ThrowOnNullApplicationLifetime()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ConfigurationValidationService(
				_configuration,
				new List<IConfigurationValidator>(),
				null!,
				_logger));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ConfigurationValidationService(
				_configuration,
				new List<IConfigurationValidator>(),
				_applicationLifetime,
				null!));
	}

	[Fact]
	public async Task IncludeExceptionMessageInErrors()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("CrashingValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection refused"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions
		{
			TreatValidatorExceptionsAsErrors = true,
			FailFast = true
		};
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act
		var exception = await Should.ThrowAsync<ConfigurationValidationException>(() => service.StartAsync(CancellationToken.None))
			.ConfigureAwait(false);

		// Assert - exception message should be wrapped as a validation error
		exception.Errors.Count.ShouldBe(1);
		exception.Errors[0].Message.ShouldContain("Connection refused");
		exception.Errors[0].Message.ShouldContain("CrashingValidator");
	}

	[Fact]
	public async Task NotStopApplicationWhenFailFastDisabledAndErrorsExist()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);
		_ = A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.Returns(ConfigurationValidationResult.Failure("Non-critical error"));

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { FailFast = false };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act - should complete without throwing
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - StopApplication should NOT be called in non-fail-fast mode
		A.CallTo(() => _applicationLifetime.StopApplication()).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotInvokeValidatorsWhenDisabled()
	{
		// Arrange
		var validator = A.Fake<IConfigurationValidator>();
		_ = A.CallTo(() => validator.ConfigurationName).Returns("TestValidator");
		_ = A.CallTo(() => validator.Priority).Returns(100);

		var validators = new List<IConfigurationValidator> { validator };
		var options = new ConfigurationValidationOptions { Enabled = false };
		var service = new ConfigurationValidationService(
			_configuration,
			validators,
			_applicationLifetime,
			_logger,
			options);

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - validator should never be called when disabled
		A.CallTo(() => validator.ValidateAsync(A<IConfiguration>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}
