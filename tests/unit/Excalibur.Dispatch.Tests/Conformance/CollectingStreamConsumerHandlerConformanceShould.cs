// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Tests.Shared.Conformance.Streaming;

namespace Excalibur.Dispatch.Tests.Conformance;

/// <summary>
/// Conformance tests for the <see cref="IStreamConsumerHandler{T}"/> streaming contract, wired with the
/// reference <see cref="CollectingStreamConsumerHandler"/> the base ships.
/// </summary>
/// <remarks>
/// Sprint 851 / <c>qxatfw</c>: this is the FIRST concrete deriver of
/// <see cref="StreamingHandlerConformanceTestBase"/> — before it, the base's 11 contract facts had ZERO
/// derivers and executed against nothing (dead-contract / false confidence). Wiring the base's own
/// reference handler makes the consume-all / in-order / empty / cancellation / chunk-metadata / large-stream
/// invariants actually run in unit CI.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Streaming")]
public sealed class CollectingStreamConsumerHandlerConformanceShould : StreamingHandlerConformanceTestBase
{
	/// <inheritdoc/>
	protected override (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler()
	{
		var handler = new CollectingStreamConsumerHandler();
		return (handler, () => handler.Processed);
	}
}
