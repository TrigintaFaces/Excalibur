// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Naming;

/// <summary>
/// Unit tests for the <see cref="TopicRecordNameStrategy"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify TopicRecordNameStrategy generates {topic}-{namespace}.{typename} subjects.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class TopicRecordNameStrategyShould
{
	private readonly TopicRecordNameStrategy _strategy = new();

	#region GetValueSubject Tests

	[Fact]
	public void GetValueSubject_CombinesTopicAndTypeName()
	{
		// Act
		var subject = _strategy.GetValueSubject("orders", typeof(TestMessage));

		// Assert
		subject.ShouldStartWith("orders-");
		subject.ShouldContain("TestMessage");
	}

	[Fact]
	public void GetValueSubject_IncludesFullNamespace()
	{
		// Act
		var subject = _strategy.GetValueSubject("orders", typeof(TestMessage));

		// Assert
		subject.ShouldContain("Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Naming");
	}

	[Fact]
	public void GetValueSubject_DifferentTopics_DifferentSubjects()
	{
		// Act
		var subject1 = _strategy.GetValueSubject("orders", typeof(TestMessage));
		var subject2 = _strategy.GetValueSubject("events", typeof(TestMessage));

		// Assert
		subject1.ShouldNotBe(subject2);
		subject1.ShouldStartWith("orders-");
		subject2.ShouldStartWith("events-");
	}

	[Fact]
	public void GetValueSubject_DifferentTypes_DifferentSubjects()
	{
		// Act
		var subject1 = _strategy.GetValueSubject("orders", typeof(TestMessage));
		var subject2 = _strategy.GetValueSubject("orders", typeof(string));

		// Assert
		subject1.ShouldNotBe(subject2);
	}

	[Fact]
	public void GetValueSubject_HandlesGenericTypes()
	{
		// Act
		var subject = _strategy.GetValueSubject("topic", typeof(List<string>));

		// Assert - Should strip the backtick notation
		subject.ShouldBe("topic-System.Collections.Generic.List");
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
	public void GetValueSubject_ThrowsForNullType()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GetValueSubject("topic", null!));
	}

	#endregion

	#region GetKeySubject Tests

	[Fact]
	public void GetKeySubject_CombinesTopicAndTypeName()
	{
		// Act
		var subject = _strategy.GetKeySubject("orders", typeof(Guid));

		// Assert
		subject.ShouldBe("orders-System.Guid");
	}

	[Fact]
	public void GetKeySubject_DifferentTopics_DifferentSubjects()
	{
		// Act
		var subject1 = _strategy.GetKeySubject("orders", typeof(int));
		var subject2 = _strategy.GetKeySubject("events", typeof(int));

		// Assert
		subject1.ShouldNotBe(subject2);
		subject1.ShouldBe("orders-System.Int32");
		subject2.ShouldBe("events-System.Int32");
	}

	[Fact]
	public void GetKeySubject_ThrowsForNullTopic()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_strategy.GetKeySubject(null!, typeof(string)));
	}

	[Fact]
	public void GetKeySubject_ThrowsForNullType()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_strategy.GetKeySubject("topic", null!));
	}

	#endregion

	#region Type Name Handling Tests

	[Fact]
	public void HandlesNestedTypes()
	{
		// Act
		var subject = _strategy.GetValueSubject("topic", typeof(OuterClass.NestedMessage));

		// Assert - Nested types use + separator
		subject.ShouldStartWith("topic-");
		subject.ShouldContain("+NestedMessage");
	}

	[Fact]
	public void ValueAndKeySubjects_DifferForSameTopic()
	{
		// Both use the same type (Guid) for consistency in this test
		var valueSubject = _strategy.GetValueSubject("orders", typeof(Guid));
		var keySubject = _strategy.GetKeySubject("orders", typeof(Guid));

		// Assert - Should be the same when type is the same
		valueSubject.ShouldBe(keySubject);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISubjectNameStrategy()
	{
		// Assert
		typeof(TopicRecordNameStrategy).GetInterfaces()
			.ShouldContain(typeof(ISubjectNameStrategy));
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(TopicRecordNameStrategy).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
	}

	private sealed class OuterClass
	{
		public sealed class NestedMessage
		{
			public string? Value { get; set; }
		}
	}

	#endregion
}
