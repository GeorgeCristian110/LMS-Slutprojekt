namespace LMS.Shared.DTOs.NotificationDtos;

public record NotificationDto
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Message { get; init; }

    // set is needed for the PATCH operation to work, but it is not settable by the client
    public bool IsRead { get; set; }  
    public DateTime CreatedAt { get; init; }
}