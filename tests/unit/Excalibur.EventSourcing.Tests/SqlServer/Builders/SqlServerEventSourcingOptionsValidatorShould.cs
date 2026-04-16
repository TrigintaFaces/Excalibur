// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerEventSourcingOptionsValidator"/> — ValidateOnStart behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventSourcingOptionsValidatorShould : UnitTestBase
{
	[Fact]
	public void Fail_WhenNoConnectionConfigured()
	{
		// Arrange — no connection string, no builder connection
		var validator = new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = false };
		var options = new SqlServerEventSourcingOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("No connection configured for EventSourcing");
		result.FailureMessage.ShouldContain("ConnectionString()");
		result.FailureMessage.ShouldContain("ConnectionFactory()");
	}

	[Fact]
	public void Succeed_WhenConnectionStringProvided()
	{
		// Arrange
		var validator = new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = false };
		var options = new SqlServerEventSourcingOptions
		{
			ConnectionString = "Server=localhost;Database=Test;"
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WhenBuilderConnectionConfigured()
	{
		// Arrange — no connection string, but builder has ConnectionFactory or ConnectionStringName
		var validator = new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = true };
		var options = new SqlServerEventSourcingOptions(); // no ConnectionString

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var validator = new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = false };
		var options = new SqlServerEventSourcingOptions
		{
			ConnectionString = "   "
		};

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}
}
