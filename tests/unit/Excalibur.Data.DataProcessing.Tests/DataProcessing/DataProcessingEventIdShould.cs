// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DataProcessing.Diagnostics;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessingEventId"/> constants.
/// </summary>
[UnitTest]
public sealed class DataProcessingEventIdShould : UnitTestBase
{
	[Fact]
	public void HaveAllValuesInExpectedRange()
	{
		// Arrange
		var fields = typeof(DataProcessingEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
			.Where(f => f.FieldType == typeof(int));

		// Act & Assert
		foreach (var field in fields)
		{
			var value = (int)field.GetValue(null)!;
			value.ShouldBeInRange(107000, 107999,
				$"EventId '{field.Name}' ({value}) should be in range 107000-107999");
		}
	}

	[Fact]
	public void HaveUniqueValues()
	{
		// Arrange
		var fields = typeof(DataProcessingEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
			.Where(f => f.FieldType == typeof(int))
			.Select(f => (Name: f.Name, Value: (int)f.GetValue(null)!))
			.ToList();

		// Act
		var duplicates = fields
			.GroupBy(f => f.Value)
			.Where(g => g.Count() > 1)
			.Select(g => $"Value {g.Key}: {string.Join(", ", g.Select(f => f.Name))}")
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty($"Duplicate EventId values found: {string.Join("; ", duplicates)}");
	}

	[Fact]
	public void HaveExpectedOrchestrationManagerIds()
	{
		DataProcessingEventId.ProcessorNotFound.ShouldBe(107000);
		DataProcessingEventId.ProcessingDataTaskError.ShouldBe(107001);
		DataProcessingEventId.UpdateCompletedCountMismatch.ShouldBe(107002);
	}

	[Fact]
	public void HaveExpectedProcessorCoreIds()
	{
		DataProcessingEventId.DisposeAsync.ShouldBe(107100);
		DataProcessingEventId.ConsumerNotCompletedAsync.ShouldBe(107101);
		DataProcessingEventId.ConsumerTimeoutAsync.ShouldBe(107102);
		DataProcessingEventId.DisposeAsyncError.ShouldBe(107103);
		DataProcessingEventId.DisposeSync.ShouldBe(107104);
		DataProcessingEventId.ConsumerNotCompletedSync.ShouldBe(107105);
		DataProcessingEventId.ConsumerTimeoutSync.ShouldBe(107106);
		DataProcessingEventId.DisposeError.ShouldBe(107107);
	}

	[Fact]
	public void HaveExpectedConsumerIds()
	{
		DataProcessingEventId.ConsumerDisposalRequested.ShouldBe(107200);
		DataProcessingEventId.NoMoreRecordsConsumerExit.ShouldBe(107201);
		DataProcessingEventId.QueueEmptyWaiting.ShouldBe(107202);
		DataProcessingEventId.ProcessingBatch.ShouldBe(107203);
		DataProcessingEventId.ProcessingRecordError.ShouldBe(107204);
		DataProcessingEventId.CompletedBatch.ShouldBe(107205);
		DataProcessingEventId.CompletedProcessing.ShouldBe(107206);
		DataProcessingEventId.ConsumerCanceled.ShouldBe(107207);
		DataProcessingEventId.ConsumerError.ShouldBe(107208);
		DataProcessingEventId.NoHandlerFound.ShouldBe(107209);
	}

	[Fact]
	public void HaveExpectedProducerIds()
	{
		DataProcessingEventId.NoMoreRecordsProducerExit.ShouldBe(107300);
		DataProcessingEventId.EnqueuingRecords.ShouldBe(107301);
		DataProcessingEventId.SuccessfullyEnqueued.ShouldBe(107302);
		DataProcessingEventId.ProducerCanceled.ShouldBe(107303);
		DataProcessingEventId.ProducerError.ShouldBe(107304);
		DataProcessingEventId.ProducerCompleted.ShouldBe(107305);
		DataProcessingEventId.ApplicationStopping.ShouldBe(107306);
		DataProcessingEventId.ProducerCancellationRequested.ShouldBe(107307);
		DataProcessingEventId.WaitingForConsumer.ShouldBe(107308);
	}
}
