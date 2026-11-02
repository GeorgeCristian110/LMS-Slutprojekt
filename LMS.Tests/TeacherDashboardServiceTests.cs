using Domain.Contracts;
using Domain.Contracts.Repositories;
using Domain.Models.Entities;
using LMS.Services;
using LMS.Shared.Constants;
using LMS.Shared.Paging;
using Moq;

namespace LMS.Tests;

/// <summary>
/// TeacherDashboardService (US13) with a fake database (mocked repositories),
/// so the dashboard rules can be tested without SQL Server. All dates are calculated from "now",
/// so the tests do not depend on the calendar.
/// </summary>
public class TeacherDashboardServiceTests
{
    private readonly Mock<ICourseRepository> _courseRepository = new();
    private readonly Mock<ICourseUserRepository> _courseUsers = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IModuleRepository> _modules = new();
    private readonly Mock<IActivityRepository> _activities = new();
    private readonly TeacherDashboardService _service;

    // The fake data. The mocks read from these collections, so a test only has to fill them.
    private readonly List<Course> _courses = [];
    private readonly Dictionary<Guid, List<CourseUser>> _membersByCourse = [];
    private readonly Dictionary<string, string> _roles = [];
    private readonly List<Module> _courseModules = [];
    private readonly List<Activity> _assignments = [];
    private readonly List<Activity> _upcoming = [];

    // The course most tests use: it is running today
    private readonly Course _course;

