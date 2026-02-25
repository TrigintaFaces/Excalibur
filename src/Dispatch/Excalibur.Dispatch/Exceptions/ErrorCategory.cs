// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Exceptions;

/// <summary>
/// Categorizes errors by their domain to enable appropriate handling strategies.
/// </summary>
public enum ErrorCategory
{
	/// <summary>
	/// Unknown or unclassified error.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Configuration-related errors (missing or invalid configuration).
	/// </summary>
	Configuration = 1,

	/// <summary>
	/// Validation errors (invalid input, business rule violations).
	/// </summary>
	Validation = 2,

	/// <summary>
	/// Messaging infrastructure errors (queue unavailable, broker connection issues).
	/// </summary>
	Messaging = 3,

	/// <summary>
	/// Serialization/deserialization errors.
	/// </summary>
	Serialization = 4,

	/// <summary>
	/// Network-related errors (connectivity, DNS, etc.).
	/// </summary>
	Network = 5,

	/// <summary>
	/// Security and authentication/authorization errors.
	/// </summary>
	Security = 6,

	/// <summary>
	/// Data access and persistence errors.
	/// </summary>
	Data = 7,

	/// <summary>
	/// Timeout errors (operation took too long).
	/// </summary>
	Timeout = 8,

	/// <summary>
	/// Resource errors (not found, exhausted, unavailable).
	/// </summary>
	Resource = 9,

	/// <summary>
	/// System-level errors (out of memory, IO errors, etc.).
	/// </summary>
	System = 10,

	/// <summary>
	/// Circuit breaker or resilience pattern errors.
	/// </summary>
	Resilience = 11,

	/// <summary>
	/// Concurrency and thread-safety errors.
	/// </summary>
	Concurrency = 12,
}
