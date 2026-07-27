// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// Configures the Excalibur operational dashboard: the route prefix its read endpoints are mapped
/// under, page-size bounds for paginated queries, and whether mutating actions (dead-letter replay)
/// are enabled.
/// </summary>
/// <remarks>
/// The dashboard is <strong>read-only by default</strong>. Mutating actions are gated by
/// <see cref="EnableMutatingActions"/>: when it is <see langword="false"/> the mutating endpoint group
/// is not mapped at all (there is no attack surface to authorize), and when it is <see langword="true"/>
/// the group is mapped and additionally requires an authenticated, authorized caller.
/// </remarks>
public sealed class DashboardOptions
{
	/// <summary>
	/// Gets the route prefix under which the dashboard read endpoints and single-page app are mapped.
	/// </summary>
	/// <value> The route prefix; defaults to <c>/dashboard</c>. Must start with <c>/</c>. </value>
	[Required]
	[RegularExpression("^/[A-Za-z0-9._~/-]*$", ErrorMessage = "RoutePrefix must start with '/' and contain only URL-path-safe characters.")]
	public string RoutePrefix { get; set; } = "/dashboard";

	/// <summary>
	/// Gets the default number of items returned by paginated read endpoints when the caller does not
	/// specify a page size.
	/// </summary>
	/// <value> The default page size; defaults to <c>50</c>. </value>
	[Range(1, 1000)]
	public int DefaultPageSize { get; set; } = 50;

	/// <summary>
	/// Gets the maximum number of items a paginated read endpoint will return, clamping larger requests.
	/// </summary>
	/// <value> The maximum page size; defaults to <c>500</c>. </value>
	[Range(1, 10000)]
	public int MaxPageSize { get; set; } = 500;

	/// <summary>
	/// Gets a value indicating whether mutating actions (such as dead-letter replay) are enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to map the auth-gated mutating endpoint group; otherwise
	/// <see langword="false"/> (the default) to omit it entirely.
	/// </value>
	public bool EnableMutatingActions { get; set; }

	/// <summary>
	/// Gets the authorization policy name applied to mutating endpoints when
	/// <see cref="EnableMutatingActions"/> is <see langword="true"/>.
	/// </summary>
	/// <value>
	/// The policy name, or <see langword="null"/> to require any authenticated user
	/// (default <c>RequireAuthorization</c> behavior).
	/// </value>
	public string? MutatingActionsPolicy { get; set; }

	/// <summary>
	/// Gets the authorization policy name applied to the read (non-mutating) endpoint group.
	/// </summary>
	/// <remarks>
	/// Read endpoints are open by default (the dashboard is read-only by design). Setting a policy makes
	/// every read endpoint require an authorized caller — symmetric with <see cref="MutatingActionsPolicy"/>
	/// and matching the health-checks pattern of gating the endpoint behind an authorization policy. Leave
	/// <see langword="null"/> to keep reads open, or gate them externally by mapping the dashboard inside a
	/// parent authorized route group.
	/// </remarks>
	/// <value>The policy name, or <see langword="null"/> (the default) to leave read endpoints open.</value>
	public string? ReadActionsPolicy { get; set; }

	/// <summary>
	/// Gets a value indicating whether potentially sensitive fields are included in read responses.
	/// </summary>
	/// <remarks>
	/// Some read projections carry fields that can embed personal or cross-tenant data: a dead-letter
	/// entry's exception message (which may quote failed-message content) and correlation id, and a saga
	/// instance's owning tenant id. These are <strong>redacted by default</strong> (omitted from the JSON)
	/// so an open, unauthenticated read cannot leak them. Set to <see langword="true"/> only when the
	/// dashboard is behind an authenticated, authorized boundary (for example via
	/// <see cref="ReadActionsPolicy"/> or a parent authorized route group) and operators need those fields
	/// for diagnostics.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> to include the sensitive fields; otherwise <see langword="false"/> (the
	/// default) to redact them.
	/// </value>
	public bool ExposeSensitiveData { get; set; }
}
