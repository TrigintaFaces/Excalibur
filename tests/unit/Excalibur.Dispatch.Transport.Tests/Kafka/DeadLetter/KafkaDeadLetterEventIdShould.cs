// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Verifies Kafka DLQ event IDs are within the assigned range and have no duplicates (S523.7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaDeadLetterEventIdShould
{
	[Theory]
	[InlineData(KafkaEventId.DlqMessageMoved, 22600)]
	[InlineData(KafkaEventId.DlqMoveFailed, 22601)]
	[InlineData(KafkaEventId.DlqMessagesRetrieved, 22602)]
	[InlineData(KafkaEventId.DlqRetrieveFailed, 22603)]
	[InlineData(KafkaEventId.DlqMessageReprocessed, 22604)]
	[InlineData(KafkaEventId.DlqReprocessFailed, 22605)]
	[InlineData(KafkaEventId.DlqStatisticsRetrieved, 22606)]
	[InlineData(KafkaEventId.DlqPurged, 22607)]
	[InlineData(KafkaEventId.DlqPurgeFailed, 22608)]
	[InlineData(KafkaEventId.DlqManagerInitialized, 22609)]
	[InlineData(KafkaEventId.DlqMessageSkipped, 22610)]
	[InlineData(KafkaEventId.DlqConsumerStarted, 22611)]
	[InlineData(KafkaEventId.DlqConsumerStopped, 22612)]
	[InlineData(KafkaEventId.DlqProducedToOriginalTopic, 22613)]
	[InlineData(KafkaEventId.DlqMessagesPeeked, 22614)]
	public void HaveCorrectEventIds(int actual, int expected)
	{
		actual.ShouldBe(expected);
	}

	[Fact]
	public void AllDlqEventIds_AreInAssignedRange()
	{
		// DLQ subcategory range: 22600-22699
		var dlqFields = typeof(KafkaEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.Name.StartsWith("Dlq", StringComparison.Ordinal))
			.Select(f => new { f.Name, Value = (int)f.GetValue(null)! })
			.ToList();

		dlqFields.ShouldNotBeEmpty();

		foreach (var field in dlqFields)
		{
			field.Value.ShouldBeInRange(22600, 22699,
				$"DLQ event ID '{field.Name}' ({field.Value}) should be in range 22600-22699");
		}
	}

	[Fact]
	public void AllDlqEventIds_AreUnique()
	{
		var dlqFields = typeof(KafkaEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.Name.StartsWith("Dlq", StringComparison.Ordinal))
			.Select(f => new { f.Name, Value = (int)f.GetValue(null)! })
			.ToList();

		var duplicates = dlqFields
			.GroupBy(f => f.Value)
			.Where(g => g.Count() > 1)
			.Select(g => $"ID {g.Key}: {string.Join(", ", g.Select(x => x.Name))}")
			.ToList();

		duplicates.ShouldBeEmpty($"Duplicate DLQ event IDs found: {string.Join("; ", duplicates)}");
	}
}
