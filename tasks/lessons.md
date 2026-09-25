# Lessons

- When adding timing to a combined API example, keep the combined call and its output. If the user asks for per-primitive examples, add one call for each primitive and time those calls separately; replacing the combined call loses the batching comparison.
- When a check needs a live local service (such as Laya on port 8000) that is not running, ask the user to start it before building a stub server as a substitute. A stub proves wiring only; the user can usually start the real service, and the live run found things a stub would have hidden (a workflow bug surfaced only as an event, and real model judgments on thresholds).
- When the remote has commits the local branch lacks, integrate them with `git pull` (a merge), not `git rebase`, unless the user asks for a rebase. The user wants history to show what actually happened, and a rebase silently rewrites local commit hashes.
