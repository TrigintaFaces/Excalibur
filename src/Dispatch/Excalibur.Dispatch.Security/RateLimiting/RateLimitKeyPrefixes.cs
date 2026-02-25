// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines the key prefixes used by <see cref="RateLimitingMiddleware"/> when building
/// rate-limit keys from message context values.
/// </summary>
/// <remarks>
/// These constants are referenced both inside the middleware and by consumers who
/// need to register per-tenant or per-tier overrides in <see cref="RateLimitingOptions"/>.
/// </remarks>
public static class RateLimitKeyPrefixes
{
	/// <summary>
	/// Prefix for tenant-scoped rate limit keys (format: <c>tenant:{tenantId}</c>).
	/// </summary>
	public const string Tenant = "tenant:";

	/// <summary>
	/// Prefix for user-scoped rate limit keys (format: <c>user:{userId}</c>).
	/// </summary>
	public const string User = "user:";

	/// <summary>
	/// Prefix for API-key-scoped rate limit keys (format: <c>api:{hashedKey}</c>).
	/// </summary>
	public const string ApiKey = "api:";

	/// <summary>
	/// Prefix for IP-address-scoped rate limit keys (format: <c>ip:{clientIp}</c>).
	/// </summary>
	public const string Ip = "ip:";

	/// <summary>
	/// Prefix for message-type-scoped rate limit keys (format: <c>type:{typeName}</c>).
	/// </summary>
	public const string MessageType = "type:";

	/// <summary>
	/// Prefix for tier-scoped rate limit keys (format: <c>tier:{tierName}</c>).
	/// </summary>
	public const string Tier = "tier:";

	/// <summary>
	/// The key used when no tenant, user, API key, or IP can be identified.
	/// </summary>
	public const string Global = "global";
}
