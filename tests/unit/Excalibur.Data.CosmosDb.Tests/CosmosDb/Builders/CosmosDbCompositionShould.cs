// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;
using Excalibur.Inbox.CosmosDb;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Saga.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Builders;

/// <summary>
/// Composition tests verifying that multiple CosmosDb builders can coexist
/// with isolated options and independent configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbCompositionShould : UnitTestBase
{
	private const string TestConnectionString =
		"AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

	[Fact]
	public void MultipleBuilders_MaintainIsolatedOptions()
	{
		// Arrange
		var esOptions = new CosmosDbEventStoreOptions();
		var esBuilder = new CosmosDbEventSourcingBuilder(esOptions);

		var dataOptions = new CosmosDbOptions();
		var dataBuilder = new CosmosDbDataBuilder(dataOptions);

		var inboxOptions = new CosmosDbInboxOptions();
		var inboxBuilder = new CosmosDbInboxBuilder(inboxOptions);

		var outboxOptions = new CosmosDbOutboxOptions();
		var outboxBuilder = new CosmosDbOutboxBuilder(outboxOptions);

		var sagaOptions = new CosmosDbSagaOptions();
		var sagaBuilder = new CosmosDbSagaBuilder(sagaOptions);

		// Act - configure each with different values
		esBuilder.ConnectionString(TestConnectionString).DatabaseName("es-db").ContainerName("events");
		dataBuilder.ConnectionString(TestConnectionString).DatabaseName("data-db").ContainerName("documents");
		inboxBuilder.ConnectionString(TestConnectionString).DatabaseName("inbox-db").ContainerName("inbox-messages");
		outboxBuilder.ConnectionString(TestConnectionString).DatabaseName("outbox-db").ContainerName("outbox-messages");
		sagaBuilder.ConnectionString(TestConnectionString).DatabaseName("saga-db").ContainerName("sagas");

		// Assert - each builder/options has its own values
		esBuilder.DatabaseNameValue.ShouldBe("es-db");
		esOptions.EventsContainerName.ShouldBe("events");

		dataOptions.DatabaseName.ShouldBe("data-db");
		dataOptions.DefaultContainerName.ShouldBe("documents");

		inboxOptions.DatabaseName.ShouldBe("inbox-db");
		inboxOptions.ContainerName.ShouldBe("inbox-messages");

		outboxOptions.DatabaseName.ShouldBe("outbox-db");
		outboxOptions.ContainerName.ShouldBe("outbox-messages");

		sagaOptions.DatabaseName.ShouldBe("saga-db");
		sagaOptions.ContainerName.ShouldBe("sagas");
	}

	[Fact]
	public void MultipleBuilders_MaintainOptionsIsolation()
	{
		// Arrange
		var esOptions = new CosmosDbEventStoreOptions();
		var esBuilder = new CosmosDbEventSourcingBuilder(esOptions);

		var sagaOptions = new CosmosDbSagaOptions();
		var sagaBuilder = new CosmosDbSagaBuilder(sagaOptions);

		// Act - configure one with ConnectionString, other with Endpoint
		esBuilder.ConnectionString(TestConnectionString);
		sagaBuilder.Endpoint("https://localhost:8081/", "dGVzdA==");

		// Assert - each has independent connection state
		esBuilder.ConnectionStringValue.ShouldBe(TestConnectionString);
		esBuilder.EndpointValue.ShouldBeNull();

		sagaBuilder.EndpointValue.ShouldBe("https://localhost:8081/");
		sagaBuilder.ConnectionStringValue.ShouldBeNull();
	}

	[Fact]
	public void MultipleBuilders_SupportOrderIndependentConfiguration()
	{
		// Arrange & Act - configure builders in any order
		var outboxOptions = new CosmosDbOutboxOptions();
		var outboxBuilder = new CosmosDbOutboxBuilder(outboxOptions);
		outboxBuilder.ContainerName("outbox-first").DatabaseName("db").ConnectionString(TestConnectionString);

		var inboxOptions = new CosmosDbInboxOptions();
		var inboxBuilder = new CosmosDbInboxBuilder(inboxOptions);
		inboxBuilder.ConnectionString(TestConnectionString).ContainerName("inbox-first").DatabaseName("db");

		// Assert - both have the expected values regardless of call order
		outboxOptions.ContainerName.ShouldBe("outbox-first");
		outboxOptions.DatabaseName.ShouldBe("db");
		outboxBuilder.ConnectionStringValue.ShouldBe(TestConnectionString);

		inboxOptions.ContainerName.ShouldBe("inbox-first");
		inboxOptions.DatabaseName.ShouldBe("db");
		inboxBuilder.ConnectionStringValue.ShouldBe(TestConnectionString);
	}
}
