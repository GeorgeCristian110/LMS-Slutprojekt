using LMS.Shared.Constants;
using LMS.Shared.DTOs.CourseDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service.Contracts;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace LMS.Presentation.Controllers;

[Route("api/student/course")]
[ApiController]
// The role name comes from the shared constant, so it cannot drift from the seeded role
[Authorize(Roles = UserRoles.Student)]
public class StudentCourseController(IServiceManager serviceManager) : ControllerBase
{
    private readonly IServiceManager _serviceManager = serviceManager;

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get my course",
        Description = "Returns the logged-in student's course and the names of its participants.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Course found", typeof(StudentCourseDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not logged in")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Logged in, but not a student")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The student is not enrolled in a course")]
    public async Task<ActionResult<StudentCourseDto>> GetMyCourse()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var course = await _serviceManager.StudentCourseService.GetMyCourseAsync(userId);
        return Ok(course);
    }
}