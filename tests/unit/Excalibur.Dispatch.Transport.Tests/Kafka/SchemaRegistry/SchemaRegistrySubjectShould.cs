// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SchemaRegistrySubject"/> static class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify subject naming helpers for TopicNameStrategy.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SchemaRegistrySubjectShould
{
	#region Constant Tests

	[Fact]
	public void DefineValueSuffixCorrectly()
	{
		// Assert
		SchemaRegistrySubject.ValueSuffix.ShouldBe("-value");
	}

	[Fact]
	public void DefineKeySuffixCorrectly()
	{
		// Assert
		SchemaRegistrySubject.KeySuffix.ShouldBe("-key");
	}

	#endregion

	#region ForValue Tests

	[Fact]
	public void ForValue_ReturnsCorrectSubject()
	{
		// Act
		var result = SchemaRegistrySubject.ForValue("orders");

		// Assert
		result.ShouldBe("orders-value");
	}

	[Theory]
	[InlineData("my-topic", "my-topic-value")]
	[InlineData("events.user.created", "events.user.created-value")]
	[InlineData("a", "a-value")]
	public void ForValue_FormatsTopicCorrectly(string topic, string expected)
	{
		// Act
		var result = SchemaRegistrySubject.ForValue(topic);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ForValue_ThrowsForNullTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForValue(null!));
	}

	[Fact]
	public void ForValue_ThrowsForEmptyTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForValue(string.Empty));
	}

	[Fact]
	public void ForValue_ThrowsForWhitespaceTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForValue("   "));
	}

	#endregion

	#region ForKey Tests

	[Fact]
	public void ForKey_ReturnsCorrectSubject()
	{
		// Act
		var result = SchemaRegistrySubject.ForKey("orders");

		// Assert
		result.ShouldBe("orders-key");
	}

	[Theory]
	[InlineData("my-topic", "my-topic-key")]
	[InlineData("events.user.created", "events.user.created-key")]
	[InlineData("a", "a-key")]
	public void ForKey_FormatsTopicCorrectly(string topic, string expected)
	{
		// Act
		var result = SchemaRegistrySubject.ForKey(topic);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ForKey_ThrowsForNullTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForKey(null!));
	}

	[Fact]
	public void ForKey_ThrowsForEmptyTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForKey(string.Empty));
	}

	[Fact]
	public void ForKey_ThrowsForWhitespaceTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ForKey("   "));
	}

	#endregion

	#region ExtractTopicName Tests

	[Theory]
	[InlineData("orders-value", "orders")]
	[InlineData("my-topic-value", "my-topic")]
	[InlineData("events.user.created-value", "events.user.created")]
	public void ExtractTopicName_RemovesValueSuffix(string subject, string expected)
	{
		// Act
		var result = SchemaRegistrySubject.ExtractTopicName(subject);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("orders-key", "orders")]
	[InlineData("my-topic-key", "my-topic")]
	[InlineData("events.user.created-key", "events.user.created")]
	public void ExtractTopicName_RemovesKeySuffix(string subject, string expected)
	{
		// Act
		var result = SchemaRegistrySubject.ExtractTopicName(subject);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("orders")]
	[InlineData("my-topic")]
	[InlineData("events.user.created")]
	public void ExtractTopicName_ReturnsOriginalForNonMatchingSubject(string subject)
	{
		// Act
		var result = SchemaRegistrySubject.ExtractTopicName(subject);

		// Assert
		result.ShouldBe(subject);
	}

	[Fact]
	public void ExtractTopicName_ThrowsForNullSubject()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ExtractTopicName(null!));
	}

	[Fact]
	public void ExtractTopicName_ThrowsForEmptySubject()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.ExtractTopicName(string.Empty));
	}

	#endregion

	#region IsValueSubject Tests

	[Theory]
	[InlineData("orders-value")]
	[InlineData("my-topic-value")]
	[InlineData("a-value")]
	public void IsValueSubject_ReturnsTrueForValueSubjects(string subject)
	{
		// Act
		var result = SchemaRegistrySubject.IsValueSubject(subject);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData("orders-key")]
	[InlineData("orders")]
	[InlineData("value")]
	public void IsValueSubject_ReturnsFalseForNonValueSubjects(string subject)
	{
		// Act
		var result = SchemaRegistrySubject.IsValueSubject(subject);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsValueSubject_ThrowsForNullSubject()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.IsValueSubject(null!));
	}

	#endregion

	#region IsKeySubject Tests

	[Theory]
	[InlineData("orders-key")]
	[InlineData("my-topic-key")]
	[InlineData("a-key")]
	public void IsKeySubject_ReturnsTrueForKeySubjects(string subject)
	{
		// Act
		var result = SchemaRegistrySubject.IsKeySubject(subject);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData("orders-value")]
	[InlineData("orders")]
	[InlineData("key")]
	public void IsKeySubject_ReturnsFalseForNonKeySubjects(string subject)
	{
		// Act
		var result = SchemaRegistrySubject.IsKeySubject(subject);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsKeySubject_ThrowsForNullSubject()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => SchemaRegistrySubject.IsKeySubject(null!));
	}

	#endregion

	#region Round-Trip Tests

	[Theory]
	[InlineData("orders")]
	[InlineData("my-topic")]
	[InlineData("events.user.created")]
	public void ForValue_AndExtract_RoundTripsCorrectly(string topic)
	{
		// Act
		var subject = SchemaRegistrySubject.ForValue(topic);
		var extracted = SchemaRegistrySubject.ExtractTopicName(subject);

		// Assert
		extracted.ShouldBe(topic);
		SchemaRegistrySubject.IsValueSubject(subject).ShouldBeTrue();
	}

	[Theory]
	[InlineData("orders")]
	[InlineData("my-topic")]
	[InlineData("events.user.created")]
	public void ForKey_AndExtract_RoundTripsCorrectly(string topic)
	{
		// Act
		var subject = SchemaRegistrySubject.ForKey(topic);
		var extracted = SchemaRegistrySubject.ExtractTopicName(subject);

		// Assert
		extracted.ShouldBe(topic);
		SchemaRegistrySubject.IsKeySubject(subject).ShouldBeTrue();
	}

	#endregion

	#region Mutual Exclusivity Tests

	[Fact]
	public void ValueSubject_IsNotKeySubject()
	{
		// Arrange
		var subject = SchemaRegistrySubject.ForValue("orders");

		// Assert
		SchemaRegistrySubject.IsValueSubject(subject).ShouldBeTrue();
		SchemaRegistrySubject.IsKeySubject(subject).ShouldBeFalse();
	}

	[Fact]
	public void KeySubject_IsNotValueSubject()
	{
		// Arrange
		var subject = SchemaRegistrySubject.ForKey("orders");

		// Assert
		SchemaRegistrySubject.IsKeySubject(subject).ShouldBeTrue();
		SchemaRegistrySubject.IsValueSubject(subject).ShouldBeFalse();
	}

	#endregion
}
