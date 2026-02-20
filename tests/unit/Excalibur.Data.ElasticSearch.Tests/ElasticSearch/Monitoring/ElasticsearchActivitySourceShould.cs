// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchActivitySourceShould : IDisposable
{
	private readonly ElasticsearchActivitySource _sut;

	public ElasticsearchActivitySourceShould()
	{
		_sut = new ElasticsearchActivitySource();
	}

	[Fact]
	public void CreateWithDefaultName()
	{
		_sut.Source.ShouldNotBeNull();
		_sut.Source.Name.ShouldBe("Excalibur.Data.ElasticSearch");
	}

	[Fact]
	public void CreateWithCustomName()
	{
		using var source = new ElasticsearchActivitySource("custom.source", "2.0");
		source.Source.Name.ShouldBe("custom.source");
		source.Source.Version.ShouldBe("2.0");
	}

	[Fact]
	public void StartOperationReturnsNullWithoutListener()
	{
		// Without an ActivityListener, StartActivity returns null
		var activity = _sut.StartOperation("search", "test-index", "doc-123");
		activity.ShouldBeNull();
	}

	[Fact]
	public void StartResilienceOperationReturnsNullWithoutListener()
	{
		var activity = _sut.StartResilienceOperation("retry", "search");
		activity.ShouldBeNull();
	}

	[Fact]
	public void StartResilienceOperationWithAdditionalAttributes()
	{
		var attrs = new Dictionary<string, object>
		{
			["retry.attempt"] = 1,
			["retry.max_attempts"] = 3,
		};
		var activity = _sut.StartResilienceOperation("retry", "search", attrs);
		// Without listener, returns null
		activity.ShouldBeNull();
	}

	[Fact]
	public void EnrichWithRetryDetailsHandlesNullActivity()
	{
		// Should not throw for null activity
		ElasticsearchActivitySource.EnrichWithRetryDetails(null, 1, 3, TimeSpan.FromMilliseconds(100),
			new InvalidOperationException("test"));
	}

	[Fact]
	public void EnrichWithCircuitBreakerDetailsHandlesNullActivity()
	{
		ElasticsearchActivitySource.EnrichWithCircuitBreakerDetails(null, "open", failureCount: 5, successCount: 10);
	}

#pragma warning disable IL2026
#pragma warning disable IL3050
	[Fact]
	public void EnrichWithRequestDetailsHandlesNullActivity()
	{
		ElasticsearchActivitySource.EnrichWithRequestDetails(null, new object());
	}

	[Fact]
	public void EnrichWithResponseDetailsHandlesNullActivity()
	{
		ElasticsearchActivitySource.EnrichWithResponseDetails(null, null!);
	}
#pragma warning restore IL3050
#pragma warning restore IL2026

	[Fact]
	public void ThrowObjectDisposedExceptionForStartOperationAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.StartOperation("search"));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionForStartResilienceAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.StartResilienceOperation("retry"));
	}

	[Fact]
	public void HandleDoubleDispose()
	{
		_sut.Dispose();
		_sut.Dispose(); // Should not throw
	}

	[Fact]
	public void EnrichWithRetryDetailsWithAllParameters()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Data.ElasticSearch",
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		var activity = _sut.StartOperation("search");
		ElasticsearchActivitySource.EnrichWithRetryDetails(
			activity, 2, 5, TimeSpan.FromMilliseconds(500), new TimeoutException("timeout"));

		if (activity != null)
		{
			activity.GetTagItem("elasticsearch.retry.attempt").ShouldBe(2);
			activity.GetTagItem("elasticsearch.retry.max_attempts").ShouldBe(5);
			activity.GetTagItem("elasticsearch.retry.delay_ms").ShouldBe(500.0);
			activity.GetTagItem("elasticsearch.retry.exception.type").ShouldBe("TimeoutException");
		}

		activity?.Dispose();
	}

	[Fact]
	public void EnrichWithCircuitBreakerDetailsWithAllParameters()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Data.ElasticSearch",
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		var activity = _sut.StartOperation("index");
		ElasticsearchActivitySource.EnrichWithCircuitBreakerDetails(activity, "open", 10, 50);

		if (activity != null)
		{
			activity.GetTagItem("elasticsearch.circuit_breaker.state").ShouldBe("open");
			activity.GetTagItem("elasticsearch.circuit_breaker.failure_count").ShouldBe(10);
			activity.GetTagItem("elasticsearch.circuit_breaker.success_count").ShouldBe(50);
		}

		activity?.Dispose();
	}

	[Fact]
	public void StartOperationWithListenerSetsCorrectTags()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Data.ElasticSearch",
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = _sut.StartOperation("search", "my-index", "doc-456");

		activity.ShouldNotBeNull();
		activity.GetTagItem("db.system").ShouldBe("elasticsearch");
		activity.GetTagItem("db.operation").ShouldBe("search");
		activity.GetTagItem("db.name").ShouldBe("my-index");
		activity.GetTagItem("elasticsearch.index.name").ShouldBe("my-index");
		activity.GetTagItem("elasticsearch.document.id").ShouldBe("doc-456");
		activity.GetTagItem("elasticsearch.operation.type").ShouldBe("search");
	}

	[Fact]
	public void StartOperationWithoutOptionalParameters()
	{
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Excalibur.Data.ElasticSearch",
			Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = _sut.StartOperation("delete");

		activity.ShouldNotBeNull();
		activity.GetTagItem("db.system").ShouldBe("elasticsearch");
		activity.GetTagItem("db.name").ShouldBeNull();
		activity.GetTagItem("elasticsearch.document.id").ShouldBeNull();
	}

	public void Dispose() => _sut.Dispose();
}
