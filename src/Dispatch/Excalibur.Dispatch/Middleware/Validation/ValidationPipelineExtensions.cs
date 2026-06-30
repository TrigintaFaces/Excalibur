// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// Extension methods for adding validation middleware to the dispatch pipeline.
/// </summary>
public static class ValidationPipelineExtensions
{
	/// <summary>
	/// Adds the validation <em>middleware only</em> to the dispatch pipeline, without registering any
	/// validator infrastructure.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// <strong>Precondition (loud):</strong> this method adds <see cref="ValidationMiddleware"/> but does
	/// <em>not</em> register the validator resolver or any <c>IValidator&lt;T&gt;</c>. If no validator
	/// infrastructure is registered, the middleware has nothing to run and messages pass through
	/// unvalidated. Use this overload only when you are wiring validator infrastructure yourself (e.g.
	/// <c>services.AddDispatchValidation()</c> plus your own <c>IValidator&lt;T&gt;</c> registrations).
	/// </para>
	/// <para>
	/// For the batteries-included path that registers the validator infrastructure <em>and</em> the
	/// middleware in one call, use
	/// <see cref="Excalibur.Dispatch.Validation.ValidationDispatchBuilderExtensions.UseValidation(Excalibur.Dispatch.Configuration.IDispatchBuilder)"/>.
	/// </para>
	/// <para>
	/// The validation middleware runs registered <c>IValidator&lt;T&gt;</c> implementations against the
	/// incoming message before passing it to downstream handlers. If validation fails, a
	/// <see cref="Excalibur.Dispatch.Exceptions.ValidationException"/> is thrown.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseValidationMiddleware() // Validate early, before authorization (validators registered separately)
	///        .UseAuthorization()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseValidationMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<ValidationMiddleware>();
	}
}
