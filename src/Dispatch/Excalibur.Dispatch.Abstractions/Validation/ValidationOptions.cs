// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Configuration options for message validation middleware.
/// </summary>
public sealed class ValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether validation is enabled.
	/// </summary>
	/// <value> <see langword="true" /> when validation middleware is active; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to fail fast on the first validation error.
	/// </summary>
	/// <value> <see langword="true" /> to stop on the first error; otherwise, <see langword="false" />. </value>
	public bool FailFast { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of validation errors to collect before stopping.
	/// </summary>
	/// <value> The maximum number of validation errors to record. </value>
	public int MaxErrors { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to include detailed error information.
	/// </summary>
	/// <value> <see langword="true" /> when detailed error information should be included; otherwise, <see langword="false" />. </value>
	public bool IncludeDetailedErrors { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate message contracts.
	/// </summary>
	/// <value> <see langword="true" /> to validate contracts; otherwise, <see langword="false" />. </value>
	public bool ValidateContracts { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate message schemas.
	/// </summary>
	/// <value> <see langword="true" /> to validate schemas; otherwise, <see langword="false" />. </value>
	public bool ValidateSchemas { get; set; }

	/// <summary>
	/// Gets or sets the timeout for validation operations.
	/// </summary>
	/// <value> The timeout budget for validation. </value>
	public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets custom validation metadata.
	/// </summary>
	/// <value> The metadata bag provided to validators. </value>
	public IDictionary<string, object> CustomMetadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
