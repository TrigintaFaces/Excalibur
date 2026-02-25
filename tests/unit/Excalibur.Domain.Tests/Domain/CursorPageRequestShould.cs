// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="CursorPageRequest{TCursor}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class CursorPageRequestShould
{
	[Fact]
	public void Constructor_SetsPageSize()
	{
		// Arrange & Act
		var request = new TestCursorPageRequest(25, PageNavigation.First);

		// Assert
		request.PageSize.ShouldBe(25);
	}

	[Fact]
	public void Constructor_SetsNavigation()
	{
		// Arrange & Act
		var request = new TestCursorPageRequest(10, PageNavigation.Next);

		// Assert
		request.Navigation.ShouldBe(PageNavigation.Next);
	}

	[Fact]
	public void Deconstruct_ReturnsAllComponents()
	{
		// Arrange
		var request = new TestCursorPageRequest(20, PageNavigation.Previous)
		{
			Cursor = "cursor-value",
		};

		// Act
		request.Deconstruct(out var pageSize, out var navigation, out var cursor);

		// Assert
		pageSize.ShouldBe(20);
		navigation.ShouldBe(PageNavigation.Previous);
		cursor.ShouldBe("cursor-value");
	}

	[Fact]
	public void Deconstruct_ReturnsNullCursor_WhenNotSet()
	{
		// Arrange
		var request = new TestCursorPageRequest(10, PageNavigation.First);

		// Act
		request.Deconstruct(out _, out _, out var cursor);

		// Assert
		cursor.ShouldBeNull();
	}

	[Fact]
	public void WorksWithDifferentNavigationValues()
	{
		// Assert all navigation types work
		var first = new TestCursorPageRequest(10, PageNavigation.First);
		var previous = new TestCursorPageRequest(10, PageNavigation.Previous);
		var next = new TestCursorPageRequest(10, PageNavigation.Next);
		var last = new TestCursorPageRequest(10, PageNavigation.Last);

		first.Navigation.ShouldBe(PageNavigation.First);
		previous.Navigation.ShouldBe(PageNavigation.Previous);
		next.Navigation.ShouldBe(PageNavigation.Next);
		last.Navigation.ShouldBe(PageNavigation.Last);
	}

	[Fact]
	public void WorksWithIntegerCursor()
	{
		// Arrange & Act
		var request = new IntCursorPageRequest(10, PageNavigation.Next) { Cursor = 42 };

		// Assert
		request.Deconstruct(out _, out _, out var cursor);
		cursor.ShouldBe(42);
	}

	[Fact]
	public void WorksWithGuidCursor()
	{
		// Arrange
		var expectedCursor = Guid.NewGuid();

		// Act
		var request = new GuidCursorPageRequest(10, PageNavigation.Next) { Cursor = expectedCursor };

		// Assert
		request.Deconstruct(out _, out _, out var cursor);
		cursor.ShouldBe(expectedCursor);
	}

	/// <summary>
	/// Test implementation with string cursor.
	/// </summary>
	private sealed class TestCursorPageRequest : CursorPageRequest<string>
	{
		public TestCursorPageRequest(int pageSize, PageNavigation navigation)
			: base(pageSize, navigation)
		{
		}

		public string? Cursor { get; set; }

		protected override string? GetCursor() => Cursor;
	}

	/// <summary>
	/// Test implementation with int cursor.
	/// </summary>
	private sealed class IntCursorPageRequest : CursorPageRequest<int?>
	{
		public IntCursorPageRequest(int pageSize, PageNavigation navigation)
			: base(pageSize, navigation)
		{
		}

		public int? Cursor { get; set; }

		protected override int? GetCursor() => Cursor;
	}

	/// <summary>
	/// Test implementation with Guid cursor.
	/// </summary>
	private sealed class GuidCursorPageRequest : CursorPageRequest<Guid?>
	{
		public GuidCursorPageRequest(int pageSize, PageNavigation navigation)
			: base(pageSize, navigation)
		{
		}

		public Guid? Cursor { get; set; }

		protected override Guid? GetCursor() => Cursor;
	}
}
