// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the types of operations that can be performed on secrets.
/// </summary>
public enum SecretOperation
{
	/// <summary>
	/// Reading a secret value.
	/// </summary>
	Read = 0,

	/// <summary>
	/// Writing or updating a secret value.
	/// </summary>
	Write = 1,

	/// <summary>
	/// Deleting a secret.
	/// </summary>
	Delete = 2,

	/// <summary>
	/// Listing secrets or checking existence.
	/// </summary>
	List = 3,

	/// <summary>
	/// Rotating a secret or key.
	/// </summary>
	Rotate = 4,

	/// <summary>
	/// Accessing secret metadata.
	/// </summary>
	Metadata = 5,
}
