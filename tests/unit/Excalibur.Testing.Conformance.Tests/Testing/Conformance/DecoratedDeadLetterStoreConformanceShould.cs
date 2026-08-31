// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// A dead-letter store wrapper that declares no administrative facet and answers for none.
/// </summary>
/// <remarks>
/// The store it wraps does provide the facet, so this type is the shape of a wrapper written without regard
/// for capability discovery: its own type does not carry the facet and it does not defer the question to
/// what it wraps. The kit must report the facet as unreached rather than certify it in silence.
/// </remarks>
/// <param name="inner"> The store being wrapped, which does provide the administrative facet. </param>
internal sealed class AdminOpaqueDeadLetterStore(InMemoryDeadLetterStore inner) : IDeadLetterStore
{
	/// <summary> Gets the administrative facet the wrapped store provides. </summary>
	/// <value> The wrapped store's administrative facet. </value>
	public IDeadLetterStoreAdmin Admin => inner;

	/// <inheritdoc />
	public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken) =>
		inner.StoreAsync(message, cancellationToken);

	/// <inheritdoc />
	public Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken) =>
		inner.GetByIdAsync(messageId, cancellationToken);

	/// <inheritdoc />
	public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(DeadLetterFilter filter, CancellationToken cancellationToken) =>
		inner.GetMessagesAsync(filter, cancellationToken);

	/// <inheritdoc />
	public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken) =>
		inner.MarkAsReplayedAsync(messageId, cancellationToken);

	/// <inheritdoc />
	public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken) =>
		inner.DeleteAsync(messageId, cancellationToken);
}

/// <summary>
/// A dead-letter store decorator that declares no administrative facet and defers capability discovery to
/// the store it wraps -- the shape a decorator on this contract is required to have.
/// </summary>
/// <remarks>
/// The declaration list is the point of this type. A decorator's interface list is fixed at compile time
/// while the capability set of the store it wraps is not, so it cannot declare the wrapped store's
/// capabilities and must not try. Deferring through <see cref="IServiceProvider.GetService(Type)"/> is what
/// keeps the capability reachable, and it is what these arms hold the kit to.
/// </remarks>
/// <param name="inner"> The store being decorated. </param>
internal sealed class ForwardingDeadLetterStoreDecorator(IDeadLetterStore inner) : IDeadLetterStore
{
	/// <inheritdoc />
	/// <remarks>
	/// Answers for anything this decorator itself implements, then defers to the decorated store. Without the
	/// second half, wrapping would silently remove every capability the wrapper does not itself declare.
	/// </remarks>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : inner.GetService(serviceType);
	}

	/// <inheritdoc />
	public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken) =>
		inner.StoreAsync(message, cancellationToken);

	/// <inheritdoc />
	public Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken) =>
		inner.GetByIdAsync(messageId, cancellationToken);

	/// <inheritdoc />
	public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(DeadLetterFilter filter, CancellationToken cancellationToken) =>
		inner.GetMessagesAsync(filter, cancellationToken);

	/// <inheritdoc />
	public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken) =>
		inner.MarkAsReplayedAsync(messageId, cancellationToken);

	/// <inheritdoc />
	public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken) =>
		inner.DeleteAsync(messageId, cancellationToken);
}

