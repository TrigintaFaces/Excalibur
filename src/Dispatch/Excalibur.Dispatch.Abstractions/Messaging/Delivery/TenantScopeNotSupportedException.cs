// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Thrown when a store is asked to perform an operation within a tenant scope that it cannot honour,
/// because it has no tenant discriminator to filter on.
/// </summary>
/// <remarks>
/// <para>
/// This is distinct from "the store does not support the operation at all". A store that cannot purge by
/// age refuses every call; a store that raises this exception performs the operation happily when no tenant
/// is resolved, and refuses only when honouring the request would require a tenant predicate it cannot
/// express. Both are refusals, but a caller can act on the difference: the first needs a different store,
/// the second needs either a different store or an explicitly estate-wide call.
/// </para>
/// <para>
/// It derives from <see cref="NotSupportedException"/> so that callers written against the broader contract —
/// including retention schedulers that stop on an unsupported capability rather than retrying forever —
/// continue to behave correctly without knowing this type exists.
/// </para>
/// <para>
/// The distinction is also what makes a refusal testable. Where both refusals share one exception type, a
/// test asserting "this throws" passes whether the tenant guard is present or absent, so the guard can be
/// deleted and the suite stays green. Asserting this type instead binds the tenant refusal specifically.
/// </para>
/// </remarks>
public sealed class TenantScopeNotSupportedException : NotSupportedException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopeNotSupportedException"/> class.
	/// </summary>
	public TenantScopeNotSupportedException()
		: base("The store cannot perform this operation within a tenant scope.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopeNotSupportedException"/> class with a message
	/// describing which store refused, why it cannot honour the scope, and what the caller can do instead.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public TenantScopeNotSupportedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantScopeNotSupportedException"/> class with a message
	/// and the underlying cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of this exception.</param>
	public TenantScopeNotSupportedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
