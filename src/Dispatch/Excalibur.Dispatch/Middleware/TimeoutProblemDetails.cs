// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Problem details for timeout errors.
/// </summary>
internal sealed class TimeoutProblemDetails(MessageTimeoutException exception) : IMessageProblemDetails
{
	/// <inheritdoc/>
	public string Title { get; set; } = "Operation Timeout";

	/// <inheritdoc/>
	public string Type { get; set; } = "timeout";

	/// <inheritdoc/>
	public string Detail { get; set; } = exception.Message;

	/// <inheritdoc/>
	public int ErrorCode { get; set; } = 504; // Gateway Timeout

	/// <inheritdoc/>
	public string Instance { get; set; } = $"/message/{exception.MessageId}";

	/// <inheritdoc/>
	public IDictionary<string, object?> Extensions { get; set; } = new Dictionary<string, object?>
(StringComparer.Ordinal)
	{
		["TimeoutExceeded"] = true,
		["ResultType"] = "Timeout",
		["ElapsedTime"] = exception.ElapsedTime,
		["TimeoutDuration"] = exception.TimeoutDuration,
	};

	/// <inheritdoc/>
	public override string ToString() => exception.Message;
}
