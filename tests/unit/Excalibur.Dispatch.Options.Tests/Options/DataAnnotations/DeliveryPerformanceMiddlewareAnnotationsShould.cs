// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.DataAnnotations;

/// <summary>
/// Verifies DataAnnotation validation on Delivery, Performance, and Middleware Options classes.
/// Sprint 564 S564.57: Delivery + Performance + Middleware Options DataAnnotation coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeliveryPerformanceMiddlewareAnnotationsShould
{
	private static bool TryValidate(object instance, out ICollection<ValidationResult> results)
	{
		results = new List<ValidationResult>();
		return Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
	}

	#region ZeroAllocOptions

	[Fact]
	public void ZeroAlloc_Succeed_WithDefaults()
	{
		var options = new ZeroAllocOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void ZeroAlloc_Fail_WhenContextPoolSizeIsZero()
	{
		var options = new ZeroAllocOptions { ContextPoolSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ZeroAllocOptions.ContextPoolSize)));
	}

	[Fact]
	public void ZeroAlloc_Fail_WhenMaxBufferSizeIsZero()
	{
		var options = new ZeroAllocOptions { MaxBufferSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ZeroAllocOptions.MaxBufferSize)));
	}

	#endregion

	#region MicroBatchOptions

	[Fact]
	public void MicroBatch_Succeed_WithDefaults()
	{
		var options = new MicroBatchOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void MicroBatch_Fail_WhenMaxBatchSizeIsZero()
	{
		var options = new MicroBatchOptions { MaxBatchSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MicroBatchOptions.MaxBatchSize)));
	}

	#endregion

	#region ShardedExecutorOptions

	[Fact]
	public void ShardedExecutor_Succeed_WithDefaults()
	{
		var options = new ShardedExecutorOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void ShardedExecutor_Fail_WhenMaxQueueDepthIsZero()
	{
		var options = new ShardedExecutorOptions { MaxQueueDepth = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ShardedExecutorOptions.MaxQueueDepth)));
	}

	[Fact]
	public void ShardedExecutor_Fail_WhenShardCountIsNegative()
	{
		var options = new ShardedExecutorOptions { ShardCount = -1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ShardedExecutorOptions.ShardCount)));
	}

	#endregion

	#region LeakTrackingOptions

	[Fact]
	public void LeakTracking_Succeed_WithDefaults()
	{
		var options = new LeakTrackingOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void LeakTracking_Fail_WhenMaximumRetainedIsZero()
	{
		var options = new LeakTrackingOptions { MaximumRetained = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(LeakTrackingOptions.MaximumRetained)));
	}

	[Fact]
	public void LeakTracking_Fail_WhenMinimumRetainedIsNegative()
	{
		var options = new LeakTrackingOptions { MinimumRetained = -1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(LeakTrackingOptions.MinimumRetained)));
	}

	#endregion

	#region UnifiedBatchingOptions

	[Fact]
	public void UnifiedBatching_Succeed_WithDefaults()
	{
		var options = new UnifiedBatchingOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void UnifiedBatching_Fail_WhenMaxBatchSizeIsZero()
	{
		var options = new UnifiedBatchingOptions { MaxBatchSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(UnifiedBatchingOptions.MaxBatchSize)));
	}

	[Fact]
	public void UnifiedBatching_Fail_WhenMaxParallelismIsZero()
	{
		var options = new UnifiedBatchingOptions { MaxParallelism = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(UnifiedBatchingOptions.MaxParallelism)));
	}

	#endregion

	#region TenantIdentityOptions

	[Fact]
	public void TenantIdentity_Succeed_WithDefaults()
	{
		var options = new TenantIdentityOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void TenantIdentity_Fail_WhenMinTenantIdLengthIsZero()
	{
		var options = new TenantIdentityOptions { MinTenantIdLength = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(TenantIdentityOptions.MinTenantIdLength)));
	}

	#endregion

	#region InboxOptions

	[Fact]
	public void Inbox_Succeed_WithDefaults()
	{
		var options = new InboxOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Inbox_Fail_WhenPerRunTotalIsZero()
	{
		var options = new InboxOptions { PerRunTotal = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(InboxOptions.PerRunTotal)));
	}

	[Fact]
	public void Inbox_Fail_WhenQueueCapacityIsZero()
	{
		var options = new InboxOptions { QueueCapacity = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(InboxOptions.QueueCapacity)));
	}

	#endregion

	#region MessageEnvelopePoolOptions

	[Fact]
	public void MessageEnvelopePool_Succeed_WithDefaults()
	{
		var options = new MessageEnvelopePoolOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void MessageEnvelopePool_Fail_WhenThreadLocalCacheSizeIsZero()
	{
		var options = new MessageEnvelopePoolOptions { ThreadLocalCacheSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MessageEnvelopePoolOptions.ThreadLocalCacheSize)));
	}

	#endregion
}
