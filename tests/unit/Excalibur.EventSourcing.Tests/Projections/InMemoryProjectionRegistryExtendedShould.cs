// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		ProjectionMode mode = ProjectionMode.Async)
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
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Async));

		// Act
		var list1 = _registry.GetByMode(ProjectionMode.Async);
		var list2 = _registry.GetByMode(ProjectionMode.Async);

		// Assert
		list1.ShouldNotBeSameAs(list2);
		list1.Count.ShouldBe(1);
		list2.Count.ShouldBe(1);
	}

	[Fact]
	public void HandleEveryMode()
	{
		// Arrange — register one of each mode
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Ephemeral));
		_registry.Register(CreateRegistration(typeof(InventoryView), ProjectionMode.Async));

		// Act
		var async = _registry.GetByMode(ProjectionMode.Async);
		var ephemeral = _registry.GetByMode(ProjectionMode.Ephemeral);
		var all = _registry.GetAll();

		// Assert
		async.Count.ShouldBe(1);
		ephemeral.Count.ShouldBe(1);
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
					i % 2 == 0 ? ProjectionMode.Async : ProjectionMode.Async,
					new MultiStreamProjection<OrderSummary>(),
					inlineApply: null));

				// Concurrent reads
				_ = _registry.GetAll();
				_ = _registry.GetByMode(ProjectionMode.Async);
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
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Async));
		_registry.Register(CreateRegistration(typeof(InventoryView), ProjectionMode.Async));

		// Act — replace OrderSummary. Same mode: a cross-mode re-registration is refused, so the
		// preserve-the-others property is exercised through the replacement that is still legal.
		var replacement = CreateRegistration(typeof(OrderSummary), ProjectionMode.Async);
		_registry.Register(replacement);

		// Assert
		var orderReg = _registry.GetRegistration(typeof(OrderSummary));
		orderReg.ShouldNotBeNull();
		orderReg.ShouldBeSameAs(replacement);

		var inventoryReg = _registry.GetRegistration(typeof(InventoryView));
		inventoryReg.ShouldNotBeNull();
		inventoryReg.Mode.ShouldBe(ProjectionMode.Async); // untouched
	}
}
