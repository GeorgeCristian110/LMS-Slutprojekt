using Domain.Contracts;
using Domain.Contracts.Repositories;
using Domain.Models.Entities;
using Domain.Models.Exceptions;
using LMS.Services;
using LMS.Shared.Constants;
using LMS.Shared.Paging;
using Moq;

namespace LMS.Tests;

public class CourseServiceFilterTests
{
    private readonly Mock<ICourseRepository> _courses = new();
    private readonly CourseService _service;

    public CourseServiceFilterTests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Courses).Returns(_courses.Object);
        _service = new CourseService(unitOfWork.Object);
    }

    [Fact]
    public async Task GetCoursesAsync_UnknownStatus_IsBadRequest()
    {
        var query = new CourseListQuery { Status = "Ongoing" };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.GetCoursesAsync(query));

        Assert.Equal("Invalid status", ex.Title);
        Assert.Contains("Upcoming, Active, Finished", ex.Message);
        _courses.Verify(r => r.GetAllCoursesAsync(It.IsAny<CourseListQuery>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(CourseStatus.Upcoming)]
    [InlineData(CourseStatus.Active)]
    [InlineData(CourseStatus.Finished)]
    public async Task GetCoursesAsync_KnownStatus_IsPassedThroughWithToday(string? status)
    {
        var query = new CourseListQuery { Search = "net", Status = status };

        // "Today" is today's date in Sweden, the same rule as in CourseService (not the date of the machine's own time zone)
        var today = DateOnly.FromDateTime(CourseTime.FromUtc(DateTime.UtcNow));
        _courses.Setup(r => r.GetAllCoursesAsync(query, today))
            .ReturnsAsync([new Course { Id = Guid.NewGuid(), Name = ".NET" }]);

        var result = (await _service.GetCoursesAsync(query)).ToList();

        Assert.Single(result);
        Assert.Equal(".NET", result[0].Name);
        _courses.Verify(r => r.GetAllCoursesAsync(query, today), Times.Once);
    }
}