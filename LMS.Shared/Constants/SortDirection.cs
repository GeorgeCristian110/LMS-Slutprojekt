namespace LMS.Shared.Constants;

/// <summary>
/// The two sort directions: the values of ?direction= on a list endpoint (issue #204).
/// The API compares them case-insensitively, so "DESC" works too.
/// </summary>
public static class SortDirection
{
    public const string Asc = "asc";
    public const string Desc = "desc";

    public static readonly IReadOnlyCollection<string> All = [Asc, Desc];
}