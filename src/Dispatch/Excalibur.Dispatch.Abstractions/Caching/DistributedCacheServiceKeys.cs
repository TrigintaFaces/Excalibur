// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Service keys under which specially-scoped <c>IDistributedCache</c> registrations are published.
/// </summary>
/// <remarks>
/// <para>
/// These live in the abstractions package on purpose. A component that requires a scoped cache depends on
/// the key, not on the caching implementation package, so requiring the guarantee costs no extra package
/// dependency.
/// </para>
/// </remarks>
public static class DistributedCacheServiceKeys
{
	/// <summary>
	/// The key for a distributed cache whose keyspace is partitioned per application.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Depend on this key — rather than on the unkeyed <c>IDistributedCache</c> — when placing entries in a
	/// cache that two applications may share and whose contents must not be read across that boundary.
	/// Resolving it fails when no scoped cache has been registered, which is the point: an unscoped read is
	/// then not something a caller can express by accident.
	/// </para>
	/// <para>
	/// The unkeyed <c>IDistributedCache</c> remains exactly what it was and is the right dependency for
	/// everything whose entries are harmless to share.
	/// </para>
	/// </remarks>
	public const string ApplicationScoped = "excalibur:application-scoped";
}
