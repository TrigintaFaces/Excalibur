// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

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
}
