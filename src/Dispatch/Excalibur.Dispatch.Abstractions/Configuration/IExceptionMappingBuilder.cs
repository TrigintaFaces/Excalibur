// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Fluent builder interface for configuring exception-to-problem-details mappings.
/// </summary>
/// <remarks>
/// <para>
/// Provides a fluent API for registering mappings that convert exceptions to
/// RFC 7807 Problem Details format. Mappings are evaluated in registration order,
/// with the first matching mapper being used.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// builder.ConfigureExceptionMapping(mapping =>
/// {
///     mapping.UseApiExceptionMapping();     // Auto-map ApiException hierarchy
///     mapping.Map&lt;DbException&gt;(ex => new MessageProblemDetails { ... });
///     mapping.MapWhen&lt;HttpRequestException&gt;(
///         ex => ex.StatusCode == HttpStatusCode.NotFound,
///         ex => new MessageProblemDetails { Status = 404 });
///     mapping.MapDefault(ex => MessageProblemDetails.InternalError("An unexpected error occurred."));
/// });
/// </code>
/// </para>
/// </remarks>
public interface IExceptionMappingBuilder
{
	/// <summary>
	/// Registers a synchronous mapping for a specific exception type.
	/// </summary>
	/// <typeparam name="TException"> The type of exception to map. </typeparam>
	/// <param name="mapper"> A function that converts the exception to problem details. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// Mappings are evaluated in registration order. The first mapping that handles
	/// the exception type (or a base type) wins.
	/// </remarks>
	IExceptionMappingBuilder Map<TException>(Func<TException, IMessageProblemDetails> mapper)
		where TException : Exception;

	/// <summary>
	/// Registers an asynchronous mapping for a specific exception type.
	/// </summary>
	/// <typeparam name="TException"> The type of exception to map. </typeparam>
	/// <param name="mapper"> An async function that converts the exception to problem details. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// Use async mappings when the conversion requires I/O operations, such as
	/// looking up error details from a database or external service.
	/// </remarks>
	IExceptionMappingBuilder MapAsync<TException>(
		Func<TException, CancellationToken, Task<IMessageProblemDetails>> mapper)
		where TException : Exception;

	/// <summary>
	/// Registers a conditional mapping for a specific exception type.
	/// </summary>
	/// <typeparam name="TException"> The type of exception to map. </typeparam>
	/// <param name="predicate"> A predicate that determines if this mapping should be used. </param>
	/// <param name="mapper"> A function that converts the exception to problem details. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// Use conditional mappings when different instances of the same exception type
	/// should map to different problem details based on exception properties.
	/// </remarks>
	IExceptionMappingBuilder MapWhen<TException>(
		Func<TException, bool> predicate,
		Func<TException, IMessageProblemDetails> mapper)
		where TException : Exception;

	/// <summary>
	/// Registers a default mapping for exceptions that don't match any specific mapping.
	/// </summary>
	/// <param name="mapper"> A function that converts any exception to problem details. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// The default mapper is used as a fallback when no type-specific mapping matches.
	/// If not specified, a built-in default mapper returns a 500 Internal Server Error.
	/// </remarks>
	IExceptionMappingBuilder MapDefault(Func<Exception, IMessageProblemDetails> mapper);

	/// <summary>
	/// Enables automatic mapping of <see cref="Exceptions.ApiException"/> and derived types
	/// using their <see cref="Exceptions.ApiException.ToProblemDetails"/> method.
	/// </summary>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// When enabled, any exception that inherits from <see cref="Exceptions.ApiException"/>
	/// will automatically use its <see cref="Exceptions.ApiException.ToProblemDetails"/>
	/// method to generate problem details.
	/// </para>
	/// <para>
	/// This is enabled by default. Call this method to explicitly enable it if you
	/// previously disabled it, or to ensure the behavior in configuration.
	/// </para>
	/// </remarks>
	IExceptionMappingBuilder UseApiExceptionMapping();
}
