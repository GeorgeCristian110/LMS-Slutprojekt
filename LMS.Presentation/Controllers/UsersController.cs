using LMS.Shared.DTOs.UserDtos;
using LMS.Shared.Paging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service.Contracts;
using Swashbuckle.AspNetCore.Annotations;
using LMS.Shared.Constants;

namespace LMS.Presentation.Controllers;

[Route("api/users")]
[ApiController]
[Authorize(Roles = UserRoles.Teacher)]
[Consumes("application/json")]
[Produces("application/json")]
public class UsersController(IServiceManager serviceManager) : ControllerBase
{
    private readonly IServiceManager _serviceManager = serviceManager;

    /// <summary>
    /// GET api/users?search=&amp;role=&amp;courseId=&amp;sort=&amp;direction=&amp;page=&amp;pageSize= (US15, issue #204).
    /// [FromQuery] binds the query string to UserListQuery; [ApiController] validates its
    /// DataAnnotations and its Validate method and returns 400 with field errors before this method runs.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List users (paged)",
        Description = "Returns one page of users with their role and course names. " +
                      "Optional filters: search (name or email), role, courseId. " +
                      "Optional sort: name, email, role or course (default name), direction asc or desc (default asc). " +
                      "page starts at 1, pageSize is 1-100.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Users retrieved successfully", typeof(PagedResult<UserDto>))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid page, pageSize, search, sort or direction")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] UserListQuery query) =>
        Ok(await _serviceManager.UserService.GetPageAsync(query));

    [HttpGet("{id}")]
    [SwaggerOperation(
        Summary = "Get user",
        Description = "Returns a single user by id.")]
    [SwaggerResponse(StatusCodes.Status200OK, "User retrieved successfully", typeof(UserDto))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "User not found")]
    public async Task<ActionResult<UserDto>> GetUser(string id) =>
        Ok(await _serviceManager.UserService.GetByIdAsync(id));

    [HttpPost]
    [SwaggerOperation(
        Summary = "Create user",
        Description = "Creates a user with the given name, email, password and role (Student or Teacher). A student must have a course.")]
    [SwaggerResponse(StatusCodes.Status201Created, "User created successfully", typeof(UserDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid input, invalid role or missing course")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Email already in use")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto dto)
    {
        var created = await _serviceManager.UserService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [SwaggerOperation(
        Summary = "Update user",
        Description = "Updates name, email, role and course of an existing user.")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "User updated successfully")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid input, invalid role or missing course")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "User not found")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Email already in use")]
    public async Task<IActionResult> UpdateUser(string id, UpdateUserDto dto)
    {
        await _serviceManager.UserService.UpdateAsync(id, dto);
        return NoContent();
    }
}