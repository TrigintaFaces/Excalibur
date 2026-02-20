// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Interface for field constraints in a profile.
/// </summary>
public interface IFieldConstraint
{
	/// <summary>
	/// Gets the name of the field this constraint applies to.
	/// </summary>
	/// <value> The field name targeted by the constraint. </value>
	string FieldName { get; }

	/// <summary>
	/// Gets the error message to display when this constraint is violated.
	/// </summary>
	/// <value> The localized or user-facing error message. </value>
	string ErrorMessage { get; }

	/// <summary>
	/// Determines whether this constraint is satisfied by the given message.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the constraint is satisfied; otherwise, false. </returns>
	bool IsSatisfied(IDispatchMessage message);
}
