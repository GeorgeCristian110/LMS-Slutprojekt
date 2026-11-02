using System.ComponentModel.DataAnnotations;
using LMS.Shared.Constants;

namespace LMS.Shared.Paging;

/// <summary>
/// The query string of GET api/users: filters, sort and page.
/// IValidatableObject: [ApiController] calls Validate after the attributes, and every result becomes
/// a field error in the 400 response, the same way as the [Range] errors of page and pageSize.
/// </summary>
public sealed record UserListQuery : PagedQuery, IValidatableObject
{
    /// <summary>Matched against name and email (contains, case-insensitive in SQL Server).</summary>
    [MaxLength(100, ErrorMessage = "search can be at most 100 characters.")]
    public string? Search { get; init; }

    /// <summary>Exact role name, e.g. "Student" or "Teacher".</summary>
    public string? Role { get; init; }

    /// <summary>Only users linked to this course.</summary>
    public Guid? CourseId { get; init; }

    /// <summary>
    /// Column to sort by: name, email, role or course (UserSortColumns). Case-insensitive.
    /// Without it the list is sorted by name, like before (issue #204).
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>asc or desc (SortDirection). Case-insensitive. Without it the order is ascending.</summary>
    public string? Direction { get; init; }

    /// <summary>
    /// Rejects an unknown sort column or direction with a 400, instead of silently falling back to the
    /// default order. A typo like ?sort=nmae should tell the client it is wrong.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Sort is not null && !UserSortColumns.All.Contains(Sort, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"sort must be one of: {string.Join(", ", UserSortColumns.All)}.", [nameof(Sort)]);
        }

        if (Direction is not null && !SortDirection.All.Contains(Direction, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"direction must be {SortDirection.Asc} or {SortDirection.Desc}.", [nameof(Direction)]);
        }
    }
}