// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Inbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreInboxExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify inbox extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "DependencyInjection")]
public sealed class FirestoreInboxExtensionsShould
{
	#region AddFirestoreInboxStore with Action Tests

	[Fact]
	public void AddFirestoreInboxStore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreInboxStore(options => { }));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreInboxStore((Action<FirestoreInboxOptions>)null!));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreInboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddFirestoreInboxStore_WithAction_RegistersFirestoreInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestoreInboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestoreInboxStore));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithAction_RegistersIInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestoreInboxStore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IInboxStore));
	}

	#endregion

	#region AddFirestoreInboxStore with ProjectId Tests

	[Fact]
	public void AddFirestoreInboxStore_WithProjectId_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreInboxStore("test-project"));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreInboxStore((string)null!));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreInboxStore(string.Empty));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithProjectId_ThrowsArgumentException_WhenProjectIdIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestoreInboxStore("   "));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithProjectId_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreInboxStore("test-project");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddFirestoreInboxStore with DbProvider Tests

	[Fact]
	public void AddFirestoreInboxStore_WithDbProvider_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddFirestoreInboxStore(_ => null!, options => { }));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithDbProvider_ThrowsArgumentNullException_WhenDbProviderIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreInboxStore(null!, options => { }));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithDbProvider_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreInboxStore(_ => null!, null!));
	}

	[Fact]
	public void AddFirestoreInboxStore_WithDbProvider_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreInboxStore(
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
		typeof(FirestoreInboxExtensions).IsAbstract.ShouldBeTrue();
		typeof(FirestoreInboxExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(FirestoreInboxExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
