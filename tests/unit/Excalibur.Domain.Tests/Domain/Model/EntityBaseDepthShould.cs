// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Depth coverage tests for <see cref="EntityBase{TKey}"/> and <see cref="EntityBase"/>.
/// Covers equality, hashing, different key types, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class EntityBaseDepthShould
{
	[Fact]
	public void Equals_ReturnsFalse_WhenOtherIsNull()
	{
		// Arrange
		var entity = new TestStringEntity("key-1");

		// Act & Assert
		entity.Equals((IEntity<string>?)null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsTrue_WhenSameTypeAndKey()
	{
		// Arrange
		var e1 = new TestStringEntity("key-1");
		var e2 = new TestStringEntity("key-1");

		// Act & Assert
		e1.Equals(e2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenDifferentKey()
	{
		// Arrange
		var e1 = new TestStringEntity("key-1");
		var e2 = new TestStringEntity("key-2");

		// Act & Assert
		e1.Equals(e2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ReturnsFalse_WhenDifferentType()
	{
		// Arrange
		var e1 = new TestStringEntity("key-1");
		var e2 = new AnotherStringEntity("key-1");

		// Act & Assert
		e1.Equals((object)e2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_ReturnsFalse_WhenNotEntity()
	{
		// Arrange
		var entity = new TestStringEntity("key-1");

		// Act & Assert
		entity.Equals("not-an-entity").ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_ReturnsFalse_WhenNull()
	{
		// Arrange
		var entity = new TestStringEntity("key-1");

		// Act & Assert
		entity.Equals((object?)null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SameForEqualEntities()
	{
		// Arrange
		var e1 = new TestStringEntity("key-1");
		var e2 = new TestStringEntity("key-1");

		// Act & Assert
		e1.GetHashCode().ShouldBe(e2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DiffersForDifferentKeys()
	{
		// Arrange
		var e1 = new TestStringEntity("key-1");
		var e2 = new TestStringEntity("key-2");

		// Act & Assert
		e1.GetHashCode().ShouldNotBe(e2.GetHashCode());
	}

	[Fact]
	public void StringEntity_InheritsFromEntityBaseOfString()
	{
		// Arrange & Act
		var entity = new TestDefaultEntity("k");

		// Assert
		entity.ShouldBeAssignableTo<EntityBase<string>>();
		entity.ShouldBeAssignableTo<EntityBase>();
	}

	[Fact]
	public void GuidKeyEntity_WorksCorrectly()
	{
		// Arrange
		var key = Guid.NewGuid();
		var e1 = new TestGuidEntity(key);
		var e2 = new TestGuidEntity(key);

		// Act & Assert
		e1.Equals(e2).ShouldBeTrue();
		e1.GetHashCode().ShouldBe(e2.GetHashCode());
	}

	[Fact]
	public void IntKeyEntity_WorksCorrectly()
	{
		// Arrange
		var e1 = new TestIntEntity(42);
		var e2 = new TestIntEntity(42);
		var e3 = new TestIntEntity(99);

		// Act & Assert
		e1.Equals(e2).ShouldBeTrue();
		e1.Equals(e3).ShouldBeFalse();
	}

	private sealed class TestStringEntity : EntityBase<string>
	{
		private readonly string _key;
		public TestStringEntity(string key) => _key = key;
		public override string Key => _key;
	}

	private sealed class AnotherStringEntity : EntityBase<string>
	{
		private readonly string _key;
		public AnotherStringEntity(string key) => _key = key;
		public override string Key => _key;
	}

	private sealed class TestDefaultEntity : EntityBase
	{
		private readonly string _key;
		public TestDefaultEntity(string key) => _key = key;
		public override string Key => _key;
	}

	private sealed class TestGuidEntity : EntityBase<Guid>
	{
		private readonly Guid _key;
		public TestGuidEntity(Guid key) => _key = key;
		public override Guid Key => _key;
	}

	private sealed class TestIntEntity : EntityBase<int>
	{
		private readonly int _key;
		public TestIntEntity(int key) => _key = key;
		public override int Key => _key;
	}
}
