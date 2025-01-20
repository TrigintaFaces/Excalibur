namespace Excalibur.Core.Concurrency;

/// <inheritdoc />
public sealed class ETag : IETag
{
	/// <inheritdoc />
	public string? IncomingValue { get; set; } = string.Empty;

	/// <inheritdoc />
	public string? OutgoingValue { get; set; } = string.Empty;
}
