namespace Excalibur;

/// <summary>
///     Represents a paginated result for a collection of items, with metadata about pagination.
/// </summary>
/// <typeparam name="T"> The type of the items in the paginated result. </typeparam>
public class PageableResult<T>
	where T : class
{
	/// <summary>
	///     Initializes a new instance of the <see cref="PageableResult{T}" /> class.
	/// </summary>
	/// <param name="items"> The collection of items for the current page. </param>
	/// <param name="pageNumber"> The current page number (1-based). Optional. Defaults to 1 if not provided. </param>
	/// <param name="pageSize"> The number of items per page. Optional. Defaults to the count of <paramref name="items" />. </param>
	/// <param name="totalItems"> The total number of items across all pages. Optional. Defaults to the count of <paramref name="items" />. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="items" /> is null. </exception>
	/// <exception cref="ArgumentException">
	///     Thrown when the provided pagination arguments are inconsistent or invalid (e.g., page number or page size is less than or equal
	///     to zero).
	/// </exception>
	public PageableResult(IEnumerable<T> items, int? pageNumber = null, int? pageSize = null, long? totalItems = null)
	{
		IList<T> enumerable;
		Items = enumerable = items switch
		{
			null => throw new ArgumentNullException(nameof(items)),
			IList<T> list => list,
			_ => items.ToList()
		};

		if (pageNumber is not null && pageSize is not null)
		{
			if (pageNumber <= 0)
			{
				throw new ArgumentException(
					$"The '{nameof(pageNumber)}' argument must me greater than zero, and must be used in conjunction with the '{pageSize}' argument.",
					nameof(pageNumber));
			}

			if (pageSize <= 0)
			{
				throw new ArgumentException(
					$"The '{nameof(pageSize)}' argument must me greater than zero, and must be used in conjunction with the '{pageNumber}' argument.",
					nameof(pageSize));
			}
		}

		if (pageNumber is null && pageSize is not null)
		{
			throw new ArgumentException(
				$"The '{nameof(pageNumber)}' argument must me greater than zero, and must be used in conjunction with the '{pageSize}' argument.",
				nameof(pageNumber));
		}

		if (pageNumber is not null && pageSize is null)
		{
			throw new ArgumentException(
				$"The '{nameof(pageSize)}' argument must me greater than zero, and must be used in conjunction with the '{pageNumber}' argument.",
				nameof(pageSize));
		}

		if (totalItems is not null && enumerable!.Count > totalItems)
		{
			throw new ArgumentException(
				$"The '{nameof(totalItems)}' argument must me greater than or equal to the count of '{enumerable}' argument.",
				nameof(totalItems));
		}

		PageNumber = pageNumber ?? 1;
		PageSize = pageSize ?? enumerable.Count;
		TotalItems = totalItems ?? enumerable.Count;
	}

	/// <summary>
	///     Gets the items for the current page.
	/// </summary>
	public IList<T> Items { get; }

	/// <summary>
	///     Gets the current page number (1-based).
	/// </summary>
	public int PageNumber { get; }

	/// <summary>
	///     Gets the number of items per page.
	/// </summary>
	public int PageSize { get; }

	/// <summary>
	///     Gets the total number of items across all pages.
	/// </summary>
	public long TotalItems { get; }

	/// <summary>
	///     Gets the total number of pages, calculated based on the page size.
	/// </summary>
	public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

	/// <summary>
	///     Indicates whether there is a next page available.
	/// </summary>
	public bool HasNextPage => PageNumber < TotalPages;

	/// <summary>
	///     Indicates whether there is a previous page available.
	/// </summary>
	public bool HasPreviousPage => PageNumber > 1;

	/// <summary>
	///     Indicates whether the current page is the first page.
	/// </summary>
	public bool IsFirstPage => PageNumber == 1;

	/// <summary>
	///     Indicates whether the current page is the last page.
	/// </summary>
	public bool IsLastPage => PageNumber == TotalPages;

	/// <summary>
	///     Gets the item at the specified index in the current page.
	/// </summary>
	/// <param name="index"> The zero-based index of the item to retrieve. </param>
	/// <returns> The item at the specified index. </returns>
	public T this[int index] => Items[index];

	/// <summary>
	///     Returns an enumerator that iterates through the items in the current page.
	/// </summary>
	/// <returns> An enumerator for the current page's items. </returns>
	public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
}
