// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Naming;

/// <summary>
/// Unit tests for the <see cref="TopicNameStrategy"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify TopicNameStrategy generates {topic}-value and {topic}-key subjects.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class TopicNameStrategyShould
{
	private readonly TopicNameStrategy _strategy = new();

	#region GetValueSubject Tests

	[Fact]
	public void GetValueSubject_ReturnsTopicDashValue()
	{
		// Act
		var subject = _strategy.GetValueSubject("orders", typeof(TestMessage));

		// Assert
		subject.ShouldBe("orders-value");
	}

	[Theory]
	[InlineData("my-topic", "my-topic-value")]
	[InlineData("events.user.created", "events.user.created-value")]
	[InlineData("a", "a-value")]
	public void GetValueSubject_FormatsTopicCorrectly(string topic, string expected)
	{
		// Act
		var subject = _strategy.GetValueSubject(topic, typeof(TestMessage));

		// Assert
		subject.ShouldBe(expected);
	}

	[Fact]
	public void GetValueSubject_IgnoresMessageType()
	{
		// Act - Same topic, different types should give same subject
		var subject1 = _strategy.GetValueSubject("orders", typeof(TestMessage));
		var subject2 = _strategy.GetValueSubject("orders", typeof(string));
		var subject3 = _strategy.GetValueSubject("orders", typeof(int));

		// Assert
		subject1.ShouldBe(subject2);
		subject2.ShouldBe(subject3);
	}

	[Fact]
	public void GetValueSubject_ThrowsForNullTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetValueSubject(null!, typeof(TestMessage)));
	}

	[Fact]
	public void GetValueSubject_ThrowsForEmptyTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetValueSubject(string.Empty, typeof(TestMessage)));
	}

	[Fact]
	public void GetValueSubject_ThrowsForWhitespaceTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetValueSubject("   ", typeof(TestMessage)));
	}

	#endregion

	#region GetKeySubject Tests

	[Fact]
	public void GetKeySubject_ReturnsTopicDashKey()
	{
		// Act
		var subject = _strategy.GetKeySubject("orders", typeof(string));

		// Assert
		subject.ShouldBe("orders-key");
	}

	[Theory]
	[InlineData("my-topic", "my-topic-key")]
	[InlineData("events.user.created", "events.user.created-key")]
	[InlineData("a", "a-key")]
	public void GetKeySubject_FormatsTopicCorrectly(string topic, string expected)
	{
		// Act
		var subject = _strategy.GetKeySubject(topic, typeof(string));

		// Assert
		subject.ShouldBe(expected);
	}

	[Fact]
	public void GetKeySubject_IgnoresKeyType()
	{
		// Act - Same topic, different key types should give same subject
		var subject1 = _strategy.GetKeySubject("orders", typeof(string));
		var subject2 = _strategy.GetKeySubject("orders", typeof(Guid));
		var subject3 = _strategy.GetKeySubject("orders", typeof(int));

		// Assert
		subject1.ShouldBe(subject2);
		subject2.ShouldBe(subject3);
	}

	[Fact]
	public void GetKeySubject_ThrowsForNullTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetKeySubject(null!, typeof(string)));
	}

	[Fact]
	public void GetKeySubject_ThrowsForEmptyTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetKeySubject(string.Empty, typeof(string)));
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISubjectNameStrategy()
	{
		// Assert
		typeof(TopicNameStrategy).GetInterfaces()
			.ShouldContain(typeof(ISubjectNameStrategy));
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(TopicNameStrategy).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
	}

	#endregion
}
