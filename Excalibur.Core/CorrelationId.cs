namespace Excalibur.Core;

/// <summary>
///     Represents a correlation ID used to track requests or operations across distributed systems.
/// </summary>
public class CorrelationId : ICorrelationId
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CorrelationId" /> class with a specified GUID value.
	/// </summary>
	/// <param name="value"> The GUID value for the correlation ID. </param>
	public CorrelationId(Guid value) => Value = value;

	/// <summary>
	///     Initializes a new instance of the <see cref="CorrelationId" /> class with a string representation of a GUID value.
	/// </summary>
	/// <param name="value"> The string representation of the GUID value. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <c> null </c>. </exception>
	/// <exception cref="FormatException"> Thrown when <paramref name="value" /> is not in a valid GUID format. </exception>
	public CorrelationId(string? value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Value = Guid.Parse(value);
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="CorrelationId" /> class with a new GUID value.
	/// </summary>
	public CorrelationId() => Value = Guid.NewGuid();

	/// <inheritdoc />
	public Guid Value { get; set; }

	/// <inheritdoc cref="ICorrelationId" />
	public override string ToString() => Value.ToString();
}
