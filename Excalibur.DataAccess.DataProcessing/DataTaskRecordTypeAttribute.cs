namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Specifies the record type name associated with a data task processor class.
/// </summary>
/// <remarks>
///     This attribute is used to annotate classes that implement <see cref="IDataProcessor" />. It helps in associating a specific record
///     type name with a processor implementation, which is used for discovery and registration.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DataTaskRecordTypeAttribute : Attribute
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DataTaskRecordTypeAttribute" /> class.
	/// </summary>
	/// <param name="recordTypeName"> The name of the record type associated with the processor class. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="recordTypeName" /> is <c> null </c> or empty. </exception>
	public DataTaskRecordTypeAttribute(string recordTypeName)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordTypeName);
		RecordTypeName = recordTypeName;
	}

	/// <summary>
	///     Gets the name of the record type associated with the processor class.
	/// </summary>
	/// <value> A string representing the record type name. </value>
	public string RecordTypeName { get; init; }

	/// <summary>
	///     Deconstructs the <see cref="DataTaskRecordTypeAttribute" /> into its components.
	/// </summary>
	/// <param name="recordTypeName"> The name of the record type. </param>
	public void Deconstruct(out string recordTypeName) => recordTypeName = RecordTypeName;
}
