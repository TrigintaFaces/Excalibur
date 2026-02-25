// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Constraint that ensures a date field is in the future.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="FutureDateConstraint" /> class. </remarks>
/// <param name="fieldName"> The name of the field to validate. </param>
/// <param name="errorMessage"> The error message to display when validation fails. </param>
public sealed class FutureDateConstraint(string fieldName, string errorMessage) : BaseFieldConstraint(fieldName, errorMessage)
{
	/// <summary>
	/// Determines whether the field contains a date value in the future.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the field contains a future date, is null, or is not a date type; otherwise, false. </returns>
	public override bool IsSatisfied(IDispatchMessage message)
	{
		var value = GetFieldValue(message);
		var now = DateTimeOffset.UtcNow;

		return value switch
		{
			DateTime dt => dt.ToUniversalTime() > now.UtcDateTime,
			DateTimeOffset dto => dto > now,
			null => true, // Optional field, don't fail if not present
			_ => true,
		};
	}
}
