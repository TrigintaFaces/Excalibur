// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines features that can be enabled or disabled in the dispatch system.
/// </summary>
[Flags]
public enum DispatchFeatures
{
	/// <summary>
	/// No specific features required.
	/// </summary>
	None = 0,

	/// <summary>
	/// Inbox pattern for message deduplication and ordering.
	/// </summary>
	Inbox = 1 << 0,

	/// <summary>
	/// Outbox pattern for reliable message publishing.
	/// </summary>
	Outbox = 1 << 1,

	/// <summary>
	/// Distributed tracing support.
	/// </summary>
	Tracing = 1 << 2,

	/// <summary>
	/// Metrics collection and reporting.
	/// </summary>
	Metrics = 1 << 3,

	/// <summary>
	/// Message validation support.
	/// </summary>
	Validation = 1 << 4,

	/// <summary>
	/// Authorization and security checks.
	/// </summary>
	Authorization = 1 << 5,

	/// <summary>
	/// Transaction support.
	/// </summary>
	Transactions = 1 << 6,
}
