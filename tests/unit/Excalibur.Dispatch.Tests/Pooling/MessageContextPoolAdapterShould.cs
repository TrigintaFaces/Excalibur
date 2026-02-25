// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Pooling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextPoolAdapterShould
{
	private static IServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		return services.BuildServiceProvider();
	}

	private static IOptions<ContextPoolingOptions> CreateOptions(
		bool enabled = true,
		int maxPoolSize = 16,
		int preWarmCount = 0,
		bool trackMetrics = false)
	{
		return Microsoft.Extensions.Options.Options.Create(new ContextPoolingOptions
		{
			Enabled = enabled,
			MaxPoolSize = maxPoolSize,
			PreWarmCount = preWarmCount,
			TrackMetrics = trackMetrics,
		});
	}

	[Fact]
	public void Constructor_WithNullServiceProvider_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageContextPoolAdapter(null!, CreateOptions()));
	}

	[Fact]
	public void Constructor_WithNullOptions_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageContextPoolAdapter(CreateServiceProvider(), null!));
	}

	[Fact]
	public void Rent_ReturnsNonNullContext()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(CreateServiceProvider(), CreateOptions());

		// Act
		var context = adapter.Rent();

		// Assert
		context.ShouldNotBeNull();
	}

	[Fact]
	public void Rent_WithMessage_ReturnsContextWithMessage()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(CreateServiceProvider(), CreateOptions());
		var message = A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>();

		// Act
		var context = adapter.Rent(message);

		// Assert
		context.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnToPool_WithNonPoolableContext_DoesNotThrow()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(CreateServiceProvider(), CreateOptions());
		var fakeContext = A.Fake<Excalibur.Dispatch.Abstractions.IMessageContext>();

		// Act & Assert - should not throw for non-poolable context
		adapter.ReturnToPool(fakeContext);
	}

	[Fact]
	public void ReturnToPool_WithRentedContext_DoesNotThrow()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(CreateServiceProvider(), CreateOptions());
		var context = adapter.Rent();

		// Act & Assert
		adapter.ReturnToPool(context);
	}

	[Fact]
	public void RentCount_WithMetricsEnabled_TracksRentals()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(
			CreateServiceProvider(),
			CreateOptions(trackMetrics: true));

		// Act
		_ = adapter.Rent();
		_ = adapter.Rent();
		_ = adapter.Rent();

		// Assert
		adapter.RentCount.ShouldBe(3);
	}

	[Fact]
	public void RentCount_WithMetricsDisabled_ReturnsZero()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(
			CreateServiceProvider(),
			CreateOptions(trackMetrics: false));

		// Act
		_ = adapter.Rent();
		_ = adapter.Rent();

		// Assert
		adapter.RentCount.ShouldBe(0);
	}

	[Fact]
	public void ReturnCount_WithMetricsEnabled_TracksReturns()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(
			CreateServiceProvider(),
			CreateOptions(trackMetrics: true));

		// Act
		var ctx1 = adapter.Rent();
		var ctx2 = adapter.Rent();
		adapter.ReturnToPool(ctx1);
		adapter.ReturnToPool(ctx2);

		// Assert
		adapter.ReturnCount.ShouldBe(2);
	}

	[Fact]
	public void PreWarm_CreatesContextsOnConstruction()
	{
		// Act - creating the adapter with pre-warm should not throw
		var adapter = new MessageContextPoolAdapter(
			CreateServiceProvider(),
			CreateOptions(preWarmCount: 4));

		// Assert - adapter should be usable after pre-warm
		var context = adapter.Rent();
		context.ShouldNotBeNull();
	}

	[Fact]
	public void Rent_MessageOverload_WithMetricsEnabled_TracksRentals()
	{
		// Arrange
		var adapter = new MessageContextPoolAdapter(
			CreateServiceProvider(),
			CreateOptions(trackMetrics: true));
		var message = A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>();

		// Act
		_ = adapter.Rent(message);

		// Assert
		adapter.RentCount.ShouldBe(1);
	}
}
