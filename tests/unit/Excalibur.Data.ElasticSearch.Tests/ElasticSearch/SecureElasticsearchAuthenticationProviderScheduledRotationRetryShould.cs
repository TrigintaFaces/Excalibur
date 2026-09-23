// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Authentication;

/// <summary>
/// Locks the scheduled credential rotation onto retrying a transient failure inside the rotation
/// interval.
/// </summary>
/// <remarks>
/// <para>
/// The timer callback used to make one attempt, catch, log and return. A single failed request therefore
/// left the credentials un-rotated until the next interval elapsed -- thirty days on the default
/// configuration -- which is the opposite of what scheduling a rotation is for.
/// </para>
/// <para>
/// The retry is a Polly pipeline, and its predicate is on the RESULT rather than on an exception:
/// <c>RotateCredentialsAsync</c> converts every fault into a failed result and never throws, so an
/// exception-keyed retry would be inert. These arms count calls into the key provider, which is the only
/// place the difference between one attempt and several is observable.
/// </para>
/// <para>
/// The delay is configured down to a millisecond so the arms assert the retry COUNT rather than any
/// wall-clock duration -- the backoff schedule is Polly's, and re-testing it here would be testing the
/// library.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "ElasticSearch.Security")]
public sealed class SecureElasticsearchAuthenticationProviderScheduledRotationRetryShould : IDisposable
{
	private readonly IElasticsearchKeyStorage _keyProvider = A.Fake<IElasticsearchKeyStorage>();
	private readonly IHttpClientFactory _httpClientFactory = A.Fake<IHttpClientFactory>();
	private readonly ILogger<SecureElasticsearchAuthenticationProvider> _logger =
		NullLogger<SecureElasticsearchAuthenticationProvider>.Instance;

	private SecureElasticsearchAuthenticationProvider? _sut;

	/// <summary>
	/// The failing rotation is retried, and a later attempt that succeeds is what ends the run.
	/// </summary>
	[Fact]
	public async Task RetryATransientFailure_AndStopOnceRotationSucceeds()
	{
		ConfigureExistingSecret();
		ConfigureKeyGeneration(false, true);

		_sut = CreateProvider(maxRetries: 3);

		await _sut.PerformScheduledRotationAsync();

		// Two attempts: the failure and the success that ended it. Three would mean the pipeline kept
		// going after a success; one would mean it never retried at all.
		AssertKeyGenerationAttempts(2);
	}

	/// <summary>
	/// Retries are bounded by the configured count, not unlimited.
	/// </summary>
	[Fact]
	public async Task StopAfterTheConfiguredNumberOfRetries_WhenEveryAttemptFails()
	{
		ConfigureExistingSecret();
		ConfigureKeyGeneration(false);

		_sut = CreateProvider(maxRetries: 2);

		await _sut.PerformScheduledRotationAsync();

		// One initial attempt plus two retries. The rotation is then abandoned until the next interval,
		// which is the behaviour a bounded retry is supposed to have.
		AssertKeyGenerationAttempts(3);
	}

	/// <summary>
	/// LIVENESS: a rotation that succeeds first time is not retried.
	/// </summary>
	/// <remarks>
	/// Without this arm the arms above are satisfied by a pipeline that runs the operation a fixed number
	/// of times regardless of outcome, which would rotate credentials repeatedly on every tick.
	/// </remarks>
	[Fact]
	public async Task NotRetry_WhenTheFirstAttemptSucceeds()
	{
		ConfigureExistingSecret();
		ConfigureKeyGeneration(true);

		_sut = CreateProvider(maxRetries: 3);

		await _sut.PerformScheduledRotationAsync();

		AssertKeyGenerationAttempts(1);
	}

	/// <summary>
	/// LIVENESS: retries can be switched off, and then a failure is a single attempt.
	/// </summary>
	[Fact]
	public async Task MakeASingleAttempt_WhenRetriesAreDisabled()
	{
		ConfigureExistingSecret();
		ConfigureKeyGeneration(false);

		_sut = CreateProvider(maxRetries: 0);

		await _sut.PerformScheduledRotationAsync();

		AssertKeyGenerationAttempts(1);
	}

	/// <inheritdoc/>
	public void Dispose() => _sut?.Dispose();

	private SecureElasticsearchAuthenticationProvider CreateProvider(int maxRetries)
	{
		var settings = new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ApiKeyId = "test-api-key-id",
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
					WarningThreshold = TimeSpan.FromDays(7),
					MaxRetries = maxRetries,
					RetryDelay = TimeSpan.FromMilliseconds(1),
				},
			},
		};

		return new SecureElasticsearchAuthenticationProvider(
			Options.Create(settings), _keyProvider, _httpClientFactory, _logger);
	}

	private void ConfigureExistingSecret() =>
		A.CallTo(() => _keyProvider.GetSecretAsync(A<string>.Ignored, A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(null));

	private void ConfigureKeyGeneration(params bool[] results)
	{
		var call = A.CallTo(() => _keyProvider.SetSecretAsync(
			"elasticsearch:apikey:test-api-key-id",
			A<string>.Ignored,
			A<SecretMetadata?>.Ignored,
			A<CancellationToken>.Ignored));

		// One answer per attempt, with the last entry repeating: a pipeline that ran one attempt too many
		// must fail on the assertion below, not by falling off the end of a sequence.
		var attempt = 0;
		_ = call.ReturnsLazily(() => Task.FromResult(results[Math.Min(attempt++, results.Length - 1)]));
	}

	private void AssertKeyGenerationAttempts(int expected) =>
		A.CallTo(() => _keyProvider.SetSecretAsync(
				"elasticsearch:apikey:test-api-key-id",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened(expected, Times.Exactly);
}
