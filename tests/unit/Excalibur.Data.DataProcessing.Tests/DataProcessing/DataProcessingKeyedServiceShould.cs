// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for Sprint 661 T.8 + T.9: DataProcessing keyed service registration,
/// IConfiguration binding overloads, and ValidateOnStart on all paths.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingKeyedServiceShould : UnitTestBase
{
	[DataTaskRecordType("KeyedTestRecord")]
	private sealed class TestProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose() { }
	}

	private sealed class TestRecordHandler : IRecordHandler<string>
	{
		public Task ProcessAsync(string record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	// --- DataProcessingKeys constants ---

	[Fact]
	public void OrchestrationConnection_HasExpectedValue()
	{
		DataProcessingKeys.OrchestrationConnection
			.ShouldBe("Excalibur.DataProcessing.Orchestration");
	}

	// --- Keyed service registration via AddDataProcessing ---

	[Fact]
	public void AddDataProcessing_RegistersKeyedSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);

		// Assert -- keyed singleton registration should exist
		services.ShouldContain(sd =>
			sd.IsKeyedService &&
			sd.ServiceType == typeof(Func<IDbConnection>) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddDataProcessing_DoesNotRegisterBareFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);

		// Assert -- bare singleton removed; only keyed registration exists
		services.ShouldNotContain(sd =>
			!sd.IsKeyedService &&
			sd.ServiceType == typeof(Func<IDbConnection>));
	}

	[Fact]
	public void AddDataProcessing_KeyedServiceResolvesToSameFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		var expectedConnection = A.Fake<IDbConnection>();
		Func<IDbConnection> factory = () => expectedConnection;

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);
		var provider = services.BuildServiceProvider();

		// Assert -- keyed resolution returns a factory that produces same connection
		var keyedFactory = provider.GetKeyedService<Func<IDbConnection>>(
			DataProcessingKeys.OrchestrationConnection);
		keyedFactory.ShouldNotBeNull();
		keyedFactory().ShouldBeSameAs(expectedConnection);
	}

	// --- AddDataProcessor<T>(IConfiguration, string) ---

	[Fact]
	public void AddDataProcessorWithIConfiguration_RegistersProcessor()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateRealConfiguration(new Dictionary<string, string?>
		{
			["DP:QueueSize"] = "256",
			["DP:ProducerBatchSize"] = "50",
			["DP:ConsumerBatchSize"] = "20"
		});

		// Act
		services.AddDataProcessor<TestProcessor>(config, "DP");

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDataProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddDataProcessorWithIConfiguration_BindsFromConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateRealConfiguration(new Dictionary<string, string?>
		{
			["DP:QueueSize"] = "256",
			["DP:ProducerBatchSize"] = "50",
			["DP:ConsumerBatchSize"] = "20"
		});

		// Act -- register IConfiguration in the container so BindConfiguration can resolve it
		services.AddSingleton<IConfiguration>(config);
		services.AddDataProcessor<TestProcessor>(config, "DP");
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<DataProcessingOptions>>();
		options.Value.QueueSize.ShouldBe(256);
		options.Value.ProducerBatchSize.ShouldBe(50);
		options.Value.ConsumerBatchSize.ShouldBe(20);
	}

	[Fact]
	public void AddDataProcessorWithIConfiguration_ThrowsOnNullServices()
	{
		var config = CreateFakeConfiguration();

		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddDataProcessor<TestProcessor>(null!, config, "DP"));
	}

	[Fact]
	public void AddDataProcessorWithIConfiguration_ThrowsOnNullConfiguration()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessor<TestProcessor>((IConfiguration)null!, "DP"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void AddDataProcessorWithIConfiguration_ThrowsOnInvalidSectionPath(string? invalidPath)
	{
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();

		Should.Throw<ArgumentException>(() =>
			services.AddDataProcessor<TestProcessor>(config, invalidPath!));
	}

	[Fact]
	public void AddDataProcessorWithIConfiguration_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();

		var result = services.AddDataProcessor<TestProcessor>(config, "DP");

		result.ShouldBeSameAs(services);
	}

	// --- AddRecordHandler<T,R>(IConfiguration, string) ---

	[Fact]
	public void AddRecordHandlerWithIConfiguration_RegistersHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateRealConfiguration(new Dictionary<string, string?>
		{
			["DP:QueueSize"] = "128"
		});

		// Act
		services.AddRecordHandler<TestRecordHandler, string>(config, "DP");

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IRecordHandler<string>) &&
			sd.ImplementationType == typeof(TestRecordHandler) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddRecordHandlerWithIConfiguration_BindsFromConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateRealConfiguration(new Dictionary<string, string?>
		{
			["DP:QueueSize"] = "128",
			["DP:ProducerBatchSize"] = "25"
		});

		// Act -- register IConfiguration in the container so BindConfiguration can resolve it
		services.AddSingleton<IConfiguration>(config);
		services.AddRecordHandler<TestRecordHandler, string>(config, "DP");
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<DataProcessingOptions>>();
		options.Value.QueueSize.ShouldBe(128);
		options.Value.ProducerBatchSize.ShouldBe(25);
	}

	[Fact]
	public void AddRecordHandlerWithIConfiguration_ThrowsOnNullServices()
	{
		var config = CreateFakeConfiguration();

		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddRecordHandler<TestRecordHandler, string>(
				null!, config, "DP"));
	}

	[Fact]
	public void AddRecordHandlerWithIConfiguration_ThrowsOnNullConfiguration()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddRecordHandler<TestRecordHandler, string>((IConfiguration)null!, "DP"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void AddRecordHandlerWithIConfiguration_ThrowsOnInvalidSectionPath(string? invalidPath)
	{
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();

		Should.Throw<ArgumentException>(() =>
			services.AddRecordHandler<TestRecordHandler, string>(config, invalidPath!));
	}

	[Fact]
	public void AddRecordHandlerWithIConfiguration_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();

		var result = services.AddRecordHandler<TestRecordHandler, string>(config, "DP");

		result.ShouldBeSameAs(services);
	}

	// --- ValidateOnStart on config overloads ---

	[Fact]
	public void AddDataProcessorWithConfig_WiresValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingOptions { QueueSize = 100 };

		// Act
		services.AddDataProcessor<TestProcessor>(config);

		// Assert -- ValidateOnStart is wired (IValidateOptions or options monitor registered)
		// Verify by checking that OptionsBuilder.ValidateOnStart was called
		// which registers an IStartupValidator or similar
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DataProcessingOptions));
	}

	[Fact]
	public void AddRecordHandlerWithConfig_WiresValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingOptions { QueueSize = 100 };

		// Act
		services.AddRecordHandler<TestRecordHandler, string>(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DataProcessingOptions));
	}

	[Fact]
	public void AddDataProcessing_WiresValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(DataProcessingOptions));
	}

	// --- DataAnnotations on DataProcessingOptions ---

	[Fact]
	public void DataProcessingOptions_RequiredTableName_ThrowsOnEmpty()
	{
		Should.Throw<ArgumentException>(() =>
			new DataProcessingOptions { TableName = "" });
	}

	[Fact]
	public void DataProcessingOptions_RangeAttributes_ThrowOnZero()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { DispatcherTimeoutMilliseconds = 0 });

		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { MaxAttempts = 0 });

		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { QueueSize = 0 });

		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { ProducerBatchSize = 0 });

		Should.Throw<ArgumentOutOfRangeException>(() =>
			new DataProcessingOptions { ConsumerBatchSize = 0 });
	}

	[Fact]
	public void DataProcessingOptions_ValidValues_Accepted()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			TableName = "CustomTable",
			DispatcherTimeoutMilliseconds = 30000,
			MaxAttempts = 5,
			QueueSize = 1000,
			ProducerBatchSize = 200,
			ConsumerBatchSize = 50
		};

		// Assert
		config.TableName.ShouldBe("CustomTable");
		config.DispatcherTimeoutMilliseconds.ShouldBe(30000);
		config.MaxAttempts.ShouldBe(5);
		config.QueueSize.ShouldBe(1000);
		config.ProducerBatchSize.ShouldBe(200);
		config.ConsumerBatchSize.ShouldBe(50);
	}

	// --- Helpers ---

	private static IConfiguration CreateFakeConfiguration()
	{
		var section = A.Fake<IConfigurationSection>();
		var config = A.Fake<IConfiguration>();
		A.CallTo(() => config.GetSection(A<string>._)).Returns(section);
		return config;
	}

	private static IConfiguration CreateRealConfiguration(Dictionary<string, string?> values)
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(values)
			.Build();
	}
}
