// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class PersistenceProviderBuilderShould
{
	[Fact]
	public void ThrowWhenInnerProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new PersistenceProviderBuilder(null!));
	}

	[Fact]
	public void ReturnInnerProviderWhenNoDecoratorsAdded()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		// Act
		var result = builder.Build();

		// Assert
		result.ShouldBeSameAs(inner);
	}

	[Fact]
	public void ApplySingleDecorator()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var decorated = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		// Act
		var result = builder.Use(_ => decorated).Build();

		// Assert
		result.ShouldBeSameAs(decorated);
	}

	[Fact]
	public void ApplyMultipleDecoratorsInOrder()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("inner");
		var callOrder = new List<string>();

		var builder = new PersistenceProviderBuilder(inner);

		// Act
		var result = builder
			.Use(p =>
			{
				callOrder.Add($"decorator1-wrapping-{p.Name}");
				var fake = A.Fake<IPersistenceProvider>();
				A.CallTo(() => fake.Name).Returns("decorator1");
				return fake;
			})
			.Use(p =>
			{
				callOrder.Add($"decorator2-wrapping-{p.Name}");
				var fake = A.Fake<IPersistenceProvider>();
				A.CallTo(() => fake.Name).Returns("decorator2");
				return fake;
			})
			.Build();

		// Assert - decorators applied in registration order
		callOrder.Count.ShouldBe(2);
		callOrder[0].ShouldBe("decorator1-wrapping-inner");
		callOrder[1].ShouldBe("decorator2-wrapping-decorator1");
		result.Name.ShouldBe("decorator2");
	}

	[Fact]
	public void ThrowWhenDecoratorIsNull()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.Use(null!));
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		// Act - chaining should return the same builder
		var returned = builder.Use(p => p);

		// Assert
		returned.ShouldBeSameAs(builder);
	}
}
