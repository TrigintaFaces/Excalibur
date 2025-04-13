using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Hosting.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting.HealthChecks;

public class HealthReportJsonConverterShould
{
	[Fact]
	public void SerializeHealthReportShouldContainExpectedProperties()
	{
		// Arrange
		var converter = new HealthReportJsonConverter();
		var entries = new Dictionary<string, HealthReportEntry>
		{
			{ "Check1", new HealthReportEntry(HealthStatus.Healthy, "OK", TimeSpan.FromMilliseconds(100), null, null) }
		};

		var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { converter, new HealthReportEntryJsonConverter() }
		};

		// Act
		var json = JsonSerializer.Serialize(report, options);

		// Assert
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		root.GetProperty("status").GetString().ShouldBe("Healthy");
		root.GetProperty("totalDuration").GetString().ShouldBe("00:00:00.1000000");

		var entry = root.GetProperty("entries").GetProperty("Check1");
		entry.GetProperty("description").GetString().ShouldBe("OK");
	}

	[Fact]
	public void SerializeWithCamelCaseNamingPolicy()
	{
		// Arrange
		var converter = new HealthReportJsonConverter();
		var entries = new Dictionary<string, HealthReportEntry>
		{
			{
				"Test", new HealthReportEntry(
					HealthStatus.Healthy,
					"Test is healthy",
					TimeSpan.FromMilliseconds(10),
					null,
					null)
			}
		};

		var healthReport = new HealthReport(entries, TimeSpan.FromMilliseconds(10));
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { converter, new HealthReportEntryJsonConverter() }
		};

		// Act
		var json = JsonSerializer.Serialize(healthReport, options);

		// Assert
		json.ShouldContain("\"entries\":");
		json.ShouldContain("\"status\":\"Healthy\"");
		json.ShouldContain("\"totalDuration\":");
	}

	[Fact]
	public void ThrowJsonExceptionWhenDeserializing()
	{
		// Arrange
		var converter = new HealthReportJsonConverter();
		var json = @"{""status"":""Healthy"",""entries"":{},""totalDuration"":""00:00:00.1000000""}";
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { converter } };

		using var jsonUtf8 = new MemoryStream(Encoding.UTF8.GetBytes(json));

		// Act & Assert
		Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<HealthReport>(jsonUtf8, options));
	}

	[Fact]
	public void ThrowTargetInvocationExceptionWhenWriterIsNull()
	{
		// Arrange
		var converter = new HealthReportJsonConverter();
		var healthReport = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

		// Act & Assert
		_ = Should.Throw<TargetInvocationException>(() =>
		{
			// Using reflection to call the internal Write method with null writer
			var method = typeof(HealthReportJsonConverter).GetMethod("Write",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

			_ = method.Invoke(converter,
				[null, healthReport, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, }]);
		});
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenValueIsNull()
	{
		// Arrange
		var converter = new HealthReportJsonConverter();
		var options = new JsonSerializerOptions();
		var stream = new MemoryStream();
		using var writer = new Utf8JsonWriter(stream);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
		{
			converter.Write(writer, null, options);
		});
	}
}
