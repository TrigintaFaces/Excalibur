// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Cdc.Builders;

/// <summary>
/// Locks the builder-managed connection path for MongoDB CDC.
/// </summary>
/// <remarks>
/// <para>
/// A consumer who supplies the client itself has no connection string to give. Every sibling Mongo
/// registration substitutes a builder-managed sentinel so options validation still passes; CDC did not,
/// so registering CDC with a client instance and no connection string failed validation at startup.
/// </para>
/// <para>
/// The arms assert the OBSERVABLE RESULT of the registration — that a real container yields a running
/// options instance and the supplied client — not that the builder stored what was set.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbCdcBuilderManagedClientShould : UnitTestBase
{
	[Fact]
	public void ValidateWhenTheBuilderSuppliesTheClientAndNoConnectionString()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var client = A.Fake<IMongoClient>();

		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(mongo =>
				mongo.Client(client)
					 .DatabaseName("TestDb")
					 .ProcessorId("test-processor")));

		using var provider = services.BuildServiceProvider();

		// Resolving the options runs the registered validator; without the sentinel this throws.
		var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>().Value;

		options.Connection.ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"a builder-managed connection must still present a connection value to the validator");
		provider.GetRequiredService<IMongoClient>().ShouldBeSameAs(
			client,
			"the processor resolves IMongoClient from DI, so the supplied client must actually be registered");
	}

	[Fact]
	public void KeepUsingAnExplicitConnectionStringWhenNoClientIsSupplied()
	{
		// LIVENESS: substituting the sentinel unconditionally would discard a real connection string.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddCdcProcessor(builder =>
			builder.UseMongoDB(mongo =>
				mongo.ConnectionString("mongodb://source-host:27017")
					 .DatabaseName("TestDb")
					 .ProcessorId("test-processor")));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>().Value;

		options.Connection.ConnectionString.ShouldBe("mongodb://source-host:27017");
	}
}
