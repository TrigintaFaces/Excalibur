// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the result of template validation operations.
/// </summary>
public sealed class TemplateValidationResult
{
	/// <summary>
	/// Gets a value indicating whether the template validation was successful.
	/// </summary>
	/// <value> True if the template is valid, false otherwise. </value>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Gets the collection of validation errors, if any.
	/// </summary>
	/// <value> A collection of validation error messages. </value>
	public IEnumerable<string> Errors { get; init; } = [];
}
