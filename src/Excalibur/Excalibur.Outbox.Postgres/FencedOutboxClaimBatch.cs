// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Postgres;

/// <summary>Internal buffered claim messages and the fence decision from the same statement.</summary>
internal sealed record FencedOutboxClaimBatch(long HighWater, IReadOnlyList<IOutboxMessage> Messages);
