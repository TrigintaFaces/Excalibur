// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

namespace Excalibur.Dispatch.Compat.MassTransit;

/// <summary>
/// Source-compatible shape of MassTransit's <c>IConsumer&lt;TMessage&gt;</c> so that simple message
/// consumers compile after a namespace swap, then bridge onto Excalibur.Dispatch via
/// <c>AddMassTransitConsumer</c>.
/// </summary>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
/// <remarks>
/// This is a <b>migration path</b>, not a transport port (spec OS-3). Only the deterministic
/// "consume the message" case is shimmed; advanced <see cref="ConsumeContext{TMessage}"/> capabilities
/// (Respond/Publish/Send/Redeliver) are intentionally not provided and require a documented manual
/// migration step.
/// </remarks>
public interface IConsumer<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Consumes the message carried by the supplied context.
	/// </summary>
	/// <param name="context">The consume context wrapping the message.</param>
	/// <returns>A task representing the asynchronous consume operation.</returns>
	Task Consume(ConsumeContext<TMessage> context);
}
