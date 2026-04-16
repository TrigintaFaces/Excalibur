// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderFirestoreExtensions" />.
/// </summary>
/// <remarks>
/// Phase C rewire: Updated from FirestoreOutboxServiceCollectionExtensions to
/// OutboxBuilderFirestoreExtensions.UseFirestore(Action&lt;IFirestoreOutboxBuilder&gt;).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FirestoreOutboxServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseFirestore_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(fs =>
		{
			fs.ProjectId("test-project");
		}));

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(FirestoreOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void UseFirestore_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(fs =>
		{
			fs.ProjectId("test-project")
			  .CollectionName("custom-collection");
		}));

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreOutboxOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void UseFirestore_ThrowsOnNullBuilder()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			OutboxBuilderFirestoreExtensions.UseFirestore(null!, fs => { }));
	}

	[Fact]
	public void UseFirestore_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseFirestore((Action<Excalibur.Outbox.Firestore.IFirestoreOutboxBuilder>)null!)));
	}

	[Fact]
	public void UseFirestore_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		Excalibur.Outbox.IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseFirestore(fs => fs.ProjectId("test-project"));
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void UseFirestore_RegistersSingleCloudNativeOutboxStore()
	{
		// Regression: Ensure UseFirestore() only registers ICloudNativeOutboxStore once

		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(fs => fs.ProjectId("test-project")));

		// Assert -- count ICloudNativeOutboxStore registrations
		var registrations = services.Where(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore)).ToList();
		registrations.Count.ShouldBe(1,
			"UseFirestore() should register ICloudNativeOutboxStore exactly once");
	}

	[Fact]
	public void UseFirestore_OptionsNamespace_IsCanonical()
	{
		// Regression: Verify the options type is from Excalibur.Outbox.Firestore namespace

		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(fs =>
		{
			fs.ProjectId("test-project");
		}));

		// Assert
		var optionsDescriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreOutboxOptions>));
		optionsDescriptor.ShouldNotBeNull();

		// Verify the options type is from the canonical namespace
		typeof(FirestoreOutboxOptions).Namespace.ShouldBe("Excalibur.Outbox.Firestore",
			"FirestoreOutboxOptions should be from the canonical Outbox.Firestore package");
	}
}
