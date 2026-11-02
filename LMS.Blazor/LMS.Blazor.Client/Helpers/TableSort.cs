using LMS.Shared.Constants;

namespace LMS.Blazor.Client.Helpers;

/// <summary>
/// Which column a table is sorted by, and in which direction (issue #204). One instance per table.
/// The page keeps it in a field, SortableHeader shows it, and Toggle changes it when a header is clicked.
///
/// Lists that are loaded whole (submissions, documents, courses) sort their rows in the page with Apply.
/// The Users page is paged, so it sends Column and Direction to the API instead: only the server sees every page.
/// </summary>
public sealed class TableSort(string column, bool descending = false)
{
    // The column key, e.g. "name". Each page decides its own keys.
    public string Column { get; private set; } = column;

    public bool Descending { get; private set; } = descending;

    // "asc" or "desc", the words the API expects in ?direction=
    public string Direction => Descending ? SortDirection.Desc : SortDirection.Asc;

    // A click on the active column flips the direction. A click on another column sorts it ascending.
    public void Toggle(string column)
    {
        if (column == Column)
        {
            Descending = !Descending;
        }
        else
        {
            Column = column;
            Descending = false;
        }
    }

    // Sorts rows that are already in memory by one key, in the current direction.
    // Returns IOrderedEnumerable, so the page can add a ThenBy for rows with the same key.
    public IOrderedEnumerable<T> Apply<T, TKey>(IEnumerable<T> rows, Func<T, TKey> key) =>
        Descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
}
