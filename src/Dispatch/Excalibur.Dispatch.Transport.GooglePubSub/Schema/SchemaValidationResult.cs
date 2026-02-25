// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents the result of a schema validation operation.
/// </summary>
/// <remarks>
/// <para>
/// Contains the validation outcome, including whether the schema is valid and
/// any diagnostic messages produced during validation.
/// </para>
/// </remarks>
public sealed class SchemaValidationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the schema is valid.
	/// </summary>
	/// <value><see langword="true"/> if the schema passed validation; otherwise, <see langword="false"/>.</value>
	public bool IsValid { get; set; }

	/// <summary>
	/// Gets the collection of diagnostic messages from validation.
	/// </summary>
	/// <remarks>
	/// Contains error messages if <see cref="IsValid"/> is <see langword="false"/>,
	/// or warning messages even if the schema is valid.
	/// </remarks>
	/// <value>The collection of diagnostic messages.</value>
	public IReadOnlyList<string> Diagnostics { get; set; } = [];
}
