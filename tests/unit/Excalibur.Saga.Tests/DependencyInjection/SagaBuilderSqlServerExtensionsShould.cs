// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagaBuilderSqlServerExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaBuilderSqlServerExtensionsShould
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

	private sealed class TestSagaBuilder : ISagaBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	#region Null Guard Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(null!));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Service Registration Tests

	[Fact]
	public void RegisterSagaStore_WhenCalled()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; });

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void RegisterSagaTimeoutStore_WhenCalled()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; });

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaTimeoutStore));
	}

	[Fact]
	public void RegisterSagaMonitoringService_WhenCalled()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; });

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaMonitoringService));
	}

	#endregion

	#region Configure Actions Tests

	[Fact]
	public void AcceptConfigureActionWithCustomOptions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- configure action with custom schema should be accepted without throwing
		var result = builder.UseSqlServer(sql =>
		{
			sql.ConnectionString = TestConnectionString;
			sql.SchemaName = "custom";
		});

		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Chaining with Other Builder Methods

	[Fact]
	public void SupportFluentChaining_WithOtherSagaBuilderMethods()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act -- verify chaining with existing saga builder extensions
		var result = builder
			.UseSqlServer(sql => { sql.ConnectionString = TestConnectionString; })
			.WithOrchestration()
			.WithTimeouts();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithSqlServerIdempotency Tests

	[Fact]
	public void ThrowArgumentNullException_WhenIdempotencyBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithSqlServerIdempotency(sql => { sql.ConnectionString = TestConnectionString; }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenIdempotencyConfigureIsNull()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithSqlServerIdempotency(null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForIdempotencyFluentChaining()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithSqlServerIdempotency(sql => { sql.ConnectionString = TestConnectionString; });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
