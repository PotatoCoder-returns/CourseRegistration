# Course Registration: AI-Assisted Engineering Assessment

A small .NET 8 service slice for **one course offering**: enroll, drop, and FIFO waitlist promotion. It uses an in-memory store because no starter project was supplied. There is no frontend, database, auth, or deployment.

## Setup and run

Requires the .NET 8 SDK.

```bash
dotnet run --project src/CourseRegistration     # small console demo
dotnet test CourseRegistration.sln              # 20 automated tests
```

## Assumptions (short version, full detail in `docs/design-spec.md`)

- **Prerequisite rule:** a learner is eligible if the prerequisite records say they have completed the offering's single required prerequisite (`Intro` in the demo).
- **Waitlist policy: skip and retain.** If an ineligible learner is at the front of the queue when a seat opens, they are skipped but **keep their place**. The first *eligible* learner behind them is promoted. Eligibility is re-checked at promotion time, not just when someone joins.
- A learner can be enrolled **or** waitlisted at most once.
- Ineligible learners are never enrolled, waitlisted, or promoted.

## Layout

| Path | What it is |
|---|---|
| `src/CourseRegistration/EnrollmentService.cs` | Service, offering state, prerequisite records |
| `src/CourseRegistration/Program.cs` | Console demo |
| `tests/CourseRegistration.Tests/` | xUnit tests |
| `docs/design-spec.md` | Design specification |
| `docs/ai-usage.md` | AI prompt and use record, including verification |
| `docs/limitations.md` | Limitations and follow-up risks |