/// <summary>
/// Holds the dead-letter conformance kit to reaching an administrative facet through a decorator, and to
/// reporting rather than concealing one it cannot reach.
/// </summary>
/// <remarks>
/// <para>
/// Every other dead-letter suite in this repository hands the kit a store it built directly. A consumer does
/// not: a store obtained from a container arrives decorated, and the kit's capability-gated arms then have
/// to find a facet the outermost type does not declare. An arm that returns early for want of a capability
/// reports exactly as an arm that ran and passed, so a decorated store could be certified without its
/// administrative surface ever being exercised.
/// </para>
/// <para>
/// The arms are deliberately opposed. A decorator that defers discovery gets the arm run with no deriver
/// involvement; a wrapper that answers for nothing gets the skip recorded; and the skip arm alone would be
/// satisfied by a kit whose administrative arms never run for anybody, which the first arm rules out.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class DecoratedDeadLetterStoreConformanceShould
{
	/// <summary>
	/// A decorator that defers capability discovery keeps the facet reachable, so the arm runs unaided.
	/// </summary>
	[Fact]
	public async Task CertifyTheAdminFacetThroughADecoratorThatDefersDiscovery()
	{
		var suite = new DecoratedSuite();

		await suite.GetCountAsync_EmptyStore_ShouldReturnZero().ConfigureAwait(false);

		suite.Skips.ShouldBeEmpty(
			"the decorator defers discovery to the store it wraps, so the kit reached the facet without the "
			+ "deriver overriding anything");
	}

	/// <summary>
	/// A wrapper that answers for nothing genuinely hides the facet, and the kit must say so rather than pass
	/// in silence.
	/// </summary>
	[Fact]
	public async Task RecordASkipWhenTheAdminFacetCannotBeReachedThroughAWrapper()
	{
		var suite = new WrappedSuite(resolveThroughTheWrapper: false);

		await suite.GetCountAsync_EmptyStore_ShouldReturnZero().ConfigureAwait(false);

		var skip = suite.Skips.ShouldHaveSingleItem();
		skip.Arm.ShouldBe(nameof(DeadLetterStoreConformanceTestKit.GetCountAsync_EmptyStore_ShouldReturnZero));
		skip.Capability.ShouldBe(typeof(IDeadLetterStoreAdmin));
	}

	/// <summary>
	/// A deriver that resolves the facet its wrapper holds gets the arm certified even when the wrapper does
	/// not defer discovery.
	/// </summary>
	[Fact]
	public async Task CertifyTheAdminFacetWhenTheDeriverResolvesItThroughTheWrapper()
	{
		var suite = new WrappedSuite(resolveThroughTheWrapper: true);

		await suite.GetCountAsync_EmptyStore_ShouldReturnZero().ConfigureAwait(false);

		suite.Skips.ShouldBeEmpty(
			"the deriver resolved the facet the wrapper holds, so the arm had everything it needed to run");
	}

	/// <summary>
	/// Keeps the arms above non-vacuous: neither wrapper may declare the facet, the store they wrap must
	/// genuinely provide one, and only the deferring decorator may surface it.
	/// </summary>
	[Fact]
	public void WrapWithoutDeclaringTheAdminFacet()
	{
		var inner = new InMemoryDeadLetterStore(
			new ConformanceAmbientTenantContext(),
			NullLogger<InMemoryDeadLetterStore>.Instance);
		var wrapped = new AdminOpaqueDeadLetterStore(inner);
		var decorated = new ForwardingDeadLetterStoreDecorator(inner);

		(inner is IDeadLetterStoreAdmin).ShouldBeTrue(
			"the wrapped store must provide the facet, or the arms would skip for want of one rather than "
			+ "for want of a way to reach it");
		// Asked of the runtime type: a direct `is` is statically known false here (CS0184), which would
		// make the guard vanish at compile time instead of failing if the wrapper ever gained the facet.
		wrapped.GetType().IsAssignableTo(typeof(IDeadLetterStoreAdmin)).ShouldBeFalse(
			"a cast that succeeds through the wrapper would make the skip arm prove nothing");
		decorated.GetType().IsAssignableTo(typeof(IDeadLetterStoreAdmin)).ShouldBeFalse(
			"a cast that succeeds through the decorator would make the first arm pass without the deferral "
			+ "under test");
		((IDeadLetterStore)decorated).GetService(typeof(IDeadLetterStoreAdmin)).ShouldBeSameAs(
			inner,
			"the deferral, not the decorator's own type, is what the first arm depends on");
		((IDeadLetterStore)wrapped).GetService(typeof(IDeadLetterStoreAdmin)).ShouldBeNull(
			"the wrapper must genuinely have no route to the facet");
	}

	private sealed class DecoratedSuite : DeadLetterStoreConformanceTestKit
	{
		public List<ConformanceArmSkip> Skips { get; } = [];

		protected override void OnArmSkipped(ConformanceArmSkip skip) => Skips.Add(skip);

		protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =>
			new ForwardingDeadLetterStoreDecorator(
				new InMemoryDeadLetterStore(ambientTenant, NullLogger<InMemoryDeadLetterStore>.Instance));
	}

	private sealed class WrappedSuite(bool resolveThroughTheWrapper) : DeadLetterStoreConformanceTestKit
	{
		public List<ConformanceArmSkip> Skips { get; } = [];

		protected override void OnArmSkipped(ConformanceArmSkip skip) => Skips.Add(skip);

		protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =>
			new AdminOpaqueDeadLetterStore(
				new InMemoryDeadLetterStore(ambientTenant, NullLogger<InMemoryDeadLetterStore>.Instance));

		protected override IDeadLetterStoreAdmin? ResolveAdminFacet(IDeadLetterStore store) =>
			resolveThroughTheWrapper && store is AdminOpaqueDeadLetterStore wrapper
				? wrapper.Admin
				: base.ResolveAdminFacet(store);
	}
}
