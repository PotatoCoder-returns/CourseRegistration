namespace CourseRegistration;

public enum EnrollmentStatus
{
    Enrolled,
    Waitlisted,
    AlreadyEnrolled,
    AlreadyWaitlisted,
    PrerequisiteNotMet,
    InvalidLearnerId,
    Dropped,
    DroppedAndPromoted,
    NotEnrolled
}

public sealed record EnrollmentResult(EnrollmentStatus Status, string Message)
{
    /// <summary>Learners moved from the waitlist into the course by this call, in promotion order.</summary>
    public IReadOnlyList<string> PromotedLearnerIds { get; init; } = Array.Empty<string>();
}

/// <summary>Source of truth for "has this learner completed this prerequisite?".</summary>
public interface IPrerequisiteRecords
{
    bool HasCompleted(string learnerId, string prerequisite);
}

/// <summary>Thread-safe in-memory prerequisite records. Grant/Revoke let eligibility change over time.</summary>
public sealed class InMemoryPrerequisiteRecords : IPrerequisiteRecords
{
    private readonly object _gate = new();
    private readonly HashSet<(string Learner, string Prerequisite)> _completed = new();

    public void Grant(string learnerId, string prerequisite) { lock (_gate) _completed.Add((learnerId, prerequisite)); }
    public void Revoke(string learnerId, string prerequisite) { lock (_gate) _completed.Remove((learnerId, prerequisite)); }
    public bool HasCompleted(string learnerId, string prerequisite) { lock (_gate) return _completed.Contains((learnerId, prerequisite)); }
}

public sealed class CourseOffering
{
    internal object SyncRoot { get; } = new();
    internal HashSet<string> Enrolled { get; } = new(StringComparer.Ordinal);
    internal List<string> Waiting { get; } = new(); // insertion order == FIFO order

    public CourseOffering(int capacity, string requiredPrerequisite)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");
        if (string.IsNullOrWhiteSpace(requiredPrerequisite))
            throw new ArgumentException("A required prerequisite must be provided.", nameof(requiredPrerequisite));

        Capacity = capacity;
        RequiredPrerequisite = requiredPrerequisite;
    }

    public int Capacity { get; }
    public string RequiredPrerequisite { get; }

    /// <summary>Snapshot of enrolled learner IDs.</summary>
    public IReadOnlyCollection<string> EnrolledLearners { get { lock (SyncRoot) return Enrolled.ToArray(); } }

    /// <summary>Snapshot of the waitlist, front of the queue first.</summary>
    public IReadOnlyList<string> Waitlist { get { lock (SyncRoot) return Waiting.ToArray(); } }
}

public sealed class EnrollmentService
{
    private readonly CourseOffering _course;
    private readonly IPrerequisiteRecords _records;

    public EnrollmentService(CourseOffering course, IPrerequisiteRecords records)
    {
        _course = course;
        _records = records;
    }

    public EnrollmentResult Enroll(string? learnerId)
    {
        if (string.IsNullOrWhiteSpace(learnerId))
            return new(EnrollmentStatus.InvalidLearnerId, "Learner ID is required.");

        lock (_course.SyncRoot)
        {
            if (_course.Enrolled.Contains(learnerId))
                return new(EnrollmentStatus.AlreadyEnrolled, "Learner is already enrolled.");
            if (_course.Waiting.Contains(learnerId))
                return new(EnrollmentStatus.AlreadyWaitlisted, "Learner is already on the waitlist.");
            if (!IsEligible(learnerId))
                return new(EnrollmentStatus.PrerequisiteNotMet, "Required prerequisite has not been completed.");

            // A seat can be open while ineligible learners were being skipped. Serve the queue first
            // so a newcomer can never jump ahead of an eligible waiting learner.
            var promoted = FillOpenSeats();

            if (_course.Enrolled.Count < _course.Capacity)
            {
                _course.Enrolled.Add(learnerId);
                return new(EnrollmentStatus.Enrolled, "Enrollment successful.") { PromotedLearnerIds = promoted };
            }

            _course.Waiting.Add(learnerId);
            return new(EnrollmentStatus.Waitlisted, "Course is full; learner added to the waitlist.") { PromotedLearnerIds = promoted };
        }
    }

    public EnrollmentResult Drop(string? learnerId)
    {
        if (string.IsNullOrWhiteSpace(learnerId))
            return new(EnrollmentStatus.InvalidLearnerId, "Learner ID is required.");

        lock (_course.SyncRoot)
        {
            if (!_course.Enrolled.Remove(learnerId))
                return new(EnrollmentStatus.NotEnrolled, "Learner is not currently enrolled.");

            var promoted = FillOpenSeats();
            return promoted.Count == 0
                ? new(EnrollmentStatus.Dropped, "Enrollment dropped; no eligible learner was waiting.")
                : new(EnrollmentStatus.DroppedAndPromoted, $"Enrollment dropped; promoted {string.Join(", ", promoted)}.") { PromotedLearnerIds = promoted };
        }
    }

    private bool IsEligible(string learnerId) => _records.HasCompleted(learnerId, _course.RequiredPrerequisite);

    // Policy: SKIP AND RETAIN. Walk the queue front to back, re-checking eligibility now.
    // An ineligible learner is skipped but keeps their place; the first eligible learner is promoted.
    // Must be called while holding the course lock.
    private List<string> FillOpenSeats()
    {
        var promoted = new List<string>();
        var i = 0;
        while (i < _course.Waiting.Count && _course.Enrolled.Count < _course.Capacity)
        {
            var candidate = _course.Waiting[i];
            if (IsEligible(candidate))
            {
                _course.Waiting.RemoveAt(i);
                _course.Enrolled.Add(candidate);
                promoted.Add(candidate);
            }
            else
            {
                i++;
            }
        }

        return promoted;
    }
}
