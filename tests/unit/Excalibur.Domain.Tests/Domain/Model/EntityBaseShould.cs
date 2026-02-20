// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Unit tests for <see cref="EntityBase{TKey}"/> and <see cref="EntityBase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EntityBaseShould
{
	#region T419.5: Core EntityBase Tests

	[Fact]
	public void Key_ReturnsCorrectValue()
	{
		// Arrange
		const string key = "entity-key-123";

		// Act
		var entity = new TestStringEntity(key);

		// Assert
		entity.Key.ShouldBe(key);
	}

	[Fact]
	public void DerivedEntity_ImplementsKeyCorrectly()
	{
		// Arrange
		var key = Guid.NewGuid();

		// Act
		var entity = new TestGuidEntity(key);

		// Assert
		entity.Key.ShouldBe(key);
		((IEntity<Guid>)entity).Key.ShouldBe(key);
	}

	[Fact]
	public void DefaultConstructor_CanBeInvoked()
	{
		// Arrange & Act
		var entity = new TestEntityWithDefaultConstructor();

		// Assert
		entity.Key.ShouldBe("default-key");
	}

	#endregion T419.5: Core EntityBase Tests

	#region T419.6: EntityBase (string) Alias Tests

	[Fact]
	public void StringEntity_InheritsFromEntityBaseOfString()
	{
		// Arrange & Act
		var entity = new TestStringEntity("test-key");

		// Assert
		_ = entity.ShouldBeAssignableTo<EntityBase<string>>();
		_ = entity.ShouldBeAssignableTo<EntityBase>();
	}

	[Fact]
	public void StringEntity_KeyPropertyWorksWithStringKeys()
	{
		// Arrange
		const string key = "string-entity-key-456";

		// Act
		var entity = new TestStringEntity(key);

		// Assert
		entity.Key.ShouldBe(key);
	}

	#endregion T419.6: EntityBase (string) Alias Tests

	#region T419.7: Equality and Hashing Tests

	[Fact]
	public void Equals_ReturnsTrue_ForSameTypeAndKey()
	{
		// Arrange
		var entity1 = new TestStringEntity("same-key");
		var entity2 = new TestStringEntity("same-key");

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnsFalse_ForSameTypeDifferentKey()
	{
		// Arrange
		var entity1 = new TestStringEntity("key-1");
		var entity2 = new TestStringEntity("key-2");

		// Act & Assert
		entity1.Equals(entity2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_ForDifferentType()
	{
		// Arrange
		var entity1 = new TestStringEntity("same-key");
		var entity2 = new AnotherTestStringEntity("same-key");

		// Act & Assert
		entity1.Equals(entity2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenOtherIsNull()
	{
		// Arrange
		var entity = new TestStringEntity("test-key");

		// Act & Assert
		entity.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenOtherIsNotEntity()
	{
		// Arrange
		var entity = new TestStringEntity("test-key");
		var notAnEntity = "not-an-entity";

		// Act & Assert
		entity.Equals(notAnEntity).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_IsConsistent_ForEqualEntities()
	{
		// Arrange
		var entity1 = new TestStringEntity("same-key");
		var entity2 = new TestStringEntity("same-key");

		// Act & Assert
		entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_IsDifferent_ForDifferentKeys()
	{
		// Arrange
		var entity1 = new TestStringEntity("key-1");
		var entity2 = new TestStringEntity("key-2");

		// Act & Assert
		entity1.GetHashCode().ShouldNotBe(entity2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_IsDifferent_ForDifferentTypes()
	{
		// Arrange
		var entity1 = new TestStringEntity("same-key");
		var entity2 = new AnotherTestStringEntity("same-key");

		// Act & Assert
		entity1.GetHashCode().ShouldNotBe(entity2.GetHashCode());
	}

	#endregion T419.7: Equality and Hashing Tests

	#region Generic EntityBase<TKey> Tests

	[Fact]
	public void GenericEntity_WorksWithGuidKey()
	{
		// Arrange
		var key = Guid.NewGuid();

		// Act
		var entity = new TestGuidEntity(key);

		// Assert
		entity.Key.ShouldBe(key);
	}

	[Fact]
	public void GenericEntity_WorksWithIntKey()
	{
		// Arrange
		const int key = 42;

		// Act
		var entity = new TestIntEntity(key);

		// Assert
		entity.Key.ShouldBe(key);
	}

	[Fact]
	public void Equals_WorksCorrectly_ForGuidKeyedEntities()
	{
		// Arrange
		var key = Guid.NewGuid();
		var entity1 = new TestGuidEntity(key);
		var entity2 = new TestGuidEntity(key);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WorksCorrectly_ForIntKeyedEntities()
	{
		// Arrange
		const int key = 123;
		var entity1 = new TestIntEntity(key);
		var entity2 = new TestIntEntity(key);

		// Act & Assert
		entity1.Equals(entity2).ShouldBeTrue();
	}

	#endregion Generic EntityBase<TKey> Tests

	#region Test Entities

	/// <summary>
	/// Test entity with string key (uses EntityBase alias).
	/// </summary>
	private sealed class TestStringEntity : EntityBase
	{
		private readonly string _key;

		public TestStringEntity(string key)
		{
			_key = key;
		}

		public override string Key => _key;
	}

	/// <summary>
	/// Another test entity with string key (for type comparison tests).
	/// </summary>
	private sealed class AnotherTestStringEntity : EntityBase
	{
		private readonly string _key;

		public AnotherTestStringEntity(string key)
		{
			_key = key;
		}

		public override string Key => _key;
	}

	/// <summary>
	/// Test entity with default constructor.
	/// </summary>
	private sealed class TestEntityWithDefaultConstructor : EntityBase
	{
		public override string Key => "default-key";
	}

	/// <summary>
	/// Test entity with Guid key.
	/// </summary>
	private sealed class TestGuidEntity : EntityBase<Guid>
	{
		private readonly Guid _key;

		public TestGuidEntity(Guid key)
		{
			_key = key;
		}

		public override Guid Key => _key;
	}

	/// <summary>
	/// Test entity with int key.
	/// </summary>
	private sealed class TestIntEntity : EntityBase<int>
	{
		private readonly int _key;

		public TestIntEntity(int key)
		{
			_key = key;
		}

		public override int Key => _key;
	}

	#endregion Test Entities
}
