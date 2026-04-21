// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines encryption policy evaluation operations for determining field encryption behavior
/// and data classification.
/// </summary>
public interface IElasticsearchFieldEncryptionPolicy
{
	/// <summary>
	/// Gets a value indicating whether this encryptor supports key rotation.
	/// </summary>
	/// <value> True if the encryptor supports automatic key rotation, false otherwise. </value>
	bool SupportsKeyRotation { get; }

	/// <summary>
	/// Gets a value indicating whether this encryptor supports integrity validation.
	/// </summary>
	/// <value> True if the encryptor provides data integrity checking, false otherwise. </value>
	bool SupportsIntegrityValidation { get; }

	/// <summary>
	/// Determines whether a specific field should be encrypted based on security rules and data classification.
	/// </summary>
	/// <param name="fieldName"> The name of the field to evaluate. </param>
	/// <param name="fieldValue"> The value of the field to evaluate. </param>
	/// <returns> True if the field should be encrypted according to security policies, false otherwise. </returns>
	bool ShouldEncryptField(string fieldName, object? fieldValue);

	/// <summary>
	/// Gets the data classification level for a specific field based on security rules.
	/// </summary>
	/// <param name="fieldName"> The name of the field to classify. </param>
	/// <param name="fieldValue"> The value of the field to classify. </param>
	/// <returns> The data classification level for the field, or Public if no specific classification applies. </returns>
	ElasticSearchDataClassification GetFieldClassification(string fieldName, object? fieldValue);
}
