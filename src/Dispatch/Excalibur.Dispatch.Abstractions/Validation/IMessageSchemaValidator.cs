// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Strategy seam for validating a message against its schema. The dispatcher is schema-technology agnostic
/// (JSON Schema, Protobuf, Avro, …), so schema validation is delegated to a consumer-supplied implementation
/// rather than baked into the framework.
/// </summary>
/// <remarks>
/// Register an implementation in the container to enable schema validation. When
/// <see cref="ValidationOptions.ValidateSchemas"/> is <see langword="true"/>, the validation service engages
/// the registered validator; the flag must never be a silent no-op (an implementation is required, or the
/// validation service surfaces an error).
/// </remarks>
public interface IMessageSchemaValidator
{
	/// <summary>
	/// Validates the supplied message against its schema, appending any violations to <paramref name="errors"/>.
	/// </summary>
	/// <param name="message"> The message instance to validate. </param>
	/// <param name="context"> The validation context describing the message under validation. </param>
	/// <param name="errors"> The collection to which schema validation errors are added. </param>
	/// <param name="cancellationToken"> A token to observe for cancellation. </param>
	/// <returns> A task that completes when validation has finished. </returns>
	ValueTask ValidateAsync(
		object message,
		MessageValidationContext context,
		ICollection<ValidationError> errors,
		CancellationToken cancellationToken);
}
