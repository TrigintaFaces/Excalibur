// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// An opaque handle to a provider-native transactional scope that enlists an inbox handler's own writes
/// together with the processed-mark, enabling <b>exactly-once</b> processing on document stores whose
/// transaction is not expressible as a BCL <see cref="System.Data.IDbTransaction"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the document-store analogue of the relational <see cref="ITransactionalInboxStore"/> seam. A
/// relational store can hand the handler a BCL <see cref="System.Data.IDbTransaction"/>; a document store's
/// native transaction (for example a MongoDB <c>IClientSessionHandle</c> or a Cosmos DB
/// <c>TransactionalBatch</c>) has no BCL equivalent. Rather than leak a provider type into the messaging
/// abstraction, the store hands the handler this <b>opaque</b> scope. A handler that needs to enlist its own
/// writes obtains the concrete provider handle through a provider-specific extension method shipped by the
/// provider package (for example <c>scope.AsMongoSession()</c>), keeping
/// <c>Excalibur.Dispatch.Abstractions</c> free of any data-access dependency.
/// </para>
/// <para>
/// The base contract intentionally exposes no members: it is a marker for the provider-native scope. The
/// contract lives in <c>Dispatch.Abstractions</c> because the inbox middleware consumes it; the concrete
/// implementations and the provider-cast extensions live in the Excalibur persistence layer.
/// </para>
/// </remarks>
public interface IInboxTransactionScope
{
}
