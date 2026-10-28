# Limitations and Follow-Up Risks

- **Memory only.** State is lost on restart. A real version needs a database transaction, a unique constraint on (offering, learner), and an ordered queue column, with integration tests against that store.
- **Process-local lock.** It does not coordinate multiple instances. Cross-instance safety needs database locking or optimistic concurrency.
- **Lock held during eligibility lookups.** Fine with the in-memory records. A slow or remote prerequisite service would block the offering, so a production design would snapshot or batch the lookups.
- **Waitlist is O(n) per scan.** Fine for a single course offering, but worth revisiting with very long queues.
- **No way to leave the waitlist.** A waitlisted learner who drops gets `NotEnrolled`. Out of scope for this slice.
- **Retained learners can wait indefinitely** if they stay ineligible. There is no expiry, and no notification when someone is skipped.
- **Concurrency tests check invariants, not interleavings.** They show capacity, uniqueness, and queue consistency under parallel load, but they cannot prove the absence of races.
