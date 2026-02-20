// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Constraint that ensures a numeric field is positive.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PositiveNumberConstraint" /> class. </remarks>
/// <param name="fieldName"> The name of the field to validate. </param>
/// <param name="errorMessage"> The error message to display when validation fails. </param>
public sealed class PositiveNumberConstraint(string fieldName, string errorMessage) : BaseFieldConstraint(fieldName, errorMessage)
{
	/// <summary>
	/// Determines whether the field contains a positive numeric value.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the field contains a positive number or is not a numeric type; otherwise, false. </returns>
	public override bool IsSatisfied(IDispatchMessage message)
	{
		var value = GetFieldValue(message);
		return value switch
		{
			int i => i >= 0,
			long l => l >= 0,
			decimal d => d >= 0,
			double d => d >= 0,
			float f => f >= 0,
			_ => true, // If not a number, don't fail on this constraint
		};
	}
}
