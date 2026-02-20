// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql;

using MySqlConnector;

namespace Excalibur.Data.Tests.MySql;

public sealed class FakeDisposableConnection : IDisposable
{
	public bool IsDisposed { get; private set; }

	public void Dispose() => IsDisposed = true;
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MySqlRetryPolicyShould
{
	private static MySqlRetryPolicy CreatePolicy(int maxRetry = 3)
	{
		var options = new MySqlProviderOptions
		{
			MaxRetryCount = maxRetry
		};
		return new MySqlRetryPolicy(options, EnabledTestLogger.Create());
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new MySqlRetryPolicy(null!, EnabledTestLogger.Create()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = new MySqlProviderOptions();

		Should.Throw<ArgumentNullException>(
			() => new MySqlRetryPolicy(options, null!));
	}

	[Fact]
	public async Task ResolveAsync_RetriesOnTimeout_AndEventuallySucceeds()
	{
		var policy = CreatePolicy(maxRetry: 2);
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<FakeDisposableConnection, int>>();
		var attempts = 0;

		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<FakeDisposableConnection, Task<int>>(_ =>
			{
				attempts++;
				return attempts == 1
					? throw new TimeoutException("transient")
					: Task.FromResult(123);
			}));

		var result = await policy.ResolveAsync(
			request,
			() => Task.FromResult(new FakeDisposableConnection()),
			CancellationToken.None);

		result.ShouldBe(123);
		attempts.ShouldBe(2);
	}

	[Fact]
	public void HaveCorrectMaxRetryAttempts()
	{
		var policy = CreatePolicy(5);
		policy.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void HaveCorrectBaseRetryDelay()
	{
		var policy = CreatePolicy();
		policy.BaseRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ShouldRetry_ForTimeoutException()
	{
		var policy = CreatePolicy();
		policy.ShouldRetry(new TimeoutException("Connection timed out")).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ForInvalidOperationWithTimeout()
	{
		var policy = CreatePolicy();
		policy.ShouldRetry(new InvalidOperationException("Connection Timeout expired")).ShouldBeTrue();
	}

	[Fact]
	public void ShouldNotRetry_ForArgumentException()
	{
		var policy = CreatePolicy();
		policy.ShouldRetry(new ArgumentException("Bad argument")).ShouldBeFalse();
	}

	[Fact]
	public void ShouldNotRetry_ForNotSupportedException()
	{
		var policy = CreatePolicy();
		policy.ShouldRetry(new NotSupportedException("Not supported")).ShouldBeFalse();
	}

	[Fact]
	public void ShouldNotRetry_ForInvalidOperationWithUnrelatedMessage()
	{
		var policy = CreatePolicy();
		policy.ShouldRetry(new InvalidOperationException("Connection pool exhausted")).ShouldBeFalse();
	}

	[Fact]
	public async Task ResolveAsync_ThrowsWhenRequestIsNull()
	{
		var policy = CreatePolicy();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			policy.ResolveAsync<FakeDisposableConnection, int>(
				null!,
				() => Task.FromResult(new FakeDisposableConnection()),
				CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_ThrowsWhenConnectionFactoryIsNull()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<FakeDisposableConnection, int>>();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			policy.ResolveAsync(
				request,
				null!,
				CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_ReturnsResultAndDisposesConnection()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<FakeDisposableConnection, int>>();

		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<FakeDisposableConnection, Task<int>>(_ => Task.FromResult(42)));

		FakeDisposableConnection? captured = null;
		var result = await policy.ResolveAsync(
			request,
			() =>
			{
				captured = new FakeDisposableConnection();
				return Task.FromResult(captured);
			},
			CancellationToken.None);

		result.ShouldBe(42);
		captured.ShouldNotBeNull();
		captured.IsDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task ResolveAsync_DisposesConnectionWhenResolverThrows()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<FakeDisposableConnection, int>>();

		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<FakeDisposableConnection, Task<int>>(_ => throw new InvalidOperationException("boom")));

		FakeDisposableConnection? captured = null;
		await Should.ThrowAsync<InvalidOperationException>(() =>
			policy.ResolveAsync(
				request,
				() =>
				{
					captured = new FakeDisposableConnection();
					return Task.FromResult(captured);
				},
				CancellationToken.None));

		captured.ShouldNotBeNull();
		captured.IsDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task ResolveDocumentAsync_ThrowsWhenRequestIsNull()
	{
		var policy = CreatePolicy();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			policy.ResolveDocumentAsync<FakeDisposableConnection, int>(
				null!,
				() => Task.FromResult(new FakeDisposableConnection()),
				CancellationToken.None));
	}

	[Fact]
	public async Task ResolveDocumentAsync_ThrowsWhenConnectionFactoryIsNull()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDocumentDataRequest<FakeDisposableConnection, int>>();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			policy.ResolveDocumentAsync(
				request,
				null!,
				CancellationToken.None));
	}

	[Fact]
	public async Task ResolveDocumentAsync_ReturnsResultAndDisposesConnection()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDocumentDataRequest<FakeDisposableConnection, int>>();

		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<FakeDisposableConnection, Task<int>>(_ => Task.FromResult(7)));

		FakeDisposableConnection? captured = null;
		var result = await policy.ResolveDocumentAsync(
			request,
			() =>
			{
				captured = new FakeDisposableConnection();
				return Task.FromResult(captured);
			},
			CancellationToken.None);

		result.ShouldBe(7);
		captured.ShouldNotBeNull();
		captured.IsDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task ResolveDocumentAsync_DisposesConnectionWhenResolverThrows()
	{
		var policy = CreatePolicy();
		var request = A.Fake<Excalibur.Data.Abstractions.IDocumentDataRequest<FakeDisposableConnection, int>>();

		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<FakeDisposableConnection, Task<int>>(_ => throw new InvalidOperationException("boom")));

		FakeDisposableConnection? captured = null;
		await Should.ThrowAsync<InvalidOperationException>(() =>
			policy.ResolveDocumentAsync(
				request,
				() =>
				{
					captured = new FakeDisposableConnection();
					return Task.FromResult(captured);
				},
				CancellationToken.None));

		captured.ShouldNotBeNull();
		captured.IsDisposed.ShouldBeTrue();
	}
}
