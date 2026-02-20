// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Naming;

/// <summary>
/// Unit tests for the <see cref="RecordNameStrategy"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify RecordNameStrategy generates {namespace}.{typename} subjects.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class RecordNameStrategyShould
{
	private readonly RecordNameStrategy _strategy = new();

	#region GetValueSubject Tests

	[Fact]
	public void GetValueSubject_ReturnsFullTypeName()
	{
		// Act
		var subject = _strategy.GetValueSubject("orders", typeof(TestMessage));

		// Assert
		subject.ShouldBe("Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry.Naming.RecordNameStrategyShould+TestMessage");
	}

	[Fact]
	public void GetValueSubject_IgnoresTopic()
	{
		// Act - Different topics, same type should give same subject
		var subject1 = _strategy.GetValueSubject("orders", typeof(TestMessage));
		var subject2 = _strategy.GetValueSubject("events", typeof(TestMessage));
		var subject3 = _strategy.GetValueSubject("another-topic", typeof(TestMessage));

		// Assert
		subject1.ShouldBe(subject2);
		subject2.ShouldBe(subject3);
	}

	[Fact]
	public void GetValueSubject_HandlesBuiltInTypes()
	{
		// Act
		var subject = _strategy.GetValueSubject("topic", typeof(string));

		// Assert
		subject.ShouldBe("System.String");
	}

	[Fact]
	public void GetValueSubject_HandlesGenericTypes()
	{
		// Act
		var subject = _strategy.GetValueSubject("topic", typeof(List<string>));

		// Assert - Should strip the backtick notation
		subject.ShouldBe("System.Collections.Generic.List");
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
	public void GetKeySubject_ReturnsFullTypeName()
	{
		// Act
		var subject = _strategy.GetKeySubject("orders", typeof(Guid));

		// Assert
		subject.ShouldBe("System.Guid");
	}

	[Fact]
	public void GetKeySubject_IgnoresTopic()
	{
		// Act - Different topics, same type should give same subject
		var subject1 = _strategy.GetKeySubject("orders", typeof(int));
		var subject2 = _strategy.GetKeySubject("events", typeof(int));

		// Assert
		subject1.ShouldBe(subject2);
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
		subject.ShouldContain("+NestedMessage");
	}

	[Fact]
	public void ValueAndKeySubjects_AreSameForSameType()
	{
		// Act
		var valueSubject = _strategy.GetValueSubject("topic", typeof(string));
		var keySubject = _strategy.GetKeySubject("different-topic", typeof(string));

		// Assert
		valueSubject.ShouldBe(keySubject);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISubjectNameStrategy()
	{
		// Assert
		typeof(RecordNameStrategy).GetInterfaces()
			.ShouldContain(typeof(ISubjectNameStrategy));
	}

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(RecordNameStrategy).IsSealed.ShouldBeTrue();
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