    public TeacherDashboardServiceTests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Courses).Returns(_courseRepository.Object);
        unitOfWork.Setup(u => u.CourseUsers).Returns(_courseUsers.Object);
        unitOfWork.Setup(u => u.Users).Returns(_users.Object);
        unitOfWork.Setup(u => u.Modules).Returns(_modules.Object);
        unitOfWork.Setup(u => u.Activities).Returns(_activities.Object);

        _courseRepository.Setup(r => r.GetAllCoursesAsync(It.IsAny<CourseListQuery>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(_courses);

        // The members of the asked-for courses, in one call (like the real repository)
        _courseUsers.Setup(r => r.GetByCourseIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((IEnumerable<Guid> ids) =>
                (IEnumerable<CourseUser>)_membersByCourse
                    .Where(pair => ids.Contains(pair.Key))
                    .SelectMany(pair => pair.Value)
                    .ToList());

        _users.Setup(r => r.GetRoleNamesByUserIdAsync()).ReturnsAsync(_roles);

        // The modules of the asked-for courses, in one call, earliest start date first
        _modules.Setup(r => r.GetByCourseIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((IEnumerable<Guid> ids) =>
                (IEnumerable<Module>)_courseModules
                    .Where(m => ids.Contains(m.CourseId))
                    .OrderBy(m => m.StartDate)
                    .ToList());

        // The assignments of the asked-for courses only (like the real repository)
        _activities.Setup(r => r.GetAssignmentsWithSubmissionsAsync(
                ActivityTypeNames.Assignment, It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((string type, IEnumerable<Guid> ids) =>
                (IEnumerable<Activity>)_assignments
                    .Where(a => ids.Contains(a.Module.CourseId))
                    .ToList());
        _activities.Setup(r => r.GetUpcomingAcrossCoursesAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(_upcoming);

        _course = AddCourse("Fullstack .NET", Today().AddDays(-30), Today().AddDays(30));

        _service = new TeacherDashboardService(unitOfWork.Object);
    }

    [Fact]
    public async Task NoCourses_GivesAnEmptyDashboard()
    {
        //ARRANGE: a teacher dashboard where nothing exists yet
        _courses.Clear();

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(0, result.AwaitingFeedbackCount);
        Assert.Equal(0, result.LateCount);
        Assert.Equal(0, result.MissingCount);
        Assert.Equal(0, result.NewSubmissionsCount);
        Assert.Null(result.OldestAwaitingSubmittedAt);
        Assert.Null(result.LateAssignmentName);
        Assert.Empty(result.NeedsAttention);
        Assert.Empty(result.UpcomingActivities);
        Assert.Empty(result.CurrentCourses);
    }

    [Fact]
    public async Task Counts_FollowTheSubmissionStatuses()
    {
        //ARRANGE: one student for each status on the same assignment
        var awaiting = AddStudent(_course, "s1", "Awaiting Student");
        var late = AddStudent(_course, "s2", "Late Student");
        var missing = AddStudent(_course, "s3", "Missing Student");
        var graded = AddStudent(_course, "s4", "Graded Student");

        var assignment = AddAssignment(_course, "Exercise 16", deadlineDaysFromNow: -3);
        // Handed in before the deadline, no feedback: Awaiting feedback
        AddSubmission(assignment, awaiting, submittedDaysFromNow: -5);
        // Handed in after the deadline, no feedback: Late
        AddSubmission(assignment, late, submittedDaysFromNow: -1);
        // Nothing handed in and the deadline has passed: Missing (missing has no submission)
        // Feedback that is newer than the hand-in: Feedback given, which needs no attention
        AddSubmission(assignment, graded, submittedDaysFromNow: -5, withFeedback: true);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(1, result.AwaitingFeedbackCount);
        Assert.Equal(1, result.LateCount);
        Assert.Equal(1, result.MissingCount);
        Assert.Equal("Exercise 16", result.LateAssignmentName);
        Assert.NotNull(result.OldestAwaitingSubmittedAt);
        Assert.Contains(result.NeedsAttention, i => i.StudentName == missing.Name && i.Status == SubmissionStatus.Missing);
        Assert.DoesNotContain(result.NeedsAttention, i => i.StudentName == graded.Name);
    }

    [Fact]
    public async Task OldestAwaiting_IsTheEarliestHandInThatWaitsForFeedback()
    {
        //ARRANGE
        var first = AddStudent(_course, "s1", "First");
        var second = AddStudent(_course, "s2", "Second");
        var assignment = AddAssignment(_course, "Exercise 1", deadlineDaysFromNow: 5);

        var older = AddSubmission(assignment, first, submittedDaysFromNow: -4);
        AddSubmission(assignment, second, submittedDaysFromNow: -1);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(older.SubmittedAt, result.OldestAwaitingSubmittedAt);
    }

    [Fact]
    public async Task Teachers_AreNotCountedAsStudents()
    {
        //ARRANGE: a teacher is a member of the course and has handed in nothing
        AddMember(_course, new ApplicationUser { Id = "t1", Name = "Teacher" }, UserRoles.Teacher);
        AddStudent(_course, "s1", "Student");
        AddAssignment(_course, "Exercise 1", deadlineDaysFromNow: -3);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT: only the student is missing
        Assert.Equal(1, result.MissingCount);
        Assert.Equal(1, Assert.Single(result.CurrentCourses).StudentCount);
    }

    [Fact]
    public async Task NewSubmissions_AreTheHandInsFromTheLast24Hours()
    {
        //ARRANGE
        var recent = AddStudent(_course, "s1", "Recent");
        var old = AddStudent(_course, "s2", "Old");
        var assignment = AddAssignment(_course, "Exercise 1", deadlineDaysFromNow: 5);

        AddSubmission(assignment, recent, submittedDaysFromNow: 0, hoursAgo: 2);
        AddSubmission(assignment, old, submittedDaysFromNow: -3);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(1, result.NewSubmissionsCount);
    }

    [Fact]
    public async Task LateAssignmentName_IsTheAssignmentWithTheMostLateHandIns()
    {
        //ARRANGE: assignment B has two late hand-ins, assignment A has one
        var s1 = AddStudent(_course, "s1", "One");
        var s2 = AddStudent(_course, "s2", "Two");
        var a = AddAssignment(_course, "A", deadlineDaysFromNow: -5);
        var b = AddAssignment(_course, "B", deadlineDaysFromNow: -5);

        AddSubmission(a, s1, submittedDaysFromNow: -1);
        AddSubmission(b, s1, submittedDaysFromNow: -1);
        AddSubmission(b, s2, submittedDaysFromNow: -2);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(3, result.LateCount);
        Assert.Equal("B", result.LateAssignmentName);
    }

    [Fact]
    public async Task NeedsAttention_ListsLateFirst_ThenAwaiting_ThenMissing()
    {
        //ARRANGE: one student of each kind, on three assignments
        var late = AddStudent(_course, "s1", "Late Student");
        var awaiting = AddStudent(_course, "s2", "Awaiting Student");
        var missing = AddStudent(_course, "s3", "Missing Student");

        var pastDeadline = AddAssignment(_course, "Past", deadlineDaysFromNow: -6);
        // late handed in after the deadline, awaiting handed in on time, missing handed in nothing
        AddSubmission(pastDeadline, late, submittedDaysFromNow: -1);
        AddSubmission(pastDeadline, awaiting, submittedDaysFromNow: -8);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(
            [SubmissionStatus.Late, SubmissionStatus.AwaitingFeedback, SubmissionStatus.Missing],
            result.NeedsAttention.Select(i => i.Status));
        Assert.Equal(
            ["Late Student", "Awaiting Student", "Missing Student"],
            result.NeedsAttention.Select(i => i.StudentName));
        Assert.Equal(missing.Id, result.NeedsAttention[2].StudentId);
    }

    [Fact]
    public async Task NeedsAttention_ShowsAtMostFive_ButCountsUseAllRows()
    {
        //ARRANGE: seven students who all missed the same deadline
        for (var i = 1; i <= 7; i++)
            AddStudent(_course, $"s{i}", $"Student {i}");
        AddAssignment(_course, "Exercise 1", deadlineDaysFromNow: -2);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(5, result.NeedsAttention.Count);
        Assert.Equal(7, result.MissingCount);
    }

    [Fact]
    public async Task AssignmentsInUnknownCourses_AreIgnored()
    {
        //ARRANGE: an assignment whose course is not in the course list
        var otherCourse = new Course { Id = Guid.NewGuid(), Name = "Gone" };
        AddAssignment(otherCourse, "Orphan", deadlineDaysFromNow: -3);

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        Assert.Equal(0, result.MissingCount);
        Assert.Empty(result.NeedsAttention);
    }

    [Fact]
    public async Task FinishedAndUpcomingCourses_AreNotCounted()
    {
        //ARRANGE: a finished and a not yet started course, each with a student who missed a deadline
        var finished = AddCourse("Finished", Today().AddDays(-90), Today().AddDays(-30));
        var upcoming = AddCourse("Upcoming", Today().AddDays(10), Today().AddDays(90));

        foreach (var course in new[] { finished, upcoming })
        {
            AddStudent(course, $"s-{course.Name}", $"{course.Name} Student");
            AddAssignment(course, $"{course.Name} task", deadlineDaysFromNow: -3);
        }

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT: only courses that run today are counted, so nothing is missing
        Assert.Equal(0, result.MissingCount);
        Assert.Equal(0, result.LateCount);
        Assert.Empty(result.NeedsAttention);
    }

    [Fact]
    public async Task MembersAndModules_AreLoadedInOneQueryEach_NotPerCourse()
    {
        //ARRANGE: two running courses
        AddCourse("Second course", Today().AddDays(-5), Today().AddDays(5));

        //ACT
        await _service.GetForTeacherAsync();

        //ASSERT: one call for all members, one for all modules, and no call per course
        _courseUsers.Verify(r => r.GetByCourseIdsAsync(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        _courseUsers.Verify(r => r.GetByCourseIdAsync(It.IsAny<Guid>()), Times.Never);
        _modules.Verify(r => r.GetByCourseIdsAsync(It.IsAny<IEnumerable<Guid>>()), Times.Once);
        _modules.Verify(r => r.GetByCourseIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CurrentCourses_OnlyIncludeCoursesThatRunToday()
    {
        //ARRANGE: the default course runs today, the other two do not
        AddCourse("Finished", Today().AddDays(-90), Today().AddDays(-30));
        AddCourse("Upcoming", Today().AddDays(10), Today().AddDays(90));

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        var course = Assert.Single(result.CurrentCourses);
        Assert.Equal("Fullstack .NET", course.Name);
    }

    [Fact]
    public async Task CurrentCourse_ShowsTheStudentCount_AndTheRunningModuleWithProgress()
    {
        //ARRANGE: two students and a module that started 10 days ago and ends in 10 days (halfway)
        AddStudent(_course, "s1", "One");
        AddStudent(_course, "s2", "Two");
        _courseModules.Add(new Module
        {
            Id = Guid.NewGuid(),
            Name = "Blazor",
            CourseId = _course.Id,
            StartDate = Today().AddDays(-10),
            EndDate = Today().AddDays(10)
        });

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        var course = Assert.Single(result.CurrentCourses);
        Assert.Equal(2, course.StudentCount);
        Assert.Equal("Blazor", course.CurrentModuleName);
        Assert.Equal(50, course.ProgressPercent);
    }

    [Fact]
    public async Task CurrentCourse_WithoutModules_HasNoModuleName()
    {
        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        var course = Assert.Single(result.CurrentCourses);
        Assert.Null(course.CurrentModuleName);
        Assert.Equal(0, course.ProgressPercent);
    }

    [Fact]
    public async Task UpcomingActivities_AreMappedWithTypeModuleAndCourseName()
    {
        //ARRANGE
        var module = new Module
        {
            Id = Guid.NewGuid(),
            Name = "Blazor",
            CourseId = _course.Id,
            Course = _course
        };
        _upcoming.Add(new Activity
        {
            Id = Guid.NewGuid(),
            Name = "Lecture 1",
            Module = module,
            ModuleId = module.Id,
            ActivityType = new ActivityType { Id = Guid.NewGuid(), Name = "Lecture" },
            StartTime = DateTime.Today.AddDays(1).AddHours(9),
            EndTime = DateTime.Today.AddDays(1).AddHours(10)
        });

        //ACT
        var result = await _service.GetForTeacherAsync();

        //ASSERT
        var item = Assert.Single(result.UpcomingActivities);
        Assert.Equal("Lecture 1", item.Name);
        Assert.Equal("Lecture", item.ActivityTypeName);
        Assert.Equal("Blazor", item.ModuleName);
        Assert.Equal("Fullstack .NET", item.CourseName);
    }

    // ---------- Helpers ----------

    // "Today" in Swedish time, the same day the service uses for the courses and modules
    private static DateOnly Today() => DateOnly.FromDateTime(CourseTime.FromUtc(DateTime.UtcNow));

    private Course AddCourse(string name, DateOnly start, DateOnly end)
    {
        var course = new Course { Id = Guid.NewGuid(), Name = name, StartDate = start, EndDate = end };
        _courses.Add(course);
        return course;
    }

    // A student who is a member of the course (the role is "Student")
    private ApplicationUser AddStudent(Course course, string id, string name)
    {
        var user = new ApplicationUser { Id = id, Name = name };
        AddMember(course, user, UserRoles.Student);
        return user;
    }

    private void AddMember(Course course, ApplicationUser user, string role)
    {
        _roles[user.Id] = role;

        if (!_membersByCourse.TryGetValue(course.Id, out var members))
            _membersByCourse[course.Id] = members = [];

        members.Add(new CourseUser { CourseId = course.Id, UserId = user.Id, User = user });
    }

    // An assignment with a deadline "deadlineDaysFromNow" days from now, as Swedish wall-clock time
    private Activity AddAssignment(Course course, string name, int deadlineDaysFromNow)
    {
        var assignment = new Activity
        {
            Id = Guid.NewGuid(),
            Name = name,
            StartTime = CourseTime.FromUtc(DateTime.UtcNow.AddDays(deadlineDaysFromNow - 7)),
            EndTime = CourseTime.FromUtc(DateTime.UtcNow.AddDays(deadlineDaysFromNow)),
            ActivityType = new ActivityType { Id = Guid.NewGuid(), Name = ActivityTypeNames.Assignment },
            Module = new Module { Id = Guid.NewGuid(), Name = "Module", CourseId = course.Id, Course = course }
        };
        assignment.ModuleId = assignment.Module.Id;
        _assignments.Add(assignment);
        return assignment;
    }

    // A hand-in (UTC) "submittedDaysFromNow" days from now, or "hoursAgo" hours ago when that is given.
    // With feedback, the feedback is newer than the hand-in, which makes the status "Feedback given".
    private static Submission AddSubmission(
        Activity assignment, ApplicationUser student, int submittedDaysFromNow,
        bool withFeedback = false, int? hoursAgo = null)
    {
        var submittedAt = hoursAgo is { } hours
            ? DateTime.UtcNow.AddHours(-hours)
            : DateTime.UtcNow.AddDays(submittedDaysFromNow);

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ActivityId = assignment.Id,
            StudentId = student.Id,
            Student = student,
            SubmittedAt = submittedAt,
            Feedbacks = withFeedback
                ? [new Feedback { Id = Guid.NewGuid(), Text = "Good", CreatedAt = submittedAt.AddHours(1) }]
                : []
        };

        assignment.Submissions.Add(submission);
        return submission;
    }
}