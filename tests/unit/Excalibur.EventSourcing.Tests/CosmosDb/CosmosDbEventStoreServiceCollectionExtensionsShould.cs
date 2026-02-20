// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddCosmosDbEventStore_WithConfigure_ValidateArgumentsAndRegisterServices()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			CosmosDbEventStoreServiceCollectionExtensions.AddCosmosDbEventStore(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbEventStore((Action<CosmosDbEventStoreOptions>)null!));

		var result = services.AddCosmosDbEventStore(options =>
		{
			options.EventsContainerName = "custom-events";
			options.ContainerThroughput = 900;
		});

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(CosmosDbEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICloudNativeEventStore));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbEventStoreOptions>>().Value;
		options.EventsContainerName.ShouldBe("custom-events");
		options.ContainerThroughput.ShouldBe(900);
	}

	[Fact]
	public void AddCosmosDbEventStore_WithConfiguration_ValidateArgumentsAndBindOptions()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			CosmosDbEventStoreServiceCollectionExtensions.AddCosmosDbEventStore(null!, new ConfigurationBuilder().Build()));
		Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbEventStore((IConfiguration)null!));

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["EventsContainerName"] = "bound-events",
				["PartitionKeyPath"] = "/streamId",
				["ContainerThroughput"] = "1200",
				["UseTransactionalBatch"] = "false"
			})
			.Build();

		var result = services.AddCosmosDbEventStore(configuration);
		result.ShouldBeSameAs(services);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbEventStoreOptions>>().Value;
		options.EventsContainerName.ShouldBe("bound-events");
		options.ContainerThroughput.ShouldBe(1200);
		options.UseTransactionalBatch.ShouldBeFalse();
	}

	[Fact]
	public void AddCosmosDbEventStore_WithSectionName_ValidateArgumentsAndBindSection()
	{
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["eventstore:EventsContainerName"] = "section-events",
				["eventstore:PartitionKeyPath"] = "/streamId",
				["eventstore:MaxBatchSize"] = "64",
				["eventstore:ChangeFeedPollIntervalMs"] = "200"
			})
			.Build();

		Should.Throw<ArgumentNullException>(() =>
			CosmosDbEventStoreServiceCollectionExtensions.AddCosmosDbEventStore(null!, configuration, "eventstore"));
		Should.Throw<ArgumentNullException>(() =>
			services.AddCosmosDbEventStore(configuration: null!, sectionName: "eventstore"));
		Should.Throw<ArgumentException>(() =>
			services.AddCosmosDbEventStore(configuration, sectionName: " "));

		var result = services.AddCosmosDbEventStore(configuration, "eventstore");
		result.ShouldBeSameAs(services);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbEventStoreOptions>>().Value;
		options.EventsContainerName.ShouldBe("section-events");
		options.MaxBatchSize.ShouldBe(64);
		options.ChangeFeedPollIntervalMs.ShouldBe(200);
	}

	[Fact]
	public void AddCosmosDbEventStore_UseTryAddForServiceRegistrations()
	{
		var services = new ServiceCollection();

		_ = services.AddCosmosDbEventStore(static _ => { });
		_ = services.AddCosmosDbEventStore(static _ => { });

		services.Count(sd => sd.ServiceType == typeof(CosmosDbEventStore)).ShouldBe(1);
		services.Count(sd => sd.ServiceType == typeof(IEventStore)).ShouldBe(1);
		services.Count(sd => sd.ServiceType == typeof(ICloudNativeEventStore)).ShouldBe(1);
	}
}
