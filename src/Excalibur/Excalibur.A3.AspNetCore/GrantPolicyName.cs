// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Builds the convention-based authorization policy names that grant authorization recognizes on an
/// ASP.NET Core endpoint, so a policy need not be registered in advance for every activity and resource
/// combination.
/// </summary>
/// <remarks>
/// <para>
/// A name has the shape <c>grant:{activity}:{resourceType}</c>, optionally followed by a resource
/// scope. A scope written in braces — <c>grant:Approve:Order:{id}</c> — names a route parameter whose
/// value is read from the request, which is what lets a single policy cover <c>/orders/{id}</c>. A
/// scope written without braces is a literal resource identifier.
/// </para>
/// <para>
/// A policy name that does not begin with <c>grant:</c> is left to the rest of the application, so
/// policies registered by name continue to resolve normally.
/// </para>
/// </remarks>
public static class GrantPolicyName
{
	/// <summary>
	/// The prefix that marks a policy name as a grant policy.
	/// </summary>
	public const string Prefix = "grant:";

	private const char Separator = ':';

	/// <summary>
	/// Builds a policy name that authorizes an activity against a resource type, without narrowing to a
	/// single resource.
	/// </summary>
	/// <param name="activityName"> The grant activity the caller must hold. </param>
	/// <param name="resourceType"> The resource type the activity applies to. </param>
	/// <returns> The policy name, for example <c>grant:Approve:Order</c>. </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when an argument is null, whitespace, or contains the policy-name separator.
	/// </exception>
	public static string For(string activityName, string resourceType)
	{
		ValidateSegment(activityName, nameof(activityName));
		ValidateSegment(resourceType, nameof(resourceType));

		return string.Concat(Prefix, activityName, ":", resourceType);
	}

	/// <summary>
	/// Builds a policy name that authorizes an activity against the single resource identified by a
	/// route parameter of the matched endpoint.
	/// </summary>
	/// <param name="activityName"> The grant activity the caller must hold. </param>
	/// <param name="resourceType"> The resource type the activity applies to. </param>
	/// <param name="routeValueName">
	/// The name of the route parameter carrying the resource identifier — <c>id</c> for a route template
	/// of <c>/orders/{id}</c>.
	/// </param>
	/// <returns> The policy name, for example <c>grant:Approve:Order:{id}</c>. </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when an argument is null, whitespace, or contains the policy-name separator.
	/// </exception>
	public static string ForRouteValue(string activityName, string resourceType, string routeValueName)
	{
		ValidateSegment(activityName, nameof(activityName));
		ValidateSegment(resourceType, nameof(resourceType));
		ValidateSegment(routeValueName, nameof(routeValueName));

		return string.Concat(Prefix, activityName, ":", resourceType, ":{", routeValueName, "}");
	}

	/// <summary>
	/// Builds a policy name that authorizes an activity against one fixed resource identifier.
	/// </summary>
	/// <param name="activityName"> The grant activity the caller must hold. </param>
	/// <param name="resourceType"> The resource type the activity applies to. </param>
	/// <param name="resourceId"> The resource identifier. May itself contain the separator. </param>
	/// <returns> The policy name, for example <c>grant:Approve:Order:order-42</c>. </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when an argument is null or whitespace, when <paramref name="activityName"/> or
	/// <paramref name="resourceType"/> contains the policy-name separator, or when
	/// <paramref name="resourceId"/> is brace-wrapped, which would instead be read as a route parameter.
	/// </exception>
	public static string ForResource(string activityName, string resourceType, string resourceId)
	{
		ValidateSegment(activityName, nameof(activityName));
		ValidateSegment(resourceType, nameof(resourceType));
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		if (IsBraceWrapped(resourceId))
		{
			throw new ArgumentException(
				"A brace-wrapped value names a route parameter. Use ForRouteValue to scope by route parameter, " +
				"or supply a resource identifier that is not brace-wrapped.",
				nameof(resourceId));
		}

		return string.Concat(Prefix, activityName, ":", resourceType, ":", resourceId);
	}

	/// <summary>
	/// Parses a grant policy name into its parts.
	/// </summary>
	/// <param name="policyName"> The policy name to parse. </param>
	/// <param name="parsed"> The parsed parts when this method returns <see langword="true"/>. </param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="policyName"/> is a well-formed grant policy name;
	/// otherwise <see langword="false"/>.
	/// </returns>
	internal static bool TryParse(string? policyName, out ParsedGrantPolicy parsed)
	{
		parsed = default;

		if (policyName is null || !policyName.StartsWith(Prefix, StringComparison.Ordinal))
		{
			return false;
		}

		// Limit of 3 so a literal resource identifier may itself contain the separator (a URN, for
		// instance): it is the trailing segment and is taken verbatim.
		var parts = policyName[Prefix.Length..].Split(Separator, 3);

		if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
		{
			return false;
		}

		var activityName = parts[0];
		var resourceType = parts[1];

		if (parts.Length == 2)
		{
			parsed = new ParsedGrantPolicy(activityName, resourceType, ResourceId: null, RouteValueName: null);
			return true;
		}

		var scope = parts[2];
		if (string.IsNullOrWhiteSpace(scope))
		{
			return false;
		}

		if (IsBraceWrapped(scope))
		{
			var routeValueName = scope[1..^1];
			if (string.IsNullOrWhiteSpace(routeValueName))
			{
				return false;
			}

			parsed = new ParsedGrantPolicy(activityName, resourceType, ResourceId: null, routeValueName);
			return true;
		}

		parsed = new ParsedGrantPolicy(activityName, resourceType, scope, RouteValueName: null);
		return true;
	}

	private static bool IsBraceWrapped(string value) =>
		value.Length >= 2 && value[0] == '{' && value[^1] == '}';

	private static void ValidateSegment(string value, string parameterName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

		if (value.Contains(Separator, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				string.Create(
					CultureInfo.InvariantCulture,
					$"'{parameterName}' must not contain the '{Separator}' separator used by grant policy names."),
				parameterName);
		}
	}
}

/// <summary>
/// The parts of a parsed grant policy name.
/// </summary>
/// <param name="ActivityName"> The grant activity the caller must hold. </param>
/// <param name="ResourceType"> The resource type the activity applies to. </param>
/// <param name="ResourceId"> A fixed resource identifier, or <see langword="null"/>. </param>
/// <param name="RouteValueName">
/// The route parameter carrying the resource identifier, or <see langword="null"/>.
/// </param>
internal readonly record struct ParsedGrantPolicy(
	string ActivityName,
	string ResourceType,
	string? ResourceId,
	string? RouteValueName);
