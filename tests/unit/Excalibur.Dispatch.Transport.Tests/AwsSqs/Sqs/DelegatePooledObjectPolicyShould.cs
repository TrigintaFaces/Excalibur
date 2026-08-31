// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class DelegatePooledObjectPolicyShould
{
	private static DefaultObjectPool<StringBuilder> CreatePool(
		Func<StringBuilder>? factory = null,
		Action<StringBuilder>? reset = null) =>
		new(new DelegatePooledObjectPolicy<StringBuilder>(
			factory ?? (() => new StringBuilder()),
			reset ?? (sb => sb.Clear())));

	[Fact]
	public void CreateNewObjectWhenPoolIsEmpty()
	{
		// Arrange
		var pool = CreatePool();

		// Act
		var item = pool.Get();

		// Assert
		item.ShouldNotBeNull();
		item.ShouldBeOfType<StringBuilder>();
	}

	[Fact]
	public void ReturnSameObjectAfterReturn()
	{
		// Arrange
		var pool = CreatePool();
		var item = pool.Get();
		_ = item.Append("test");

		// Act
		pool.Return(item);
		var reused = pool.Get();

		// Assert — same instance reused, reset action applied
		reused.ShouldBeSameAs(item);
		reused.Length.ShouldBe(0); // Clear was called
	}

	[Fact]
	public void InvokeFactoryOnceForFirstGet()
	{
		// Arrange
		var callCount = 0;
		var pool = CreatePool(() =>
		{
			callCount++;
			return new StringBuilder();
		});

		// Act
		var item = pool.Get();

		// Assert
		item.ShouldNotBeNull();
		callCount.ShouldBe(1);
	}

	[Fact]
	public void InvokeResetActionOnReturn()
	{
		// Arrange
		var resetCalled = false;
		var policy = new DelegatePooledObjectPolicy<StringBuilder>(
			() => new StringBuilder(),
			_ => resetCalled = true);
		var pool = new DefaultObjectPool<StringBuilder>(policy);
		var item = pool.Get();

		// Act
		pool.Return(item);

		// Assert
		resetCalled.ShouldBeTrue();
	}

	[Fact]
	public void RejectNullOnReturnWithoutInvokingReset()
	{
		// Arrange
		var resetCalled = false;
		var policy = new DelegatePooledObjectPolicy<StringBuilder>(
			() => new StringBuilder(),
			_ => resetCalled = true);

		// Act
		var retained = policy.Return(null!);

		// Assert
		retained.ShouldBeFalse();
		resetCalled.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenFactoryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new DelegatePooledObjectPolicy<StringBuilder>(null!, sb => sb.Clear()));
	}

	[Fact]
	public void ThrowWhenResetActionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new DelegatePooledObjectPolicy<StringBuilder>(() => new StringBuilder(), null!));
	}

	[Fact]
	public void HandleMultipleGetAndReturn()
	{
		// Arrange
		var created = 0;
		var pool = CreatePool(() =>
		{
			_ = Interlocked.Increment(ref created);
			return new StringBuilder();
		});

		// Act — get multiple, return, re-get
		var a = pool.Get();
		var b = pool.Get();
		pool.Return(a);
		pool.Return(b);
		var c = pool.Get();
		var d = pool.Get();

		// Assert — should reuse from pool
		created.ShouldBe(2);
		c.ShouldNotBeNull();
		d.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewObjectsWhenPoolDepleted()
	{
		// Arrange
		var created = 0;
		var pool = CreatePool(() =>
		{
			_ = Interlocked.Increment(ref created);
			return new StringBuilder();
		});

		// Act — get 3 without returning
		var a = pool.Get();
		var b = pool.Get();
		var c = pool.Get();

		// Assert — each one should be a fresh creation
		created.ShouldBe(3);
		a.ShouldNotBeSameAs(b);
		b.ShouldNotBeSameAs(c);
	}
}
