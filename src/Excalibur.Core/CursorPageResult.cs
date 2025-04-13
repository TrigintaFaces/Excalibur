namespace Excalibur.Core;

public abstract class CursorPageRequest<TCursor>(int pageSize, PageNavigation navigation)
{
	public int PageSize { get; set; } = pageSize;
	public PageNavigation Navigation { get; set; } = navigation;

	public void Deconstruct(out int pageSize, out PageNavigation navigation, out TCursor? cursor)
	{
		pageSize = PageSize;
		navigation = Navigation;
		cursor = GetCursor();
	}

	protected abstract TCursor? GetCursor();
}

public class CursorPageResult<T>(IEnumerable<T> items, int pageSize, long totalRecords)
{
	public IEnumerable<T> Items { get; init; } = items;
	public int PageSize { get; init; } = pageSize;
	public long TotalRecords { get; init; } = totalRecords;
	public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)PageSize);

	public void Deconstruct(out IEnumerable<T> Items, out int PageSize, out long TotalRecords)
	{
		Items = this.Items;
		PageSize = this.PageSize;
		TotalRecords = this.TotalRecords;
	}
}

public enum PageNavigation
{
	First,
	Previous,
	Next,
	Last
}
