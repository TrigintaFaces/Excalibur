// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.Persistence;
using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Behavioural contract for the <c>GetService</c> capability probe on <see cref="IPersistenceProvider"/>
/// and <see cref="ICloudNativeEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Both interfaces document the return as "the service instance, or null if not supported". Consumers and
/// the conformance kits read a null as "this implementation does not provide the capability" and skip the
/// work that depends on it. A default that answers null unconditionally therefore reports "not supported"
/// for a capability the implementation demonstrably supports, and the skip reads as a pass.
/// </para>
/// <para>
/// The stubs below deliberately do NOT override <c>GetService</c>. That is the whole point: they exercise
/// the default interface implementation, which is the member an implementer inherits by saying nothing.
/// Each capability is covered by a matched pair -- an implementation that provides it must answer, one that
/// does not must decline -- so neither arm passes on an implementation that always answers or always
/// declines.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait(TraitNames.Feature, TestFeatures.Abstractions)]
public sealed class GetServiceCapabilityContractShould : UnitTestBase
{
	[Fact]
	public void AnswerForACapabilityThePersistenceProviderImplements()
	{
		IPersistenceProvider provider = new HealthyProvider();

		provider.GetService(typeof(IPersistenceProviderHealth)).ShouldBeSameAs(
			provider,
			"the provider implements IPersistenceProviderHealth, so a null here reports 'not supported' "
			+ "for a capability it supports and every consumer that probes before using it silently "
			+ "skips the health check");
	}

	[Fact]
	public void DeclineACapabilityThePersistenceProviderDoesNotImplement()
	{
		IPersistenceProvider provider = new BareProvider();

		provider.GetService(typeof(IPersistenceProviderHealth)).ShouldBeNull(
			"answering for a capability the provider does not implement would hand the caller an instance "
			+ "that fails on first use");
	}

	[Fact]
	public void AnswerForItsOwnInterfaceFromThePersistenceProviderDefault()
	{
		IPersistenceProvider provider = new BareProvider();

		provider.GetService(typeof(IPersistenceProvider)).ShouldBeSameAs(provider);
	}

	[Fact]
	public void RejectANullServiceTypeFromThePersistenceProviderDefault()
	{
		IPersistenceProvider provider = new BareProvider();

		_ = Should.Throw<ArgumentNullException>(() => provider.GetService(null!));
	}

	[Fact]
	public void AnswerForACapabilityTheCloudNativeEventStoreImplements()
	{
		ICloudNativeEventStore store = new InfoBearingCloudStore();

		store.GetService(typeof(ICloudNativeEventStoreInfo)).ShouldBeSameAs(
			store,
			"the store implements ICloudNativeEventStoreInfo, so a null here reports 'not supported' for "
			+ "a capability it supports");
	}

	[Fact]
	public void DeclineACapabilityTheCloudNativeEventStoreDoesNotImplement()
	{
		ICloudNativeEventStore store = new InfoBearingCloudStore();

		store.GetService(typeof(ICloudNativeEventStoreChangeFeed)).ShouldBeNull(
			"the store does not implement the change feed, and advertising it would hand the caller a "
			+ "subscription source that cannot subscribe");
	}

	[Fact]
	public void RejectANullServiceTypeFromTheCloudNativeEventStoreDefault()
	{
		ICloudNativeEventStore store = new InfoBearingCloudStore();

		_ = Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}

	private sealed class BareProvider : IPersistenceProvider
	{
		public string Name => nameof(BareProvider);

		public string ProviderType => "Test";

		public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public void Dispose()
		{
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class HealthyProvider : IPersistenceProvider, IPersistenceProviderHealth
	{
		public string Name => nameof(HealthyProvider);

		public string ProviderType => "Test";

		public bool IsAvailable => true;

		public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(true);

		public Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>(StringComparer.Ordinal));

		public void Dispose()
		{
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class InfoBearingCloudStore : ICloudNativeEventStore, ICloudNativeEventStoreInfo
	{
		public Task<CloudEventLoadResult> LoadAsync(
			string aggregateId,
			string aggregateType,
			IPartitionKey partitionKey,
			IConsistencyOptions? consistencyOptions,
			CancellationToken cancellationToken) => Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudEventLoadResult> LoadFromVersionAsync(
			string aggregateId,
			string aggregateType,
			IPartitionKey partitionKey,
			long fromVersion,
			IConsistencyOptions? consistencyOptions,
			CancellationToken cancellationToken) => Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudAppendResult> AppendAsync(
			string aggregateId,
			string aggregateType,
			IPartitionKey partitionKey,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			CancellationToken cancellationToken) => Task.FromResult(CloudAppendResult.CreateSuccess(0, 0));

		public Task<long> GetCurrentVersionAsync(
			string aggregateId,
			string aggregateType,
			IPartitionKey partitionKey,
			CancellationToken cancellationToken) => Task.FromResult(-1L);
	}
}
