// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.Health;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Locks that the outbox store health check can be CONSTRUCTED against the registration shape a
/// provider actually makes, rather than the shape the extension used to assume.
/// </summary>
/// <remarks>
/// <para>
/// The extension resolved <c>GetRequiredKeyedService&lt;IOutboxStoreAdmin&gt;("default")</c> on the
/// strength of a comment asserting that every outbox provider registers the admin surface under that
/// key. That was false. Of the keyed registrations providers make, the admin surface is registered by
/// one provider only; everywhere else the store class implements both interfaces and is registered as
/// <c>IOutboxStore</c>. So the health check threw at probe time on every other provider — and because
/// it threw rather than reporting, the SQL Server outbox could not be moved off its bare connectivity
/// probe without converting a lying check into a throwing one.
/// </para>
/// <para>
/// These arms therefore register ONLY what a provider registers — a keyed <c>IOutboxStore</c> whose
/// implementation also implements the admin surface — and assert the factory produces a check. An arm
/// that registered the admin surface keyed would pass against the broken code and prove nothing, which
/// is the shape of the assumption being removed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxStoreHealthCheckResolutionShould
{
	/// <summary>
	/// The provider shape: a keyed <c>IOutboxStore</c> only, no keyed admin registration.
	/// </summary>
	[Fact]
	public void ResolveWhenOnlyTheKeyedStoreIsRegistered()
	{
		var registration = SingleRegistrationFor(services =>
			services.AddKeyedSingleton<Excalibur.Dispatch.IOutboxStore>("default", (_, _) => BothInterfaces()));

		var check = registration.Factory(BuildProvider(services =>
			services.AddKeyedSingleton<Excalibur.Dispatch.IOutboxStore>("default", (_, _) => BothInterfaces())));

		_ = check.ShouldNotBeNull(
			"this is the registration shape every outbox provider but one actually makes. Before the fix "
			+ "the factory called GetRequiredKeyedService for the admin surface, which is not registered "
			+ "under that key, so constructing the check threw at probe time.");
	}

	/// <summary>
	/// The one provider that does register the admin surface keyed must keep working.
	/// </summary>
	[Fact]
	public void ResolveWhenTheKeyedAdminSurfaceIsRegistered()
	{
		var registration = SingleRegistrationFor(services =>
			services.AddKeyedSingleton<Excalibur.Dispatch.IOutboxStoreAdmin>("default", (_, _) => (Excalibur.Dispatch.IOutboxStoreAdmin)BothInterfaces()));

		var check = registration.Factory(BuildProvider(services =>
			services.AddKeyedSingleton<Excalibur.Dispatch.IOutboxStoreAdmin>("default", (_, _) => (Excalibur.Dispatch.IOutboxStoreAdmin)BothInterfaces())));

		_ = check.ShouldNotBeNull(
			"the keyed admin registration is tried FIRST and must still be honoured. Without this arm a "
			+ "fix could satisfy the store-only case by dropping the keyed admin lookup altogether, "
			+ "silently breaking the one provider the original code did work for.");
	}

	/// <summary>
	/// Nothing registered must FAIL. This is the arm that stops the fix becoming a check that
	/// constructs successfully no matter what it was handed.
	/// </summary>
	[Fact]
	public void FailWhenNeitherSurfaceIsRegistered() =>
		_ = Should.Throw<InvalidOperationException>(
			() => SingleRegistrationFor(static _ => { }).Factory(BuildProvider(static _ => { })),
			"a composition with no outbox store at all must not yield a working health check. Without "
			+ "this the two arms above are satisfied by a factory that never resolves anything.");

	private static HealthCheckRegistration SingleRegistrationFor(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		register(services);
		_ = services.AddHealthChecks().AddOutboxStoreHealthCheck();

		return services.BuildServiceProvider()
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.Single(r => r.Name == "outbox-store");
	}

	private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		register(services);
		return services.BuildServiceProvider();
	}

	// Mirrors every shipped provider: ONE object implementing BOTH surfaces. Measured at the class
	// declarations -- all eight IOutboxStore implementations in the tree also declare IOutboxStoreAdmin,
	// which is what makes the fallback cast safe. Built with FakeItEasy rather than hand-written so the
	// fixture cannot drift from either contract as members are added.
	private static Excalibur.Dispatch.IOutboxStore BothInterfaces() =>
		A.Fake<Excalibur.Dispatch.IOutboxStore>(o => o.Implements<Excalibur.Dispatch.IOutboxStoreAdmin>());
}
