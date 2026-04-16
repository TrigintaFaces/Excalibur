// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Unit tests for MongoDB CDC state store options registration via the builder pattern.
/// </summary>
/// <remarks>
/// Updated Sprint 779: State store is now configured via <c>UseMongoDB(Action&lt;IMongoDbCdcBuilder&gt;)</c>
/// with <c>WithStateStore()</c>, not the deleted <c>AddMongoDbCdcStateStore</c> standalone method.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "MongoDbCdcServiceCollectionExtensions")]
public sealed class MongoDbCdcServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseMongoDB_WithStateStore_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act — register via builder with state store configuration
		builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo => mongo
			.ConnectionString("mongodb://localhost:27017")
			.DatabaseName("cdc_source")
			.WithStateStore(state =>
			{
				state.ConnectionString("mongodb://localhost:27017");
				state.SchemaName("custom_db");
				state.TableName("overridden");
			})));

		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>();
		options.Value.DatabaseName.ShouldBe("custom_db");
		options.Value.CollectionName.ShouldBe("overridden");
	}

	[Fact]
	public void UseMongoDB_WithStateStore_WithInvalidTableName_ThrowsArgumentException()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert — builder validates eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		Should.Throw<ArgumentException>(() =>
			builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo => mongo
				.ConnectionString("mongodb://localhost:27017")
				.WithStateStore(state =>
				{
					state.TableName(" ");
				}))));
	}
}
