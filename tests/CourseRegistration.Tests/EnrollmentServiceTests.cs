using CourseRegistration;
using Xunit;

public class EnrollmentServiceTests
{
    private const string Intro = "Intro";

    private static (CourseOffering Course, InMemoryPrerequisiteRecords Records, EnrollmentService Service) Setup(
        int capacity, params string[] eligibleLearners)
    {
        var course = new CourseOffering(capacity, Intro);
        var records = new InMemoryPrerequisiteRecords();
        foreach (var id in eligibleLearners) records.Grant(id, Intro);
        return (course, records, new EnrollmentService(course, records));
    }

    // Criterion 1: capacity available

    [Fact]
    public void EnrollsWhenCapacityIsAvailableAndPrerequisiteMet()
    {
        var (course, _, service) = Setup(2, "A");

        var result = service.Enroll("A");

        Assert.Equal(EnrollmentStatus.Enrolled, result.Status);
        Assert.Contains("A", course.EnrolledLearners);
        Assert.Empty(course.Waitlist);
    }

    // Criterion 2: full course and duplicates

    [Fact]
    public void FullCourseAddsEligibleLearnerToWaitlistOnce()
    {
        var (course, _, service) = Setup(1, "A", "B");
        service.Enroll("A");

        Assert.Equal(EnrollmentStatus.Waitlisted, service.Enroll("B").Status);
        Assert.Equal(EnrollmentStatus.AlreadyWaitlisted, service.Enroll("B").Status);

        Assert.Equal(new[] { "B" }, course.Waitlist);
        Assert.Single(course.EnrolledLearners);
    }

    [Fact]
    public void DuplicateEnrollOfEnrolledLearnerChangesNothing()
    {
        var (course, _, service) = Setup(2, "A");
        service.Enroll("A");

        var result = service.Enroll("A");

        Assert.Equal(EnrollmentStatus.AlreadyEnrolled, result.Status);
        Assert.Single(course.EnrolledLearners);
        Assert.Empty(course.Waitlist);
    }

    // Criterion 3: FIFO promotion

    [Fact]
    public void DropsPromoteWaitlistedLearnersInStrictFifoOrder()
    {
        var (course, _, service) = Setup(2, "A", "B", "C", "D", "E");
        foreach (var id in new[] { "A", "B", "C", "D", "E" }) service.Enroll(id);

        var first = service.Drop("A");
        Assert.Equal(EnrollmentStatus.DroppedAndPromoted, first.Status);
        Assert.Equal(new[] { "C" }, first.PromotedLearnerIds);

        var second = service.Drop("B");
        Assert.Equal(new[] { "D" }, second.PromotedLearnerIds);

        Assert.Equal(new[] { "E" }, course.Waitlist);
        Assert.Equal(2, course.EnrolledLearners.Count);
    }

    [Fact]
    public void DropWithEmptyWaitlistJustOpensSeat()
    {
        var (course, _, service) = Setup(1, "A");
        service.Enroll("A");

        var result = service.Drop("A");

        Assert.Equal(EnrollmentStatus.Dropped, result.Status);
        Assert.Empty(result.PromotedLearnerIds);
        Assert.Empty(course.EnrolledLearners);
    }

    // Criterion 4: prerequisite failure and the skip-and-retain policy

    [Fact]
    public void IneligibleLearnerIsNotEnrolledWhenSeatsAreAvailable()
    {
        var (course, _, service) = Setup(1); // nobody has the prerequisite

        var result = service.Enroll("A");

        Assert.Equal(EnrollmentStatus.PrerequisiteNotMet, result.Status);
        Assert.Empty(course.EnrolledLearners);
        Assert.Empty(course.Waitlist);
    }

    [Fact]
    public void IneligibleLearnerIsNotWaitlistedWhenCourseIsFull()
    {
        var (course, _, service) = Setup(1, "A");
        service.Enroll("A");

        var result = service.Enroll("Z");

        Assert.Equal(EnrollmentStatus.PrerequisiteNotMet, result.Status);
        Assert.Empty(course.Waitlist);
    }

    [Fact]
    public void IneligibleLearnerAtFrontIsSkippedAndRetained()
    {
        var (course, records, service) = Setup(1, "A", "B", "C");
        foreach (var id in new[] { "A", "B", "C" }) service.Enroll(id);
        records.Revoke("B", Intro); // B's eligibility lapses while waiting

        var result = service.Drop("A");

        Assert.Equal(new[] { "C" }, result.PromotedLearnerIds); // C promoted past B
        Assert.Equal(new[] { "C" }, course.EnrolledLearners);
        Assert.Equal(new[] { "B" }, course.Waitlist);           // B retained, not removed
    }

    [Fact]
    public void RetainedLearnerKeepsTheirPlaceInLine()
    {
        var (course, records, service) = Setup(1, "A", "B", "C", "D");
        foreach (var id in new[] { "A", "B", "C" }) service.Enroll(id);
        records.Revoke("B", Intro);
        service.Drop("A");                 // C promoted, B retained
        records.Grant("B", Intro);         // B regains eligibility
        service.Enroll("D");               // D joins behind B

        Assert.Equal(new[] { "B", "D" }, course.Waitlist);

        var result = service.Drop("C");

        Assert.Equal(new[] { "B" }, result.PromotedLearnerIds); // B ahead of D
        Assert.Equal(new[] { "D" }, course.Waitlist);
    }

