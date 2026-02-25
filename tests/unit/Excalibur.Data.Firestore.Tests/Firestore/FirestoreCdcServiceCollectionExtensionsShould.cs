// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

using Microsoft.Extensions.Options;

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.Cdc;

/// <summary>
/// Unit tests for Firestore CDC service collection extensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "FirestoreCdcServiceCollectionExtensions")]
public sealed class FirestoreCdcServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddFirestoreCdcStateStore_RegistersDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddFirestoreCdcStateStore(opts => { });
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<FirestoreCdcStateStoreOptions>>();
		options.Value.CollectionName.ShouldBe("_cdc_positions");
	}

	[Fact]
	public void AddFirestoreCdcStateStore_WithCollectionName_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddFirestoreCdcStateStore("custom", options => options.CollectionName = "overridden");
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<FirestoreCdcStateStoreOptions>>();
		options.Value.CollectionName.ShouldBe("overridden");
	}

	[Fact]
	public void AddFirestoreCdcStateStore_WithInvalidConfiguration_ThrowsOptionsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddFirestoreCdcStateStore("valid", options => options.CollectionName = " ");
		using var provider = services.BuildServiceProvider();

		// Assert
		_ = Should.Throw<OptionsValidationException>(() =>
				provider.GetRequiredService<IOptions<FirestoreCdcStateStoreOptions>>().Value);
	}
}
