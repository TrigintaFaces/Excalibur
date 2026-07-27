// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware.Versioning;

/// <summary>
/// Default <see cref="IContractVersionService" /> implementation used when no consumer-supplied service is registered.
/// </summary>
/// <remarks>
/// <para>
/// Performs a membership check of the message version against the configured supported-versions allow-list:
/// </para>
/// <list type="bullet">
/// <item> When no supported-versions list is configured, every version is treated as compatible (no constraint to enforce). </item>
/// <item> When a supported-versions list is configured, a version is compatible only if it appears in the list (ordinal comparison). </item>
/// </list>
/// <para>
/// This keeps contract-version checking a working, turnkey control on the default pipeline while remaining permissive
/// until a consumer opts in by configuring <c>SupportedVersions</c> or by registering a richer
/// <see cref="IContractVersionService" /> (e.g. a schema-registry-backed implementation).
/// </para>
/// </remarks>
internal sealed class DefaultContractVersionService : IContractVersionService
{
	/// <inheritdoc />
	public Task<VersionCompatibilityResult> CheckCompatibilityAsync(
		string schemaId,
		string version,
		string[]? supportedVersions,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// No allow-list configured => no constraint to enforce.
		if (supportedVersions is null || supportedVersions.Length == 0)
		{
			return Task.FromResult(VersionCompatibilityResult.Compatible());
		}

		// Unspecified version cannot be matched against a configured allow-list.
		if (string.IsNullOrEmpty(version))
		{
			return Task.FromResult(VersionCompatibilityResult.Unknown(
				ErrorConstants.EventVersionNotSpecifiedAndExplicitVersionsRequired));
		}

		foreach (var supported in supportedVersions)
		{
			if (string.Equals(supported, version, StringComparison.Ordinal))
			{
				return Task.FromResult(VersionCompatibilityResult.Compatible());
			}
		}

		return Task.FromResult(VersionCompatibilityResult.Incompatible(
			$"Version '{version}' is not in the configured supported-versions list for schema '{schemaId}'."));
	}
}
