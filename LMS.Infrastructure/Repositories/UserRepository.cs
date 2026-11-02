using Domain.Contracts.Repositories;
using Domain.Models.Entities;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Extensions;
using LMS.Shared.Paging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UserRepository(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
    {
        return await _userManager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user)
    {
        return await _userManager.UpdateAsync(user);
    }

    public async Task<IEnumerable<string>> GetRolesAsync(ApplicationUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        return await _userManager.AddToRoleAsync(user, role);
    }

    public async Task<IdentityResult> RemoveFromRoleAsync(ApplicationUser user, string role)
    {
        return await _userManager.RemoveFromRoleAsync(user, role);
    }

    /// <summary>
    /// Returns every user's role name in one query, as userId → role name.
    /// Each user has one role, so the first role per user is used.
    /// </summary>
    public async Task<IDictionary<string, string>> GetRoleNamesByUserIdAsync()
    {
        var pairs = await _context.UserRoles
            .Join(_context.Roles,
                 userRole => userRole.RoleId,
                 role => role.Id,
                 (userRole, role) => new { userRole.UserId, RoleName = role.Name ?? "" })
            .ToListAsync();

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleName);
    }

    /// <summary>
    /// Returns one page of users, filtered by the optional search text, role and course (US15).
    ///
    /// How it works: the method builds ONE SQL query step by step. Each "if" adds a WHERE condition
    /// only when that filter was given. Nothing is sent to the database until the last line
    /// (ToPagedResultAsync), which runs a COUNT and then fetches only the rows of the asked page.
    /// So with 2000 users and page size 20, only 20 user rows are loaded into memory.
    ///
    /// The result is ordered by the sort column in the query (name when none is given), then by id,
    /// so the pages are always the same for the same data.
    /// The users come with their CourseUsers and Course loaded, because UserService.MapToDto needs
    /// the course names. Roles are NOT loaded here; the service fetches them in a separate query.
    /// </summary>
    /// <param name="query">The filters and the page to return. Already validated by the controller.</param>
    public async Task<PagedResult<ApplicationUser>> GetPageAsync(UserListQuery query)
    {
        // Start from all users. This is a query description, not a list: no SQL has run yet.
        // AsNoTracking: the list is read-only, so EF Core does not need to watch these objects for changes.
        //   That makes the query faster and uses less memory.
        // Include + ThenInclude: also load each user's course links and the course rows behind them,
        //   so the service can read the course names without extra queries (no N+1 problem).
        // AsQueryable: makes the variable type IQueryable<ApplicationUser>, so the "users = users.Where(...)"
        //   lines below compile. Without it the variable would have the narrower Include type.
        var users = _userManager.Users
            .AsNoTracking()
            .Include(u => u.CourseUsers)
            .ThenInclude(cu => cu.Course)
            .AsQueryable();

        // Filter 1: search text.
        // The user typed something in the search box. We keep the users whose name OR email contains it.
        // EF Core turns string.Contains into SQL "LIKE '%text%'". With SQL Server's default collation
        // this is case-insensitive, so "anna" also finds "Anna".
        // Trim removes spaces at the start and end, so " anna " matches the same as "anna".
        // Email is marked nullable by Identity; the "!" tells the compiler we know it is set for our users.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(u => u.Name.Contains(search) || u.Email!.Contains(search));
        }

        // Filter 2: role.
        // ASP.NET Identity does not store the role on the user row. It uses two extra tables:
        //   AspNetUserRoles (UserId, RoleId) and AspNetRoles (Id, Name).
        // So "users with role Student" means: users for which a row exists in AspNetUserRoles
        // whose RoleId points to the AspNetRoles row named "Student".
        // The two nested Any calls become an EXISTS sub-query in SQL. The database does the matching;
        // we never load the role tables into memory.
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            users = users.Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == query.Role)));
        }

        // Filter 3: course.
        // "is Guid courseId" is true only when a course id was given (CourseId is nullable).
        // Keep the users that have at least one CourseUsers link to that course.
        // This also becomes an EXISTS sub-query in SQL.
        if (query.CourseId is Guid courseId)
        {
            users = users.Where(u => u.CourseUsers.Any(cu => cu.CourseId == courseId));
        }

        // Sort, then page.
        // ApplySort (UserSortExtensions) adds the ORDER BY for the chosen column and direction, and always
        // ends with Id, so the order is the same every time and a user can never appear on two pages.
        // It must come before ToPagedResultAsync: the database sorts the whole filtered list first,
        // then OFFSET/FETCH cuts out the page. Sorting after paging would only sort the 20 rows of one page.
        // ToPagedResultAsync (our extension method) now runs the two queries: COUNT, then OFFSET/FETCH.
        return await users
            .ApplySort(query, _context.UserRoles, _context.Roles)
            .ToPagedResultAsync(query.Page, query.PageSize);
    }

    public async Task<ApplicationUser?> FindByIdWithCourseAsync(string id)
    {
        return await _userManager.Users
            .Include(u => u.CourseUsers)
            .ThenInclude(cu => cu.Course)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}
