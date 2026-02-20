// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Constraint that ensures a field contains a valid GUID.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="GuidFormatConstraint" /> class. </remarks>
/// <param name="fieldName"> The name of the field to validate. </param>
/// <param name="errorMessage"> The error message to display when validation fails. </param>
public sealed class GuidFormatConstraint(string fieldName, string errorMessage) : BaseFieldConstraint(fieldName, errorMessage)
{
	/// <summary>
	/// Determines whether the field contains a valid GUID.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the field contains a valid non-empty GUID; otherwise, false. </returns>
	public override bool IsSatisfied(IDispatchMessage message)
	{
		var value = GetFieldValue(message);
		return value switch
		{
			Guid g => g != Guid.Empty,
			string s => Guid.TryParse(s, out var g) && g != Guid.Empty,
			_ => false,
		};
	}
}
