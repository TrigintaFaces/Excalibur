// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MessageResult = Excalibur.Dispatch.Messaging.MessageResult;

namespace Excalibur.Dispatch.Tests.Functional.ErrorHandling;

public sealed class SlowMessageHandler
{
	public static async Task<IMessageResult> HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(true);
		return MessageResult.Success();
	}
}
