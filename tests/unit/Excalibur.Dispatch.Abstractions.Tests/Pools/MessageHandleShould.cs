namespace Excalibur.Dispatch.Abstractions.Tests.Pools;

/// <summary>
/// Unit tests for MessageHandle struct.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageHandleShould : UnitTestBase
{
	[Fact]
	public void Constructor_WithNullPool_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MessageHandle<TestPoolMessage>(null!, new TestPoolMessage()));
	}

	[Fact]
	public void Constructor_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MessageHandle<TestPoolMessage>(pool, null!));
	}

	[Fact]
	public void Message_ReturnsProvidedMessage()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var message = new TestPoolMessage { Value = 42 };

		// Act
		var handle = new MessageHandle<TestPoolMessage>(pool, message);

		// Assert
		handle.Message.ShouldBe(message);
		handle.Message.Value.ShouldBe(42);
	}

	[Fact]
	public void Dispose_ReturnsMessageToPool()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var message = new TestPoolMessage();
		var handle = new MessageHandle<TestPoolMessage>(pool, message);

		// Act
		handle.Dispose();

		// Assert
		A.CallTo(() => pool.ReturnToPool(message)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Equals_SamePoolAndMessage_ReturnsTrue()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var message = new TestPoolMessage();
		var handle1 = new MessageHandle<TestPoolMessage>(pool, message);
		var handle2 = new MessageHandle<TestPoolMessage>(pool, message);

		// Act & Assert
		handle1.Equals(handle2).ShouldBeTrue();
		(handle1 == handle2).ShouldBeTrue();
		(handle1 != handle2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentMessage_ReturnsFalse()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var handle1 = new MessageHandle<TestPoolMessage>(pool, new TestPoolMessage());
		var handle2 = new MessageHandle<TestPoolMessage>(pool, new TestPoolMessage());

		// Act & Assert
		handle1.Equals(handle2).ShouldBeFalse();
		(handle1 == handle2).ShouldBeFalse();
		(handle1 != handle2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentPool_ReturnsFalse()
	{
		// Arrange
		var pool1 = A.Fake<IMessagePool>();
		var pool2 = A.Fake<IMessagePool>();
		var message = new TestPoolMessage();
		var handle1 = new MessageHandle<TestPoolMessage>(pool1, message);
		var handle2 = new MessageHandle<TestPoolMessage>(pool2, message);

		// Act & Assert
		handle1.Equals(handle2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_ObjectOverload_WithMatchingHandle_ReturnsTrue()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var message = new TestPoolMessage();
		var handle1 = new MessageHandle<TestPoolMessage>(pool, message);
		object handle2 = new MessageHandle<TestPoolMessage>(pool, message);

		// Act & Assert
		handle1.Equals(handle2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_ObjectOverload_WithNonHandle_ReturnsFalse()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var handle = new MessageHandle<TestPoolMessage>(pool, new TestPoolMessage());

		// Act & Assert
		handle.Equals("not a handle").ShouldBeFalse();
		handle.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SamePoolAndMessage_ReturnsSameHash()
	{
		// Arrange
		var pool = A.Fake<IMessagePool>();
		var message = new TestPoolMessage();
		var handle1 = new MessageHandle<TestPoolMessage>(pool, message);
		var handle2 = new MessageHandle<TestPoolMessage>(pool, message);

		// Act & Assert
		handle1.GetHashCode().ShouldBe(handle2.GetHashCode());
	}

	internal sealed class TestPoolMessage
	{
		public int Value { get; set; }
	}
}
