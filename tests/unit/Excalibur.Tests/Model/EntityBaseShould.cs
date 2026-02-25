// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Model;

/// <summary>
///     Unit tests for the <see cref="EntityBase" /> and <see cref="EntityBase{TKey}" /> classes.
/// </summary>
/// <remarks>
///     Tests entity equality semantics, hash code generation, interface compliance, and inheritance behavior for both generic and
///     non-generic entity base classes.
/// </remarks>
[Trait("Category", "Unit")]
public class EntityBaseShould
{
	[Fact]
	public void StringKeyEntityShouldImplementIEntityInterface()
	{
		// Arrange & Act
		var entity = new TestEntity("test-key");

		// Assert
		_ = entity.ShouldBeAssignableTo<IEntity>();
		_ = entity.ShouldBeAssignableTo<IEntity<string>>();
	}

	[Fact]
	public void GenericEntityShouldImplementIEntityInterface()
	{
		// Arrange & Act
		var entity = new TestEntityWithGuidKey(Guid.NewGuid());

		// Assert
		_ = entity.ShouldBeAssignableTo<IEntity>();
		_ = entity.ShouldBeAssignableTo<IEntity<Guid>>();
	}

	[Fact]
	public void EntitiesWithSameKeyAndTypeShouldBeEqual()
	{
		// Arrange
		const string key = "test-key";
		var entity1 = new TestEntity(key);
		var entity2 = new TestEntity(key);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
		entity2.Equals(entity1).ShouldBeTrue();
		(entity1 == entity2).ShouldBeFalse(); // Reference equality, not overridden
	}

	[Fact]
	public void EntitiesWithDifferentKeysShouldNotBeEqual()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key2");

		// Act & Assert
		entity1.Equals(entity2).ShouldBeFalse();
		entity2.Equals(entity1).ShouldBeFalse();
	}

	[Fact]
	public void EntitiesWithSameKeyButDifferentTypesShouldNotBeEqual()
	{
		// Arrange
		const string key = "test-key";
		var entity1 = new TestEntity(key);
		var entity2 = new DifferentTestEntity(key);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeFalse();
		entity2.Equals(entity1).ShouldBeFalse();
	}

	[Fact]
	[SuppressMessage("Style", "CA1508:Avoid dead code", Justification = "Testing explicit null comparison behavior")]
	public void EntityShouldNotEqualNull()
	{
		// Arrange
		var entity = new TestEntity("test-key");

		// Act & Assert
		entity.Equals(null).ShouldBeFalse();
		entity.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EntityShouldNotEqualObjectOfDifferentType()
	{
		// Arrange
		var entity = new TestEntity("test-key");
		var otherObject = "test-string";

		// Act & Assert
		entity.Equals(otherObject).ShouldBeFalse();
	}

	[Fact]
	public void EntitiesWithSameKeyAndTypeShouldHaveSameHashCode()
	{
		// Arrange
		const string key = "test-key";
		var entity1 = new TestEntity(key);
		var entity2 = new TestEntity(key);

		// Act
		var hashCode1 = entity1.GetHashCode();
		var hashCode2 = entity2.GetHashCode();

		// Assert
		hashCode1.ShouldBe(hashCode2);
	}

	[Fact]
	public void EntitiesWithDifferentKeysShouldHaveDifferentHashCodes()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key2");

		// Act
		var hashCode1 = entity1.GetHashCode();
		var hashCode2 = entity2.GetHashCode();

		// Assert
		hashCode1.ShouldNotBe(hashCode2);
	}

	[Fact]
	public void EntitiesWithSameKeyButDifferentTypesShouldHaveDifferentHashCodes()
	{
		// Arrange
		const string key = "test-key";
		var entity1 = new TestEntity(key);
		var entity2 = new DifferentTestEntity(key);

		// Act
		var hashCode1 = entity1.GetHashCode();
		var hashCode2 = entity2.GetHashCode();

		// Assert
		hashCode1.ShouldNotBe(hashCode2);
	}

	[Fact]
	public void GuidKeyEntitiesShouldFollowEqualityRules()
	{
		// Arrange
		var key = Guid.NewGuid();
		var entity1 = new TestEntityWithGuidKey(key);
		var entity2 = new TestEntityWithGuidKey(key);
		var entity3 = new TestEntityWithGuidKey(Guid.NewGuid());

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
		entity1.Equals(entity3).ShouldBeFalse();
		entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
		entity1.GetHashCode().ShouldNotBe(entity3.GetHashCode());
	}

	[Fact]
	public void IntKeyEntitiesShouldFollowEqualityRules()
	{
		// Arrange
		const int key = 42;
		var entity1 = new TestEntityWithIntKey(key);
		var entity2 = new TestEntityWithIntKey(key);
		var entity3 = new TestEntityWithIntKey(43);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
		entity1.Equals(entity3).ShouldBeFalse();
		entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
		entity1.GetHashCode().ShouldNotBe(entity3.GetHashCode());
	}

	[Fact]
	public void HashCodeShouldIncorporateEntityType()
	{
		// Arrange - two different entity types with the same key
		var entity = new TestEntity("test-key");
		var intEntity = new TestEntityWithIntKey(0); // Different type

		// Act
		var hashCode = entity.GetHashCode();
		var expectedHashCode = HashCode.Combine(typeof(TestEntity), (object)"test-key");

		// Assert - hash code matches HashCode.Combine(type, key) and differs from other entity types
		hashCode.ShouldBe(expectedHashCode);
		hashCode.ShouldNotBe(intEntity.GetHashCode());
	}

	[Fact]
	public void HashCodeShouldIncorporateKey()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key2");

		// Act
		var hashCode1 = entity1.GetHashCode();
		var hashCode2 = entity2.GetHashCode();

		// Assert
		hashCode1.ShouldNotBe(hashCode2);
	}

	[Fact]
	public void EntityShouldBeEqualsToItselfThroughObjectEquals()
	{
		// Arrange
		var entity = new TestEntity("test-key");

		// Act & Assert
		entity.Equals(entity).ShouldBeTrue();
	}

	[Fact]
	public void EntityShouldBeEqualsToAnotherEntityWithSameKeyThroughObjectEquals()
	{
		// Arrange
		const string key = "test-key";
		var entity1 = new TestEntity(key);
		var entity2 = new TestEntity(key);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
	}

	[Fact]
	public void DefaultStringEntityShouldInheritFromGenericEntityBase()
	{
		// Arrange & Act
		var entity = new TestEntity("test-key");

		// Assert
		_ = entity.ShouldBeAssignableTo<EntityBase<string>>();
	}

	// Test entity implementations for testing purposes
	private sealed class TestEntity(string key) : EntityBase
	{
		public override string Key { get; } = key;
	}

	private sealed class TestEntityWithGuidKey(Guid key) : EntityBase<Guid>
	{
		public override Guid Key { get; } = key;
	}

	private sealed class TestEntityWithIntKey(int key) : EntityBase<int>
	{
		public override int Key { get; } = key;
	}

	private sealed class DifferentTestEntity(string key) : EntityBase
	{
		public override string Key { get; } = key;
	}
}
