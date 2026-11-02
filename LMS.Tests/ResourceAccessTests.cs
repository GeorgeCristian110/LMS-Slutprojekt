using Domain.Contracts;
using Domain.Contracts.Repositories;
using Domain.Models.Entities;
using Domain.Models.Exceptions;
using LMS.Services;
using Moq;
using Service.Contracts;

namespace LMS.Tests;

/// <summary>
/// US16, criterion 3: changing an id in the URL must not open another course, a module in another
/// course or another student's feedback. Each rule answers 404, the same as for an id that does not
/// exist, so a student cannot find out what exists. Documents and hand-ins are covered in
/// DocumentServiceStudentTests, DocumentServiceDownloadTests and SubmissionServiceTests.
/// </summary>
public class ResourceAccessTests
{
    private const string StudentId = "student-1";
    private const string ClassmateId = "student-2";
    private const string TeacherId = "teacher-1";

    private readonly Guid _ownCourseId = Guid.NewGuid();
    private readonly Guid _otherCourseId = Guid.NewGuid();
    private readonly Guid _ownModuleId = Guid.NewGuid();
    private readonly Guid _otherModuleId = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ResourceAccessTests()
    {
        // The student is enrolled in one course only
        var courseUsers = new Mock<ICourseUserRepository>();
        courseUsers
            .Setup(r => r.GetByUserIdAsync(StudentId))
            .ReturnsAsync(new List<CourseUser> { new() { CourseId = _ownCourseId, UserId = StudentId } });

        // Both courses exist, so a 404 can only come from the ownership rule
        var courses = new Mock<ICourseRepository>();
        courses.Setup(r => r.GetCourseByIdAsync(_ownCourseId)).ReturnsAsync(new Course { Id = _ownCourseId });
        courses.Setup(r => r.GetCourseByIdAsync(_otherCourseId)).ReturnsAsync(new Course { Id = _otherCourseId });

        // One module in the student's course and one in another course
        var modules = new Mock<IModuleRepository>();
        modules
            .Setup(r => r.GetByIdAsync(_ownModuleId, It.IsAny<bool>()))
            .ReturnsAsync(new Module { Id = _ownModuleId, CourseId = _ownCourseId });
        modules
            .Setup(r => r.GetByIdAsync(_otherModuleId, It.IsAny<bool>()))
            .ReturnsAsync(new Module { Id = _otherModuleId, CourseId = _otherCourseId });
        modules.Setup(r => r.GetByCourseIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Module>());

        var activities = new Mock<IActivityRepository>();
        activities.Setup(r => r.GetByModuleIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<Activity>());

        _unitOfWork.Setup(u => u.CourseUsers).Returns(courseUsers.Object);
        _unitOfWork.Setup(u => u.Courses).Returns(courses.Object);
        _unitOfWork.Setup(u => u.Modules).Returns(modules.Object);
        _unitOfWork.Setup(u => u.Activities).Returns(activities.Object);
    }

    /* Modules: GET api/courses/{courseId}/modules */

    [Fact]
    public async Task Student_CanList_ModulesOfOwnCourse()
    {
        var service = new ModuleService(_unitOfWork.Object);

        // ACT + ASSERT: no exception (the mock returns no modules, that is not the point here)
        await service.GetByCourseIdAsync(_ownCourseId, StudentId, isTeacher: false);
    }

    [Fact]
    public async Task Student_CannotList_ModulesOfOtherCourse()
    {
        var service = new ModuleService(_unitOfWork.Object);

        await Assert.ThrowsAsync<CourseNotFoundException>(
            () => service.GetByCourseIdAsync(_otherCourseId, StudentId, isTeacher: false));
    }

    [Fact]
    public async Task Teacher_CanList_ModulesOfAnyCourse()
    {
        var service = new ModuleService(_unitOfWork.Object);

        await service.GetByCourseIdAsync(_otherCourseId, TeacherId, isTeacher: true);
    }

    /* Activities: GET api/student/modules/{moduleId}/activities */

    [Fact]
    public async Task Student_CanList_ActivitiesOfModuleInOwnCourse()
    {
        var service = new ActivityService(_unitOfWork.Object);

        // ACT + ASSERT: no exception. Without this test, a rule that refused everyone would still pass.
        await service.GetForStudentAsync(_ownModuleId, StudentId);
    }

    [Fact]
    public async Task Student_CannotList_ActivitiesOfModuleInOtherCourse()
    {
        var service = new ActivityService(_unitOfWork.Object);

        await Assert.ThrowsAsync<ModuleNotFoundException>(
            () => service.GetForStudentAsync(_otherModuleId, StudentId));
    }

    /* Feedback: GET api/submissions/{submissionId}/feedback */

    [Fact]
    public async Task Student_CannotRead_FeedbackOnClassmatesSubmission()
    {
        var submissionId = Guid.NewGuid();
        var submissions = new Mock<ISubmissionRepository>();
        submissions
            .Setup(r => r.GetByIdAsync(submissionId))
            .ReturnsAsync(new Submission { Id = submissionId, StudentId = ClassmateId });
        _unitOfWork.Setup(u => u.Submissions).Returns(submissions.Object);

        var service = new FeedbackService(_unitOfWork.Object, Mock.Of<INotificationService>());

        await Assert.ThrowsAsync<SubmissionNotFoundException>(
            () => service.GetBySubmissionIdAsync(submissionId, StudentId, isTeacher: false));
    }

    [Fact]
    public async Task Teacher_CanRead_FeedbackOnAnySubmission()
    {
        var submissionId = Guid.NewGuid();
        var submissions = new Mock<ISubmissionRepository>();
        submissions
            .Setup(r => r.GetByIdAsync(submissionId))
            .ReturnsAsync(new Submission { Id = submissionId, StudentId = ClassmateId });

        // The teacher gets past the ownership check, so the service then reads the feedback list
        var feedbacks = new Mock<IFeedbackRepository>();
        feedbacks.Setup(r => r.GetBySubmissionIdAsync(submissionId)).ReturnsAsync(new List<Feedback>());

        _unitOfWork.Setup(u => u.Submissions).Returns(submissions.Object);
        _unitOfWork.Setup(u => u.Feedbacks).Returns(feedbacks.Object);

        var service = new FeedbackService(_unitOfWork.Object, Mock.Of<INotificationService>());

        // ACT + ASSERT: no exception for someone else's submission
        await service.GetBySubmissionIdAsync(submissionId, TeacherId, isTeacher: true);
    }
}
