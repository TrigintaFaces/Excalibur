// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryProjectionRegistryShould
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
	public void ReturnNullForUnregisteredType()
	{
		// Act
		var result = _registry.GetRegistration(typeof(OrderSummary));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RegisterAndRetrieveProjection()
	{
		// Arrange
		var registration = CreateRegistration(typeof(OrderSummary));

		// Act
		_registry.Register(registration);
		var result = _registry.GetRegistration(typeof(OrderSummary));

		// Assert
		result.ShouldNotBeNull();
		result.ProjectionType.ShouldBe(typeof(OrderSummary));
		result.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void ReplaceExistingRegistrationForSameType()
	{
		// Arrange
		var first = CreateRegistration(typeof(OrderSummary), ProjectionMode.Ephemeral);
		var second = CreateRegistration(typeof(OrderSummary), ProjectionMode.Async);

		// Act
		_registry.Register(first);
		_registry.Register(second);
		var result = _registry.GetRegistration(typeof(OrderSummary));

		// Assert -- second registration replaces first (R27.37)
		result.ShouldNotBeNull();
		result.Mode.ShouldBe(ProjectionMode.Async);
	}

	[Fact]
	public void GetAllRegisteredProjections()
	{
		// Arrange
		_registry.Register(CreateRegistration(typeof(OrderSummary)));
		_registry.Register(CreateRegistration(typeof(InventoryView)));

		// Act
		var all = _registry.GetAll();

		// Assert
		all.Count.ShouldBe(2);
	}

	[Fact]
	public void FilterByMode()
	{
		// Arrange
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Ephemeral));
		_registry.Register(CreateRegistration(typeof(InventoryView), ProjectionMode.Async));

		// Act
		var ephemeralOnly = _registry.GetByMode(ProjectionMode.Ephemeral);
		var asyncOnly = _registry.GetByMode(ProjectionMode.Async);

		// Assert
		ephemeralOnly.Count.ShouldBe(1);
		ephemeralOnly[0].ProjectionType.ShouldBe(typeof(OrderSummary));
		asyncOnly.Count.ShouldBe(1);
		asyncOnly[0].ProjectionType.ShouldBe(typeof(InventoryView));
	}

	[Fact]
	public void ReturnEmptyListWhenNoMatchingMode()
	{
		// Arrange
		_registry.Register(CreateRegistration(typeof(OrderSummary), ProjectionMode.Async));

		// Act
		var ephemeral = _registry.GetByMode(ProjectionMode.Ephemeral);

		// Assert
		ephemeral.ShouldBeEmpty();
	}

	[Fact]
	public void ThrowOnNullProjectionType()
	{
		Should.Throw<ArgumentNullException>(() => _registry.GetRegistration(null!));
	}

	[Fact]
	public void ThrowOnNullRegistration()
	{
		Should.Throw<ArgumentNullException>(() => _registry.Register(null!));
	}

	[Fact]
	public void ReturnEmptyGetAllWhenEmpty()
	{
		_registry.GetAll().ShouldBeEmpty();
	}
}
