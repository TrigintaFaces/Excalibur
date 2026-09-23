// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Binds the registration contract for projection rebuild: the shipped interface resolves, and a host that
/// opts in without a stream to replay stops at startup instead of failing every rebuild later.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> <c>IProjectionRebuildService</c> ships in the public API and its implementation was
/// internal, complete, tested — and registered nowhere. A consumer could name the interface, inject it, and
/// receive null. These arms bind the registration that closes that, and the guard that keeps closing it from
/// being worse than leaving it open.
/// </para>
/// <para>
/// <b>Why the guard is needed at all.</b> The service resolves its collaborators from
/// <see cref="IServiceProvider"/> at call time, so the container validates none of them when the host starts.
/// Registering it naively would turn "returns null" into "reports Failed for every projection forever", which
/// is harder to diagnose, not easier.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionRebuildRegistrationShould
{
	/// <summary>
	/// LIVENESS, and the point of the whole change: the shipped interface now resolves.
	/// </summary>
	[Fact]
	public void Resolve_the_shipped_interface_once_the_host_opts_in()
	{
		using var provider = HostWith();

		_ = provider.GetService<IProjectionRebuildService>().ShouldNotBeNull(
			"IProjectionRebuildService is in the shipped public API; a consumer naming it must get an "
			+ "implementation rather than null");
	}

	/// <summary>
	/// LIVENESS. A host that opted in and has a stream to replay starts.
	/// </summary>
	[Fact]
	public void Start_cleanly_when_a_global_stream_query_is_registered()
	{
		using var provider = HostWith();

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	/// <summary>
	/// SAFETY. Opting in without a global stream query is a configuration error, and it stops the host.
	/// </summary>
	/// <remarks>
	/// Without this the service registers happily and every rebuild of every projection records Failed, on a
	/// host that looks healthy. The guard converts a permanent silent no-op into one loud startup message.
	/// </remarks>
	[Fact]
	public void Refuse_to_start_when_no_global_stream_query_is_registered()
	{
		using var provider = HostWith(globalStreamQuery: false);

		var thrown = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		thrown.Message.Contains("IGlobalStreamQuery", StringComparison.Ordinal).ShouldBeTrue(
			"the message must name the missing registration, or an operator cannot act on it");
	}

	/// <summary>
	/// SAFETY. The event serializer is a constructor dependency, and the guard stops a host that lacks it.
	/// </summary>
	/// <remarks>
	/// Without the guard this host starts: nothing resolves the rebuild service until someone asks for a
	/// rebuild, so the missing registration surfaces on first use rather than at startup. The validator
	/// resolves the service rather than listing its dependencies, so this arm also covers any constructor
	/// dependency added later.
	/// </remarks>
	[Fact]
	public void Refuse_to_start_when_a_constructor_dependency_is_missing()
	{
		using var provider = HostWith(eventSerializer: false);

		var thrown = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());

		thrown.Message.Contains(nameof(IEventSerializer), StringComparison.Ordinal).ShouldBeTrue(
			"the message must name the dependency that could not be resolved");
	}

	/// <summary>
	/// The guard is silent on a host that never opted in, so adding it cannot break an unrelated composition.
	/// </summary>
	/// <remarks>
	/// This is the arm that keeps the safety arm above from being satisfied by a guard that refuses
	/// everything. A host with no projection rebuild registered has nothing to validate and must start.
	/// </remarks>
	[Fact]
	public void Stay_silent_on_a_host_that_never_registered_projection_rebuild()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.ValidateStartupGates());
	}

	private static ServiceProvider HostWith(bool globalStreamQuery = true, bool eventSerializer = true)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (globalStreamQuery)
		{
			_ = services.AddSingleton<IGlobalStreamQuery, StubGlobalStreamQuery>();
		}

		if (eventSerializer)
		{
			_ = services.AddSingleton(A.Fake<IEventSerializer>());
		}

		_ = services.AddProjectionRebuild();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Present so the registration can be observed; it is never read by these arms.
	/// </summary>
	private sealed class StubGlobalStreamQuery : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(0L);
	}
}
