// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerEventSourcingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerEventSourcingOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveNullConnectionStringByDefault()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultEventStoreSchema()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.EventStoreSchema.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultEventStoreTable()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.EventStoreTable.ShouldBe("Events");
	}

	[Fact]
	public void HaveDefaultSnapshotStoreSchema()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.SnapshotStoreSchema.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultSnapshotStoreTable()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.SnapshotStoreTable.ShouldBe("Snapshots");
	}

	[Fact]
	public void HaveDefaultOutboxSchema()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.OutboxSchema.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultOutboxTable()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.OutboxTable.ShouldBe("EventSourcedOutbox");
	}

	[Fact]
	public void HaveDefaultRegisterHealthChecks()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.RegisterHealthChecks.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultEventStoreHealthCheckName()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.EventStoreHealthCheckName.ShouldBe("sqlserver-event-store");
	}

	[Fact]
	public void HaveDefaultSnapshotStoreHealthCheckName()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.SnapshotStoreHealthCheckName.ShouldBe("sqlserver-snapshot-store");
	}

	[Fact]
	public void HaveDefaultOutboxStoreHealthCheckName()
	{
		// Arrange & Act
		var options = new SqlServerEventSourcingOptions();

		// Assert
		options.OutboxStoreHealthCheckName.ShouldBe("sqlserver-outbox-store");
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=TestDb";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=TestDb");
	}

	[Fact]
	public void AllowCustomEventStoreSchema()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.EventStoreSchema = "events";

		// Assert
		options.EventStoreSchema.ShouldBe("events");
	}

	[Fact]
	public void AllowCustomEventStoreTable()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.EventStoreTable = "DomainEvents";

		// Assert
		options.EventStoreTable.ShouldBe("DomainEvents");
	}

	[Fact]
	public void AllowCustomSnapshotStoreSchema()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.SnapshotStoreSchema = "snapshots";

		// Assert
		options.SnapshotStoreSchema.ShouldBe("snapshots");
	}

	[Fact]
	public void AllowCustomSnapshotStoreTable()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.SnapshotStoreTable = "AggregateSnapshots";

		// Assert
		options.SnapshotStoreTable.ShouldBe("AggregateSnapshots");
	}

	[Fact]
	public void AllowCustomOutboxSchema()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.OutboxSchema = "messaging";

		// Assert
		options.OutboxSchema.ShouldBe("messaging");
	}

	[Fact]
	public void AllowCustomOutboxTable()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.OutboxTable = "OutboxMessages";

		// Assert
		options.OutboxTable.ShouldBe("OutboxMessages");
	}

	[Fact]
	public void AllowDisablingHealthChecks()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.RegisterHealthChecks = false;

		// Assert
		options.RegisterHealthChecks.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomEventStoreHealthCheckName()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.EventStoreHealthCheckName = "custom-event-store-health";

		// Assert
		options.EventStoreHealthCheckName.ShouldBe("custom-event-store-health");
	}

	[Fact]
	public void AllowCustomSnapshotStoreHealthCheckName()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.SnapshotStoreHealthCheckName = "custom-snapshot-store-health";

		// Assert
		options.SnapshotStoreHealthCheckName.ShouldBe("custom-snapshot-store-health");
	}

	[Fact]
	public void AllowCustomOutboxStoreHealthCheckName()
	{
		// Arrange
		var options = new SqlServerEventSourcingOptions();

		// Act
		options.OutboxStoreHealthCheckName = "custom-outbox-store-health";

		// Assert
		options.OutboxStoreHealthCheckName.ShouldBe("custom-outbox-store-health");
	}
}
