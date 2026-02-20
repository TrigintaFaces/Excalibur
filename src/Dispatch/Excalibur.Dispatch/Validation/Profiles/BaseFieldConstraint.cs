// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Base class for field constraints.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BaseFieldConstraint" /> class. </remarks>
/// <param name="fieldName"> The name of the field this constraint applies to. </param>
/// <param name="errorMessage"> The error message to display when violated. </param>
/// <exception cref="ArgumentNullException"> Thrown when fieldName or errorMessage is null. </exception>
public abstract class BaseFieldConstraint(string fieldName, string errorMessage) : IFieldConstraint
{
	/// <summary>
	/// Gets the name of the field this constraint applies to.
	/// </summary>
	/// <value> The target field name enforced by the constraint. </value>
	public string FieldName { get; } = fieldName ?? throw new ArgumentNullException(nameof(fieldName));

	/// <summary>
	/// Gets the error message to display when this constraint is violated.
	/// </summary>
	/// <value> The message provided when validation fails. </value>
	public string ErrorMessage { get; } = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));

	/// <summary>
	/// Determines whether this constraint is satisfied by the given message.
	/// </summary>
	/// <param name="message"> The message to validate. </param>
	/// <returns> True if the constraint is satisfied; otherwise, false. </returns>
	public abstract bool IsSatisfied(IDispatchMessage message);

	/// <summary>
	/// Gets the value of the field from the message using reflection.
	/// </summary>
	/// <param name="message"> The message to extract the field value from. </param>
	/// <returns> The field value, or null if the field doesn't exist. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties'",
		Justification = "Message types are preserved through handler registration and DI container")]
	protected object? GetFieldValue(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);
		var property = message.GetType().GetProperty(FieldName);
		return property?.GetValue(message);
	}
}