    [Fact]
    public void SeatStaysOpenWhenEveryWaitingLearnerIsIneligible()
    {
        var (course, records, service) = Setup(1, "A", "B");
        service.Enroll("A");
        service.Enroll("B");
        records.Revoke("B", Intro);

        var result = service.Drop("A");

        Assert.Equal(EnrollmentStatus.Dropped, result.Status);
        Assert.Empty(course.EnrolledLearners);
        Assert.Equal(new[] { "B" }, course.Waitlist);
    }

    [Fact]
    public void NewcomerCannotJumpAheadOfWaitingLearnerWhoRegainedEligibility()
    {
        var (course, records, service) = Setup(1, "A", "B", "C");
        service.Enroll("A");
        service.Enroll("B");
        records.Revoke("B", Intro);
        service.Drop("A");                 // seat open, B retained
        records.Grant("B", Intro);         // B eligible again

        var result = service.Enroll("C");  // C arrives while a seat is open

        Assert.Equal(new[] { "B" }, result.PromotedLearnerIds);
        Assert.Equal(EnrollmentStatus.Waitlisted, result.Status);
        Assert.Equal(new[] { "B" }, course.EnrolledLearners);
        Assert.Equal(new[] { "C" }, course.Waitlist);
    }

    // Criterion 5: invalid and repeated requests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankLearnerIdsAreRejectedWithoutChangingState(string? id)
    {
        var (course, _, service) = Setup(1, "A");
        service.Enroll("A");

        Assert.Equal(EnrollmentStatus.InvalidLearnerId, service.Enroll(id).Status);
        Assert.Equal(EnrollmentStatus.InvalidLearnerId, service.Drop(id).Status);
        Assert.Equal(new[] { "A" }, course.EnrolledLearners);
        Assert.Empty(course.Waitlist);
    }

    [Fact]
    public void DroppingUnknownOrAlreadyDroppedLearnerDoesNotCorruptState()
    {
        var (course, _, service) = Setup(1, "A", "B");
        service.Enroll("A");
        service.Enroll("B");

        Assert.Equal(EnrollmentStatus.NotEnrolled, service.Drop("missing").Status);
        Assert.Equal(EnrollmentStatus.DroppedAndPromoted, service.Drop("A").Status);
        Assert.Equal(EnrollmentStatus.NotEnrolled, service.Drop("A").Status); // repeated drop

        Assert.Equal(new[] { "B" }, course.EnrolledLearners); // B promoted exactly once
        Assert.Empty(course.Waitlist);
    }

    [Fact]
    public void DroppingAWaitlistedLearnerIsRejectedAndLeavesQueueIntact()
    {
        var (course, _, service) = Setup(1, "A", "B");
        service.Enroll("A");
        service.Enroll("B");

        Assert.Equal(EnrollmentStatus.NotEnrolled, service.Drop("B").Status);
        Assert.Equal(new[] { "B" }, course.Waitlist);
    }

    [Fact]
    public void CourseOfferingRejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CourseOffering(0, Intro));
        Assert.Throws<ArgumentException>(() => new CourseOffering(1, " "));
    }

    // Criterion 6: concurrency

    [Fact]
    public void ConcurrentEnrollmentNeverExceedsCapacity()
    {
        var ids = Enumerable.Range(0, 100).Select(i => $"L{i}").ToArray();
        var (course, _, service) = Setup(5, ids);

        Parallel.ForEach(ids, id => service.Enroll(id));

        Assert.Equal(5, course.EnrolledLearners.Count);
        Assert.Equal(95, course.Waitlist.Count);
        Assert.Empty(course.EnrolledLearners.Intersect(course.Waitlist));
    }

    [Fact]
    public void ConcurrentDuplicateRequestsForOneLearnerCreateOneRecord()
    {
        var (course, _, service) = Setup(1, "A");

        var results = new System.Collections.Concurrent.ConcurrentBag<EnrollmentStatus>();
        Parallel.For(0, 50, _ => results.Add(service.Enroll("A").Status));

        Assert.Equal(1, results.Count(s => s == EnrollmentStatus.Enrolled));
        Assert.Equal(49, results.Count(s => s == EnrollmentStatus.AlreadyEnrolled));
        Assert.Single(course.EnrolledLearners);
        Assert.Empty(course.Waitlist);
    }

    [Fact]
    public void ConcurrentDropsAndEnrollsKeepStateConsistent()
    {
        var initial = new[] { "E1", "E2", "E3" };
        var queued = Enumerable.Range(0, 20).Select(i => $"W{i}").ToArray();
        var late = Enumerable.Range(0, 10).Select(i => $"N{i}").ToArray();
        var (course, _, service) = Setup(3, initial.Concat(queued).Concat(late).ToArray());
        foreach (var id in initial.Concat(queued)) service.Enroll(id);

        var work = initial.Select(id => (Action)(() => service.Drop(id)))
            .Concat(late.Select(id => (Action)(() => service.Enroll(id)))).ToArray();
        Parallel.Invoke(work);

        var enrolled = course.EnrolledLearners;
        var waiting = course.Waitlist;
        Assert.Equal(3, enrolled.Count);                         // capacity respected and refilled
        Assert.Empty(enrolled.Intersect(waiting));               // nobody in both places
        Assert.Equal(waiting.Count, waiting.Distinct().Count()); // no duplicate queue entries
        Assert.Equal(30, enrolled.Count + waiting.Count);        // 20 queued + 10 late, 3 dropped gone
    }
}
