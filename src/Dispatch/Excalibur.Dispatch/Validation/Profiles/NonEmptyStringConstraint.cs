// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Constraint that ensures a string field is not empty.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="NonEmptyStringConstraint" /> class. </remarks>
/// <param name="fieldName"> The name of the field to validate. </param>
/// <param name="errorMessage"> The error message to display when validation fails. </param>
public sealed class NonEmptyStringConstraint(string fieldName, string errorMessage) : BaseFieldConstraint(fieldName, errorMessage)
{
	/// <summary>
	/// Determines whether the field contains a non-empty string value.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the field contains a non-empty string; otherwise, false. </returns>
	public override bool IsSatisfied(IDispatchMessage message)
	{
		var value = GetFieldValue(message);
		return value is string str && !string.IsNullOrWhiteSpace(str);
	}
}
