using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.HealthChecks;

/// <summary>
///     Converts <see cref="HealthReport" /> objects to JSON representation.
/// </summary>
/// <remarks>
///     This converter handles the serialization of the <see cref="HealthReport" /> object, including its entries, status, and total
///     duration. It respects the <see cref="JsonSerializerOptions.PropertyNamingPolicy" /> for property naming.
/// </remarks>
internal sealed class HealthReportJsonConverter : JsonConverter<HealthReport>
{
	/// <inheritdoc />
	public override HealthReport? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => null;

	/// <inheritdoc />
	/// <remarks> Writes a <see cref="HealthReport" /> to JSON format. </remarks>
	public override void Write(Utf8JsonWriter writer, HealthReport? value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer, nameof(writer));
		ArgumentNullException.ThrowIfNull(value, nameof(value));

		writer.WriteStartObject();

		// Serialize the "Entries" property
		writer.WritePropertyName(ConvertPropertyName(nameof(value.Entries), options));
		JsonSerializer.Serialize(writer, value.Entries, options);

		// Serialize the "Status" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.Status), options),
			value.Status.ToString());

		// Serialize the "TotalDuration" property
		writer.WriteString(
			ConvertPropertyName(nameof(value.TotalDuration), options),
			value.TotalDuration.ToString());

		writer.WriteEndObject();
	}

	/// <summary>
	///     Converts a property name to respect the specified <see cref="JsonSerializerOptions.PropertyNamingPolicy" />.
	/// </summary>
	/// <param name="propertyName"> The original property name. </param>
	/// <param name="options"> The serializer options containing the naming policy. </param>
	/// <returns> The converted property name, or the original name if no naming policy is set. </returns>
	private static string ConvertPropertyName(string propertyName, JsonSerializerOptions? options) =>
		options?.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
