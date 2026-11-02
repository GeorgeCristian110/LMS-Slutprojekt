using Domain.Contracts;
using Domain.Models.Entities;
using Domain.Models.Exceptions;
using LMS.Shared.Constants;
using LMS.Shared.DTOs.CourseDtos;
using LMS.Shared.Paging;
using Service.Contracts;

namespace LMS.Services;

/// <summary>
/// Business rules for courses. Controllers call this service,
/// and it talks to the database only through the unit of work.
/// </summary>
public class CourseService(IUnitOfWork unitOfWork) : ICourseService
{
    /// <summary>Creates a course after validating that the dates make sense.</summary>
    public async Task<CourseDto> CreateCourseAsync(CreateCourseDto createCourseDto)
    {
        // Rule: a course cannot start after it ends (returns 400).
        EnsureValidDates(createCourseDto.StartDate, createCourseDto.EndDate);

        var course = new Course
        {
            Name = createCourseDto.Name.Trim(),
            Description = createCourseDto.Description?.Trim(),
            StartDate = createCourseDto.StartDate,
            EndDate = createCourseDto.EndDate
        };

        await unitOfWork.Courses.CreateCourseAsync(course);

        // The repository only adds the entity. Saving happens once, here.
        await unitOfWork.SaveChangesAsync();

        return ToDto(course);
    }

    /// <summary>Returns the courses that match the filters, earliest start date first (US15).</summary>
    public async Task<IEnumerable<CourseDto>> GetCoursesAsync(CourseListQuery query)
    {
        // Rule: the status must be one of the known names (returns 400). A typo should not silently return everything.
        if (query.Status is not null && !CourseStatus.All.Contains(query.Status))
            throw new BadRequestException(
                $"'{query.Status}' is not a valid status. Please choose between: {string.Join(", ", CourseStatus.All)}.",
                "Invalid status");

        // "Today" is decided here, in the service, so the repository stays a pure query and tests can pass any date.
        // It is today's date in Sweden (like the dashboards), not the date of the server's own time zone.
        var today = DateOnly.FromDateTime(CourseTime.FromUtc(DateTime.UtcNow));
        var courses = await unitOfWork.Courses.GetAllCoursesAsync(query, today);

        // The repository already orders; the mapping runs over the loaded courses.
        return courses
            .Select(ToDto)
            .ToList();
    }

    /// <summary>Returns one course, or throws a 404 if it does not exist.</summary>
    public async Task<CourseDto?> GetCourseByIdAsync(Guid id)
    {
        var course = await unitOfWork.Courses.GetCourseByIdAsync(id)
            ?? throw new CourseNotFoundException($"Course with id '{id}' was not found.", "Course not found");

        return ToDto(course);
    }

    /// <summary>Updates an existing course, or throws a 404 if it does not exist.</summary>
    public async Task UpdateCourseAsync(Guid id, UpdateCourseDto updateCourseDto)
    {
        EnsureValidDates(updateCourseDto.StartDate, updateCourseDto.EndDate);

        var course = await unitOfWork.Courses.GetCourseByIdAsync(id)
            ?? throw new CourseNotFoundException($"Course with id '{id}' was not found.", "Course not found");

        // Check that no module are outside new start/end dates
        bool modulesInsideNewDates = await DatesFitsModules(id, updateCourseDto.StartDate, updateCourseDto.EndDate);
        if (modulesInsideNewDates == false)
            throw new BadRequestException(
                $"The course has modules that starts before new start date or after new end date.",
                "Invalid dates");

        // The course is tracked by the context, so changing the properties
        // and saving is enough. No explicit Update call is needed.
        course.Name = updateCourseDto.Name.Trim();
        course.Description = updateCourseDto.Description?.Trim();
        course.StartDate = updateCourseDto.StartDate;
        course.EndDate = updateCourseDto.EndDate;

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>Checks if course has modules that is outside course dates
    /// Returns true if no module is before/after new dates, else return false
    /// </summary>
    private async Task<bool> DatesFitsModules(Guid id, DateOnly newStartDate, DateOnly newEndDate)
    {
        // Get modules for the course - order by Startdate
        var modules = await unitOfWork.Modules.GetByCourseIdAsync(id);

        if (modules.Any())
        {
            DateOnly firstStartDate = modules.Min(m => m.StartDate);
            if (newStartDate > firstStartDate) return false;

            DateOnly lastEndDate = modules.Max(m => m.EndDate);
            if (newEndDate < lastEndDate) return false;
        }

        return true;
    }

    /// <summary>Throws a 400 (BadRequestException) if the start date is after the end date.</summary>
    private static void EnsureValidDates(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new BadRequestException("Start date can not be after end date.");
        }
    }

    /// <summary>Single place that maps a Course entity to its DTO.</summary>
    private static CourseDto ToDto(Course course) => new()
    {
        Id = course.Id,
        Name = course.Name,
        Description = course.Description,
        StartDate = course.StartDate,
        EndDate = course.EndDate
    };
}