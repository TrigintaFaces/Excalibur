// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Functional.ErrorHandling;

public sealed class SlowMessageHandler
{
	public static async Task<IMessageResult> HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
		return MessageResult.Success();
	}
}
