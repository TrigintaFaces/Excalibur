using Excalibur.Domain.Model;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Model;

public class EntityBaseShould
{
	[Fact]
	public void HaveEqualityBasedOnKeyAndType()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key1");
		var entity3 = new TestEntity("key2");
		var differentTypeEntity = new DifferentTestEntity("key1");

		// Act & Assert Same key, same type
		entity1.Equals(entity2).ShouldBeTrue();

		// Different key, same type
		entity1.Equals(entity3).ShouldBeFalse();

		// Same key, different type
		entity1.Equals(differentTypeEntity).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenComparingWithNull()
	{
		// Arrange
		var entity = new TestEntity("key1");

		// Act & Assert
#pragma warning disable CA1508 // Avoid dead conditional code
		entity.Equals((object?)null).ShouldBeFalse();
#pragma warning restore CA1508 // Avoid dead conditional code
	}

	[Fact]
	public void ImplementEqualsObject()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key1");
		object object2 = entity2;

		// Act & Assert
		entity1.Equals(object2).ShouldBeTrue();
	}

	[Fact]
	public void GenerateConsistentHashCodes()
	{
		// Arrange
		var entity1 = new TestEntity("key1");
		var entity2 = new TestEntity("key1");
		var entity3 = new TestEntity("key2");

		// Act & Assert Same key = same hash code
		entity1.GetHashCode().ShouldBe(entity2.GetHashCode());

		// Different key = different hash code
		entity1.GetHashCode().ShouldNotBe(entity3.GetHashCode());
	}

	[Fact]
	public void ImplementIEntityInterface()
	{
		// Arrange
		var entity = new TestEntity("key1");

		// Act & Assert
		_ = entity.ShouldBeAssignableTo<IEntity>();
		_ = entity.ShouldBeAssignableTo<IEntity<string>>();
	}

	// Test concrete implementation of EntityBase
	private sealed class TestEntity(string key) : EntityBase<string>
	{
		public override string Key => EntityKey;
		private string EntityKey { get; } = key;
	}

	// Another implementation for testing type comparison
	private sealed class DifferentTestEntity(string key) : EntityBase<string>
	{
		public override string Key => EntityKey;
		private string EntityKey { get; } = key;
	}
}
