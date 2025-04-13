using Excalibur.Domain;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class IActivityContextShould
{
	[Fact]
	public void SupportGenericGetOperation()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var key = "TestKey";
		var defaultValue = "DefaultValue";
		var expectedValue = "TestValue";

		_ = A.CallTo(() => context.Get(key, defaultValue)).Returns(expectedValue);

		// Act
		var result = context.Get(key, defaultValue);

		// Assert
		result.ShouldBe(expectedValue);
		_ = A.CallTo(() => context.Get(key, defaultValue)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportGenericSetOperation()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var key = "TestKey";
		var value = "TestValue";

		// Act
		context.Set(key, value);

		// Assert
		_ = A.CallTo(() => context.Set(key, value)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportContainsKeyOperation()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var existingKey = "ExistingKey";
		var nonExistingKey = "NonExistingKey";

		_ = A.CallTo(() => context.ContainsKey(existingKey)).Returns(true);
		_ = A.CallTo(() => context.ContainsKey(nonExistingKey)).Returns(false);

		// Act & Assert
		context.ContainsKey(existingKey).ShouldBeTrue();
		context.ContainsKey(nonExistingKey).ShouldBeFalse();
	}

	[Fact]
	public void SupportRemoveOperation()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var key = "TestKey";

		// Act
		context.Remove(key);

		// Assert
		_ = A.CallTo(() => context.Remove(key)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnDefaultValueWhenKeyNotFound()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var key = "NonExistingKey";
		var defaultValue = "DefaultValue";

		_ = A.CallTo(() => context.Get(key, defaultValue)).Returns(defaultValue);

		// Act
		var result = context.Get(key, defaultValue);

		// Assert
		result.ShouldBe(defaultValue);
	}

	[Fact]
	public void BeUsableWithDifferentGenericTypes()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var stringKey = "StringKey";
		var intKey = "IntKey";
		var guidKey = "GuidKey";
		var stringValue = "StringValue";
		var intValue = 42;
		var guidValue = Guid.NewGuid();

		_ = A.CallTo(() => context.Get(stringKey, A<string>._)).Returns(stringValue);
		_ = A.CallTo(() => context.Get(intKey, A<int>._)).Returns(intValue);
		_ = A.CallTo(() => context.Get(guidKey, A<Guid>._)).Returns(guidValue);

		// Act & Assert
		context.Get(stringKey, string.Empty).ShouldBe(stringValue);
		context.Get(intKey, 0).ShouldBe(intValue);
		context.Get(guidKey, Guid.Empty).ShouldBe(guidValue);
	}
}
