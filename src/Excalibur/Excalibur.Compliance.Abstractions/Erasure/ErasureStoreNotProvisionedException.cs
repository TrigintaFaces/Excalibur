// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Thrown when an erasure store's backing schema is absent, or is present but missing columns the store's
/// statements bind.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>deployment fault, not an operation outcome</b>. Nothing the caller passed is wrong and no
/// retry, backoff, or alternative identifier will clear it — the database has not been provisioned, or was
/// provisioned before columns this version binds were introduced. The remedy is to run the shipped
/// migration scripts and restart.
/// </para>
/// <para>
/// It deliberately sits <b>outside</b> the <see cref="InvalidOperationException"/> hierarchy. A store that
/// reported a provisioning fault as <see cref="InvalidOperationException"/> would be indistinguishable, to
/// a caller, from <see cref="DuplicateErasureRequestException"/> — so an unprovisioned store would be read
/// as "this erasure request is already on file", and every request filed against it would be silently
/// discarded by a caller behaving correctly.
/// </para>
/// <para>
/// A hosted application should never observe this at all: the erasure schema-validation hosted service
/// verifies the schema during startup, so a mis-provisioned deployment fails to start rather than failing
/// one write at a time. This exception is the fail-closed floor for consumers that never run that hosted
/// service — a store constructed directly, or a serverless host with no startup pipeline.
/// </para>
/// </remarks>
public sealed class ErasureStoreNotProvisionedException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureStoreNotProvisionedException"/> class.
	/// </summary>
	public ErasureStoreNotProvisionedException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureStoreNotProvisionedException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message, naming the table and any missing columns.</param>
	public ErasureStoreNotProvisionedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureStoreNotProvisionedException"/> class with a
	/// message and inner exception.
	/// </summary>
	/// <param name="message">The error message, naming the table and any missing columns.</param>
	/// <param name="innerException">The underlying failure encountered while inspecting the schema.</param>
	public ErasureStoreNotProvisionedException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the name of the table whose absence or shape caused the failure, when a single table is at fault.
	/// </summary>
	/// <value>The qualified table name, or <see langword="null"/> when not attributable to one table.</value>
	public string? TableName { get; init; }
}
