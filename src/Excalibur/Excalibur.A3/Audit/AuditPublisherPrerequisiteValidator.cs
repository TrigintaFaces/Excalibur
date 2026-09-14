// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Audit.Internal;

/// <summary>
/// Marker options type whose only purpose is to give the audit wiring a <c>ValidateOnStart()</c> anchor.
/// It carries no settings: the thing being validated is the shape of the container, not a value a
/// consumer configured.
/// </summary>
internal sealed class AuditWiringOptions;

/// <summary>
/// The single source of truth for the missing-publisher message, shared by the startup validator and by
/// the middleware factory. Two callers enforce the same prerequisite at two moments, and a consumer who
/// hits either one must read the same sentence.
/// </summary>
internal static class AuditPublisherPrerequisite
{
	/// <summary>
	/// Gets the message shown when <c>AddExcaliburAudit</c> was called without an
	/// <see cref="IAuditMessagePublisher"/> registration.
	/// </summary>
	/// <value> A message naming the prerequisite, the registration that required it, and how to satisfy it. </value>
	internal static string MissingPublisherMessage =>
		"AddExcaliburAudit requires an IAuditMessagePublisher, and none was found in the container. "
		+ "Audit records are built for every IAmAuditable command that flows through the dispatch "
		+ "pipeline, and the framework deliberately does not choose their destination for you. "
		+ "Register one alongside the audit call, for example "
		+ "services.AddSingleton<IAuditMessagePublisher, MyAuditPublisher>(), or remove the audit "
		+ "registration if this host does not audit.";
}

/// <summary>
/// Fails host startup when <c>AddExcaliburAudit</c> has been called without an
/// <see cref="IAuditMessagePublisher"/> registered.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddExcaliburAudit</c> registers every sibling the audit middleware needs with <c>TryAdd</c>
/// defaults, with exactly one deliberate exception: the publisher. The destination for an audit record
/// is a product decision (Kafka, SNS, EventHubs, a SIEM), so the framework refuses to invent one — which
/// makes the publisher a non-defaultable prerequisite rather than a missing default.
/// </para>
/// <para>
/// <b>Why this exists as its own check rather than letting the container complain.</b> Without it, a host
/// that omits the publisher starts cleanly and fails later, at the first resolution of the middleware,
/// with the container's own "unable to resolve service" text. That message names the interface but says
/// nothing about <em>which registration</em> required it, so the consumer learns that something wanted an
/// <see cref="IAuditMessagePublisher"/> and not that <c>AddExcaliburAudit</c> is what asked. Worse, the
/// failure surfaces wherever the first auditable command happens to be dispatched, which may be far from
/// startup and may be in production.
/// </para>
/// <para>
/// <b>This is also why the conformance arms passed while the check did not exist.</b> Their declared
/// fragment is the interface name, and the platform's own failure message contains the interface name, so
/// the arms could not distinguish our fail-fast from .NET falling over. The kit now rejects the platform's
/// message by its signature; this validator supplies the message it is looking for instead.
/// </para>
/// </remarks>
internal sealed class AuditPublisherPrerequisiteValidator : IValidateOptions<AuditWiringOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditPublisherPrerequisiteValidator"/> class.
	/// </summary>
	/// <param name="services"> The container whose registrations are inspected. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is <see langword="null"/>. </exception>
	public AuditPublisherPrerequisiteValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AuditWiringOptions options)
	{
		// GetService, not GetRequiredService: resolving a missing service here would throw the container's
		// own message and defeat the entire point of this class.
		if (_services.GetService<IAuditMessagePublisher>() is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(AuditPublisherPrerequisite.MissingPublisherMessage);
	}
}
