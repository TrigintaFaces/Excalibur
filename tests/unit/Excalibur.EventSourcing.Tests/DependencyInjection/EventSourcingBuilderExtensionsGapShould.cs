// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Snapshots;

using Excalibur.Domain.Model;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Gap-fill tests for <see cref="EventSourcingBuilderExtensions"/> methods:
/// AddSnapshotUpgrading, UseEventStoreErasure, UseTransactionalOutboxWriter,
/// and null-guard coverage for existing snapshot methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderExtensionsGapShould
{
	private static IEventSourcingBuilder CreateBuilder()
	{
		var services = new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(services);
	}

	#region UseTransactionalOutboxWriter

	[Fact]
	public void UseTransactionalOutboxWriter_RegistersWriter()
	{
		var builder = CreateBuilder();

		_ = builder.UseTransactionalOutboxWriter<FakeOutboxWriter>();

		var sp = builder.Services.BuildServiceProvider();
		sp.GetService<ITransactionalOutboxWriter>().ShouldNotBeNull();
		sp.GetService<ITransactionalOutboxWriter>().ShouldBeOfType<FakeOutboxWriter>();
	}

	[Fact]
	public void UseTransactionalOutboxWriter_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseTransactionalOutboxWriter<FakeOutboxWriter>());
	}

	[Fact]
	public void UseTransactionalOutboxWriter_UsesTryAdd_DoesNotOverwrite()
	{
		var builder = CreateBuilder();
		var existingWriter = A.Fake<ITransactionalOutboxWriter>();
		builder.Services.AddSingleton(existingWriter);

		_ = builder.UseTransactionalOutboxWriter<FakeOutboxWriter>();

		var sp = builder.Services.BuildServiceProvider();
		sp.GetService<ITransactionalOutboxWriter>().ShouldBeSameAs(existingWriter);
	}

	#endregion

	#region AddSnapshotUpgrading

	[Fact]
	public void AddSnapshotUpgrading_RegistersSnapshotVersionManager()
	{
		var builder = CreateBuilder();
		// SnapshotVersionManager requires ILogger<SnapshotVersionManager>
		builder.Services.AddLogging();

		_ = builder.AddSnapshotUpgrading(b =>
		{
			b.SetCurrentVersion(3);
			b.EnableAutoUpgradeOnLoad();
		});

		var sp = builder.Services.BuildServiceProvider();
		sp.GetService<SnapshotVersionManager>().ShouldNotBeNull();
	}

	[Fact]
	public void AddSnapshotUpgrading_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddSnapshotUpgrading(_ => { }));
	}

	[Fact]
	public void AddSnapshotUpgrading_ThrowsOnNullConfigure()
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentNullException>(() => builder.AddSnapshotUpgrading(null!));
	}

	#endregion

	#region UseEventStoreErasure

	[Fact]
	public void UseEventStoreErasure_RegistersMapping()
	{
		var builder = CreateBuilder();

		_ = builder.UseEventStoreErasure<FakeDataSubjectMapping>();

		var sp = builder.Services.BuildServiceProvider();
		sp.GetService<IAggregateDataSubjectMapping>().ShouldNotBeNull();
	}

	[Fact]
	public void UseEventStoreErasure_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseEventStoreErasure<FakeDataSubjectMapping>());
	}

	#endregion

	#region Null guards for snapshot methods

	[Fact]
	public void UseIntervalSnapshots_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseIntervalSnapshots());
	}

	[Fact]
	public void UseTimeBasedSnapshots_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseTimeBasedSnapshots(TimeSpan.FromMinutes(5)));
	}

	[Fact]
	public void UseSizeBasedSnapshots_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseSizeBasedSnapshots(1024 * 1024));
	}

	[Fact]
	public void AddSnapshotStrategy_ThrowsOnNullBuilder()
	{
		IEventSourcingBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddSnapshotStrategy<FakeSnapshotStrategy>());
	}

	#endregion

	#region Test doubles

	private sealed class FakeOutboxWriter : ITransactionalOutboxWriter
	{
		public ValueTask StageMessageAsync(
			OutboundMessage message,
			IDbTransaction transaction,
			CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;
	}

	private sealed class FakeDataSubjectMapping : IAggregateDataSubjectMapping
	{
		public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
			string dataSubjectId,
			string? aggregateType,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AggregateReference>>(Array.Empty<AggregateReference>());
	}

#pragma warning disable IL2026,IL3050 // Test double -- AOT/trim not relevant
	private sealed class FakeSnapshotStrategy : ISnapshotStrategy
	{
		public bool ShouldCreateSnapshot(IAggregateRoot aggregate) => false;
	}
#pragma warning restore IL2026,IL3050

	#endregion
}
