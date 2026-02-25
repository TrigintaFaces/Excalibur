// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.Tests.HealthChecks;

/// <summary>
/// Tests for the internal HealthReportJsonConverter class via reflection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HealthReportJsonConverterShould : UnitTestBase
{
	private static readonly Type ConverterType = typeof(Excalibur.Hosting.HealthChecksBuilderExtensions).Assembly
		.GetType("Excalibur.Hosting.HealthChecks.HealthReportJsonConverter")!;

	private static readonly Type EntryConverterType = typeof(Excalibur.Hosting.HealthChecksBuilderExtensions).Assembly
		.GetType("Excalibur.Hosting.HealthChecks.HealthReportEntryJsonConverter")!;

	[Fact]
	public void WriteHealthReportAsJson()
	{
		// Arrange
		var entries = new Dictionary<string, HealthReportEntry>
		{
			["test-check"] = new(
				status: HealthStatus.Healthy,
				description: "All good",
				duration: TimeSpan.FromMilliseconds(42),
				exception: null,
				data: new Dictionary<string, object> { ["key"] = "value" },
				tags: ["tag1"]),
		};
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var entryConverter = (JsonConverter)Activator.CreateInstance(EntryConverterType)!;
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};
		options.Converters.Add(converter);
		options.Converters.Add(entryConverter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(report, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("\"entries\"");
		json.ShouldContain("\"status\"");
		json.ShouldContain("\"totalDuration\"");
		json.ShouldContain("Healthy");
	}

	[Fact]
	public void WriteHealthReportWithDefaultNamingPolicy()
	{
		// Arrange
		var entries = new Dictionary<string, HealthReportEntry>();
		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(50));

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var entryConverter = (JsonConverter)Activator.CreateInstance(EntryConverterType)!;
		var options = new JsonSerializerOptions();
		options.Converters.Add(converter);
		options.Converters.Add(entryConverter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(report, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("\"Entries\"");
		json.ShouldContain("\"Status\"");
		json.ShouldContain("\"TotalDuration\"");
	}

	[Fact]
	public void WriteEmptyEntriesCollection()
	{
		// Arrange
		var entries = new Dictionary<string, HealthReportEntry>();
		var report = new HealthReport(entries, TimeSpan.Zero);

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var entryConverter = (JsonConverter)Activator.CreateInstance(EntryConverterType)!;
		var options = new JsonSerializerOptions();
		options.Converters.Add(converter);
		options.Converters.Add(entryConverter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(report, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("\"Entries\"");
		json.ShouldContain("\"Status\"");
	}
}
