// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain;
using Excalibur.Domain.Extensions;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Hosting;

/// <summary>
/// The single definition of what an unconfigured application context defaults to.
/// </summary>
/// <remarks>
/// <para>
/// This type exists because the defaults were previously applied in one place and validated in
/// another. The host-builder path read the configuration section into a detached dictionary, filled
/// the gaps, and handed that to the static context; the options path bound from
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> directly, which never saw those
/// additions because they had been made to a copy. The result was an entry point that computed a
/// perfectly good default and then failed startup validation for the absence of it.
/// </para>
/// <para>
/// Both paths now derive their values here, so the two cannot disagree about what "unset" means
/// without this file changing. That is the point: the previous defect was not a wrong default, it
/// was two defaults maintained independently.
/// </para>
/// </remarks>
internal static class ApplicationContextDefaults
{
	/// <summary>The configuration key holding the application name.</summary>
	internal const string ApplicationNameKey = "ApplicationName";

	/// <summary>The configuration key holding the application system name.</summary>
	internal const string ApplicationSystemNameKey = "ApplicationSystemName";

	/// <summary>
	/// The application name to use when configuration supplies none: the host environment's own
	/// application name, which is the assembly name unless the host was told otherwise.
	/// </summary>
	/// <param name="environment">The host environment.</param>
	/// <returns>A non-empty application name.</returns>
	internal static string ApplicationName(IHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		return environment.ApplicationName;
	}

	/// <summary>
	/// The system name to use when configuration supplies none: the application name in
	/// kebab-case, which is the form the rest of the framework uses for identifiers that appear in
	/// URLs, cache keys and stored records.
	/// </summary>
	/// <param name="environment">The host environment.</param>
	/// <returns>A non-empty system name.</returns>
	internal static string ApplicationSystemName(IHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		return environment.ApplicationName.ToKebabCaseLower(clean: true);
	}

	/// <summary>
	/// Fills any unset required value on <paramref name="options"/> from the host environment.
	/// </summary>
	/// <remarks>
	/// Only blank values are replaced, so an explicit configuration value always wins. This runs
	/// after binding and before validation, which is the order that makes the validator's job
	/// "is this usable" rather than "did the consumer type it out".
	/// </remarks>
	/// <param name="options">The options to complete.</param>
	/// <param name="environment">The host environment supplying the defaults.</param>
	internal static void Apply(ApplicationContextOptions options, IHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(environment);

		if (string.IsNullOrWhiteSpace(options.ApplicationName))
		{
			options.ApplicationName = ApplicationName(environment);
		}

		if (string.IsNullOrWhiteSpace(options.ApplicationSystemName))
		{
			options.ApplicationSystemName = ApplicationSystemName(environment);
		}
	}
}
