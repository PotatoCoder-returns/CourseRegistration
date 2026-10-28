# Design Specification: Course Registration

## Scope
Enrollment, dropping, and FIFO waitlist promotion for a single course offering. It is a bounded, in-memory .NET 8 service slice, not a registration platform.

## Service boundary
- `EnrollmentService.Enroll(learnerId)` and `EnrollmentService.Drop(learnerId)` are the only mutating operations.
- `CourseOffering` owns capacity, the required prerequisite, the enrolled set, and the waitlist. It exposes read-only snapshots (`EnrolledLearners`, `Waitlist`).
- `IPrerequisiteRecords.HasCompleted(learnerId, prerequisite)` is the service's only view of eligibility. `InMemoryPrerequisiteRecords` is the implementation used here, with `Grant`/`Revoke` so eligibility can change over time.

## Data model
- Enrolled learners: `HashSet<string>`.
- Waitlist: `List<string>` in insertion order, so list order is FIFO order.
- `EnrollmentResult`: a status enum, a message, and `PromotedLearnerIds` (the learners moved into the course by that call).

## Invariants
1. `Enrolled.Count <= Capacity` at all times.
2. A learner appears at most once across enrolled + waitlist.
3. Everyone enrolled or newly waitlisted satisfied the prerequisite at the moment they were enrolled, waitlisted, or promoted.
4. Waitlist order is arrival order. Skipping a learner never reorders the queue.
5. Invalid, duplicate, or unknown requests never change state.

## Waitlist-promotion rule: skip and retain
`FillOpenSeats` walks the queue front to back while seats are free. For each learner it re-checks eligibility now:
- **Eligible:** removed from the queue and enrolled.
- **Ineligible:** skipped, **kept in place**, and the walk continues.

This runs on every `Drop`, and also at the start of `Enroll`. Without that second call, a seat could stay open after a drop (everyone waiting was ineligible), the waiting learner could become eligible again, and a newcomer could then take the seat ahead of them. Serving the queue first prevents that.

Eligibility is re-checked at promotion time rather than only at queue entry. Otherwise a learner who loses eligibility while waiting could never be skipped, and the policy would be unreachable.

## Request behavior

| Situation | Result |
|---|---|
| Null/blank learner ID | `InvalidLearnerId` (Enroll and Drop) |
| Already enrolled | `AlreadyEnrolled` |
| Already waitlisted | `AlreadyWaitlisted` |
| Prerequisite not met (seat free or course full) | `PrerequisiteNotMet`; not enrolled or waitlisted |
| Seat free and eligible | `Enrolled` |
| Full and eligible | `Waitlisted` |
| Drop of enrolled learner, someone promoted | `DroppedAndPromoted` + `PromotedLearnerIds` |
| Drop of enrolled learner, nobody eligible waiting | `Dropped` |
| Drop of anyone not enrolled (unknown, repeated, or waitlisted-only) | `NotEnrolled`; no state change |

## Concurrency
One lock per `CourseOffering` guards every read and write of its state, so each operation is atomic within the process. The prerequisite records are called while that lock is held, so any real implementation must be fast and must not call back into the service.

## Assumptions and trade-offs
- One offering and one required prerequisite, as the scope asks. No policy engine.
- Dropping a learner who is only waitlisted is reported as `NotEnrolled`. Leaving the waitlist is not part of this slice.
- `PrerequisiteNotMet` is checked after the duplicate checks, so an already-enrolled learner gets `AlreadyEnrolled` even if their prerequisite is later revoked.
- Eligibility is the caller's data via `IPrerequisiteRecords`. The service does not decide how prerequisites are earned.
