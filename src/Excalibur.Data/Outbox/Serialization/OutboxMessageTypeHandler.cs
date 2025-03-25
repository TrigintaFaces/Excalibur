using System.Data;
using System.Text.Json;

namespace Excalibur.Data.Outbox.Serialization;

/// <summary>
///     A custom type handler for serializing and deserializing objects to and from JSON when interacting with a database using Dapper.
/// </summary>
internal sealed class OutboxMessageTypeHandler : Dapper.SqlMapper.ITypeHandler
{
	private readonly JsonSerializerOptions _serializerOptions;

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxMessageTypeHandler" /> class with specified JSON serializer options.
	/// </summary>
	/// <param name="options"> The JSON serializer options. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="options" /> is null. </exception>
	public OutboxMessageTypeHandler(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_serializerOptions = options;
	}

	/// <summary>
	///     Assigns a serialized JSON string representation of the value to the database parameter.
	/// </summary>
	/// <param name="parameter"> The database parameter to set. </param>
	/// <param name="value"> The value to be serialized and assigned to the parameter. </param>
	public void SetValue(IDbDataParameter parameter, object? value)
	{
		parameter.DbType = DbType.AnsiString;
		parameter.Value = (value == null)
			? DBNull.Value
			: JsonSerializer.Serialize(value, _serializerOptions);
	}

	/// <summary>
	///     Parses a JSON string value from the database into the specified destination type.
	/// </summary>
	/// <param name="destinationType"> The type to deserialize the value into. </param>
	/// <param name="value"> The value retrieved from the database. </param>
	/// <returns> The deserialized object of the specified type, or <c> null </c> if the value is <c> null </c>. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="destinationType" /> is null. </exception>
	public object? Parse(Type destinationType, object? value)
	{
		if (value == null || value == DBNull.Value)
		{
			return null;
		}

		var jsonString = value.ToString() ?? string.Empty;
		try
		{
			return JsonSerializer.Deserialize(jsonString, destinationType, _serializerOptions);
		}
		catch (JsonException ex)
		{
			throw new JsonException($"Error deserializing value to type {destinationType.FullName}: {ex.Message}", ex);
		}
	}
}
