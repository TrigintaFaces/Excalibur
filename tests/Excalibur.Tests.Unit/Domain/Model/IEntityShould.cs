using Excalibur.Domain.Model;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain.Model;

public class IEntityShould
{
	[Fact]
	public void ProvideAccessToKey()
	{
		// Arrange
		IEntity<string> entity = new TestEntity("test-key");

		// Act
		var key = entity.Key;

		// Assert
		key.ShouldBe("test-key");
	}

	[Fact]
	public void BeMarkerInterfaceWhenNotGeneric()
	{
		// Arrange
		var entity = new TestEntity("test-key");

		// Act & Assert
		_ = entity.ShouldBeAssignableTo<IEntity>();
	}

	[Fact]
	public void SupportDifferentKeyTypes()
	{
		// Arrange
		IEntity<string> stringKeyEntity = new TestEntity("string-key");
		IEntity<int> intKeyEntity = new TestIntKeyEntity(42);
		IEntity<Guid> guidKeyEntity = new TestGuidKeyEntity(Guid.NewGuid());

		// Act & Assert
		_ = stringKeyEntity.Key.ShouldBeOfType<string>();
		_ = intKeyEntity.Key.ShouldBeOfType<int>();
		_ = guidKeyEntity.Key.ShouldBeOfType<Guid>();
	}

	[Fact]
	public void AllowGenericTypeToInheritFromNonGeneric()
	{
		// Arrange
		var entity = new TestEntity("test-key");

		// Act & Assert
		_ = entity.ShouldBeAssignableTo<IEntity>();
		_ = entity.ShouldBeAssignableTo<IEntity<string>>();
	}

	// Test Entity implementations with different key types
	private sealed class TestEntity(string key) : IEntity<string>
	{
		public string Key { get; } = key;
	}

	private sealed class TestIntKeyEntity(int key) : IEntity<int>
	{
		public int Key { get; } = key;
	}

	private sealed class TestGuidKeyEntity(Guid key) : IEntity<Guid>
	{
		public Guid Key { get; } = key;
	}
}
