// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for the cross-property <see cref="IValidateOptions{DataProcessingOptions}"/>
/// validator (DataProcessingOptionsValidator) added in Sprint 662.
/// Tests ProducerBatchSize/ConsumerBatchSize vs QueueSize and DispatcherTimeout range constraints.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingOptionsValidatorShould : UnitTestBase
{
	private readonly IValidateOptions<DataProcessingOptions> _validator;

	public DataProcessingOptionsValidatorShould()
	{
		// Resolve the internal validator via DI registration
		var services = new ServiceCollection();
		services.AddDataProcessor<FakeDataProcessor>(new DataProcessingOptions());
		var provider = services.BuildServiceProvider();

		// The validator is registered as IValidateOptions<DataProcessingOptions>
		_validator = provider.GetServices<IValidateOptions<DataProcessingOptions>>()
			.First(v => v.GetType().Name == "DataProcessingOptionsValidator");
	}

	// --- ProducerBatchSize > QueueSize ---

	[Fact]
	public void Fail_WhenProducerBatchSize_ExceedsQueueSize()
	{
		// Arrange
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 101,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 5000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ProducerBatchSize");
	}

	[Fact]
	public void Succeed_WhenProducerBatchSize_EqualsQueueSize()
	{
		// Arrange -- boundary: exactly equal is valid
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 5000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- ConsumerBatchSize > QueueSize ---

	[Fact]
	public void Fail_WhenConsumerBatchSize_ExceedsQueueSize()
	{
		// Arrange
		var options = new DataProcessingOptions
		{
			QueueSize = 50,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 51,
			DispatcherTimeoutMilliseconds = 5000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ConsumerBatchSize");
	}

	[Fact]
	public void Succeed_WhenConsumerBatchSize_EqualsQueueSize()
	{
		// Arrange -- boundary: exactly equal is valid
		var options = new DataProcessingOptions
		{
			QueueSize = 50,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 50,
			DispatcherTimeoutMilliseconds = 5000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- DispatcherTimeout below minimum (1000ms) ---

	[Fact]
	public void Fail_WhenDispatcherTimeout_BelowMinimum()
	{
		// Arrange
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 999,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DispatcherTimeoutMilliseconds");
	}

	[Fact]
	public void Succeed_WhenDispatcherTimeout_EqualsMinimum()
	{
		// Arrange -- boundary: exactly 1000 is valid
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 1000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- DispatcherTimeout above maximum (3600000ms) ---

	[Fact]
	public void Fail_WhenDispatcherTimeout_AboveMaximum()
	{
		// Arrange
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 3_600_001,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DispatcherTimeoutMilliseconds");
	}

	[Fact]
	public void Succeed_WhenDispatcherTimeout_EqualsMaximum()
	{
		// Arrange -- boundary: exactly 3600000 is valid
		var options = new DataProcessingOptions
		{
			QueueSize = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 10,
			DispatcherTimeoutMilliseconds = 3_600_000,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- Valid configuration ---

	[Fact]
	public void Succeed_WithValidDefaultOptions()
	{
		// Arrange -- defaults should be valid
		var options = new DataProcessingOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	// --- Multiple failures ---

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		// Arrange -- both batch sizes exceed queue, and timeout out of range
		var options = new DataProcessingOptions
		{
			QueueSize = 10,
			ProducerBatchSize = 20,
			ConsumerBatchSize = 30,
			DispatcherTimeoutMilliseconds = 500,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ProducerBatchSize");
		result.FailureMessage.ShouldContain("ConsumerBatchSize");
		result.FailureMessage.ShouldContain("DispatcherTimeoutMilliseconds");
	}

	// --- Validator registered on ALL registration paths (blocking fix from T.12 review) ---

	[Fact]
	public void ValidatorRegistered_ViaAddDataProcessorWithIConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["DP:QueueSize"] = "100" })
			.Build();

		// Act
		services.AddSingleton<IConfiguration>(config);
		services.AddDataProcessor<FakeDataProcessor>(config, "DP");

		// Assert -- validator is registered
		var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DataProcessingOptions>>().ToList();
		validators.ShouldContain(v => v.GetType().Name == "DataProcessingOptionsValidator");
	}

	[Fact]
	public void ValidatorRegistered_ViaAddRecordHandlerWithIConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["DP:QueueSize"] = "100" })
			.Build();

		// Act
		services.AddSingleton<IConfiguration>(config);
		services.AddRecordHandler<FakeRecordHandler, string>(config, "DP");

		// Assert
		var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DataProcessingOptions>>().ToList();
		validators.ShouldContain(v => v.GetType().Name == "DataProcessingOptionsValidator");
	}

	[Fact]
	public void ValidatorRegistered_ViaAddDataProcessing()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["DP:QueueSize"] = "100" })
			.Build();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(FakeDataProcessor).Assembly);

		// Assert
		var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DataProcessingOptions>>().ToList();
		validators.ShouldContain(v => v.GetType().Name == "DataProcessingOptionsValidator");
	}

	/// <summary>
	/// Minimal implementation of <see cref="IDataProcessor"/> for DI registration.
	/// </summary>
	[DataTaskRecordType("ValidatorTestRecord")]
	private sealed class FakeDataProcessor : IDataProcessor
	{
		public Task<long> RunAsync(
			long completedCount,
			UpdateCompletedCount updateCompletedCount,
			CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
			// no-op
		}
	}

	private sealed class FakeRecordHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
