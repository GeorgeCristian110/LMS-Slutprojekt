using Domain.Models.Entities;
using LMS.Shared.Paging;
using Microsoft.AspNetCore.Identity;

namespace Domain.Contracts.Repositories;

public interface IUserRepository
{
    /// <summary>
    /// One page of users, filtered by search text, role and course (US15).
    /// Ordered by the sort column (name by default), then id, so the pages are stable. Includes CourseUsers.Course.
    /// </summary>
    Task<PagedResult<ApplicationUser>> GetPageAsync(UserListQuery query);
    Task<ApplicationUser?> FindByIdAsync(string id);
    Task<ApplicationUser?> FindByIdWithCourseAsync(string id);
    Task<ApplicationUser?> FindByEmailAsync(string email);
    Task<IdentityResult> CreateAsync(ApplicationUser user, string password);
    Task<IdentityResult> UpdateAsync(ApplicationUser user);
    Task<IEnumerable<string>> GetRolesAsync(ApplicationUser user);
    Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role);
    Task<IdentityResult> RemoveFromRoleAsync(ApplicationUser user, string role);
    Task<IDictionary<string, string>> GetRoleNamesByUserIdAsync();
}
