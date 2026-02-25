// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Data.Postgres.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.Postgres;

namespace Excalibur.Data.Tests.Postgres.Cdc;

/// <summary>
/// Unit tests for Postgres CDC service collection extensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "PostgresCdcServiceCollectionExtensions")]
public sealed class PostgresCdcServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddPostgresCdc_RegistersStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPostgresCdc(
				options =>
				{
					options.ConnectionString = "Host=localhost;Username=test;Password=test";
					options.PublicationName = "pub";
					options.ReplicationSlotName = "slot";
				},
				configureStateStoreOptions: stateStoreOptions =>
				{
					stateStoreOptions.SchemaName = "custom_schema";
					stateStoreOptions.TableName = "custom_state";
				});

		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		options.Value.SchemaName.ShouldBe("custom_schema");
		options.Value.TableName.ShouldBe("custom_state");
	}

	[Fact]
	public void AddPostgresCdc_WithInvalidStateStoreOptions_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddPostgresCdc(
				options =>
				{
					options.ConnectionString = "Host=localhost;Username=test;Password=test";
					options.PublicationName = "pub";
					options.ReplicationSlotName = "slot";
				},
				configureStateStoreOptions: stateStoreOptions =>
				{
					stateStoreOptions.SchemaName = " ";
				});

		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>().Value);
	}
}
