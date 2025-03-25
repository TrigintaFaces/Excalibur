using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data.Outbox;
using Excalibur.Data.Outbox.Serialization;

namespace Excalibur.Data.TypeHandlers;

/// <summary>
///     A custom Dapper type handler for handling <see cref="OutboxMessage" /> serialization and deserialization.
/// </summary>
public class OutboxMessageTypeHandler : SqlMapper.TypeHandler<OutboxMessage>
{
	private readonly JsonSerializerOptions? _options;

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxMessageTypeHandler" /> class.
	/// </summary>
	/// <param name="options">
	///     Optional JSON serialization options. If not provided, default options with the <see cref="OutboxMessageJsonConverter" /> are used.
	/// </param>
	public OutboxMessageTypeHandler(JsonSerializerOptions? options = null)
	{
		_options = options ?? new JsonSerializerOptions();
		_options.Converters.Add(new OutboxMessageJsonConverter()); // Ensure the custom converter is added
	}

	/// <summary>
	///     Assigns a serialized JSON representation of the <see cref="OutboxMessage" /> value to a database parameter.
	/// </summary>
	/// <param name="parameter"> The database parameter to set. </param>
	/// <param name="value"> The <see cref="OutboxMessage" /> value to serialize and assign. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="parameter" /> or <paramref name="value" /> is null. </exception>
	public override void SetValue(IDbDataParameter parameter, OutboxMessage value)
	{
		ArgumentNullException.ThrowIfNull(parameter);
		ArgumentNullException.ThrowIfNull(value);

		parameter.DbType = DbType.AnsiString;

		// Serialize OutboxMessage to JSON with the $messageBodyType included
		var jsonString = JsonSerializer.Serialize(value, _options);
		parameter.Value = (object?)jsonString ?? DBNull.Value;
	}

	/// <summary>
	///     Parses a database value into an <see cref="OutboxMessage" /> instance.
	/// </summary>
	/// <param name="value"> The value retrieved from the database, expected to be a JSON string. </param>
	/// <returns> The deserialized <see cref="OutboxMessage" /> instance. </returns>
	/// <exception cref="ArgumentException"> Thrown if the <paramref name="value" /> is not a valid JSON string. </exception>
	/// <exception cref="JsonException"> Thrown if deserialization fails. </exception>
	public override OutboxMessage Parse(object value)
	{
		if (value is string jsonString)
		{
			// Deserialize JSON back to OutboxMessage, preserving $messageBodyType
			return JsonSerializer.Deserialize<OutboxMessage>(jsonString, _options)
				   ?? throw new JsonException("Failed to parse OutboxMessage from JSON");
		}

		throw new ArgumentException("Invalid value for OutboxMessage parsing");
	}
}
