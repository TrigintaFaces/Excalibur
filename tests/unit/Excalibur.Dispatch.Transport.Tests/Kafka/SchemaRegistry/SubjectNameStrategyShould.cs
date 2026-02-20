// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="SubjectNameStrategy"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify enum values and the ToStrategy() extension method.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class SubjectNameStrategyShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineTopicNameAsZero()
	{
		// Assert
		((int)SubjectNameStrategy.TopicName).ShouldBe(0);
	}

	[Fact]
	public void DefineRecordNameAsOne()
	{
		// Assert
		((int)SubjectNameStrategy.RecordName).ShouldBe(1);
	}

	[Fact]
	public void DefineTopicRecordNameAsTwo()
	{
		// Assert
		((int)SubjectNameStrategy.TopicRecordName).ShouldBe(2);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveThreeDefinedValues()
	{
		// Act
		var values = Enum.GetValues<SubjectNameStrategy>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void ContainAllExpectedStrategies()
	{
		// Act
		var values = Enum.GetValues<SubjectNameStrategy>();

		// Assert
		values.ShouldContain(SubjectNameStrategy.TopicName);
		values.ShouldContain(SubjectNameStrategy.RecordName);
		values.ShouldContain(SubjectNameStrategy.TopicRecordName);
	}

	#endregion

	#region ToStrategy Extension Tests

	[Fact]
	public void ToStrategy_ReturnsTopicNameStrategy_ForTopicName()
	{
		// Act
		var result = SubjectNameStrategy.TopicName.ToStrategy();

		// Assert
		result.ShouldBeOfType<TopicNameStrategy>();
	}

	[Fact]
	public void ToStrategy_ReturnsRecordNameStrategy_ForRecordName()
	{
		// Act
		var result = SubjectNameStrategy.RecordName.ToStrategy();

		// Assert
		result.ShouldBeOfType<RecordNameStrategy>();
	}

	[Fact]
	public void ToStrategy_ReturnsTopicRecordNameStrategy_ForTopicRecordName()
	{
		// Act
		var result = SubjectNameStrategy.TopicRecordName.ToStrategy();

		// Assert
		result.ShouldBeOfType<TopicRecordNameStrategy>();
	}

	[Fact]
	public void ToStrategy_ThrowsForInvalidValue()
	{
		// Arrange
		var invalidStrategy = (SubjectNameStrategy)999;

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => invalidStrategy.ToStrategy());
	}

	#endregion

	#region Enum Parse Tests

	[Theory]
	[InlineData("TopicName", SubjectNameStrategy.TopicName)]
	[InlineData("RecordName", SubjectNameStrategy.RecordName)]
	[InlineData("TopicRecordName", SubjectNameStrategy.TopicRecordName)]
	public void ParseFromString_WithValidName(string name, SubjectNameStrategy expected)
	{
		// Act
		var result = Enum.Parse<SubjectNameStrategy>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("topicname", SubjectNameStrategy.TopicName)]
	[InlineData("RECORDNAME", SubjectNameStrategy.RecordName)]
	[InlineData("topicrecordname", SubjectNameStrategy.TopicRecordName)]
	public void ParseFromString_WithCaseInsensitiveMatch(string name, SubjectNameStrategy expected)
	{
		// Act
		var result = Enum.Parse<SubjectNameStrategy>(name, ignoreCase: true);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Enum Name Tests

	[Fact]
	public void HaveCorrectNameForTopicName()
	{
		// Assert
		SubjectNameStrategy.TopicName.ToString().ShouldBe("TopicName");
	}

	[Fact]
	public void HaveCorrectNameForRecordName()
	{
		// Assert
		SubjectNameStrategy.RecordName.ToString().ShouldBe("RecordName");
	}

	[Fact]
	public void HaveCorrectNameForTopicRecordName()
	{
		// Assert
		SubjectNameStrategy.TopicRecordName.ToString().ShouldBe("TopicRecordName");
	}

	#endregion
}
