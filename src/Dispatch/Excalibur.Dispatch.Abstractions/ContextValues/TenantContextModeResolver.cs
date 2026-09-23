// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch;

/// <summary>
/// Resolves <see cref="ITenantContext"/> from the contributed modes: the highest precedence wins.
/// </summary>
/// <remarks>
/// This is the single place that decides the tenant context. Every contributing package only adds its mode,
/// so the answer depends on which modes are present and never on the order they were added in.
/// </remarks>
internal static class TenantContextModeResolver
{
	/// <summary>Resolves the tenant context.</summary>
	/// <param name="services">The root service provider.</param>
	/// <returns>The context of the highest-precedence mode.</returns>
	/// <exception cref="InvalidOperationException">Thrown when two different modes share the highest precedence.</exception>
	public static ITenantContext Resolve(IServiceProvider services)
	{
		var modes = services.GetServices<ITenantContextMode>().ToArray();

		if (FindConflict(modes) is { } conflict)
		{
			throw new InvalidOperationException(conflict);
		}

		// The single-tenant mode is always contributed with the resolver, so there is at least one.
		return modes.MaxBy(static m => m.Precedence)!.Create(services);
	}

	/// <summary>Describes two different modes that share the highest precedence, if there are any.</summary>
	/// <param name="modes">The contributed modes.</param>
	/// <returns>A description naming both modes, or <see langword="null"/> when the choice is unambiguous.</returns>
	public static string? FindConflict(IReadOnlyCollection<ITenantContextMode> modes)
	{
		if (modes.Count == 0)
		{
			return null;
		}

		var top = modes.Max(static m => m.Precedence);
		var tied = modes
			.Where(m => m.Precedence == top)
			.Select(static m => m.GetType())
			.Distinct()
			.ToArray();

		return tied.Length < 2
			? null
			: $"Tenant-context modes {string.Join(" and ", tied.Select(static t => t.FullName))} both have "
				+ $"precedence {top}, so neither can be chosen over the other. Remove one of them, or give them "
				+ "different precedences.";
	}
}

/// <summary>
/// Reports two tenant-context modes of equal highest precedence at startup, before anything resolves the
/// tenant.
/// </summary>
internal sealed class TenantContextModeValidator(IEnumerable<ITenantContextMode> modes)
	: IValidateOptions<TenantContextOptions>
{
	private readonly ITenantContextMode[] _modes = [.. modes];

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TenantContextOptions options) =>
		TenantContextModeResolver.FindConflict(_modes) is { } conflict
			? ValidateOptionsResult.Fail(conflict)
			: ValidateOptionsResult.Success;
}

/// <summary>
/// The single-tenant default: always present, and chosen only when no other mode is.
/// </summary>
internal sealed class SingleTenantContextMode : ITenantContextMode
{
	/// <inheritdoc />
	public int Precedence => 0;

	/// <inheritdoc />
	public ITenantContext Create(IServiceProvider services) => new SingleTenantContext();
}
