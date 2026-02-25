// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Verifies that DLQ event IDs for each transport are within their assigned ranges.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqEventIdRangeShould
{
	[Fact]
	public void AzureServiceBus_DlqEventIds_InCorrectRange()
	{
		// Arrange — Azure SB DLQ should use 24950-24969
		var assembly = Assembly.Load("Excalibur.Dispatch.Transport.AzureServiceBus");
		var eventIdType = assembly.GetType("Excalibur.Dispatch.Transport.AzureServiceBus.AzureServiceBusEventId");
		eventIdType.ShouldNotBeNull("AzureServiceBusEventId type should exist");

		// Act — get all DLQ event IDs
		var dlqFields = eventIdType.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.Name.StartsWith("Dlq", StringComparison.Ordinal) && f.FieldType == typeof(int))
			.Select(f => new { f.Name, Value = (int)f.GetValue(null)! })
			.ToList();

		// Assert
		dlqFields.Count.ShouldBeGreaterThan(0, "Should have DLQ event ID constants");

		foreach (var field in dlqFields)
		{
			field.Value.ShouldBeInRange(24950, 24969,
				$"Azure SB DLQ event ID '{field.Name}' = {field.Value} is outside range 24950-24969");
		}
	}

	[Fact]
	public void RabbitMq_DlqEventIds_InCorrectRange()
	{
		// Arrange — RabbitMQ DLQ should use 21500-21519
		var assembly = Assembly.Load("Excalibur.Dispatch.Transport.RabbitMQ");
		var eventIdType = assembly.GetType("Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqEventId");
		eventIdType.ShouldNotBeNull("RabbitMqEventId type should exist");

		// Act
		var dlqFields = eventIdType.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.Name.StartsWith("Dlq", StringComparison.Ordinal) && f.FieldType == typeof(int))
			.Select(f => new { f.Name, Value = (int)f.GetValue(null)! })
			.ToList();

		// Assert
		dlqFields.Count.ShouldBeGreaterThan(0, "Should have DLQ event ID constants");

		foreach (var field in dlqFields)
		{
			field.Value.ShouldBeInRange(21500, 21519,
				$"RabbitMQ DLQ event ID '{field.Name}' = {field.Value} is outside range 21500-21519");
		}
	}
}
