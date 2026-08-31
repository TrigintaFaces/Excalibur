// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

namespace Excalibur.Testing.Conformance.Tests.Testing.Conformance;

/// <summary>
/// Binds the persistence conformance kit to the optionality its own interface documents: declining a
/// capability is conformant, advertising one and refusing to honour it is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why both arms exist.</b> The kit previously threw when <c>GetService</c> returned
/// <see langword="null"/> — the exact answer <see cref="IPersistenceProvider.GetService"/> documents for
/// an unsupported capability, and the one its own default implementation returns. Relaxing that alone
/// would have been worse than the defect: a kit that simply stops asserting cannot tell a provider that
/// declines cleanly from one that claims a capability and throws on first use. The first arm proves the
/// relaxation happened; the second proves it did not go too far.
/// </para>
/// <para>
/// <b>These arms ship.</b> The kit is published, so its strictness is a contract imposed on consumers
/// writing their own provider. Wrong in one direction it rejects correct providers; wrong in the other
/// it certifies broken ones.
/// </para>
/// <para>
/// <b>Fixture shape.</b> Both providers implement <see cref="IPersistenceProvider"/> directly, with no
/// first-party base supplying members, so the arms bind the interface's own requirements rather than
/// re-testing an inherited convenience.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class PersistenceKitOptionalCapabilityShould
{
    private abstract class ProviderBase(string name) : IPersistenceProvider
    {
        public string Name => name;

        public string ProviderType => "test";

        public Task<TResult> ExecuteAsync<TConnection, TResult>(
            IDataRequest<TConnection, TResult> request,
            CancellationToken cancellationToken)
            where TConnection : IDisposable => throw new NotSupportedException("not exercised here");

        public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public abstract object? GetService(Type serviceType);

        public void Dispose() => GC.SuppressFinalize(this);

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Declines both optional capabilities the documented way: by returning null.</summary>
    private sealed class DecliningProvider(string name) : ProviderBase(name)
    {
        public override object? GetService(Type serviceType) => null;
    }

    /// <summary>Advertises transactions, then refuses to honour them.</summary>
    private sealed class LyingProvider(string name) : ProviderBase(name), IPersistenceProviderTransaction
    {
        public override object? GetService(Type serviceType) =>
            serviceType == typeof(IPersistenceProviderTransaction) ? this : null;

        public string ConnectionString => "test-connection";

        public IDataRequestRetryPolicy RetryPolicy => throw new NotSupportedException("not exercised here");

        public ITransactionScope CreateTransactionScope(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            TimeSpan? timeout = null) =>
            throw new NotSupportedException("advertised but not honoured");

        public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
            IDataRequest<TConnection, TResult> request,
            ITransactionScope transactionScope,
            CancellationToken cancellationToken)
            where TConnection : IDisposable =>
            throw new NotSupportedException("advertised but not honoured");
    }

    /// <summary>
    /// The shape the capability split exists to make expressible: a document store that can describe
    /// its connection and retry policy but cannot honour an ambient transaction scope.
    /// </summary>
    /// <remarks>
    /// Before the split this provider had no truthful answer available. Both facts lived on one
    /// contract, so <c>GetService</c> could either advertise transactions it would then throw from (the
    /// <see cref="LyingProvider"/> shape below) or decline the whole contract and withdraw two members
    /// it genuinely supplies. Separating them makes the honest answer sayable.
    /// </remarks>
    private sealed class HonestDocumentStoreProvider(string name) : ProviderBase(name), IPersistenceProviderConnection
    {
        public string ConnectionString => "test-connection";

        public IDataRequestRetryPolicy RetryPolicy { get; } = new StubRetryPolicy();

        public override object? GetService(Type serviceType) =>
            serviceType == typeof(IPersistenceProviderConnection) ? this : null;
    }

    private sealed class StubRetryPolicy : IDataRequestRetryPolicy
    {
        public int MaxRetryAttempts => 3;

        public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(1);

        public bool ShouldRetry(Exception exception) => false;
    }

    /// <summary>Declares only what the honest document store actually offers.</summary>
    private sealed class HonestDocumentStoreKit : PersistenceProviderConformanceTestKit
    {
        protected override string ExpectedProviderType => "test";

        protected override IReadOnlyCollection<Type> RequiredCapabilities =>
            [typeof(IPersistenceProviderConnection)];

        protected override IPersistenceProvider CreateProvider(string providerName) => new HonestDocumentStoreProvider(providerName);
    }

    /// <summary>Declares transactions over a provider that correctly declines them.</summary>
    private sealed class KitDeclaringTransactionsOverTheHonestDocumentStore : PersistenceProviderConformanceTestKit
    {
        protected override string ExpectedProviderType => "test";

        protected override IReadOnlyCollection<Type> RequiredCapabilities =>
            [typeof(IPersistenceProviderTransaction)];

        protected override IPersistenceProvider CreateProvider(string providerName) => new HonestDocumentStoreProvider(providerName);
    }

    private sealed class DecliningKit : PersistenceProviderConformanceTestKit
    {
        /// <summary>Exposes the protected declaration so the default can be asserted from the test.</summary>
        internal IReadOnlyCollection<Type> DeclaredCapabilities => RequiredCapabilities;

        protected override string ExpectedProviderType => "test";

        protected override IPersistenceProvider CreateProvider(string providerName) => new DecliningProvider(providerName);
    }

    private sealed class LyingKit : PersistenceProviderConformanceTestKit
    {
        protected override string ExpectedProviderType => "test";

        protected override IPersistenceProvider CreateProvider(string providerName) => new LyingProvider(providerName);
    }

    /// <summary>Declares the transaction capability its provider genuinely offers.</summary>
    private sealed class DeclaringKitOverACapableProvider : PersistenceProviderConformanceTestKit
    {
        protected override string ExpectedProviderType => "test";

        protected override IReadOnlyCollection<Type> RequiredCapabilities =>
            [typeof(IPersistenceProviderTransaction)];

        protected override IPersistenceProvider CreateProvider(string providerName) => new LyingProvider(providerName);
    }

    /// <summary>Declares a capability its provider declines -- the vacuous binding this seam exists to catch.</summary>
    private sealed class DeclaringKitOverADecliningProvider : PersistenceProviderConformanceTestKit
    {
        protected override string ExpectedProviderType => "test";

        protected override IReadOnlyCollection<Type> RequiredCapabilities =>
            [typeof(IPersistenceProviderTransaction)];

        protected override IPersistenceProvider CreateProvider(string providerName) => new DecliningProvider(providerName);
    }

    /// <summary>
    /// DETECTION: a suite that never declared a capability its provider DOES offer must fail.
    /// </summary>
    /// <remarks>
    /// This is the vacuity the rest of the capability seam cannot see. LyingKit declares nothing, so every
    /// transaction arm resolves null, returns early, and reports Passed — while its provider offers
    /// transactions the whole time. Before this arm, that suite was green and had asserted nothing about
    /// the capability it was bound to.
    /// </remarks>
    [Fact]
    public void RejectASuiteThatUnderDeclaresACapabilityItsProviderOffers()
    {
        var kit = new LyingKit();

        var thrown = Should.Throw<TestFixtureAssertionException>(
            kit.ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers);

        thrown.Message.ShouldContain("IPersistenceProviderTransaction", Case.Sensitive);
        thrown.Message.ShouldContain(
            "reported Passed without asserting anything",
            Case.Sensitive,
            "the diagnostic must say what the under-declaration COST, not merely that it happened.");
    }

    /// <summary>
    /// LIVENESS: a provider that genuinely offers nothing has nothing to declare, and must still pass.
    /// </summary>
    /// <remarks>
    /// The half that keeps this arm from becoming the mandatory-declaration rule it deliberately is not.
    /// Declining every optional capability is conformant, so a minimal provider must cost its suite
    /// nothing here — otherwise the arm would fail correct implementations, which is exactly why the
    /// default was left alone.
    /// </remarks>
    [Fact]
    public void AcceptASuiteDeclaringNothingWhenItsProviderOffersNothing() =>
        Should.NotThrow(
            new DecliningKit().ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers,
            "a provider that offers nothing has nothing to declare.");

    /// <summary>LIVENESS: a correctly-declared capability must pass.</summary>
    [Fact]
    public void AcceptASuiteThatDeclaresTheCapabilityItsProviderOffers() =>
        Should.NotThrow(
            new DeclaringKitOverACapableProvider().ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers,
            "declaring exactly what the provider offers is the conformant shape.");

    [Fact]
    public void AcceptAProviderThatDeclinesAnOptionalCapability()
    {
        var kit = new DecliningKit();

        // GetService is specified as returning null when a capability is unsupported, and the interface
        // supplies a default that does exactly that. A kit failing this provider would be telling a
        // consumer their correct implementation is non-conforming.
        Should.NotThrow(
            () => kit.CreateTransactionScope_ShouldReturnNonNullScope(),
            "declining an optional capability is the documented answer, not a conformance failure.");
    }

    [Fact]
    public void StillRejectAProviderThatAdvertisesACapabilityItRefusesToHonour()
    {
        var kit = new LyingKit();

        // The safety half, and the reason the relaxation above is safe to ship. Returning a non-null
        // instance answers "yes" to the only question a consumer can ask about the capability, so the kit
        // must still exercise it. Without this arm, the fix would licence any provider to advertise
        // anything and throw at the point of use.
        _ = Should.Throw<NotSupportedException>(
            () => kit.CreateTransactionScope_ShouldReturnNonNullScope(),
            "a provider that offers a capability and then throws must still fail: the way to decline is "
            + "to return null from GetService, not to advertise and refuse.");
    }

    [Fact]
    public void FailWhenTheSuiteDeclaresACapabilityItsProviderDeclines()
    {
        var kit = new DeclaringKitOverADecliningProvider();

        // The defect this seam exists to catch. Without a declaration the arm below returns without
        // asserting and reports Passed, so a suite bound to a provider that offers nothing is
        // indistinguishable in the report from one that verified everything. Declaring the capability
        // makes the empty arm loud.
        _ = Should.Throw<TestFixtureAssertionException>(
            () => kit.CreateTransactionScope_ShouldReturnNonNullScope(),
            "a suite that declares a capability its provider declines must fail, not skip the arm.");

        _ = Should.Throw<TestFixtureAssertionException>(
            () => kit.Provider_ShouldOfferRequiredCapabilities(),
            "the dedicated arm must name the declared capability the provider declined.");
    }

    [Fact]
    public void NotFailWhenTheSuiteDeclaresACapabilityItsProviderOffers()
    {
        var kit = new DeclaringKitOverACapableProvider();

        // The liveness half. Without it the arm above is satisfied by a check that rejects everything,
        // including correct providers. This provider genuinely offers the capability through GetService,
        // so the declaration must be met rather than merely asserted against.
        Should.NotThrow(
            () => kit.Provider_ShouldOfferRequiredCapabilities(),
            "a declaration the provider satisfies must pass.");
    }

    [Fact]
    public void VerifyConnectionMetadataOnAProviderThatDeclinesTransactions()
    {
        var kit = new HonestDocumentStoreKit();

        // The liveness half of the capability split, and the whole point of it. This provider cannot
        // honour a transaction scope, and before the split saying so would have cost it the two members
        // it does supply. Both connection arms must now assert and pass.
        Should.NotThrow(
            () => kit.Provider_ShouldHaveNonNullConnectionString(),
            "declining transactions must not withdraw the connection string the provider supplies.");

        Should.NotThrow(
            () => kit.Provider_ShouldHaveNonNullRetryPolicy(),
            "declining transactions must not withdraw the retry policy the provider supplies.");

        Should.NotThrow(
            () => kit.Provider_ShouldOfferRequiredCapabilities(),
            "the provider offers the one capability its suite declares.");
    }

    [Fact]
    public void TreatTheDeclinedTransactionCapabilityAsConformantForADocumentStore()
    {
        var kit = new HonestDocumentStoreKit();

        // The safety half's counterpart: declining is the documented answer, so the scope arms return
        // without asserting rather than failing. That is only correct because the suite does not declare
        // the transaction capability -- see the next arm for what happens when it does.
        Should.NotThrow(
            () => kit.CreateTransactionScope_ShouldReturnNonNullScope(),
            "a document store that declines transactions at discovery is conformant.");
    }

    [Fact]
    public void FailWhenASuiteClaimsTransactionsForAProviderThatDeclinesThem()
    {
        var kit = new KitDeclaringTransactionsOverTheHonestDocumentStore();

        // Guards the split against being used to hide a real gap. Declining is conformant, but a suite
        // that declares the capability anyway is asserting something false, and must fail rather than
        // quietly emptying the scope arms into a green report.
        _ = Should.Throw<TestFixtureAssertionException>(
            () => kit.Provider_ShouldOfferRequiredCapabilities(),
            "declaring a capability the provider declines must fail even after the split.");
    }

    [Fact]
    public void LeaveDecliningConformantWhenTheSuiteDeclaresNothing()
    {
        var kit = new DecliningKit();

        // The default must stay permissive: declining an optional capability is the documented answer,
        // and a suite that declares nothing is opting out of the stricter check, not failing it.
        kit.DeclaredCapabilities.ShouldBeEmpty();

        Should.NotThrow(
            () => kit.Provider_ShouldOfferRequiredCapabilities(),
            "with nothing declared the capability arm must pass, preserving optionality by default.");
    }
}
