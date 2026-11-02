using System.ComponentModel.DataAnnotations;
using Domain.Models.Entities;
using LMS.Infrastructure.Extensions;
using LMS.Shared.Paging;
using Microsoft.AspNetCore.Identity;

namespace LMS.Tests;

/// <summary>
/// Issue #204: sorting the user list.
/// Part 1 tests the validation (what [ApiController] turns into a 400).
/// Part 2 runs ApplySort on plain lists. That proves which column and direction is used and that id
/// is the last key. It does not prove the SQL; that is checked by hand against the real database.
/// </summary>
public class UserSortTests
{
    // ---------- Part 1: validation ----------

    [Theory]
    [InlineData(null)]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("role")]
    [InlineData("course")]
    [InlineData("Role")]
    public void Validate_AllowedSortColumn_IsValid(string? sort)
    {
        var errors = Validate(new UserListQuery { Sort = sort });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnknownSortColumn_GivesAFieldError()
    {
        var errors = Validate(new UserListQuery { Sort = "password" });

        var error = Assert.Single(errors);
        Assert.Equal([nameof(UserListQuery.Sort)], error.MemberNames);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    [InlineData("ASC")]
    [InlineData("Desc")]
    public void Validate_Direction_IsCaseInsensitive(string direction)
    {
        var errors = Validate(new UserListQuery { Direction = direction });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnknownDirection_GivesAFieldError()
    {
        var errors = Validate(new UserListQuery { Direction = "up" });

        var error = Assert.Single(errors);
        Assert.Equal([nameof(UserListQuery.Direction)], error.MemberNames);
    }

    // ---------- Part 2: the order ----------

    // Four users. Their names, emails, roles and courses are in different orders on purpose,
    // so each sort column gives a different result.
    //   id  name   email           role     course
    //   u1  Cecil  a@lms.local     Student  Java
    //   u2  Anna   d@lms.local     Student  .NET
    //   u3  Bo     c@lms.local     Teacher  (none)
    //   u4  Anna   b@lms.local     Student  .NET     (same name as u2: id decides)
    private static readonly IdentityRole Teacher = new("Teacher") { Id = "r-teacher" };
    private static readonly IdentityRole Student = new("Student") { Id = "r-student" };
    private static readonly Course Java = new() { Id = Guid.NewGuid(), Name = "Java" };
    private static readonly Course DotNet = new() { Id = Guid.NewGuid(), Name = ".NET" };

    private readonly List<ApplicationUser> _users =
    [
        GivenUser("u1", "Cecil", "a@lms.local", Java),
        GivenUser("u2", "Anna", "d@lms.local", DotNet),
        GivenUser("u3", "Bo", "c@lms.local"),
        GivenUser("u4", "Anna", "b@lms.local", DotNet)
    ];

    private readonly List<IdentityUserRole<string>> _userRoles =
    [
        new() { UserId = "u1", RoleId = Student.Id },
        new() { UserId = "u2", RoleId = Student.Id },
        new() { UserId = "u3", RoleId = Teacher.Id },
        new() { UserId = "u4", RoleId = Student.Id }
    ];

    [Fact]
    public void ApplySort_WithoutSort_OrdersByNameThenId()
    {
        Assert.Equal(["u2", "u4", "u3", "u1"], SortedIds(new UserListQuery()));
    }

    [Theory]
    [InlineData("name", new[] { "u2", "u4", "u3", "u1" })]
    [InlineData("email", new[] { "u1", "u4", "u3", "u2" })]
    [InlineData("role", new[] { "u2", "u4", "u1", "u3" })]   // Student before Teacher, then by name
    [InlineData("course", new[] { "u3", "u2", "u4", "u1" })] // no course first, then .NET (by name), then Java
    public void ApplySort_EachColumn_SortsAscending(string sort, string[] expected)
    {
        Assert.Equal(expected, SortedIds(new UserListQuery { Sort = sort }));
    }

    [Fact]
    public void ApplySort_Desc_ReversesTheColumn_ButIdStaysAscending()
    {
        // Name descending: Cecil, Bo, Anna, Anna. The two Annas keep id order (u2 before u4).
        Assert.Equal(["u1", "u3", "u2", "u4"], SortedIds(new UserListQuery { Sort = "name", Direction = "desc" }));
    }

    [Fact]
    public void ApplySort_DirectionAndColumn_AreCaseInsensitive()
    {
        var lower = SortedIds(new UserListQuery { Sort = "email", Direction = "desc" });
        var upper = SortedIds(new UserListQuery { Sort = "EMAIL", Direction = "DESC" });

        Assert.Equal(["u2", "u3", "u4", "u1"], upper);
        Assert.Equal(lower, upper);
    }

    // ---------- Helpers ----------

    // Runs the same DataAnnotations validation that [ApiController] runs before the action:
    // the attributes ([Range], [MaxLength]) and then IValidatableObject.Validate.
    private static List<ValidationResult> Validate(UserListQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }

    // AsQueryable turns the lists into IQueryable, so ApplySort runs the same expressions it sends to
    // EF Core, only executed in memory.
    private string[] SortedIds(UserListQuery query) =>
        _users.AsQueryable()
            .ApplySort(query, _userRoles.AsQueryable(), new[] { Teacher, Student }.AsQueryable())
            .Select(u => u.Id)
            .ToArray();

    private static ApplicationUser GivenUser(string id, string name, string email, Course? course = null)
    {
        var user = new ApplicationUser { Id = id, Name = name, Email = email, UserName = email };

        if (course is not null)
            user.CourseUsers.Add(new CourseUser { UserId = id, CourseId = course.Id, Course = course });

        return user;
    }
}
