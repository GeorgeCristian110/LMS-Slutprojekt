using Domain.Models.Exceptions;
using LMS.Shared.Constants;
using LMS.Shared.DTOs.NotificationDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service.Contracts;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace LMS.Presentation.Controllers;

[Route("api")]
[ApiController]
[Authorize(Roles = UserRoles.Teacher + "," + UserRoles.Student)]
public class NotificationsController(IServiceManager serviceManager) : ControllerBase
{
    private readonly IServiceManager _serviceManager = serviceManager;
    private const int maxItems = 10;  // Max and default number of notification

    [HttpGet("notifications")]
    [SwaggerOperation(
        Summary = "Get users notifications",
        Description = "Returns the logged-in student's notifications. Latest first")]
    [SwaggerResponse(StatusCodes.Status200OK, "Notifications found", typeof(IEnumerable<NotificationDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not logged in")]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] int nbrItems = maxItems)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        if (nbrItems <= 0 || nbrItems > maxItems)
            throw new BadRequestException($"Number of notifications must be between 1 and {maxItems}.");

        var notifictions = await _serviceManager.NotificationService.GetByStudentIdAsync(userId, nbrItems);

        return Ok(notifictions);
    }

    [HttpPatch("notifications/{notificationId:guid}")]
    [SwaggerOperation(
        Summary = "Update notification - only change of IsRead is supported right now",
        Description = "Updates notification")]
    [SwaggerResponse(StatusCodes.Status200OK, "Notification has been updated")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not logged in")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Notification not found")]
    public async Task<ActionResult<bool>> NotificationIsRead([FromRoute] Guid notificationId, UpdateNotificationDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        if (notificationId != dto.Id)
            throw new BadRequestException("Id in URI is not the the same as in body");

        await _serviceManager.NotificationService.UpdateNotification(userId, dto);

        return Ok();
    }

    [HttpPatch("notifications/allRead")]
    [SwaggerOperation(
        Summary = "Sets all notifications for active user as read",
        Description = "Updates active users notifications to read")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Notifications has been set to read")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Not logged in")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Notification not found")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Technical error")]
    //public async Task<ActionResult<bool>> NotificationAllIsRead() // UpdateNotificationDto dto
    public async Task<ActionResult> NotificationAllIsRead() // UpdateNotificationDto dto
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        bool success = await _serviceManager.NotificationService.UpdateAllRead(userId);

        if (success)
            return NoContent();
        else
            return StatusCode(500);
    }
}
