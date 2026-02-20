// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBulkCleanupServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddOutboxBulkCleanup());
	}

	[Fact]
	public void RegisterOutboxBulkCleanupAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxStoreAdmin>());
		services.AddLogging();

		// Act
		services.AddOutboxBulkCleanup();

		// Assert
		var descriptor = services.SingleOrDefault(d =>
			d.ServiceType == typeof(IOutboxBulkCleanup));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(OutboxBulkCleanupAdapter));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddOutboxBulkCleanup();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void NotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeCleanup = A.Fake<IOutboxBulkCleanup>();
		services.AddSingleton(fakeCleanup);

		// Act
		services.AddOutboxBulkCleanup();

		// Assert - TryAddSingleton should not replace existing
		var descriptors = services.Where(d =>
			d.ServiceType == typeof(IOutboxBulkCleanup)).ToList();
		descriptors.Count.ShouldBe(1);
	}
}
