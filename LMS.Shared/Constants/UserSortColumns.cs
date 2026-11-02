namespace LMS.Shared.Constants;

/// <summary>
/// The columns the user list can be sorted by: the values of ?sort= on GET api/users (issue #204).
/// Shared by the API (validation + repository) and Blazor (the column headers), so both use the same words.
/// </summary>
public static class UserSortColumns
{
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Course = "course";

    public static readonly IReadOnlyCollection<string> All = new[] { Name, Email, Role, Course };
}