// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Authorization;

namespace Excalibur.Data.Tests.Firestore.Authorization;

/// <summary>
/// Unit tests for <see cref="A3BuilderFirestoreExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
[Trait("Feature", "DependencyInjection")]
public sealed class A3BuilderFirestoreExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		IA3Builder? builder = null;

		Should.Throw<ArgumentNullException>(() =>
			builder!.UseFirestore(options => { options.ProjectId = "test"; }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore(null!));
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UseFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(builder);
	}

	[Fact]
	public void RegisterIGrantStore_AsFirestoreGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(FirestoreGrantStore));
	}

	[Fact]
	public void RegisterIActivityGroupGrantStore_AsFirestoreActivityGroupGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupGrantStore) &&
			sd.ImplementationType == typeof(FirestoreActivityGroupGrantStore));
	}
}
