// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="ConnectionConfiguration{TConnection}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class ConnectionConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ConnectionFactory_IsNull()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.ConnectionFactory.ShouldBeNull();
	}

	[Fact]
	public void Default_ConnectionValidator_IsNull()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.ConnectionValidator.ShouldBeNull();
	}

	[Fact]
	public void Default_ConnectionDisposal_IsNull()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.ConnectionDisposal.ShouldBeNull();
	}

	[Fact]
	public void Default_HealthChecker_IsNull()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.HealthChecker.ShouldBeNull();
	}

	[Fact]
	public void Default_MaxLifetime_IsNull()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.MaxLifetime.ShouldBeNull();
	}

	[Fact]
	public void Default_Priority_IsZero()
	{
		// Arrange & Act
		var config = new ConnectionConfiguration<TestConnection>();

		// Assert
		config.Priority.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ConnectionFactory_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();
		Func<CancellationToken, ValueTask<TestConnection>> factory = _ => ValueTask.FromResult(new TestConnection());

		// Act
		config.ConnectionFactory = factory;

		// Assert
		config.ConnectionFactory.ShouldBe(factory);
	}

	[Fact]
	public async Task ConnectionFactory_CanCreateConnection()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>
		{
			ConnectionFactory = _ => ValueTask.FromResult(new TestConnection()),
		};

		// Act
		var connection = await config.ConnectionFactory(CancellationToken.None);

		// Assert
		_ = connection.ShouldNotBeNull();
	}

	[Fact]
	public void ConnectionValidator_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();
		Func<TestConnection, CancellationToken, ValueTask<bool>> validator = (_, _) => ValueTask.FromResult(true);

		// Act
		config.ConnectionValidator = validator;

		// Assert
		config.ConnectionValidator.ShouldBe(validator);
	}

	[Fact]
	public async Task ConnectionValidator_CanValidateConnection()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>
		{
			ConnectionValidator = (_, _) => ValueTask.FromResult(true),
		};

		// Act
		var isValid = await config.ConnectionValidator(new TestConnection(), CancellationToken.None);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void ConnectionDisposal_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();
		Func<TestConnection, CancellationToken, ValueTask> disposal = (_, _) => ValueTask.CompletedTask;

		// Act
		config.ConnectionDisposal = disposal;

		// Assert
		config.ConnectionDisposal.ShouldBe(disposal);
	}

	[Fact]
	public async Task ConnectionDisposal_CanDisposeConnection()
	{
		// Arrange
		var disposed = false;
		var config = new ConnectionConfiguration<TestConnection>
		{
			ConnectionDisposal = (_, _) =>
			{
				disposed = true;
				return ValueTask.CompletedTask;
			},
		};

		// Act
		await config.ConnectionDisposal(new TestConnection(), CancellationToken.None);

		// Assert
		disposed.ShouldBeTrue();
	}

	[Fact]
	public void HealthChecker_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();
		Func<TestConnection, CancellationToken, ValueTask<bool>> healthChecker = (_, _) => ValueTask.FromResult(true);

		// Act
		config.HealthChecker = healthChecker;

		// Assert
		config.HealthChecker.ShouldBe(healthChecker);
	}

	[Fact]
	public async Task HealthChecker_CanCheckHealth()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>
		{
			HealthChecker = (_, _) => ValueTask.FromResult(true),
		};

		// Act
		var isHealthy = await config.HealthChecker(new TestConnection(), CancellationToken.None);

		// Assert
		isHealthy.ShouldBeTrue();
	}

	[Fact]
	public void MaxLifetime_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();

		// Act
		config.MaxLifetime = TimeSpan.FromHours(1);

		// Assert
		config.MaxLifetime.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();

		// Act
		config.Priority = 100;

		// Assert
		config.Priority.ShouldBe(100);
	}

	[Fact]
	public void Priority_CanBeNegative()
	{
		// Arrange
		var config = new ConnectionConfiguration<TestConnection>();

		// Act
		config.Priority = -50;

		// Assert
		config.Priority.ShouldBe(-50);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		Func<CancellationToken, ValueTask<TestConnection>> factory = _ => ValueTask.FromResult(new TestConnection());
		Func<TestConnection, CancellationToken, ValueTask<bool>> validator = (_, _) => ValueTask.FromResult(true);
		Func<TestConnection, CancellationToken, ValueTask> disposal = (_, _) => ValueTask.CompletedTask;
		Func<TestConnection, CancellationToken, ValueTask<bool>> healthChecker = (_, _) => ValueTask.FromResult(true);

		// Act
		var config = new ConnectionConfiguration<TestConnection>
		{
			ConnectionFactory = factory,
			ConnectionValidator = validator,
			ConnectionDisposal = disposal,
			HealthChecker = healthChecker,
			MaxLifetime = TimeSpan.FromMinutes(30),
			Priority = 10,
		};

		// Assert
		config.ConnectionFactory.ShouldBe(factory);
		config.ConnectionValidator.ShouldBe(validator);
		config.ConnectionDisposal.ShouldBe(disposal);
		config.HealthChecker.ShouldBe(healthChecker);
		config.MaxLifetime.ShouldBe(TimeSpan.FromMinutes(30));
		config.Priority.ShouldBe(10);
	}

	#endregion

	#region Test Helpers

	private sealed class TestConnection;

	#endregion
}
