// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Storage abstraction for legal holds.
/// </summary>
/// <remarks>
/// Legal holds support GDPR Article 17(3) exceptions
/// that block erasure when data must be retained for legal reasons.
/// </remarks>
public interface ILegalHoldStore
{
	/// <summary>
	/// Saves a new legal hold.
	/// </summary>
	/// <param name="hold">The legal hold to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a legal hold by ID.
	/// </summary>
	/// <param name="holdId">The hold ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The legal hold, or null if not found.</returns>
	Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates a legal hold.
	/// </summary>
	/// <param name="hold">The hold with updated values.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the hold was updated.</returns>
	Task<bool> UpdateHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="ILegalHoldQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
