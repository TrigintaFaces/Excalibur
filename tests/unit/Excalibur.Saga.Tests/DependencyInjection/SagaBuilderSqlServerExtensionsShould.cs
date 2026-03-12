// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.DependencyInjection;
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
			((ISagaBuilder)null!).UseSqlServer(TestConnectionString));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsNull()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(string.Empty));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer("   "));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseSqlServer(TestConnectionString);

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
		builder.UseSqlServer(TestConnectionString);

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
		builder.UseSqlServer(TestConnectionString);

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
		builder.UseSqlServer(TestConnectionString);

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaMonitoringService));
	}

	#endregion

	#region Configure Actions Tests

	[Fact]
	public void AcceptConfigureActionsWithoutThrowing()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- configure actions should be accepted without throwing
		var result = builder.UseSqlServer(TestConnectionString,
			configureSagaStore: opts => { },
			configureTimeoutStore: opts => { });

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AcceptNullConfigureActions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- should not throw
		var result = builder.UseSqlServer(TestConnectionString,
			configureSagaStore: null,
			configureTimeoutStore: null);

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
			.UseSqlServer(TestConnectionString)
			.WithOrchestration()
			.WithTimeouts();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
