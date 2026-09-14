// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.A3.Authorization;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// An explicit set of grants. A grant is held only when the exact
/// <c>user | tenant | activity | resource</c> tuple was added, so a handler that stopped consulting the
/// resource identifier, or stopped consulting the tenant, changes the outcome of a test.
/// </summary>
internal sealed class TestGrantStore
{
	private readonly HashSet<string> _grants = new(StringComparer.Ordinal);

	/// <summary>Grants <paramref name="activity"/> on a specific resource.</summary>
	public TestGrantStore Grant(string userId, string tenantId, string activity, string? resourceId)
	{
		_ = _grants.Add(Key(userId, tenantId, activity, resourceId));
		return this;
	}

	public bool IsGranted(string userId, string tenantId, string activity, string? resourceId) =>
		_grants.Contains(Key(userId, tenantId, activity, resourceId));

	private static string Key(string userId, string tenantId, string activity, string? resourceId) =>
		string.Create(CultureInfo.InvariantCulture, $"{userId}|{tenantId}|{activity}|{resourceId ?? "*"}");
}

/// <summary>
/// The policy handed back for one request, bound to the user and tenant that were resolved from the
/// request itself.
/// </summary>
internal sealed class TestAuthorizationPolicy(string userId, string tenantId, TestGrantStore grants)
	: IAuthorizationPolicy
{
	// UserId is nullable on the interface but is always supplied here, so the lookup reads this
	// non-nullable copy. Every constructor parameter below is used ONLY to initialize state, never
	// captured into a method body as well -- doing both is what the compiler rejects.
	private readonly string _userId = userId;

	public string TenantId { get; } = tenantId;

	public string? UserId { get; } = userId;

	public bool IsAuthorized(string activityName, string? resourceId) =>
		grants.IsGranted(_userId, TenantId, activityName, resourceId);

	public bool HasGrant(string activityName) => IsAuthorized(activityName, null);

	public bool HasGrant<TActivity>() => HasGrant(typeof(TActivity).Name);

	public bool HasGrant(string resourceType, string resourceId) => IsAuthorized(resourceType, resourceId);

	public bool HasGrant<TResourceType>(string resourceId) => HasGrant(typeof(TResourceType).Name, resourceId);
}
