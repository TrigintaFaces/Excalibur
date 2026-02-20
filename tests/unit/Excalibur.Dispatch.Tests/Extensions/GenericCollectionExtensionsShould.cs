// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Tests.Extensions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GenericCollectionExtensionsShould
{
	// --- AsList ---

	[Fact]
	public void AsList_FromArray_ReturnNewList()
	{
		// Arrange
		int[] source = [1, 2, 3];

		// Act
		var result = source.AsList();

		// Assert
		result.ShouldBe([1, 2, 3]);
		result.ShouldBeOfType<List<int>>();
	}

	[Fact]
	public void AsList_FromList_ReturnNewList()
	{
		// Arrange
		var source = new List<int> { 1, 2, 3 };

		// Act
		var result = source.AsList();

		// Assert
		result.ShouldBe([1, 2, 3]);
		result.ShouldNotBeSameAs(source);
	}

	[Fact]
	public void AsList_FromCollection_ReturnList()
	{
		// Arrange
		ICollection<int> source = new Collection<int> { 1, 2, 3 };

		// Act
		var result = ((IEnumerable<int>)source).AsList();

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public void AsList_FromEnumerable_ReturnList()
	{
		// Arrange
		static IEnumerable<int> Generate()
		{
			yield return 1;
			yield return 2;
			yield return 3;
		}

		// Act
		var result = Generate().AsList();

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public void AsList_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).AsList());
	}

	[Fact]
	public async Task AsListAsync_ConvertAsyncResult()
	{
		// Arrange
		var task = Task.FromResult<IEnumerable<int>>([1, 2, 3]);

		// Act
		var result = await task.AsListAsync().ConfigureAwait(false);

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task AsListAsync_ThrowOnNullTask()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((Task<IEnumerable<int>>)null!).AsListAsync()).ConfigureAwait(false);
	}

	// --- AsCollection ---

	[Fact]
	public void AsCollection_FromList_ReturnCollection()
	{
		// Arrange
		var source = new List<int> { 1, 2, 3 };

		// Act
		var result = source.AsCollection();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldContain(1);
		result.ShouldContain(2);
		result.ShouldContain(3);
	}

	[Fact]
	public void AsCollection_FromExistingCollection_ReturnNewCollection()
	{
		// Arrange
		var source = new Collection<int> { 1, 2, 3 };

		// Act
		var result = ((IEnumerable<int>)source).AsCollection();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldNotBeSameAs(source);
	}

	[Fact]
	public void AsCollection_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).AsCollection());
	}

	[Fact]
	public async Task AsCollectionAsync_ConvertAsyncResult()
	{
		// Arrange
		var task = Task.FromResult<IEnumerable<int>>([1, 2, 3]);

		// Act
		var result = await task.AsCollectionAsync().ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
	}

	// --- AsReadOnlyCollection ---

	[Fact]
	public void AsReadOnlyCollection_FromList_ReturnReadOnly()
	{
		// Arrange
		var source = new List<int> { 1, 2, 3 };

		// Act
		var result = source.AsReadOnlyCollection();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldBeOfType<ReadOnlyCollection<int>>();
	}

	[Fact]
	public void AsReadOnlyCollection_FromReadOnlyCollection_ReturnNewCopy()
	{
		// Arrange
		var source = new ReadOnlyCollection<int>([1, 2, 3]);

		// Act
		var result = source.AsReadOnlyCollection();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldNotBeSameAs(source);
	}

	[Fact]
	public void AsReadOnlyCollection_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).AsReadOnlyCollection());
	}

	[Fact]
	public async Task AsReadOnlyCollectionAsync_ConvertAsyncResult()
	{
		// Arrange
		var task = Task.FromResult<IEnumerable<int>>([1, 2, 3]);

		// Act
		var result = await task.AsReadOnlyCollectionAsync().ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
	}

	// --- AsArray ---

	[Fact]
	public void AsArray_FromArray_ReturnClone()
	{
		// Arrange
		int[] source = [1, 2, 3];

		// Act
		var result = source.AsArray();

		// Assert
		result.ShouldBe([1, 2, 3]);
		result.ShouldNotBeSameAs(source);
	}

	[Fact]
	public void AsArray_FromCollection_ReturnArray()
	{
		// Arrange
		ICollection<int> source = new List<int> { 1, 2, 3 };

		// Act
		var result = ((IEnumerable<int>)source).AsArray();

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public void AsArray_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).AsArray());
	}

	[Fact]
	public async Task AsArrayAsync_ConvertAsyncResult()
	{
		// Arrange
		var task = Task.FromResult<IEnumerable<int>>([1, 2, 3]);

		// Act
		var result = await task.AsArrayAsync().ConfigureAwait(false);

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	// --- AsReadOnlyList ---

	[Fact]
	public void AsReadOnlyList_FromReadOnlyList_ReturnNewCopy()
	{
		// Arrange
		IReadOnlyList<int> source = [1, 2, 3];

		// Act
		var result = ((IEnumerable<int>)source).AsReadOnlyList();

		// Assert
		result.Count.ShouldBe(3);
	}

	[Fact]
	public void AsReadOnlyList_FromList_ReturnReadOnlyList()
	{
		// Arrange
		IList<int> source = [1, 2, 3];

		// Act
		var result = ((IEnumerable<int>)source).AsReadOnlyList();

		// Assert
		result.Count.ShouldBe(3);
	}

	[Fact]
	public void AsReadOnlyList_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).AsReadOnlyList());
	}

	[Fact]
	public async Task AsReadOnlyListAsync_ConvertAsyncResult()
	{
		// Arrange
		var task = Task.FromResult<IEnumerable<int>>([1, 2, 3]);

		// Act
		var result = await task.AsReadOnlyListAsync().ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
	}

	// --- IsNullOrEmpty ---

	[Fact]
	public void IsNullOrEmpty_Null_ReturnTrue()
	{
		// Act & Assert
		((IEnumerable<int>?)null).IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_EmptyArray_ReturnTrue()
	{
		// Act & Assert
		Array.Empty<int>().IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_EmptyList_ReturnTrue()
	{
		// Act & Assert
		new List<int>().IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_EmptyReadOnlyCollection_ReturnTrue()
	{
		// Act & Assert
		new ReadOnlyCollection<int>([]).IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_NonEmptyList_ReturnFalse()
	{
		// Act & Assert
		new List<int> { 1 }.IsNullOrEmpty().ShouldBeFalse();
	}

	[Fact]
	public void IsNullOrEmpty_NonEmptyEnumerable_ReturnFalse()
	{
		// Arrange
		static IEnumerable<int> Generate()
		{
			yield return 42;
		}

		// Act & Assert
		Generate().IsNullOrEmpty().ShouldBeFalse();
	}

	[Fact]
	public void IsNullOrEmpty_EmptyEnumerable_ReturnTrue()
	{
		// Arrange
		static IEnumerable<int> Generate()
		{
			yield break;
		}

		// Act & Assert
		Generate().IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_EmptyString_ReturnTrue()
	{
		// Note: string implements IEnumerable<char>
		string.Empty.IsNullOrEmpty().ShouldBeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_NonEmptyString_ReturnFalse()
	{
		"hello".IsNullOrEmpty().ShouldBeFalse();
	}
}
