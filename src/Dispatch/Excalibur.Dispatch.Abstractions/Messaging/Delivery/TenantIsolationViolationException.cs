// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Thrown when an operation addresses data belonging to a different tenant than the one in scope.
/// </summary>
/// <remarks>
/// <para>
/// This is <b>not</b> a concurrency conflict, and the distinction is operational rather than cosmetic. A
/// concurrency conflict is transient and the correct response is to reload and retry: another writer moved
/// the record, so the next attempt has a real chance of succeeding. A tenant-isolation violation is
/// permanent. The record belongs to somebody else, reloading returns nothing, and every retry fails
/// identically — so a caller that treats it as a conflict retries a cross-tenant write forever, burning the
/// operation and any retry budget on an attempt that cannot succeed by construction.
/// </para>
/// <para>
/// It carries no version, no record state, and nothing else read from the other tenant's data. Reporting
/// "the persisted version is 7" would disclose that a record exists at that identifier and how far it has
/// advanced — facts this caller is not entitled to. The exception says only that the operation was refused.
/// </para>
/// <para>
/// It deliberately does <b>not</b> derive from the concurrency exception type. Sharing that type is exactly
/// what makes the two indistinguishable to a correct caller, and a caller written against the transient
/// contract would inherit the infinite retry rather than being forced to handle this case explicitly.
/// </para>
/// </remarks>
public sealed class TenantIsolationViolationException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TenantIsolationViolationException"/> class.
	/// </summary>
	public TenantIsolationViolationException()
		: base("The operation addressed data belonging to a different tenant and was refused.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantIsolationViolationException"/> class for a specific
	/// record, naming the resource and identifier without disclosing anything about the record's contents.
	/// </summary>
	/// <param name="resourceType">The kind of record that was addressed, for example the saga state type name.</param>
	/// <param name="resourceId">The identifier the caller supplied. This came from the caller, so echoing it discloses nothing.</param>
	public TenantIsolationViolationException(string resourceType, string resourceId)
		: base($"The operation addressed a '{resourceType}' with identifier '{resourceId}' that belongs to a " +
			"different tenant, and was refused. This is not a concurrency conflict and retrying will not " +
			"succeed: establish the correct tenant scope, or use an identifier belonging to the current one.")
	{
		ResourceType = resourceType;
		ResourceId = resourceId;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantIsolationViolationException"/> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public TenantIsolationViolationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantIsolationViolationException"/> class with a message
	/// and the underlying cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of this exception.</param>
	public TenantIsolationViolationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the kind of record that was addressed, or <see langword="null"/> when not specified.
	/// </summary>
	public string? ResourceType { get; }

	/// <summary>
	/// Gets the identifier the caller supplied, or <see langword="null"/> when not specified.
	/// </summary>
	public string? ResourceId { get; }
}
