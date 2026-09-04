// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Locks the startup guard that reports a domain event declaring no message name.
/// </summary>
/// <remarks>
/// Writing an event needs its declared name, so without this the failure arrives inside AppendAsync,
/// mid-transaction. The liveness arm matters more than the safety arm here: a guard that refused every
/// host would satisfy "it catches the undeclared type" and brick every correct consumer, and this
/// assembly contains declared events for it to pass over.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UndeclaredEventNameStartupGuardShould
{
	private static IEnumerable<IStartupPrerequisiteValidator> ValidatorsFor(params Type[] registered)
	{
		var services = new ServiceCollection();
		_ = services.AddEventTypes(registered);
		return services.BuildServiceProvider().GetServices<IStartupPrerequisiteValidator>();
	}

	[Fact]
	public void BeRegisteredWheneverAnyEventTypeIs()
	{
		// The guard is wired by the registration path itself, so a consumer cannot end up without it
		// by forgetting to opt in -- which is the same forgetting it exists to catch.
		ValidatorsFor(typeof(DeclaredDomainEventFixture))
			.ShouldContain(v => v.GetType().Name == "UndeclaredEventNameStartupValidator");
	}

	[Fact]
	public void ReportTheUndeclaredEventsInAnAssemblyItWasPointedAt()
	{
		// SAFETY. Registering one declared event names this test assembly, which also contains the
		// undeclared type below -- exactly the shape of a consumer who registered the events they
		// remembered and left a sibling out.
		var validator = ValidatorsFor(typeof(DeclaredDomainEventFixture))
			.Single(v => v.GetType().Name == "UndeclaredEventNameStartupValidator");

		var exception = Should.Throw<InvalidOperationException>(validator.Validate);

		exception.Message.ShouldContain(nameof(UndeclaredDomainEventFixture));
		exception.Message.ShouldContain("MessageName");
	}

	[Fact]
	public void SayNothingAboutAnEventThatDeclaresItsName()
	{
		// LIVENESS, first half: the declared type must not appear in the report. Without this, a guard
		// that listed every event would pass the safety arm above.
		var validator = ValidatorsFor(typeof(DeclaredDomainEventFixture))
			.Single(v => v.GetType().Name == "UndeclaredEventNameStartupValidator");

		var exception = Should.Throw<InvalidOperationException>(validator.Validate);

		// Dot-prefixed: the undeclared fixture's own name ENDS with the declared one's, so a bare
		// substring check can never pass and would look like a guard defect rather than a test bug.
		exception.Message.ShouldNotContain("." + nameof(DeclaredDomainEventFixture));
		exception.Message.ShouldStartWith("1 domain event type(s)");
	}

	[Fact]
	public void AllowAHostThatRegistersNoEventTypes()
	{
		// LIVENESS, second half: with nothing registered there is no assembly to infer, and the host
		// must still start. This is the arm that fails if the guard ever refuses unconditionally --
		// and an empty allow-list already has its own dedicated guard with a better message.
		var validator = new UndeclaredEventNameStartupValidator(new EventTypeRegistry());

		Should.NotThrow(validator.Validate);
	}

	[Fact]
	public void ReportOnlyTheEventsThatActuallyLackAName()
	{
		// LIVENESS, third half -- selectivity. This assembly holds many declared events; a guard that
		// listed every event it found would satisfy the safety arm and be useless. The report must
		// name the one undeclared fixture and no declared sibling.
		var validator = ValidatorsFor(typeof(DeclaredDomainEventFixture))
			.Single(v => v.GetType().Name == "UndeclaredEventNameStartupValidator");

		var exception = Should.Throw<InvalidOperationException>(validator.Validate);

		exception.Message.ShouldNotContain("Test.Shared.DeclaredDomainEventFixture");
		exception.Message.ShouldNotContain("customer-created");
	}

}
