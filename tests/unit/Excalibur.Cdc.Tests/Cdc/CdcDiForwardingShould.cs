// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Data.Tests.Cdc;

/// <summary>
/// Tests verifying that CDC provider DI registrations forward to base interfaces
/// (ICdcProcessor&lt;T&gt; and ICdcStreamProcessor&lt;T, TPos&gt;) so consumers can
/// depend on the abstraction level they need. Uses InMemory provider as the
/// concrete test target since it requires no external dependencies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "CDC")]
public sealed class CdcDiForwardingShould : IDisposable
{
	private readonly ServiceProvider _provider;

	public CdcDiForwardingShould()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCdcProcessor(cdc => cdc.UseInMemory());
		_provider = services.BuildServiceProvider();
	}

	public void Dispose() => _provider.Dispose();

	// ========================================
	// Marker interface resolution
	// ========================================

	[Fact]
	public void ResolveInMemoryCdcProcessor_ViaMarkerInterface()
	{
		// Act
		var processor = _provider.GetService<IInMemoryCdcProcessor>();

		// Assert
		processor.ShouldNotBeNull();
	}

	// ========================================
	// Base interface forwarding
	// ========================================

	[Fact]
	public void ResolveICdcProcessor_ViaBaseInterface()
	{
		// Act
		var processor = _provider.GetService<ICdcProcessor<InMemoryCdcChange>>();

		// Assert
		processor.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameInstance_ForMarkerAndBaseInterface()
	{
		// Act
		var marker = _provider.GetRequiredService<IInMemoryCdcProcessor>();
		var baseInterface = _provider.GetRequiredService<ICdcProcessor<InMemoryCdcChange>>();

		// Assert — forwarding should resolve to the same singleton instance
		ReferenceEquals(marker, baseInterface).ShouldBeTrue(
			"ICdcProcessor<T> should forward to the same instance as IInMemoryCdcProcessor");
	}

	// ========================================
	// Store resolution
	// ========================================

	[Fact]
	public void ResolveInMemoryCdcStore()
	{
		// Act
		var store = _provider.GetService<IInMemoryCdcStore>();

		// Assert
		store.ShouldNotBeNull();
	}

	// ========================================
	// Double registration safety (TryAdd)
	// ========================================

	[Fact]
	public void NotDuplicateRegistrations_WhenCalledTwice()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCdcProcessor(cdc => cdc.UseInMemory());
		services.AddCdcProcessor(cdc => cdc.UseInMemory());

		using var provider = services.BuildServiceProvider();

		// Act — should resolve without ambiguity
		var processor = provider.GetService<ICdcProcessor<InMemoryCdcChange>>();

		// Assert
		processor.ShouldNotBeNull();
	}

	// ========================================
	// Pre-configured store overload
	// ========================================

	[Fact]
	public void ResolveBaseInterface_WithPreConfiguredStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var store = A.Fake<IInMemoryCdcStore>();
		services.AddCdcProcessor(cdc => cdc.UseInMemory(store));

		using var provider = services.BuildServiceProvider();

		// Act
		var processor = provider.GetService<ICdcProcessor<InMemoryCdcChange>>();

		// Assert
		processor.ShouldNotBeNull();
	}
}