// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Service for mapping exceptions to RFC 7807 Problem Details format.
/// </summary>
/// <remarks>
/// <para>
/// This service provides the runtime capability to convert exceptions to
/// standardized problem details. It uses the mappings configured via
/// <see cref="Configuration.IExceptionMappingBuilder"/>.
/// </para>
/// <para>
/// The service is thread-safe and designed to be registered as a singleton
/// in the dependency injection container.
/// </para>
/// </remarks>
public interface IExceptionMapper
{
	/// <summary>
	/// Synchronously maps an exception to problem details.
	/// </summary>
	/// <param name="exception"> The exception to map. </param>
	/// <returns> The problem details representing the exception. </returns>
	/// <remarks>
	/// <para>
	/// If a specific mapping exists for the exception type, it will be used.
	/// Otherwise, falls back to <see cref="Exceptions.ApiException.ToProblemDetails"/>
	/// (if the exception is an ApiException and ApiException mapping is enabled),
	/// or the default mapper.
	/// </para>
	/// <para>
	/// This method should only be used when all registered mappings are synchronous.
	/// If async mappings are registered, use <see cref="MapAsync"/> instead.
	/// </para>
	/// </remarks>
	IMessageProblemDetails Map(Exception exception);

	/// <summary>
	/// Asynchronously maps an exception to problem details.
	/// </summary>
	/// <param name="exception"> The exception to map. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A task that resolves to the problem details representing the exception. </returns>
	/// <remarks>
	/// Use this method when async mappings may be registered. It properly handles
	/// both synchronous and asynchronous mapping functions.
	/// </remarks>
	Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether the mapper can handle the specified exception type.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> <see langword="true"/> if a mapping exists for the exception; otherwise, <see langword="false"/>. </returns>
	/// <remarks>
	/// Returns <see langword="true"/> if there is a specific mapping for the exception type,
	/// or if the exception is an <see cref="Exceptions.ApiException"/> and ApiException mapping is enabled.
	/// Always returns <see langword="true"/> if a default mapper is configured.
	/// </remarks>
	bool CanMap(Exception exception);
}
