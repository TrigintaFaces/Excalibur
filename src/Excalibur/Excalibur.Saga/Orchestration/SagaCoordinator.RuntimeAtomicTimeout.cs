// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch;

namespace Excalibur.Saga.Orchestration;

public sealed partial class SagaCoordinator
{
	/// <summary>Explicitly admits a fresh application-created timeout; never use as the default inbound receiver.</summary>
	/// <param name="messageContext">A new nonowned delivery context with a nonempty ID.</param>
	/// <param name="evt">The registered timeout event.</param>
	/// <param name="cancellationToken">The caller cancellation token.</param>
	/// <returns>The atomic timeout processing operation.</returns>
	[RequiresUnreferencedCode("Runtime saga and timeout dispatch select registered types reflectively.")]
	[RequiresDynamicCode("Runtime saga and timeout dispatch construct closed generic types.")]
	public Task ProcessManualAtomicTimeoutAsync(IMessageContext messageContext, ISagaEvent evt, CancellationToken cancellationToken)
		=> ProcessEventCoreAsync(messageContext, evt, manualTimeout: true, cancellationToken);

}
