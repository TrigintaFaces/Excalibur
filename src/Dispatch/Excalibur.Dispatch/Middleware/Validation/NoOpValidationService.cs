// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// A no-operation implementation of <see cref="IValidationService" /> that always returns a successful validation result.
/// Used as the default implementation when custom validation is not configured.
/// </summary>
public sealed class NoOpValidationService : IValidationService
{
	/// <inheritdoc />
	public Task<MessageValidationResult> ValidateAsync(
		IDispatchMessage message,
		MessageValidationContext context,
		CancellationToken cancellationToken) =>
		Task.FromResult(MessageValidationResult.Success());
}
