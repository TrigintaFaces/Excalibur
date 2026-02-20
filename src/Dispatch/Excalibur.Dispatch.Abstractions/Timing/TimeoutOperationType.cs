// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the types of operations that can have timeout policies applied. R7.4: Operation categorization for timeout management.
/// </summary>
public enum TimeoutOperationType
{
	/// <summary>
	/// Default timeout for general operations.
	/// </summary>
	Default = 0,

	/// <summary>
	/// Timeout for message handler execution.
	/// </summary>
	Handler = 1,

	/// <summary>
	/// Timeout for message serialization operations.
	/// </summary>
	Serialization = 2,

	/// <summary>
	/// Timeout for transport operations (sending/receiving).
	/// </summary>
	Transport = 3,

	/// <summary>
	/// Timeout for message validation operations.
	/// </summary>
	Validation = 4,

	/// <summary>
	/// Timeout for middleware execution.
	/// </summary>
	Middleware = 5,

	/// <summary>
	/// Timeout for pipeline execution.
	/// </summary>
	Pipeline = 6,

	/// <summary>
	/// Timeout for outbox operations.
	/// </summary>
	Outbox = 7,

	/// <summary>
	/// Timeout for inbox operations.
	/// </summary>
	Inbox = 8,

	/// <summary>
	/// Timeout for scheduling operations.
	/// </summary>
	Scheduling = 9,

	/// <summary>
	/// Timeout for database operations.
	/// </summary>
	Database = 10,

	/// <summary>
	/// Timeout for HTTP operations.
	/// </summary>
	Http = 11,
}
