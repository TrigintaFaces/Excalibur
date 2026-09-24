// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Excalibur.Compliance.Aws;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using AwsKeyMetadata = Amazon.KeyManagementService.Model.KeyMetadata;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// An erasure on AWS KMS with KMS-generated keys reaches a terminal state once AWS's pending-deletion window ends,
/// and never before: a CMK in PendingDeletion can still be recovered with CancelKeyDeletion, and a superseded
/// version left enabled by rotation can still decrypt.
/// </summary>
/// <remarks>
/// The fake models KMS as a set of CMKs (id to state) and aliases (name to CMK id). Deleting a CMK, as AWS does
/// at the end of its window, removes the CMK and every alias pointing at it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsKmsErasureReachesTerminalShould : IDisposable
{
	private readonly IAmazonKeyManagementService _kms = A.Fake<IAmazonKeyManagementService>();
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly AwsKmsOptions _options = new()
	{
		KeyAliasPrefix = "test-dispatch",
		Environment = "test",
		Cache = { MetadataCacheDurationSeconds = 60 },
	};

	private readonly Dictionary<string, KeyState> _cmks = [];
	private readonly Dictionary<string, string> _aliases = [];
	private readonly AwsKmsProvider _provider;

	public AwsKmsErasureReachesTerminalShould()
	{
		_provider = new AwsKmsProvider(
			_kms, Microsoft.Extensions.Options.Options.Create(_options), NullLogger<AwsKmsProvider>.Instance, _cache);

		A.CallTo(() => _kms.DescribeKeyAsync(A<DescribeKeyRequest>._, A<CancellationToken>._))
			.ReturnsLazily((DescribeKeyRequest request, CancellationToken _) =>
			{
				var cmk = _aliases.TryGetValue(request.KeyId, out var target) ? target : request.KeyId;
				if (!_cmks.TryGetValue(cmk, out var state))
				{
					throw new NotFoundException("Key not found");
				}

				return Task.FromResult(new DescribeKeyResponse
				{
					KeyMetadata = new AwsKeyMetadata
					{
						KeyId = cmk,
						KeyState = state,
						Origin = OriginType.AWS_KMS,
						CreationDate = DateTime.UtcNow.AddDays(-30),
						DeletionDate = state == KeyState.PendingDeletion ? DateTime.UtcNow.AddDays(7) : null,
					},
				});
			});
		A.CallTo(() => _kms.ScheduleKeyDeletionAsync(A<ScheduleKeyDeletionRequest>._, A<CancellationToken>._))
			.ReturnsLazily((ScheduleKeyDeletionRequest request, CancellationToken _) =>
			{
				if (_cmks[request.KeyId] == KeyState.PendingDeletion)
				{
					throw new KMSInvalidStateException("already pending deletion");
				}

				_cmks[request.KeyId] = KeyState.PendingDeletion;
				return Task.FromResult(new ScheduleKeyDeletionResponse());
			});
		A.CallTo(() => _kms.ListAliasesAsync(A<ListAliasesRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(new ListAliasesResponse
			{
				Aliases = [.. _aliases.Select(a => new AliasListEntry { AliasName = a.Key, TargetKeyId = a.Value })],
				Truncated = false,
			}));
	}

	public void Dispose()
	{
		_provider.Dispose();
		_cache.Dispose();
		_kms.Dispose();
	}

	[Fact]
	public async Task Wait_through_the_pending_window_then_complete()
	{
		var keyId = SubjectKey("aws-subject-1");
		GivenKeyWithVersions(keyId, "cmk-1");
		var harness = new ErasureLifecycleHarness(_provider, _provider);

		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("aws-subject-1");
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);

		// Inside the window: PendingDeletion, still recoverable.
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);

		AwsDeletes("cmk-1");
		_cache.Remove($"key:{keyId}");

		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(1);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
	}

	[Fact]
	public async Task Destroy_every_version_of_a_rotated_key_not_only_the_current_one()
	{
		// Rotation left cmk-1 ENABLED behind its version alias so it can still decrypt older ciphertext.
		var keyId = SubjectKey("aws-subject-2");
		GivenKeyWithVersions(keyId, "cmk-1", "cmk-2");

		var outcome = await _provider.DeleteKeyAsync(keyId, 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.ScheduledIrreversible);
		_cmks["cmk-1"].ShouldBe(KeyState.PendingDeletion, "a superseded version must be destroyed too");
		_cmks["cmk-2"].ShouldBe(KeyState.PendingDeletion);
	}

	[Fact]
	public async Task Not_complete_while_a_superseded_version_of_the_key_still_exists()
	{
		// The current CMK is deleted and its alias gone -- the name the lookup uses answers NotFound -- but the
		// previous version is still recoverable. A lookup by that name would call this destroyed.
		var keyId = SubjectKey("aws-subject-3");
		GivenKeyWithVersions(keyId, "cmk-1", "cmk-2");
		var harness = new ErasureLifecycleHarness(_provider, _provider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("aws-subject-3");

		AwsDeletes("cmk-2");
		_cache.Remove($"key:{keyId}");

		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);

		AwsDeletes("cmk-1");
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(1);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
	}

	[Fact]
	public async Task Report_an_already_pending_key_as_scheduled_without_scheduling_it_again()
	{
		var keyId = SubjectKey("aws-subject-4");
		GivenKeyWithVersions(keyId, "cmk-1");
		_cmks["cmk-1"] = KeyState.PendingDeletion;

		var outcome = await _provider.DeleteKeyAsync(keyId, 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.ScheduledIrreversible);
		A.CallTo(() => _kms.ScheduleKeyDeletionAsync(A<ScheduleKeyDeletionRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static string SubjectKey(string subjectId) => TestDataSubjectHasher.Instance.HashDataSubjectId(subjectId);

	/// <summary>Stages a logical key whose versions are the given CMKs, oldest first; the last is current.</summary>
	private void GivenKeyWithVersions(string keyId, params string[] cmks)
	{
		for (var i = 0; i < cmks.Length; i++)
		{
			_cmks[cmks[i]] = KeyState.Enabled;
			_aliases[_options.BuildVersionAlias(keyId, i + 1)] = cmks[i];
		}

		_aliases[_options.BuildKeyAlias(keyId)] = cmks[^1];
	}

	/// <summary>AWS deletes the CMK at the end of its window, and with it every alias that names it.</summary>
	private void AwsDeletes(string cmk)
	{
		_ = _cmks.Remove(cmk);
		foreach (var alias in _aliases.Where(a => a.Value == cmk).Select(a => a.Key).ToList())
		{
			_ = _aliases.Remove(alias);
		}
	}
}
