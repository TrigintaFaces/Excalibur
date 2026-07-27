// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Indicates whether a pipeline middleware entry may be omitted from the built pipeline when it cannot be materialized from the service
/// provider.
/// </summary>
/// <remarks>
/// Criticality is decided by the code that declares the middleware, never inferred from whether its services happen to be registered. A
/// profile that names a middleware has asked for it; omitting how much protection it wants is not the same as asking for none.
/// </remarks>
public enum MiddlewareCriticality
{
	/// <summary>
	/// No criticality was stated. This is the zero value, so it is what an entry that was never given one holds, and building a pipeline
	/// from such an entry fails, naming the entry and the profile that declared it.
	/// </summary>
	/// <remarks>
	/// This value exists so that omission is loud rather than silent. It is deliberately not a synonym for either real criticality: a
	/// profile that skipped the question has not asked for best-effort behaviour, and guessing on its behalf is what this value prevents.
	/// It is never a valid criticality for a built pipeline, and no caller should state it explicitly.
	/// </remarks>
	Unspecified = 0,

	/// <summary>
	/// The middleware is skipped and logged when it cannot be materialized, and the pipeline is built without it.
	/// </summary>
	Optional = 1,

	/// <summary>
	/// The middleware must be materialized. Building the pipeline fails when it cannot be, naming the middleware and the service that is
	/// missing.
	/// </summary>
	Required = 2,
}

/// <summary>
/// Declares a single middleware within a pipeline profile, together with whether the built pipeline may omit it.
/// </summary>
/// <param name="MiddlewareType"> The middleware implementation type to include in the pipeline. </param>
/// <param name="Criticality">
/// Whether the entry may be skipped when it cannot be materialized. Callers that use this constructor and omit the argument get
/// <see cref="MiddlewareCriticality.Required" />, so a profile that declares a middleware without stating a criticality gets the protection
/// it named rather than a silent omission. Values obtained without running this constructor hold
/// <see cref="MiddlewareCriticality.Unspecified" /> instead, which no pipeline will build.
/// </param>
/// <remarks>
/// <para>
/// This type is the single authoritative declaration of a profile's middleware. A profile exposes entries and nothing else, so the
/// criticality of an entry cannot become separated from the type it applies to.
/// </para>
/// <para>
/// The default is deliberate. A profile that names a middleware and omits its criticality has asked for protection without saying how much,
/// which is materially different from declaring the entry optional. Callers that genuinely want a best-effort entry state
/// <see cref="MiddlewareCriticality.Optional" /> explicitly.
/// </para>
/// <para>
/// The constructor default governs only entries created through the constructor. A value of this type obtained without running it - the
/// default value, or an unfilled slot of an array - holds <see cref="MiddlewareCriticality.Unspecified" /> and a <see langword="null" />
/// <see cref="MiddlewareType" />. Neither is a usable entry, and building a pipeline from one fails rather than resolving it to a
/// criticality nobody stated. That is why the zero value is <see cref="MiddlewareCriticality.Unspecified" /> and not a real criticality:
/// the unsafe outcome is not reachable by omitting anything.
/// </para>
/// </remarks>
public readonly record struct MiddlewareEntry(
	Type MiddlewareType,
	MiddlewareCriticality Criticality = MiddlewareCriticality.Required);
