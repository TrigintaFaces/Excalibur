// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Provides the current user's audit log access role.
/// </summary>
/// <remarks>
/// <para>
/// Consumers must implement this interface to integrate with their authorization system.
/// Dispatch provides no default implementation - this is intentional to force explicit
/// authorization configuration.
/// </para>
/// <para>
/// Implementation examples:
/// <list type="bullet">
/// <item>Claims-based: Check ASP.NET Core role claims</item>
/// <item>Grant-based: Use A3 or similar grant management systems</item>
/// <item>Custom: Implement any authorization logic needed</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class ClaimsAuditRoleProvider : IAuditRoleProvider
/// {
///     private readonly IHttpContextAccessor _httpContextAccessor;
///
///     public Task&lt;AuditLogRole&gt; GetCurrentRoleAsync(CancellationToken ct)
///     {
///         var user = _httpContextAccessor.HttpContext?.User;
///
///         if (user?.IsInRole("AuditAdmin") == true)
///             return Task.FromResult(AuditLogRole.Administrator);
///         if (user?.IsInRole("ComplianceOfficer") == true)
///             return Task.FromResult(AuditLogRole.ComplianceOfficer);
///         if (user?.IsInRole("SecurityAnalyst") == true)
///             return Task.FromResult(AuditLogRole.SecurityAnalyst);
///
///         return Task.FromResult(AuditLogRole.None);
///     }
/// }
/// </code>
/// </example>
public interface IAuditRoleProvider
{
	/// <summary>
	/// Gets the current user's audit log access role.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The audit log role for the current user.</returns>
	/// <remarks>
	/// This method is called for each audit log operation. Implementations should
	/// be efficient and may cache role information for the duration of a request.
	/// </remarks>
	Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken);
}
