// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessingServiceCollectionExtensions"/>.
/// </summary>
[UnitTest]
public sealed class DataProcessingServiceCollectionExtensionsShould : UnitTestBase
{
	[DataTaskRecordType("DITestRecord")]
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

	[Fact]
	public void AddDataProcessor_RegistersProcessorAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataProcessor<TestProcessor>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDataProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddRecordHandler_RegistersHandlerAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRecordHandler<TestRecordHandler, string>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IRecordHandler<string>) &&
			sd.ImplementationType == typeof(TestRecordHandler) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddDataProcessor_ThrowsOnNullServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddDataProcessor<TestProcessor>(null!));
	}

	[Fact]
	public void AddRecordHandler_ThrowsOnNullServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddRecordHandler<TestRecordHandler, string>(null!));
	}

	[Fact]
	public void AddDataProcessor_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDataProcessor<TestProcessor>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddRecordHandler_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddRecordHandler<TestRecordHandler, string>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- Config overload tests (Sprint 654 O.2) ---

	[Fact]
	public void AddDataProcessorWithConfig_RegistersProcessorAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration { QueueSize = 128 };

		// Act
		services.AddDataProcessor<TestProcessor>(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDataProcessor) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddDataProcessorWithConfig_RegistersOptionsViaTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration
		{
			QueueSize = 256,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 20
		};

		// Act
		services.AddDataProcessor<TestProcessor>(config);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DataProcessingConfiguration>>();
		options.Value.QueueSize.ShouldBe(256);
		options.Value.ProducerBatchSize.ShouldBe(50);
		options.Value.ConsumerBatchSize.ShouldBe(20);
	}

	[Fact]
	public void AddDataProcessorWithConfig_FirstConfigWinsViaTryAdd()
	{
		// Arrange
		var services = new ServiceCollection();
		var configFirst = new DataProcessingConfiguration { QueueSize = 100 };
		var configSecond = new DataProcessingConfiguration { QueueSize = 999 };

		// Act - register twice with different configs
		services.AddDataProcessor<TestProcessor>(configFirst);
		services.AddDataProcessor<TestProcessor>(configSecond);

		// Assert - TryAddSingleton means first config wins
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DataProcessingConfiguration>>();
		options.Value.QueueSize.ShouldBe(100);
	}

	[Fact]
	public void AddDataProcessorWithConfig_ThrowsOnNullServices()
	{
		// Arrange
		var config = new DataProcessingConfiguration();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddDataProcessor<TestProcessor>(null!, config));
	}

	[Fact]
	public void AddDataProcessorWithConfig_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessor<TestProcessor>((DataProcessingConfiguration)null!));
	}

	[Fact]
	public void AddDataProcessorWithConfig_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration();

		// Act
		var result = services.AddDataProcessor<TestProcessor>(config);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddDataProcessorWithConfig_ParameterlessOverloadDoesNotRegisterOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataProcessor<TestProcessor>();

		// Assert - no IOptions<DataProcessingConfiguration> registered
		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IOptions<DataProcessingConfiguration>));
	}

	// --- AddRecordHandler config overload tests (Sprint 655 P.4) ---

	[Fact]
	public void AddRecordHandlerWithConfig_RegistersHandlerAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration { QueueSize = 128 };

		// Act
		services.AddRecordHandler<TestRecordHandler, string>(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IRecordHandler<string>) &&
			sd.ImplementationType == typeof(TestRecordHandler) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddRecordHandlerWithConfig_RegistersOptionsViaTryAddSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration
		{
			QueueSize = 256,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 20
		};

		// Act
		services.AddRecordHandler<TestRecordHandler, string>(config);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DataProcessingConfiguration>>();
		options.Value.QueueSize.ShouldBe(256);
		options.Value.ProducerBatchSize.ShouldBe(50);
		options.Value.ConsumerBatchSize.ShouldBe(20);
	}

	[Fact]
	public void AddRecordHandlerWithConfig_FirstConfigWinsViaTryAdd()
	{
		// Arrange
		var services = new ServiceCollection();
		var first = new DataProcessingConfiguration { QueueSize = 100 };
		var second = new DataProcessingConfiguration { QueueSize = 999 };

		// Act
		services.AddRecordHandler<TestRecordHandler, string>(first);
		services.AddRecordHandler<TestRecordHandler, string>(second);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DataProcessingConfiguration>>();
		options.Value.QueueSize.ShouldBe(100);
	}

	[Fact]
	public void AddRecordHandlerWithConfig_ThrowsOnNullServices()
	{
		var config = new DataProcessingConfiguration();

		Should.Throw<ArgumentNullException>(() =>
			DataProcessingServiceCollectionExtensions.AddRecordHandler<TestRecordHandler, string>(
				null!, config));
	}

	[Fact]
	public void AddRecordHandlerWithConfig_ThrowsOnNullConfiguration()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddRecordHandler<TestRecordHandler, string>(
				(DataProcessingConfiguration)null!));
	}

	[Fact]
	public void AddRecordHandlerWithConfig_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();
		var config = new DataProcessingConfiguration();

		var result = services.AddRecordHandler<TestRecordHandler, string>(config);

		result.ShouldBeSameAs(services);
	}

	// --- R.7: AddDataProcessing(Func<IDbConnection>, ...) tests (Sprint 657) ---

	[Fact]
	public void AddDataProcessing_RegistersConnectionFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DataProcessing", typeof(TestProcessor).Assembly);

		// Assert -- connection factory registered as singleton
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Func<IDbConnection>) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddDataProcessing_RegistersOrchestrationManager()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDataOrchestrationManager) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly));
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnNullConnectionFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessing(null!, config, "DP", typeof(TestProcessor).Assembly));
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessing(factory, null!, "DP", typeof(TestProcessor).Assembly));
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnNullConfigurationSection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDataProcessing(factory, config, null!, typeof(TestProcessor).Assembly));
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnEmptyConfigurationSection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddDataProcessing(factory, config, "", typeof(TestProcessor).Assembly));
	}

	[Fact]
	public void AddDataProcessing_ThrowsOnNullAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessing(factory, config, "DP", null!));
	}

	[Fact]
	public void AddDataProcessing_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = CreateFakeConfiguration();
		Func<IDbConnection> factory = () => A.Fake<IDbConnection>();

		// Act
		var result = services.AddDataProcessing(factory, config, "DP", typeof(TestProcessor).Assembly);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void OldAddDataProcessing_WithTwoTypeParameters_NoLongerExists()
	{
		// Verify the removed AddDataProcessing<TDb1, TDb2>() overload is truly gone.
		// This is a compile-time verification -- the method had exactly 2 generic type params.
		var methods = typeof(DataProcessingServiceCollectionExtensions).GetMethods()
			.Where(m => m.Name == "AddDataProcessing" && m.IsGenericMethod);

		methods.ShouldBeEmpty(
			"AddDataProcessing<TDb1, TDb2> should have been removed in Sprint 657 R.6");
	}

	[Fact]
	public void RemovedTypes_NoLongerExist()
	{
		// Verify IDataProcessorDb, IDataToProcessDb, DataProcessorDb, DataToProcessDb are gone
		var assembly = typeof(DataOrchestrationManager).Assembly;

		assembly.GetType("Excalibur.Data.DataProcessing.IDataProcessorDb").ShouldBeNull();
		assembly.GetType("Excalibur.Data.DataProcessing.IDataToProcessDb").ShouldBeNull();
		assembly.GetType("Excalibur.Data.DataProcessing.DataProcessorDb").ShouldBeNull();
		assembly.GetType("Excalibur.Data.DataProcessing.DataToProcessDb").ShouldBeNull();
	}

	private static IConfiguration CreateFakeConfiguration()
	{
		var section = A.Fake<IConfigurationSection>();
		var config = A.Fake<IConfiguration>();
		A.CallTo(() => config.GetSection(A<string>._)).Returns(section);
		return config;
	}
}
