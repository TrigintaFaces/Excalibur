// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines data classification and encryption rules.
/// </summary>
public sealed class DataClassificationRule
{
	/// <summary>
	/// Gets the field pattern to match for encryption.
	/// </summary>
	/// <value> A regular expression pattern matching field names that should be encrypted. </value>
	[Required]
	public string FieldPattern { get; init; } = string.Empty;

	/// <summary>
	/// Gets the data classification level.
	/// </summary>
	/// <value> The sensitivity level of the data. </value>
	public DataClassification Classification { get; init; } = DataClassification.Public;

	/// <summary>
	/// Gets a value indicating whether this rule is enabled.
	/// </summary>
	/// <value> True if the rule should be applied, false to disable. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the encryption algorithm for this data type.
	/// </summary>
	/// <value> The specific encryption algorithm to use, or null to use the default. </value>
	public string? EncryptionAlgorithm { get; init; }
}
