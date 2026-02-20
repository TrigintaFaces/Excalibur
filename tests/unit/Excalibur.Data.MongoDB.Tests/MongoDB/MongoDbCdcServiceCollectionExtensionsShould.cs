// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Unit tests for MongoDB CDC service collection extensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "MongoDbCdcServiceCollectionExtensions")]
public sealed class MongoDbCdcServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddMongoDbCdcStateStore_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMongoDbCdcStateStore(
				"mongodb://localhost",
				options =>
				{
					options.DatabaseName = "custom_db";
					options.CollectionName = "overridden";
				});

		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>();
		options.Value.DatabaseName.ShouldBe("custom_db");
		options.Value.CollectionName.ShouldBe("overridden");
	}

	[Fact]
	public void AddMongoDbCdcStateStore_WithInvalidConfiguration_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMongoDbCdcStateStore(
				"mongodb://localhost",
				options => options.CollectionName = " ");

		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>().Value);
	}
}
