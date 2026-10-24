// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.Projections;

/// <summary>A provider-bound source identity and checkpoint store in that same source database.</summary>
internal sealed record InlineProjectionSource(string Identity, ISubscriptionCheckpointStore Checkpoints, IGlobalStreamQuery Query);
