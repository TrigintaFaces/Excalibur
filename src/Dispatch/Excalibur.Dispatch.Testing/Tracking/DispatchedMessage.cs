// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Tracking;

/// <summary>
/// Records a single message that was dispatched through the test harness pipeline.
/// </summary>
/// <param name="Message">The dispatched message.</param>
/// <param name="Context">The message context at the time of dispatch.</param>
/// <param name="Timestamp">When the dispatch occurred (UTC).</param>
/// <param name="Result">The result returned by the pipeline, or <see langword="null"/> if not yet available.</param>
public sealed record DispatchedMessage(
	IDispatchMessage Message,
	IMessageContext Context,
	DateTimeOffset Timestamp,
	IMessageResult? Result);
