// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Query class for <see cref="Grant" /> aggregates.
/// </summary>
/// <remarks>
/// Provides criteria for querying Grant aggregates via <see cref="IEventSourcedRepository{TAggregate, TKey}.QueryAsync{TQuery}"/>.
/// </remarks>
public sealed class GrantQuery : IAggregateQuery<Grant>;
