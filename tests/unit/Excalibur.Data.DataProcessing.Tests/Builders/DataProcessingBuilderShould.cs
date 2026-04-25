// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data.Common;

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.DataProcessing.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IDataProcessingBuilder"/> — connection factories,
/// BindConfiguration, processor/handler registration, background processing,
/// and argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProcessingBuilderShould : UnitTestBase
{
	// --- Test fixtures ---

	private sealed class TestDbConnection : DbConnection
	{
		public override string? ConnectionString { get; set; } = "";
		public override string Database => "TestDb";
		public override string DataSource => "localhost";
		public override string ServerVersion => "1.0";
		public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
		public override void ChangeDatabase(string databaseName) { }
		public override void Close() { }
		public override void Open() { }
		protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => null!;
		protected override DbCommand CreateDbCommand() => null!;
	}

	private sealed class TestProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);
		public void Dispose() { }
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed record TestRecord(string Value);

	private sealed class TestRecordHandler : IRecordHandler<TestRecord>
	{
		public Task ProcessAsync(TestRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	// --- Simple ConnectionFactory ---

	[Fact]
	public void SimpleConnectionFactory_StoreFactory()
	{
		// Arrange
		var builder = new DataProcessingBuilder(new ServiceCollection());
		Func<IDbConnection> factory = () => new TestDbConnection();

		// Act
		builder.ConnectionFactory(factory);

		// Assert
		builder.SimpleConnectionFactory.ShouldBe(factory);
		builder.DependencyAwareConnectionFactory.ShouldBeNull();
	}

	[Fact]
	public void SimpleConnectionFactory_ThrowOnNull()
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());

		Should.Throw<ArgumentNullException>(() =>
			builder.ConnectionFactory((Func<IDbConnection>)null!));
	}

	// --- DI-aware ConnectionFactory ---

	[Fact]
	public void DiAwareConnectionFactory_StoreFactory()
	{
		// Arrange
		var builder = new DataProcessingBuilder(new ServiceCollection());
		Func<IServiceProvider, Func<IDbConnection>> factory = _ => () => new TestDbConnection();

		// Act
		builder.ConnectionFactory(factory);

		// Assert
		builder.DependencyAwareConnectionFactory.ShouldBe(factory);
		builder.SimpleConnectionFactory.ShouldBeNull();
	}

	[Fact]
	public void DiAwareConnectionFactory_ThrowOnNull()
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());

		Should.Throw<ArgumentNullException>(() =>
			builder.ConnectionFactory((Func<IServiceProvider, Func<IDbConnection>>)null!));
	}

	// --- Last-wins between factory overloads ---

	[Fact]
	public void DiAwareFactory_ClearSimpleFactory()
	{
		// Arrange
		var builder = new DataProcessingBuilder(new ServiceCollection());

		// Act — simple first, then DI-aware (last-wins)
		builder.ConnectionFactory(() => new TestDbConnection());
		builder.ConnectionFactory((Func<IServiceProvider, Func<IDbConnection>>)(_ => () => new TestDbConnection()));

		// Assert
		builder.SimpleConnectionFactory.ShouldBeNull();
		builder.DependencyAwareConnectionFactory.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleFactory_ClearDiAwareFactory()
	{
		// Arrange
		var builder = new DataProcessingBuilder(new ServiceCollection());

		// Act — DI-aware first, then simple (last-wins)
		builder.ConnectionFactory((Func<IServiceProvider, Func<IDbConnection>>)(_ => () => new TestDbConnection()));
		builder.ConnectionFactory(() => new TestDbConnection());

		// Assert
		builder.DependencyAwareConnectionFactory.ShouldBeNull();
		builder.SimpleConnectionFactory.ShouldNotBeNull();
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_StorePath()
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());

		builder.BindConfiguration("DataProcessing");

		builder.BindConfigurationPath.ShouldBe("DataProcessing");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());

		Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
	}

	// --- AddProcessor ---

	[Fact]
	public void AddProcessor_RegisterProcessorInServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new DataProcessingBuilder(services);

		// Act
		builder.AddProcessor<TestProcessor>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDataProcessor) &&
			sd.ImplementationType == typeof(TestProcessor));
	}

	// --- AddRecordHandler ---

	[Fact]
	public void AddRecordHandler_RegisterHandlerInServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new DataProcessingBuilder(services);

		// Act
		builder.AddRecordHandler<TestRecordHandler, TestRecord>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IRecordHandler<TestRecord>) &&
			sd.ImplementationType == typeof(TestRecordHandler));
	}

	// --- EnableBackgroundProcessing ---

	[Fact]
	public void EnableBackgroundProcessing_SetFlag()
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());

		builder.EnableBackgroundProcessing();

		builder.BackgroundProcessingEnabled.ShouldBeTrue();
	}

	[Fact]
	public void EnableBackgroundProcessing_AcceptConfigureAction()
	{
		var builder = new DataProcessingBuilder(new ServiceCollection());
		Action<DataProcessingHostedServiceOptions> configure =
			opts => opts.PollingInterval = TimeSpan.FromSeconds(10);

		builder.EnableBackgroundProcessing(configure);

		builder.BackgroundProcessingEnabled.ShouldBeTrue();
		builder.BackgroundProcessingConfigure.ShouldBe(configure);
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_ReturnBuilderForChaining()
	{
		// Arrange
		var builder = new DataProcessingBuilder(new ServiceCollection());

		// Act & Assert — all methods return same builder
		var result = builder
			.ConnectionFactory(() => new TestDbConnection())
			.BindConfiguration("DataProcessing")
			.AddProcessor<TestProcessor>()
			.AddRecordHandler<TestRecordHandler, TestRecord>()
			.EnableBackgroundProcessing();

		result.ShouldBeSameAs(builder);
	}

	// --- Entry point ---

	[Fact]
	public void AddDataProcessing_ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddDataProcessing(dp =>
				dp.ConnectionFactory(() => new TestDbConnection())));
	}

	[Fact]
	public void AddDataProcessing_ThrowOnNullConfigure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDataProcessing((Action<IDataProcessingBuilder>)null!));
	}

	// --- Constructor ---

	[Fact]
	public void Constructor_ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingBuilder(null!));
	}
}
