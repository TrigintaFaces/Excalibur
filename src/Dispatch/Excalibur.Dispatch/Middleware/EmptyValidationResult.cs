// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Empty implementation of IValidationResult for contexts with no primary context.
/// </summary>
internal sealed class EmptyValidationResult : IValidationResult
{
	/// <inheritdoc/>
	public IReadOnlyCollection<object> Errors { get; } = Array.Empty<object>();

	/// <inheritdoc/>
	public bool IsValid { get; set; } = true;

	public static IValidationResult Failed(params object[] errors) => new EmptyValidationResult { IsValid = false };

	public static IValidationResult Success() => new EmptyValidationResult { IsValid = true };
}
