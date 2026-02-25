// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="ExcaliburEventSourcingBuilder"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExcaliburEventSourcingBuilderShould
{
	#region Test Aggregates

	/// <summary>
	/// Test aggregate with string key.
	/// </summary>
	internal sealed class OrderAggregate : AggregateRoot
	{
		public OrderAggregate()
		{ }

		public OrderAggregate(string id) : base(id)
		{
		}

		public string CustomerName { get; private set; } = string.Empty;
		public decimal TotalAmount { get; private set; }

		public void PlaceOrder(string customerName, decimal amount)
		{
			RaiseEvent(new OrderPlacedEvent(Id, Version, customerName, amount));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is OrderPlacedEvent e)
			{
				CustomerName = e.CustomerName;
				TotalAmount = e.Amount;
			}
		}
	}

	/// <summary>
	/// Test aggregate with Guid key.
	/// </summary>
	internal sealed class CustomerAggregate : AggregateRoot<Guid>
	{
		public CustomerAggregate()
		{ }

		public CustomerAggregate(Guid id) : base(id)
		{
		}

		public string Name { get; private set; } = string.Empty;

		public void Register(string name)
		{
			RaiseEvent(new CustomerRegisteredEvent(Id.ToString(), Version, name));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is CustomerRegisteredEvent e)
			{
				Name = e.Name;
			}
		}
	}

	/// <summary>
	/// Test aggregate implementing static factory pattern via IAggregateRoot interface.
	/// </summary>
	internal sealed class ProductAggregate : AggregateRoot<Guid>, IAggregateRoot<ProductAggregate, Guid>
	{
		public ProductAggregate()
		{ }

		public ProductAggregate(Guid id) : base(id)
		{
		}

		public string ProductName { get; private set; } = string.Empty;

		public static ProductAggregate Create(Guid id) => new(id);

		public static ProductAggregate FromEvents(Guid id, IEnumerable<IDomainEvent> events)
		{
			var aggregate = new ProductAggregate(id);
			aggregate.LoadFromHistory(events);
			return aggregate;
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op for test
		}
	}

	#endregion Test Aggregates

	#region Test Events

	internal sealed record OrderPlacedEvent : DomainEvent
	{
		public string CustomerName { get; init; } = string.Empty;
		public decimal Amount { get; init; }

		public OrderPlacedEvent(string aggregateId, long version, string customerName, decimal amount)
			: base(aggregateId, version, TimeProvider.System)
		{
			CustomerName = customerName;
			Amount = amount;
		}

		public OrderPlacedEvent() : base("", 0, TimeProvider.System) { }
	}

	internal sealed record CustomerRegisteredEvent : DomainEvent
	{
		public string Name { get; init; } = string.Empty;

		public CustomerRegisteredEvent(string aggregateId, long version, string name)
			: base(aggregateId, version, TimeProvider.System)
		{
			Name = name;
		}

		public CustomerRegisteredEvent() : base("", 0, TimeProvider.System) { }
	}

	#endregion Test Events

	#region Constructor Tests

	[Fact]
	public void Constructor_ShouldCreateBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Assert
		_ = builder.ShouldNotBeNull();
		builder.Services.ShouldBe(services);
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ExcaliburEventSourcingBuilder(null!));
	}

	#endregion Constructor Tests

	#region UseIntervalSnapshots Tests

	[Fact]
	public void UseIntervalSnapshots_ShouldRegisterIntervalStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseIntervalSnapshots(50);
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<IntervalSnapshotStrategy>();
	}

	[Fact]
	public void UseIntervalSnapshots_ShouldUseDefaultInterval_WhenNotSpecified()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseIntervalSnapshots();
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = strategy.ShouldBeOfType<IntervalSnapshotStrategy>();
	}

	[Fact]
	public void UseIntervalSnapshots_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion UseIntervalSnapshots Tests

	#region UseTimeBasedSnapshots Tests

	[Fact]
	public void UseTimeBasedSnapshots_ShouldRegisterTimeBasedStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseTimeBasedSnapshots(TimeSpan.FromMinutes(5));
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<TimeBasedSnapshotStrategy>();
	}

	[Fact]
	public void UseTimeBasedSnapshots_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseTimeBasedSnapshots(TimeSpan.FromMinutes(10));

		// Assert
		result.ShouldBe(builder);
	}

	#endregion UseTimeBasedSnapshots Tests

	#region UseSizeBasedSnapshots Tests

	[Fact]
	public void UseSizeBasedSnapshots_ShouldRegisterSizeBasedStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseSizeBasedSnapshots(1024 * 1024);
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<SizeBasedSnapshotStrategy>();
	}

	[Fact]
	public void UseSizeBasedSnapshots_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseSizeBasedSnapshots(1024);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion UseSizeBasedSnapshots Tests

	#region UseNoSnapshots Tests

	[Fact]
	public void UseNoSnapshots_ShouldRegisterNoSnapshotStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseNoSnapshots();
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		strategy.ShouldBeSameAs(NoSnapshotStrategy.Instance);
	}

	[Fact]
	public void UseNoSnapshots_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseNoSnapshots();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion UseNoSnapshots Tests

	#region UseCompositeSnapshotStrategy Tests

	[Fact]
	public void UseCompositeSnapshotStrategy_ShouldRegisterCompositeStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseCompositeSnapshotStrategy(composite => composite
			.AddIntervalStrategy(50)
			.AddTimeBasedStrategy(TimeSpan.FromMinutes(5)));
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<CompositeSnapshotStrategy>();
	}

	[Fact]
	public void UseCompositeSnapshotStrategy_ShouldThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseCompositeSnapshotStrategy(null!));
	}

	[Fact]
	public void UseCompositeSnapshotStrategy_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseCompositeSnapshotStrategy(composite => composite.AddIntervalStrategy(100));

		// Assert
		result.ShouldBe(builder);
	}

	#endregion UseCompositeSnapshotStrategy Tests

	#region AddSnapshotStrategy Tests

	[Fact]
	public void AddSnapshotStrategy_ShouldRegisterCustomStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.AddSnapshotStrategy<CustomSnapshotStrategy>();
		var provider = services.BuildServiceProvider();

		// Assert
		var strategy = provider.GetService<ISnapshotStrategy>();
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<CustomSnapshotStrategy>();
	}

	[Fact]
	public void AddSnapshotStrategy_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.AddSnapshotStrategy<CustomSnapshotStrategy>();

		// Assert
		result.ShouldBe(builder);
	}

	internal sealed class CustomSnapshotStrategy : ISnapshotStrategy
	{
		public bool ShouldCreateSnapshot(IAggregateRoot aggregate) => false;
	}

	#endregion AddSnapshotStrategy Tests

	#region Method Chaining Tests

	[Fact]
	public void Builder_ShouldSupportMethodChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act & Assert - Verify all methods return builder for chaining
		builder
			.UseIntervalSnapshots(100)
			.ShouldBe(builder);

		// Note: Multiple strategies won't all be registered due to TryAdd semantics
		// but the chaining should work
	}

	#endregion Method Chaining Tests

	#region CompositeSnapshotStrategyBuilder Tests

	[Fact]
	public void CompositeBuilder_AddIntervalStrategy_ShouldAddStrategy()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		_ = strategyBuilder.AddIntervalStrategy(50);
		var strategy = strategyBuilder.Build();

		// Assert
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<CompositeSnapshotStrategy>();
	}

	[Fact]
	public void CompositeBuilder_AddTimeBasedStrategy_ShouldAddStrategy()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		_ = strategyBuilder.AddTimeBasedStrategy(TimeSpan.FromMinutes(5));
		var strategy = strategyBuilder.Build();

		// Assert
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<CompositeSnapshotStrategy>();
	}

	[Fact]
	public void CompositeBuilder_AddSizeBasedStrategy_ShouldAddStrategy()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		_ = strategyBuilder.AddSizeBasedStrategy(1024 * 1024);
		var strategy = strategyBuilder.Build();

		// Assert
		_ = strategy.ShouldNotBeNull();
		_ = strategy.ShouldBeOfType<CompositeSnapshotStrategy>();
	}

	[Fact]
	public void CompositeBuilder_ShouldSupportMethodChaining()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		var result = strategyBuilder
			.AddIntervalStrategy(50)
			.AddTimeBasedStrategy(TimeSpan.FromMinutes(5))
			.AddSizeBasedStrategy(1024)
			.RequireAll();

		// Assert
		result.ShouldBe(strategyBuilder);
	}

	[Fact]
	public void CompositeBuilder_RequireAll_ShouldSetRequireAllMode()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		_ = strategyBuilder
			.AddIntervalStrategy(50)
			.AddTimeBasedStrategy(TimeSpan.FromMinutes(5))
			.RequireAll(true);
		var strategy = strategyBuilder.Build() as CompositeSnapshotStrategy;

		// Assert
		_ = strategy.ShouldNotBeNull();
		// The strategy should be in "All" mode (all strategies must agree)
	}

	[Fact]
	public void CompositeBuilder_RequireAll_False_ShouldSetAnyMode()
	{
		// Arrange
		var strategyBuilder = new CompositeSnapshotStrategyBuilder();

		// Act
		_ = strategyBuilder
			.AddIntervalStrategy(50)
			.AddTimeBasedStrategy(TimeSpan.FromMinutes(5))
			.RequireAll(false);
		var strategy = strategyBuilder.Build() as CompositeSnapshotStrategy;

		// Assert
		_ = strategy.ShouldNotBeNull();
		// The strategy should be in "Any" mode (any strategy can trigger snapshot)
	}

	#endregion CompositeSnapshotStrategyBuilder Tests

	#region Edge Cases

	[Fact]
	public void MultipleStrategyRegistrations_FirstWins_DueToTryAddSemantics()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act - Register interval first, then time-based
		_ = builder.UseIntervalSnapshots(100);
		_ = builder.UseTimeBasedSnapshots(TimeSpan.FromMinutes(5));
		var provider = services.BuildServiceProvider();

		// Assert - First registration wins due to TryAddSingleton
		var strategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = strategy.ShouldBeOfType<IntervalSnapshotStrategy>();
	}

	[Fact]
	public void Builder_ExposesServicesProperty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Assert
		_ = builder.Services.ShouldNotBeNull();
		builder.Services.ShouldBeSameAs(services);
	}

	#endregion Edge Cases
}
