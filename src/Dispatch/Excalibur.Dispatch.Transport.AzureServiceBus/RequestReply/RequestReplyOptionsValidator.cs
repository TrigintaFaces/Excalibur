// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Validates <see cref="RequestReplyOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class RequestReplyOptionsValidator : IValidateOptions<RequestReplyOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RequestReplyOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Request/reply options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.ReplyQueueName))
		{
			return ValidateOptionsResult.Fail(
				"ReplyQueueName is required. Set RequestReplyOptions.ReplyQueueName to the session-enabled reply queue name.");
		}

		if (options.MaxConcurrentRequests is < 1 or > 10000)
		{
			return ValidateOptionsResult.Fail(
				$"MaxConcurrentRequests must be between 1 and 10000, but was {options.MaxConcurrentRequests}.");
		}

		return ValidateOptionsResult.Success;
	}
}
