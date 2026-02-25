// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MessageResult = Excalibur.Dispatch.Messaging.MessageResult;

namespace Excalibur.Dispatch.Tests.Functional.ErrorHandling;

public sealed class SuccessfulMessageHandler
{
	public static Task<IMessageResult> HandleAsync(TestMessage message, IMessageContext context, CancellationToken cancellationToken) =>
		Task.FromResult<IMessageResult>(MessageResult.Success());
}
