// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Erasure;

/// <summary>
/// Maps data subject identifiers to the aggregate instances that contain their personal data.
/// </summary>
/// <remarks>
/// <para>
/// Consumers MUST implement this interface to support GDPR erasure of event-sourced aggregates.
/// The implementation is application-specific because only the application knows which aggregates
/// belong to which data subjects.
/// </para>
/// <para>
/// <b>WHAT YOU RETURN HERE IS WHAT GETS DESTROYED, AND THE DESTRUCTION IS TOTAL AND IRREVERSIBLE.</b>
/// Erasure does not remove a subject's fields from the aggregates you name. It tombstones
/// <i>every event of every aggregate you name</i> &#8212; the payload is nulled and the event type is
/// replaced with a reserved marker. Nothing is selective: not by field, not by event type.
/// </para>
/// <para>
/// So if you return a transaction aggregate &#8212; an order, a sale, a service visit, a loan &#8212;
/// you lose the transaction, not just the personal data in it. The amount, the dates, the line items and
/// the vehicle or account identifier go with the customer's name. Those are frequently records you are
/// separately obliged to keep: tax records, warranty and product-recall traceability, and
/// anti-money-laundering records with multi-year retention minimums. Privacy regimes generally carve
/// such material out of the erasure right &#8212; but this framework cannot act on that carve-out,
/// because erasure here operates on whole aggregates and this interface is where you told it the
/// aggregate belonged to the subject. Consult your own counsel on what you must retain; what is stated
/// here is only what OUR mechanism does.
/// </para>
/// <para>
/// <b>Return the aggregates that hold the PERSONAL DATA, not the aggregates the subject participated
/// in.</b> Model so a Customer aggregate carries the identifying data and transaction aggregates carry a
/// reference to it; return the Customer. Erasing it then removes the person and leaves the transactions
/// standing, which is the outcome both regimes describe.
/// </para>
/// <para>
/// <b>Known gap, stated rather than left to be discovered:</b> where personal data is unavoidably
/// embedded in a transaction event &#8212; the buyer's name on an invoice &#8212; there is no supported
/// way to erase the name and keep the invoice. Whole-aggregate tombstoning is the only mechanism, so the
/// choice is to destroy the record or to retain the personal data. Returning fewer aggregates and
/// handling the embedded case in your own retention process is the honest workaround today.
/// </para>
/// <para>
/// Common strategies for locating the aggregates that hold personal data:
/// <list type="bullet">
/// <item>Query a lookup table mapping subject IDs to the aggregate that holds their personal data</item>
/// <item>Use naming conventions (e.g., the personal-data aggregate ID equals the subject ID)</item>
/// <item>Search an index or projection that tracks data subject ownership</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Returns the aggregate holding the subject's PERSONAL DATA -- not their orders.
/// // Erasing this removes the person; the orders survive, carrying only a customer reference.
/// public class CustomerAggregateMapping : IAggregateDataSubjectMapping
/// {
///     private readonly ICustomerRepository _repository;
///
///     public async Task&lt;IReadOnlyList&lt;AggregateReference&gt;&gt; GetAggregatesForDataSubjectAsync(
///         string dataSubjectIdHash, string? tenantId, CancellationToken cancellationToken)
///     {
///         var customerId = await _repository.FindIdByHashAsync(dataSubjectIdHash, cancellationToken);
///
///         return customerId is null
///             ? []
///             : [new AggregateReference(customerId, "Customer")];
///     }
/// }
///
/// // DO NOT do this unless the order aggregate genuinely holds nothing you must retain.
/// // Every event of every returned order is tombstoned: amount, dates, line items, all of it.
/// //     return orderIds.Select(id =&gt; new AggregateReference(id, "Order")).ToList();
/// </code>
/// </example>
public interface IAggregateDataSubjectMapping
{
	/// <summary>
	/// Resolves all aggregate references associated with a data subject.
	/// </summary>
	/// <param name="dataSubjectIdHash">The SHA-256 hash of the data subject identifier.</param>
	/// <param name="tenantId">The tenant ID for multi-tenant scenarios, or null.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of aggregate references that contain data for the specified data subject.</returns>
	Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Identifies an aggregate instance holding a data subject's personal data.
/// </summary>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">
/// The aggregate type name &#8212; for erasure, the type holding the subject's PERSONAL DATA
/// (e.g. "Customer", "Patient", "Employee"). Naming a transaction aggregate here destroys the whole
/// transaction; see <see cref="IAggregateDataSubjectMapping"/>.
/// </param>
public sealed record AggregateReference(string AggregateId, string AggregateType);
