// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using FakeItEasy.Creation;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Builds outbox-store fakes that answer capability discovery the way a real store does.
/// </summary>
/// <remarks>
/// <para>
/// The framework discovers optional store capabilities through <c>GetService</c>, never by casting, because
/// a cast sees only the outermost type and so reports a capability absent whenever the store sits behind a
/// decorator. <c>GetService</c> is a default interface member, which FakeItEasy intercepts rather than
/// running — so a fake built with <c>Implements&lt;TCapability&gt;()</c> and nothing else returns a dummy of
/// the wrong type, and the production discovery path reads that as "capability absent".
/// </para>
/// <para>
/// The consequence is worth stating plainly, because it is the opposite of the usual mock hazard: such a
/// fake is invisible to the code under test in exactly the way a correctly-decorated real store is NOT.
/// A test using one is asserting against a store the framework cannot see, so it passes or fails for
/// reasons unrelated to the behaviour it names.
/// </para>
/// </remarks>
internal static class CapabilityHonouringFakes
{
	/// <summary>
	/// Creates an <see cref="IOutboxStore"/> fake whose <c>GetService</c> answers for every capability the
	/// fake actually implements, and <see langword="null"/> for the rest — the real contract.
	/// </summary>
	/// <param name="configure">Declares the capability interfaces the fake implements, if any.</param>
	/// <returns>A fake the production capability-discovery path can see.</returns>
	public static IOutboxStore OutboxStore(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var store = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);

		A.CallTo(() => store.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(store) ? store : null);

		return store;
	}
}
