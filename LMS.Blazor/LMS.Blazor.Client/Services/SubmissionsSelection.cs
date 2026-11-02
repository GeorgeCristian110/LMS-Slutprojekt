namespace LMS.Blazor.Client.Services;


/// <summary>
/// Remembers the course and assignment picked on the Submissions page,
/// so they are still chosen when the teacher comes back from a review.
/// </summary>
public class SubmissionsSelection
{
    public Guid? CourseId { get; set; }
    public Guid? AssignmentId { get; set; }
}
