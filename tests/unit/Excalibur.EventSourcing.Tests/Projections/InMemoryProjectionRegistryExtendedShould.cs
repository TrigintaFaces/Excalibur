// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Extended tests for <see cref="InMemoryProjectionRegistry"/>:
/// - GetAll returns a fresh list copy each call (not same reference)
/// - GetByMode returns fresh list each call
/// - Concurrent register/get thread safety
/// - Multiple modes coexist correctly
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryProjectionRegistryExtendedShould
{
	private readonly InMemoryProjectionRegistry _registry = new();

	private static ProjectionRegistration CreateRegistration(
		Type projectionType,
		ProjectionMode mode = ProjectionMode.Inline)
	{
		return new ProjectionRegistration(
			projectionType,
			mode,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: null);
	}

	[Fact]
	public void GetAll_ReturnsFreshListOnEachCall()
	{
		// Arrange
		_registry.Register(CreateRegistration(typeof(OrderSummary)));

		// Act
		var list1 = _registry.GetAll();
		var list2 = _registry.GetAll();

		// Assert — different list instances (defensive copy, not shared mutable state)
		list1.ShouldNotBeSameAs(list2);
		list1.Count.ShouldBe(1);
		list2.Count.ShouldBe(1);
	}

	[Fact]
	public void GetByMode_ReturnsFreshListOnEachCall()
	{
		// Arrange
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Inline));

		// Act
		var list1 = _registry.GetByMode(ProjectionMode.Inline);
		var list2 = _registry.GetByMode(ProjectionMode.Inline);

		// Assert
		list1.ShouldNotBeSameAs(list2);
		list1.Count.ShouldBe(1);
		list2.Count.ShouldBe(1);
	}

	[Fact]
	public void HandleAllThreeModes()
	{
		// Arrange — register one of each mode
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Inline));
		_registry.Register(CreateRegistration(typeof(InventoryView), ProjectionMode.Async));

		// Act
		var inline = _registry.GetByMode(ProjectionMode.Inline);
		var async = _registry.GetByMode(ProjectionMode.Async);
		var ephemeral = _registry.GetByMode(ProjectionMode.Ephemeral);
		var all = _registry.GetAll();

		// Assert
		inline.Count.ShouldBe(1);
		async.Count.ShouldBe(1);
		ephemeral.Count.ShouldBe(0);
		all.Count.ShouldBe(2);
	}

	[Fact]
	public void ConcurrentRegisterAndGet_IsThreadSafe()
	{
		// Arrange — concurrent writes and reads should not throw
		var exceptions = new List<Exception>();

		// Act — register 100 types concurrently while reading
		Parallel.For(0, 100, i =>
		{
			try
			{
				// Create a unique type name by using a unique projection type per iteration
				// Using OrderSummary for all to exercise the replacement path
				_registry.Register(new ProjectionRegistration(
					typeof(OrderSummary),
					i % 2 == 0 ? ProjectionMode.Inline : ProjectionMode.Async,
					new MultiStreamProjection<OrderSummary>(),
					inlineApply: null));

				// Concurrent reads
				_ = _registry.GetAll();
				_ = _registry.GetByMode(ProjectionMode.Inline);
				_ = _registry.GetRegistration(typeof(OrderSummary));
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert — no exceptions
		exceptions.ShouldBeEmpty();

		// Final state: should have exactly one registration (last write wins for same type)
		var all = _registry.GetAll();
		all.Count.ShouldBe(1);
	}

	[Fact]
	public void RegisterReplace_PreservesOtherTypes()
	{
		// Arrange — register two types, then replace one
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Inline));
		_registry.Register(CreateRegistration(typeof(InventoryView), ProjectionMode.Async));

		// Act — replace OrderSummary
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Ephemeral));

		// Assert
		var orderReg = _registry.GetRegistration(typeof(OrderSummary));
		orderReg.ShouldNotBeNull();
		orderReg.Mode.ShouldBe(ProjectionMode.Ephemeral);

		var inventoryReg = _registry.GetRegistration(typeof(InventoryView));
		inventoryReg.ShouldNotBeNull();
		inventoryReg.Mode.ShouldBe(ProjectionMode.Async); // untouched
	}
}
