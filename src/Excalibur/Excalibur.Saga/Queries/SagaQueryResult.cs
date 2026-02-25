// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Queries;

/// <summary>
/// Represents the result of a saga correlation query.
/// </summary>
/// <param name="SagaId">The unique saga instance identifier.</param>
/// <param name="SagaName">The saga type name.</param>
/// <param name="Status">The current saga status.</param>
/// <param name="CorrelationId">The correlation identifier associated with the saga.</param>
/// <param name="CreatedAt">The timestamp when the saga was created.</param>
public sealed record SagaQueryResult(
	string SagaId,
	string SagaName,
	SagaStatus Status,
	string CorrelationId,
	DateTimeOffset CreatedAt);
