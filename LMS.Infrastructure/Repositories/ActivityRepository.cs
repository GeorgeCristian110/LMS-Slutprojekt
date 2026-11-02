using Domain.Contracts.Repositories;
using Domain.Models.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Repositories;

public class ActivityRepository : IActivityRepository
{
    private readonly ApplicationDbContext _context;

    public ActivityRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Read only (the schedule list), so tracking is turned off.          
    // Include is needed so MapToDto can read ActivityType.Name. 
    public async Task<IEnumerable<Activity>> GetByModuleIdAsync(Guid moduleId)
    {
        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.ActivityType)
            .Where(a => a.ModuleId == moduleId)
            .OrderBy(a => a.StartTime)
            .ToListAsync();
    }

    // Tracked on purpose: UpdateAsync and DeleteAsync change the entity and save it.  
    // Include is needed so MapToDto can read ActivityType.Name. 
    public async Task<Activity?> GetByIdAsync(Guid id)
    {
        return await _context.Activities
            .Include(a => a.ActivityType)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    // Overlap = starts before the other ends AND ends after the other starts.
    // Strict < and > so back-to-back activities (10:00 end, 10:00 start) are allowed.
    public async Task<Activity?> FindOverlappingAsync(Guid moduleId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeActivityId)
    {
        return await _context.Activities
            .Where(a => a.ModuleId == moduleId)
            .Where(a => excludeActivityId == null || a.Id != excludeActivityId)
            .Where(a => startTime < a.EndTime && endTime > a.StartTime)
            .FirstOrDefaultAsync();
    }

    // The next activities in a course (US12, the student dashboard). An activity counts until it has ended,
    // so one that is running right now is still "next". Read only, so tracking is turned off.
    // Module (for its name) and ActivityType (for the badge) are included. One query, no N+1.
    public async Task<IEnumerable<Activity>> GetUpcomingByCourseIdAsync(
        Guid courseId, DateTime fromCourseTime, int take)
    {
        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.ActivityType)
            .Include(a => a.Module)
            .Where(a => a.Module.CourseId == courseId && a.EndTime >= fromCourseTime)
            .OrderBy(a => a.StartTime)
            .Take(take)
            .ToListAsync();
    }

    // The next activities across all courses (US13, the teacher dashboard). Same rule as above:
    // an activity counts until it has ended. Module and its Course are included for the names
    // shown in the list, ActivityType for the badge. One query, no N+1. Read only.
    public async Task<IEnumerable<Activity>> GetUpcomingAcrossCoursesAsync(DateTime fromCourseTime, int take)
    {
        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.ActivityType)
            .Include(a => a.Module)
                .ThenInclude(m => m.Course)
            .Where(a => a.EndTime >= fromCourseTime)
            .OrderBy(a => a.StartTime)
            .Take(take)
            .ToListAsync();
    }

    // The assignments of the given courses with their hand-ins (US13, the teacher dashboard), loaded in one query.
    // Only the courses in courseIds (the ones that are running), so assignments of finished courses are not loaded.
    // Each submission brings its Feedbacks (needed by SubmissionStatusCalculator) and its Student (for the name).
    // Module is included so the course of the assignment is known. Read only, so tracking is turned off.
    public async Task<IEnumerable<Activity>> GetAssignmentsWithSubmissionsAsync(
        string assignmentTypeName, IEnumerable<Guid> courseIds)
    {
        // A list is needed for Contains to be translated to SQL (IN)
        var ids = courseIds.ToList();

        // No courses means no assignments: no need to ask the database
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.ActivityType)
            .Include(a => a.Module)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Feedbacks)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Student)
            .Where(a => a.ActivityType.Name == assignmentTypeName && ids.Contains(a.Module.CourseId))
            .ToListAsync();
    }

    // Read only (the dropdown list), so tracking is turned off
    public async Task<IEnumerable<ActivityType>> GetActivityTypesAsync()
    {
        return await _context.ActivityTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<bool> ActivityTypeExistsAsync(Guid activityTypeId)
    {
        return await _context.ActivityTypes
            .AnyAsync(t => t.Id == activityTypeId);
    }

    public void Create(Activity activity) => _context.Activities.Add(activity);
    public void Update(Activity activity) => _context.Activities.Update(activity);
    public void Delete(Activity activity) => _context.Activities.Remove(activity);
}