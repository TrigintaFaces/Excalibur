// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Outbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreOutboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify outbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "DependencyInjection")]
public sealed class FirestoreOutboxExtensionsShould
{
	#region AddFirestoreOutboxStore with Action Tests

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreOutboxStore(options => { }));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreOutboxStore((Action<FirestoreOutboxOptions>)null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreOutboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_RegistersFirestoreOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestoreOutboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestoreOutboxStore));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithAction_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestoreOutboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion

	#region AddFirestoreOutboxStore with ProjectId Tests

	[Fact]
	public void AddFirestoreOutboxStore_WithProjectId_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreOutboxStore("test-project"));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreOutboxStore((string)null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreOutboxStore(string.Empty));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreOutboxStore("   "));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithProjectId_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreOutboxStore("test-project");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddFirestoreOutboxStore with DbProvider Tests

	[Fact]
	public void AddFirestoreOutboxStore_WithDbProvider_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddFirestoreOutboxStore(_ => null!, options => { }));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithDbProvider_ThrowsArgumentNullException_WhenDbProviderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreOutboxStore(null!, options => { }));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithDbProvider_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreOutboxStore(_ => null!, null!));
	}

	[Fact]
	public void AddFirestoreOutboxStore_WithDbProvider_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreOutboxStore(
			_ => null!,
			options => { options.ProjectId = "test"; });

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(FirestoreOutboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(FirestoreOutboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(FirestoreOutboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
