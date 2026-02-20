// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SimpleObjectPoolShould
{
	[Fact]
	public void CreateNewObjectWhenPoolIsEmpty()
	{
		// Arrange
		var pool = new SimpleObjectPool<StringBuilder>(
			() => new StringBuilder(),
			sb => sb.Clear());

		// Act
		var item = pool.Rent();

		// Assert
		item.ShouldNotBeNull();
		item.ShouldBeOfType<StringBuilder>();
	}

	[Fact]
	public void ReturnSameObjectAfterReturn()
	{
		// Arrange
		var pool = new SimpleObjectPool<StringBuilder>(
			() => new StringBuilder(),
			sb => sb.Clear());
		var item = pool.Rent();
		item.Append("test");

		// Act
		pool.Return(item);
		var reused = pool.Rent();

		// Assert — same instance reused, reset action applied
		reused.ShouldBeSameAs(item);
		reused.Length.ShouldBe(0); // Clear was called
	}

	[Fact]
	public void GetIsAliasForRent()
	{
		// Arrange
		var callCount = 0;
		var pool = new SimpleObjectPool<StringBuilder>(
			() =>
			{
				callCount++;
				return new StringBuilder();
			},
			sb => sb.Clear());

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
		var pool = new SimpleObjectPool<StringBuilder>(
			() => new StringBuilder(),
			_ => resetCalled = true);
		var item = pool.Rent();

		// Act
		pool.Return(item);

		// Assert
		resetCalled.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenObjectGeneratorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SimpleObjectPool<StringBuilder>(null!, sb => sb.Clear()));
	}

	[Fact]
	public void ThrowWhenResetActionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SimpleObjectPool<StringBuilder>(() => new StringBuilder(), null!));
	}

	[Fact]
	public void HandleMultipleRentAndReturn()
	{
		// Arrange
		var created = 0;
		var pool = new SimpleObjectPool<StringBuilder>(
			() =>
			{
				Interlocked.Increment(ref created);
				return new StringBuilder();
			},
			sb => sb.Clear());

		// Act — rent multiple, return, re-rent
		var a = pool.Rent();
		var b = pool.Rent();
		pool.Return(a);
		pool.Return(b);
		var c = pool.Rent();
		var d = pool.Rent();

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
		var pool = new SimpleObjectPool<StringBuilder>(
			() =>
			{
				Interlocked.Increment(ref created);
				return new StringBuilder();
			},
			sb => sb.Clear());

		// Act — rent 3 without returning
		var a = pool.Rent();
		var b = pool.Rent();
		var c = pool.Rent();

		// Assert — each one should be a fresh creation
		created.ShouldBe(3);
		a.ShouldNotBeSameAs(b);
		b.ShouldNotBeSameAs(c);
	}
}
