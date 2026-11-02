using System.Linq.Expressions;
using Domain.Models.Entities;
using LMS.Shared.Constants;
using LMS.Shared.Paging;
using Microsoft.AspNetCore.Identity;

namespace LMS.Infrastructure.Extensions;

/* 
 <summary>
    The ORDER BY of the user list (issue #204).
    It is a separate static method, not inside UserRepository, so the unit tests can run it on plain
    lists (LINQ to Objects) without a database. In the repository the same code becomes SQL. 
 </summary> 
*/

public static class UserSortExtensions
{
    /// <summary>
    /// Sorts the users by the column and direction in the query. Call it after the filters and
    /// before ToPagedResultAsync, so the database sorts first and then cuts out the page.
    ///
    /// Id is always the last sort key. Two users can have the same name, role or course; without a
    /// unique last key SQL Server may return them in a different order per request, and a user could
    /// appear on two pages or on none.
    /// </summary>
    /// <param name="users">The filtered users.</param>
    /// <param name="query">The sort column and direction. Already validated (UserListQuery.Validate).</param>
    /// <param name="userRoles">AspNetUserRoles: which user has which role id.</param>
    /// <param name="roles">AspNetRoles: the role names.</param>
    public static IQueryable<ApplicationUser> ApplySort(
        this IQueryable<ApplicationUser> users,
        UserListQuery query,
        IQueryable<IdentityUserRole<string>> userRoles,
        IQueryable<IdentityRole> roles)
    {
        var descending = string.Equals(query.Direction, SortDirection.Desc, StringComparison.OrdinalIgnoreCase);

        // The column names are compared in lower case, so ?sort=Role works like ?sort=role.
        // Null (no sort given) falls through to the default: name.
        var ordered = query.Sort?.ToLowerInvariant() switch
        {
            UserSortColumns.Email => users.OrderByDirection(u => u.Email, descending),

            // The role is not on the user row (see the role filter in UserRepository), so the sort key is a
            // sub-query: the user's role name. Min() picks one name if a user ever had two roles,
            // and gives null for a user without a role. Users with the same role are sorted by name.
            UserSortColumns.Role => users
                .OrderByDirection(u => roles
                    .Where(r => userRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                    .Select(r => r.Name)
                    .Min(), descending)
                .ThenBy(u => u.Name),

            // A student has one course, a teacher may have several or none.
            // Min() picks the first course name alphabetically, null when there is no course.
            UserSortColumns.Course => users
                .OrderByDirection(u => u.CourseUsers.Select(cu => cu.Course.Name).Min(), descending)
                .ThenBy(u => u.Name),

            _ => users.OrderByDirection(u => u.Name, descending)
        };

        return ordered.ThenBy(u => u.Id);
    }

    // OrderBy or OrderByDescending, chosen by a bool. Saves writing every case twice.
    private static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);
}