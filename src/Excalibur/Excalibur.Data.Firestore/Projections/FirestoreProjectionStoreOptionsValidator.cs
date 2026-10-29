// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Projections;

/// <summary>
/// Validates <see cref="FirestoreProjectionStoreOptions"/> for every named instance.
/// </summary>
/// <remarks>
/// Hand-written rather than data-annotation driven: annotation validation reflects over the options type
/// and so is unsafe under trimming, and the collection name needs a check annotations cannot express.
/// </remarks>
internal sealed class FirestoreProjectionStoreOptionsValidator
	: IValidateOptions<FirestoreProjectionStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, FirestoreProjectionStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var instance = string.IsNullOrEmpty(name) ? "(default)" : name;

		if (string.IsNullOrWhiteSpace(options.CollectionName))
		{
			return ValidateOptionsResult.Fail(
				$"FirestoreProjectionStoreOptions.CollectionName must be a non-empty collection name (options instance '{instance}').");
		}

		// "/" is the Firestore path separator, so a collection name carrying one addresses a different
		// path than the one configured -- silently, and writes land where no read looks.
		if (options.CollectionName.Contains('/', StringComparison.Ordinal))
		{
			return ValidateOptionsResult.Fail(
				$"FirestoreProjectionStoreOptions.CollectionName must not contain '/' (options instance '{instance}'); it is the Firestore path separator.");
		}

		return ValidateOptionsResult.Success;
	}
}
