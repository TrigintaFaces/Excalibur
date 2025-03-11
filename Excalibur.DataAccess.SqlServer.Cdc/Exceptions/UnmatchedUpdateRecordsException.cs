using Excalibur.Core.Exceptions;

namespace Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

public class UnmatchedUpdateRecordsException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="UnmatchedUpdateRecordsException" /> class.
	/// </summary>
	/// <param name="lsn"> The lsn with unmatched update records. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. Defaults to 500 if not specified. </param>
	/// <param name="message">
	///     The custom error message. Defaults to a message indicating that no handler is found for the specified table.
	/// </param>
	/// <param name="innerException"> The inner exception that caused the current exception, if applicable. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="lsn" /> is null. </exception>
	public UnmatchedUpdateRecordsException(byte[] lsn, int? statusCode = null, string? message = null, Exception? innerException = null)
		: base(
			statusCode ?? 500,
			message ?? $"Unmatched UpdateBefore/UpdateAfter pairs detected at the end of LSN processing for LSN {lsn}.",
			innerException)
	{
		ArgumentNullException.ThrowIfNull(lsn, nameof(lsn));

		Lsn = lsn;
	}

	/// <summary>
	///     Gets the LSN for which the duplicate record exists.
	/// </summary>
	public byte[] Lsn { get; protected set; }
}
