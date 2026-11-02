namespace LMS.Shared.DTOs.DashboardDtos;

/// <summary>
/// One row in the "Needs your attention" card: one student's hand-in (or missing hand-in)
/// that the teacher should look at. Status is one of the SubmissionStatus words.
/// </summary>
/// <param name="StudentId">The student.</param>
/// <param name="StudentName">Shown in the row.</param>
/// <param name="ActivityId">The assignment, used to open the Submissions page for it.</param>
/// <param name="ActivityName">The assignment name, for example "Övning 16: Movie API".</param>
/// <param name="Status">Late, Awaiting feedback or Missing.</param>
/// <param name="SubmittedAt">When the student handed in (UTC). Null when nothing was handed in (Missing).</param>
/// <param name="Deadline">The assignment deadline as Swedish wall-clock time, shown as it is.</param>
/// <param name="CourseId">The course of the assignment, used to preselect the course on the Submissions page.</param>
/// <param name="SubmissionId">The hand-in, used to open the review page directly. Null when nothing was handed in (Missing).</param>
public sealed record TeacherAttentionItemDto(
    string StudentId,
    string StudentName,
    Guid ActivityId,
    string ActivityName,
    string Status,
    DateTime? SubmittedAt,
    DateTime Deadline,
    Guid CourseId,
    Guid? SubmissionId);