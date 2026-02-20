// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Tests.Data.DynamoDb;

/// <summary>
/// Unit tests for DynamoDB CDC service collection extensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbCdcServiceCollectionExtensionsShould
{
	[Fact]
	public void AddDynamoDbCdcStateStore_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDynamoDbCdcStateStore("state_table", options => options.TableName = "override");
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<DynamoDbCdcStateStoreOptions>>();
		options.Value.TableName.ShouldBe("override");
	}

	[Fact]
	public void AddDynamoDbCdcStateStore_WithInvalidConfiguration_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDynamoDbCdcStateStore("state_table", options => options.TableName = " ");
		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<DynamoDbCdcStateStoreOptions>>().Value);
	}
}
