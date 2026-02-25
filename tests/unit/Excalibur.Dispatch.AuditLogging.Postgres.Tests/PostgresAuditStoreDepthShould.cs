// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Postgres.Tests;

/// <summary>
/// Depth coverage tests for <see cref="PostgresAuditStore"/> covering
/// options defaults, FullyQualifiedTableName computation, DataAnnotations,
/// ValidateOnStart, and property defaults.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresAuditStoreDepthShould
{
	[Fact]
	public void Options_HaveCorrectDefaults()
	{
		// Act
		var options = new PostgresAuditOptions();

		// Assert
		options.SchemaName.ShouldBe("audit");
		options.TableName.ShouldBe("audit_events");
		options.BatchSize.ShouldBe(1000);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
		options.RetentionCleanupBatchSize.ShouldBe(10000);
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.EnableHashChain.ShouldBeTrue();
	}

	[Fact]
	public void Options_FullyQualifiedTableName_UsePostgresQuoting()
	{
		// Arrange
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost",
			SchemaName = "mySchema",
			TableName = "myTable"
		};

		// Act & Assert — Postgres uses double-quote quoting
		options.FullyQualifiedTableName.ShouldBe("\"mySchema\".\"myTable\"");
	}

	[Fact]
	public void Options_FullyQualifiedTableName_UseDefaults()
	{
		// Act
		var options = new PostgresAuditOptions();

		// Assert
		options.FullyQualifiedTableName.ShouldBe("\"audit\".\"audit_events\"");
	}

	[Fact]
	public void OptionsConnectionString_HaveRequiredAttribute()
	{
		var prop = typeof(PostgresAuditOptions).GetProperty(nameof(PostgresAuditOptions.ConnectionString));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public void OptionsSchemaName_HaveRequiredAttribute()
	{
		var prop = typeof(PostgresAuditOptions).GetProperty(nameof(PostgresAuditOptions.SchemaName));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public void OptionsTableName_HaveRequiredAttribute()
	{
		var prop = typeof(PostgresAuditOptions).GetProperty(nameof(PostgresAuditOptions.TableName));
		prop.ShouldNotBeNull();
		prop!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddPostgresAuditStore(o =>
			o.ConnectionString = "Host=localhost;Database=audit");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterValidateOnStartOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresAuditStore(o =>
			o.ConnectionString = "Host=localhost;Database=audit");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IValidateOptions<PostgresAuditOptions>));
	}

	[Fact]
	public void RegisterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresAuditStore(o =>
			o.ConnectionString = "Host=localhost;Database=audit");

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(PostgresAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AcceptCustomRetentionPeriod()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			RetentionPeriod = TimeSpan.FromDays(365)
		});

		// Act
		var store = new PostgresAuditStore(options, NullLogger<PostgresAuditStore>.Instance);

		// Assert
		store.ShouldNotBeNull();
		store.Dispose();
	}
}
