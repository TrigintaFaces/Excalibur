// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.Tests.HealthChecks;

/// <summary>
/// Tests for the internal HealthReportEntryJsonConverter class via reflection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HealthReportEntryJsonConverterShould : UnitTestBase
{
	private static readonly Type ConverterType = typeof(Excalibur.Hosting.HealthChecksBuilderExtensions).Assembly
		.GetType("Excalibur.Hosting.HealthChecks.HealthReportEntryJsonConverter")!;

	[Fact]
	public void WriteEntryWithAllFields()
	{
		// Arrange
		var entry = new HealthReportEntry(
			status: HealthStatus.Healthy,
			description: "OK",
			duration: TimeSpan.FromMilliseconds(10),
			exception: null,
			data: new Dictionary<string, object> { ["metric"] = 42 },
			tags: ["db", "core"]);

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};
		options.Converters.Add(converter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(entry, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("\"data\"");
		json.ShouldContain("\"description\"");
		json.ShouldContain("\"duration\"");
		json.ShouldContain("\"status\"");
		json.ShouldContain("\"tags\"");
		json.ShouldContain("\"exception\"");
		json.ShouldContain("OK");
		json.ShouldContain("Healthy");
	}

	[Fact]
	public void WriteEntryWithException()
	{
		// Arrange
		var ex = new InvalidOperationException("Something went wrong");
		var entry = new HealthReportEntry(
			status: HealthStatus.Unhealthy,
			description: null,
			duration: TimeSpan.FromSeconds(1),
			exception: ex,
			data: null,
			tags: null);

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var options = new JsonSerializerOptions();
		options.Converters.Add(converter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(entry, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("Something went wrong");
		json.ShouldContain("InvalidOperationException");
	}

	[Fact]
	public void WriteEntryWithNullDescription()
	{
		// Arrange
		var entry = new HealthReportEntry(
			status: HealthStatus.Degraded,
			description: null,
			duration: TimeSpan.Zero,
			exception: null,
			data: null,
			tags: null);

		var converter = (JsonConverter)Activator.CreateInstance(ConverterType)!;
		var options = new JsonSerializerOptions();
		options.Converters.Add(converter);

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var json = JsonSerializer.Serialize(entry, options);
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		json.ShouldContain("\"Description\"");
	}
}
