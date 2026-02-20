// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Query operations for data inventory.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface of <see cref="IDataInventoryStore"/> isolates query/listing
/// concerns per the Interface Segregation Principle (ISP).
/// </para>
/// <para>
/// Consumers that only need to query data inventory can depend on this
/// interface directly. Implementations that also implement <see cref="IDataInventoryStore"/>
/// expose this interface via <c>GetService(typeof(IDataInventoryQueryStore))</c>.
/// </para>
/// </remarks>
public interface IDataInventoryQueryStore
{
	/// <summary>
	/// Finds registrations for a data subject.
	/// </summary>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="idType">The identifier type.</param>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of matching registrations.</returns>
	Task<IReadOnlyList<DataLocationRegistration>> FindRegistrationsForDataSubjectAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all discovered locations for a data subject.
	/// </summary>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of discovered locations.</returns>
	Task<IReadOnlyList<DataLocation>> GetDiscoveredLocationsAsync(
		string dataSubjectId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets data map entries for RoPA reporting.
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of data map entries.</returns>
	Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
		string? tenantId,
		CancellationToken cancellationToken);
}
