// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.ErrorHandling;

public sealed class FailingMessageHandler
{
	public static Task<IMessageResult> HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		throw new InvalidOperationException("Processing failed for message: " + message.Content);
	}
}
