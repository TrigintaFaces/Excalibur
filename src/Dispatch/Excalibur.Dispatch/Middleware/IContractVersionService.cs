// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for managing contract versions and compatibility.
/// </summary>
public interface IContractVersionService
{
	/// <summary>
	/// Checks version compatibility for a schema.
	/// </summary>
	/// <param name="schemaId"> The schema identifier. </param>
	/// <param name="version"> The version to check. </param>
	/// <param name="supportedVersions"> List of supported versions. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Compatibility result. </returns>
	Task<VersionCompatibilityResult> CheckCompatibilityAsync(
		string schemaId,
		string version,
		string[]? supportedVersions,
		CancellationToken cancellationToken);
}
