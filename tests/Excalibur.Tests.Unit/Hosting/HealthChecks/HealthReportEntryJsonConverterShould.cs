using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Hosting.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting.HealthChecks;

public class HealthReportEntryJsonConverterShould
{
	[Fact]
	public void SerializeHealthReportEntryCorrectly()
	{
		// Arrange
		var converter = new HealthReportEntryJsonConverter();
		var data = new Dictionary<string, object> { { "connectionString", "Server=localhost;Database=TestDb" }, { "responseTime", 42 } };
		var tags = new[] { "database", "critical" };
		var entry = new HealthReportEntry(
			HealthStatus.Healthy,
			"Database connection is working",
			TimeSpan.FromMilliseconds(50),
			null,
			data,
			tags);

		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { converter }
		};

		// Act
		var json = JsonSerializer.Serialize(entry, options);

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("Database connection is working");
		json.ShouldContain("00:00:00.0500000");
		json.ShouldContain("Healthy");
		json.ShouldContain("database");
		json.ShouldContain("critical");
		json.ShouldContain("connectionString");
		json.ShouldContain("responseTime");
	}

	[Fact]
	public void SerializeWithExceptionDetails()
	{
		// Arrange
		var converter = new HealthReportEntryJsonConverter();
		var exception = new InvalidOperationException("Connection failed");

		var entry = new HealthReportEntry(
			HealthStatus.Unhealthy,
			null,
			TimeSpan.FromMilliseconds(30),
			exception,
			null);

		var options = new JsonSerializerOptions { Converters = { converter } };

		// Act
		var json = JsonSerializer.Serialize(entry, options);

		// Assert
		json.ShouldContain("\"Exception\":\"Connection failed\"");
		json.ShouldContain("\"Description\":\"InvalidOperationException\"");
	}

	[Fact]
	public void SerializeWithCamelCaseNamingPolicy()
	{
		// Arrange
		var converter = new HealthReportEntryJsonConverter();
		var entry = new HealthReportEntry(
			HealthStatus.Healthy,
			"Test is healthy",
			TimeSpan.FromMilliseconds(10),
			null,
			null);

		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { converter } };

		// Act
		var json = JsonSerializer.Serialize(entry, options);

		// Assert
		json.ShouldContain("\"data\":");
		json.ShouldContain("\"description\":");
		json.ShouldContain("\"duration\":");
		json.ShouldContain("\"exception\":");
		json.ShouldContain("\"status\":");
		json.ShouldContain("\"tags\":");
	}

	[Fact]
	public void ReturnDefaultWhenDeserializing()
	{
		// Arrange
		var converter = new HealthReportEntryJsonConverter();
		var json = @"{""Status"":""Healthy"",""Description"":""Test"",""Duration"":""00:00:00.1000000""}";
		var options = new JsonSerializerOptions { Converters = { converter } };

		using var jsonUtf8 = new MemoryStream(Encoding.UTF8.GetBytes(json));

		// Act & Assert
		_ = Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<HealthReportEntry>(jsonUtf8, options));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenWriterIsNull()
	{
		// Arrange
		var converter = new HealthReportEntryJsonConverter();
		var entry = new HealthReportEntry(HealthStatus.Healthy, "Test", TimeSpan.Zero, null, null);

		// Act
		var ex = Should.Throw<TargetInvocationException>(() =>
		{
			var method = typeof(HealthReportEntryJsonConverter).GetMethod("Write")!;
			_ = method.Invoke(converter, new object[] { null, entry, new JsonSerializerOptions() });
		});

		// Assert inner exception
		_ = ex.InnerException.ShouldBeOfType<ArgumentNullException>();
		ex.InnerException.Message.ShouldContain("writer");
	}
}
