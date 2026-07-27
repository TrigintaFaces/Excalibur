// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Security lock for the read-side PII redaction default: the dashboard read API must NOT leak a
/// dead-letter entry's exception message / correlation id or a saga instance's owning tenant id on an
/// open, unauthenticated read. Those fields are redacted (omitted) by default and only surface when the
/// host explicitly opts in via <see cref="DashboardOptions.ExposeSensitiveData"/> behind an authorized
/// boundary.
/// </summary>
/// <remarks>
/// Drives the real committed <c>MapDashboardApi</c> read modules end-to-end through an in-process
/// ASP.NET Core <see cref="TestServer"/> with fake stores that return entries carrying sensitive values.
/// Non-vacuous: default (redact) asserts the fields are absent; opt-in asserts they are present — so a
/// regression that passes the raw values through fails the default case. Never skipped, no Docker.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class DashboardSensitiveDataRedactionShould
{
	private const string SecretException = "SSN 123-45-6789 not found";
	private const string SecretCorrelation = "user-4021-order-77";
	private const string SecretTenant = "tenant-acme";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildHostAsync(bool exposeSensitive)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.ExposeSensitiveData = exposeSensitive);
		builder.Services.AddSingleton<IDeadLetterQueue>(new FakeDeadLetterQueue());
		builder.Services.AddSingleton<ISagaStoreAdmin>(new FakeSagaStoreAdmin());

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task RedactDlqExceptionMessageAndCorrelationIdByDefault()
	{
		await using var app = await BuildHostAsync(exposeSensitive: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var body = await client.GetStringAsync(
			new Uri("/dashboard/api/dlq/entries", UriKind.Relative)).ConfigureAwait(false);

		body.ShouldNotContain(SecretException);
		body.ShouldNotContain(SecretCorrelation);
		using var doc = JsonDocument.Parse(body);
		var entry = doc.RootElement[0];
		entry.TryGetProperty("exceptionMessage", out _).ShouldBeFalse("exceptionMessage must be omitted");
		entry.TryGetProperty("correlationId", out _).ShouldBeFalse("correlationId must be omitted");
		// Non-sensitive fields still flow.
		entry.GetProperty("messageType").GetString().ShouldBe("OrderPlaced");
	}

	[Fact]
	public async Task RedactSagaTenantIdByDefault()
	{
		await using var app = await BuildHostAsync(exposeSensitive: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var body = await client.GetStringAsync(
			new Uri("/dashboard/api/saga/instances", UriKind.Relative)).ConfigureAwait(false);

		body.ShouldNotContain(SecretTenant);
		using var doc = JsonDocument.Parse(body);
		doc.RootElement[0].TryGetProperty("tenantId", out _).ShouldBeFalse("tenantId must be omitted");
	}

	[Fact]
	public async Task ExposeDlqSensitiveFieldsWhenOptedIn()
	{
		await using var app = await BuildHostAsync(exposeSensitive: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var entries = await client.GetFromJsonAsync<JsonElement[]>(
			new Uri("/dashboard/api/dlq/entries", UriKind.Relative), JsonOptions).ConfigureAwait(false);

		entries.ShouldNotBeNull();
		entries[0].GetProperty("exceptionMessage").GetString().ShouldBe(SecretException);
		entries[0].GetProperty("correlationId").GetString().ShouldBe(SecretCorrelation);
	}

	[Fact]
	public async Task ExposeSagaTenantIdWhenOptedIn()
	{
		await using var app = await BuildHostAsync(exposeSensitive: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var instances = await client.GetFromJsonAsync<JsonElement[]>(
			new Uri("/dashboard/api/saga/instances", UriKind.Relative), JsonOptions).ConfigureAwait(false);

		instances.ShouldNotBeNull();
		instances[0].GetProperty("tenantId").GetString().ShouldBe(SecretTenant);
	}

	private sealed class FakeDeadLetterQueue : IDeadLetterQueue
	{
		public Task<Guid> EnqueueAsync<T>(
			T message,
			DeadLetterReason reason,
			CancellationToken cancellationToken,
			Exception? exception = null,
			IDictionary<string, string>? metadata = null) => Task.FromResult(Guid.NewGuid());

		public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
			CancellationToken cancellationToken,
			DeadLetterQueryFilter? filter = null,
			int limit = 100)
		{
			IReadOnlyList<DeadLetterEntry> entries =
			[
				new DeadLetterEntry
				{
					Id = Guid.NewGuid(),
					MessageType = "OrderPlaced",
					Payload = [],
					Reason = DeadLetterReason.MaxRetriesExceeded,
					ExceptionMessage = SecretException,
					CorrelationId = SecretCorrelation,
					EnqueuedAt = DateTimeOffset.UtcNow,
					OriginalAttempts = 3,
				},
			];
			return Task.FromResult(entries);
		}

		public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult<DeadLetterEntry?>(null);

		public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
			Task.FromResult(1L);
	}

	private sealed class FakeSagaStoreAdmin : ISagaStoreAdmin
	{
		public ValueTask<IReadOnlyList<SagaInstanceSummary>> QuerySagasAsync(
			SagaQueryFilter filter,
			CancellationToken cancellationToken)
		{
			IReadOnlyList<SagaInstanceSummary> summaries =
			[
				new SagaInstanceSummary
				{
					SagaId = Guid.NewGuid(),
					SagaType = "OrderSaga",
					IsCompleted = false,
					TenantId = SecretTenant,
					Version = 1,
				},
			];
			return ValueTask.FromResult(summaries);
		}

		public ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken) =>
			ValueTask.FromResult<SagaInstanceSummary?>(null);

		public ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(new SagaStoreStatistics());
	}
}
