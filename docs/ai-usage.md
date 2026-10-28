# AI Prompt and Use Record

I have used AI in two ways on this assessment. I want to be clear and honest about what each contributed, so this is written as it happened.

## Activity / Use Case 1: Initial implementation

- **Context:** I gave the assessment PDF to an LLM and asked it to produce the solution. Here is the prompt that was used.

- **Prompt Used for Code Generation :** I want to build a course registration and waitlist feature. I want to use AI to help me with it as an assistant, and still keep me in the loop (Just as I usually do)

The solution should be in C# and .NET since that's what I work with. I only have the assessment PDF, no starter code or repository. The prompt should explain the requirements in a simple way and ask for a basic working solution with proper handling of enrollment, waitlisting, dropping a course, and promoting the next eligible learner.

I also want unit tests for the important and negative scenarios, including duplicates, prerequisites, FIFO order, invalid drops, and concurrent requests. Keep the solution simple and straightforward instead of overengineering it.

- **AI output (summary):** The LLM provided me with a .NET 8 project with `EnrollmentService` / `CourseOffering`, an xUnit test file, a README, a design spec, and a template AI-usage file.
- **Decision:** used the generated code as a **starting draft only**, and did not trust until reviewed manually (Activity 2).
- **Informed:** the initial structure of `src/`, `tests/`, and `docs/`.

## Activity 2: Review against the assessment

- **Prompts (summarized):** I have then reviewed the generated code, examined the methods which were written, ran the tests and then the code, observed that not all the cases were covered and the retention and removal of ineligible users was not covered, Hence used a secondary LLM as a judge to go through the code, provided my observations on the missing gaps and asked to address them.

- **AI output (summary):** Then a list of gaps in the generated project (below) were identified and were rewritten to address them.

## Activity 2: Documentation and other information

- **Prompts (summarized):** Please create a couple of simple markdown documents for this project, one explaining the design, assumptions, and key implementation decisions that were taken through this course, and another documenting how I have taken the assistance of AI during development, including prompts, suggestions I accepted or changed, and how I verified the code. Keep both documents short and natural, like something a developer would write at work. Also include setup instructions, limitations, and possible improvements where relevant. Don't make up decisions or test results that haven't been verified.

- **AI output (summary):** Created three Markdown documents: `docs/design-spec.md`, covering the implementation approach, assumptions, enrollment and waitlist rules, FIFO promotion, and synchronization; and `docs/ai-usage.md`, documenting AI-assisted development and verification. Also included setup instructions. `docs/limitations.md` for the current limirations and possible future improvements. The documents were added to the project under the folder `docs/` for review.

| # | Finding | Decision | What changed |
|---|---|---|---|
| 1 | Then the secondary LLM **removed** ineligible learners from the queue while the docs said "removed/skipped". That is not a single consistent policy (criterion 4). | **Accepted.** I chose **skip and retain**. | `FillOpenSeats` skips ineligible learners and leaves them in place. Spec and README state the policy. |
| 2 | Eligibility was snapshotted at queue entry, so an ineligible learner could never reach the front. The policy was unreachable except by editing internals in a test. | **Accepted.** | Added `IPrerequisiteRecords` and re-check at promotion time. Tests use `Grant`/`Revoke`. |
| 3 | Test gaps: strict FIFO over several drops, duplicate enroll of an enrolled learner, blank IDs, ineligible when full, concurrent drops. | **Accepted.** | Tests went from 7 to 20. |
| 4 | `Enroll` could let a newcomer take an open seat ahead of a waiting learner who had regained eligibility. | **Accepted, with an edit.** The fix runs the queue before enrolling. | `Enroll` calls `FillOpenSeats` first. Covered by a dedicated test. |
| 5 | Results were a message string only. | **Accepted.** | `EnrollmentResult.PromotedLearnerIds`; duplicate status split into `AlreadyEnrolled` / `AlreadyWaitlisted`. |
| 6 | The original test mutated internal state through `lock (course.SyncRoot)` and `InternalsVisibleTo`. | **Rejected.** | Removed `AssemblyInfo.cs`; tests use only the public API. |
| 7 | Alternative: keep removing ineligible learners (original behavior). | **Rejected** in favor of finding 1. | n/a |

## Verification

- Compiled the service and test code with the .NET 8 compiler and ran all 20 tests through a minimal stand-in runner.
- Mutation checks. Putting back "remove ineligible learners" made 4 tests fail. Ignoring capacity made several fail.
- Ran the console demo and pasted the output from the tests below.

```text
$ dotnet test CourseRegistration.sln
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 75 ms - CourseRegistration.Tests.dll (net8.0)

```


```text
Enrollment successful.
Enrollment successful.
Course is full; learner added to the waitlist.
Required prerequisite has not been completed.
Enrollment dropped; promoted learner-3.
Enrolled: learner-3, learner-2
Waitlist: 
```

- [x] `dotnet test` passes on my machine
- [x] I read the final `EnrollmentService.cs` and can explain `FillOpenSeats`
- [x] I ran `dotnet run --project src/CourseRegistration`

## Final disclosure on AI Usage
Most of the initial code came from an LLM as I was not supplied an existing codebase or a repo. A second AI review found the policy and test gaps mentioned above, and I have reviewed and chose between its suggestions.
