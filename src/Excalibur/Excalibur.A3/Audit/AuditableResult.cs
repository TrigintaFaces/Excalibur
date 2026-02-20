// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Audit;

/// <summary>
/// Represents a result that includes an optional audit message.
/// </summary>
/// <typeparam name="TResult"> The type of the result value. </typeparam>
/// <param name="result"> The result value. </param>
/// <param name="auditMessage"> An optional audit message associated with the result. </param>
public class AuditableResult<TResult>(TResult result, string? auditMessage = null)
{
	/// <summary>
	/// Gets or sets the optional audit message.
	/// </summary>
	/// <value>The optional audit message, or <see langword="null"/> if not provided.</value>
	public string? AuditMessage { get; set; } = auditMessage;

	/// <summary>
	/// Gets or sets the result value.
	/// </summary>
	/// <value>The result value of type <typeparamref name="TResult"/>.</value>
	public TResult Result { get; set; } = result;

	/// <summary>
	/// Returns a string representation of the result or the audit message.
	/// </summary>
	/// <returns> The audit message if it is not null or empty; otherwise, the string representation of the result. </returns>
	public override string ToString()
	{
		if (string.IsNullOrEmpty(AuditMessage))
		{
			return Result?.ToString() ?? string.Empty;
		}

		return AuditMessage;
	}
}
