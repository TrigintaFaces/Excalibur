// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Storage abstraction for data inventory registrations.
/// </summary>
/// <remarks>
/// <para>
/// The data inventory supports:
/// - Automatic discovery from [PersonalData] attributed fields
/// - Manual registration for custom data locations
/// - Data mapping for GDPR RoPA (Records of Processing Activities)
/// </para>
/// <para>
/// Query operations (FindRegistrationsForDataSubject, GetDiscoveredLocations, GetDataMapEntries)
/// are available via <see cref="IDataInventoryQueryStore"/>, accessed through <see cref="GetService"/>.
/// </para>
/// </remarks>
public interface IDataInventoryStore
{
	/// <summary>
	/// Saves a data location registration.
	/// </summary>
	/// <param name="registration">The registration to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveRegistrationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Removes a data location registration.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <param name="fieldName">The field name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the registration was removed.</returns>
	Task<bool> RemoveRegistrationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all registrations.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of all registrations.</returns>
	Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a discovered data location for a data subject.
	/// </summary>
	/// <param name="location">The discovered location.</param>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task RecordDiscoveredLocationAsync(
		DataLocation location,
		string dataSubjectId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="IDataInventoryQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
