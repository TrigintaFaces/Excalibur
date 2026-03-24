// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Firestore;

namespace Excalibur.Data.Tests.Firestore.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IFirestoreCdcBuilder.WithStateStore"/> and
/// <see cref="IFirestoreCdcBuilder.BindConfiguration"/> methods.
/// Validates the unified WithStateStore(Action&lt;ICdcStateStoreBuilder&gt;) pattern.
/// Firestore uses project IDs via ConnectionString() instead of traditional connection strings.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FirestoreCdcWithStateStoreShould : UnitTestBase
{
	private const string StateProjectId = "my-state-project";

	// --- WithStateStore(Action<ICdcStateStoreBuilder>) ---

	[Fact]
	public void WithStateStore_ProjectId_AcceptsValidValue()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseFirestore(firestore =>
				firestore.CollectionPath("orders")
				         .ProcessorName("order-cdc")
				         .WithStateStore(state =>
					         state.ConnectionString(StateProjectId))));

		// Assert -- FirestoreCdcStateStoreOptions should be registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(FirestoreCdcStateStoreOptions));
	}

	[Fact]
	public void WithStateStore_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseFirestore(firestore =>
					firestore.WithStateStore((Action<ICdcStateStoreBuilder>)null!))));
	}

	// --- WithStateStore with TableName ---

	[Fact]
	public void WithStateStore_ProjectIdWithConfigure_AppliesStateStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseFirestore(firestore =>
				firestore.CollectionPath("orders")
				         .ProcessorName("order-cdc")
				         .WithStateStore(state =>
					         state.ConnectionString(StateProjectId)
					              .TableName("custom-positions"))));

		// Assert -- state store options reflect custom collection name
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<FirestoreCdcStateStoreOptions>>();
		stateOptions.Value.CollectionName.ShouldBe("custom-positions");
	}

	// --- Backward compatibility: omitting WithStateStore ---

	[Fact]
	public void WithoutWithStateStore_SourceOptionsStillRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- no WithStateStore call
		services.AddCdcProcessor(builder =>
			builder.UseFirestore(firestore =>
				firestore.CollectionPath("orders")
				         .ProcessorName("order-cdc")));

		// Assert -- FirestoreCdcOptions are registered
		services.ShouldContain(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(FirestoreCdcOptions));
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_SetsSourceBindConfigurationPath()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- BindConfiguration is accepted without error
		services.AddCdcProcessor(builder =>
			builder.UseFirestore(firestore =>
				firestore.BindConfiguration("Cdc:Firestore")));

		// Assert -- IConfigureOptions<FirestoreCdcOptions> registration exists from BindConfiguration
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(FirestoreCdcOptions));

		optionsDescriptors.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowsOnInvalidSectionPath(string? invalidPath)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseFirestore(firestore =>
					firestore.BindConfiguration(invalidPath!))));
	}

	// --- State store BindConfiguration via ICdcStateStoreBuilder ---

	[Fact]
	public void WithStateStore_StateStoreBindConfiguration_Accepted()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseFirestore(firestore =>
				firestore.CollectionPath("orders")
				         .ProcessorName("order-cdc")
				         .WithStateStore(state =>
					         state.ConnectionString(StateProjectId)
					              .BindConfiguration("Cdc:State"))));

		// Assert -- state store options BindConfiguration is wired
		var stateOptionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(FirestoreCdcStateStoreOptions));

		stateOptionsDescriptors.ShouldNotBeEmpty();
	}
}
