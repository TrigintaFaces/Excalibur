namespace Excalibur.Application.Requests.Jobs;

/// <summary>
///     Represents the result of a job execution.
/// </summary>
public readonly struct JobResult : IEquatable<JobResult>
{
	/// <summary>
	///     Indicates that no work was performed during the job execution.
	/// </summary>
	public static readonly JobResult NoWorkPerformed = new(1);

	/// <summary>
	///     Indicates that the operation succeeded during the job execution.
	/// </summary>
	public static readonly JobResult OperationSucceeded = new(2);

	/// <summary>
	///     Initializes a new instance of the <see cref="JobResult" /> struct with the specified value.
	/// </summary>
	/// <param name="value"> The integer value representing the job result. </param>
	private JobResult(int value)
	{
		Value = value;
	}

	/// <summary>
	///     Gets the underlying value of the job result.
	/// </summary>
	private int Value { get; }

	/// <summary>
	///     Determines whether two <see cref="JobResult" /> instances are equal.
	/// </summary>
	/// <param name="left"> The first job result to compare. </param>
	/// <param name="right"> The second job result to compare. </param>
	/// <returns> <c> true </c> if the two job results are equal; otherwise, <c> false </c>. </returns>
	public static bool operator ==(JobResult left, JobResult right) => left.Equals(right);

	/// <summary>
	///     Determines whether two <see cref="JobResult" /> instances are not equal.
	/// </summary>
	/// <param name="left"> The first job result to compare. </param>
	/// <param name="right"> The second job result to compare. </param>
	/// <returns> <c> true </c> if the two job results are not equal; otherwise, <c> false </c>. </returns>
	public static bool operator !=(JobResult left, JobResult right) => !(left == right);

	/// <inheritdoc />
	public override int GetHashCode() => Value.GetHashCode();

	/// <inheritdoc />
	public bool Equals(JobResult other) => Value == other.Value;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is JobResult other && Equals(other);
}
