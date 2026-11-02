using Domain.Models.Entities;

namespace Domain.Contracts.Repositories;

/// <summary>
/// Database access for activities. Nothing is saved at this level: Create/Update/Delete
/// only mark changes, and the service saves once through the unit of work.
/// </summary>
public interface IActivityRepository
{
    Task<IEnumerable<Activity>> GetByModuleIdAsync(Guid moduleId);
    Task<Activity?> GetByIdAsync(Guid id);

    /// <summary>True if an activity type with this id exists.</summary>
    Task<bool> ActivityTypeExistsAsync(Guid activityTypeId);

    void Create(Activity activity);
    void Update(Activity activity);
    void Delete(Activity activity);

    /// <summary>Returns all activity types, sorted by name. Read only.</summary>
    Task<IEnumerable<ActivityType>> GetActivityTypesAsync();

    /// <summary>
    /// Returns the first activity in the module that overlaps the given times, or null if the slot is free.
    /// On update, pass the activity's own id so it doesn't clash with itself. On create, pass null.
    /// </summary>
    Task<Activity?> FindOverlappingAsync(Guid moduleId, DateTime startTime,
        DateTime endTime, Guid? excludeActivityId);

    /// <summary>
    /// Returns the next activities in a course (US12): activities that have not ended yet, earliest start first,
    /// at most "take" of them. Module and ActivityType are loaded. Read only.
    /// fromCourseTime is Swedish wall-clock time (see CourseTime.FromUtc), the same time the activities are stored in.
    /// </summary>
    Task<IEnumerable<Activity>> GetUpcomingByCourseIdAsync(Guid courseId, DateTime fromCourseTime, int take);

    /// <summary>
    /// Returns the next activities across ALL courses (US13, the teacher dashboard): activities that have not
    /// ended yet, earliest start first, at most "take" of them. ActivityType and Module (with its Course)
    /// are loaded. Read only. fromCourseTime is Swedish wall-clock time, like in GetUpcomingByCourseIdAsync.
    /// </summary>
    Task<IEnumerable<Activity>> GetUpcomingAcrossCoursesAsync(DateTime fromCourseTime, int take);

    /// <summary>
    /// Returns the assignments (US13) of the given courses only, each with its submissions, and for each
    /// submission its Feedbacks and Student. Module is loaded too, so the course is known. Read only.
    /// The activity type name is passed in (ActivityTypeNames.Assignment), so the data layer does not need
    /// to know the constant. The course ids are the courses that are running, so assignments of finished
    /// courses are never loaded. An empty list of ids gives an empty result.
    /// </summary>
    Task<IEnumerable<Activity>> GetAssignmentsWithSubmissionsAsync(string assignmentTypeName, IEnumerable<Guid> courseIds);
}