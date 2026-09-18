# JobEngine

A generic background job processing engine built in C# and .NET 10.

Jobs are rows in a PostgreSQL table. Each row carries a type string and a JSON payload.
Worker processes claim jobs atomically, dispatch them to registered handlers, retry transient
failures with exponential backoff, and record every execution attempt. The engine itself knows
nothing about what any given job does, which is what makes it generic: adding a new kind of job
means writing a handler and registering it, not changing the engine.

This is a learning project. The goal is a realistic implementation of the concurrency and
reliability problems a job queue actually has, not a minimal demo.

## Execution guarantee

**This engine provides at-least-once execution, not exactly-once.**

A job whose handler succeeds but whose outcome write then fails will be reclaimed and run
again. That is unavoidable: committing a handler's side effects and committing the engine's
record of them are two separate operations, and no amount of care makes them one atomic step
across a process boundary and a database.

Handlers that must not repeat work need to be idempotent. Check whether the work is already
done before doing it, or make the operation naturally repeatable.

## Status

| Phase | Scope | State |
| --- | --- | --- |
| 0 | C# and .NET fundamentals | Done |
| 1 | Job model and persistence | Done |
| 2 | Handler registration and dispatch | Done |
| 3 | Worker loop and atomic claiming | Done |
| 4 | Retry policy, backoff, dead-lettering, stale claim recovery | Done |
| 5 | Multiple concurrent workers, verified | In progress |
| 6 | Minimal API and React dashboard | Not started |

### What is verified, and what is not

62 tests pass, covering claiming, concurrency, dispatch, retry decisions, recovery, and
outcome recording. Worth being precise about what that does and does not establish.

**Established:**

- Removing `FOR UPDATE SKIP LOCKED` from the claim query makes the Phase 3 concurrency test
  fail reliably, producing 27 claims across 20 jobs. The primitive is load-bearing, not
  decorative.
- The unique constraint on `(job_id, attempt)` makes it structurally impossible to record two
  executions of the same attempt. Because the execution row is inserted before dispatch, a
  duplicate is blocked rather than merely detected.
- Retry, dead-lettering, permanent-failure classification, and stale claim recovery all behave
  as intended, each with negative checks confirming the tests detect breakage.

**Not established:**

- The execution-level concurrency tests pass even with `SKIP LOCKED` removed. They do not
  provoke the race hard enough to serve as a negative check. The evidence that claiming is safe
  comes from the Phase 3 test, not from these.
- All tests run in a single process. Real workers are separate processes with separate
  connection and thread pools, and some interleavings only occur across process boundaries.
- A handler running longer than `StaleClaimThresholdSeconds` will have its job reclaimed while
  still executing. This is a configuration hazard rather than a code defect, but it is real.

## Stack

- C# on .NET 10
- Entity Framework Core 10 with the Npgsql provider
- PostgreSQL 17, run in Docker
- xUnit for tests, with Testcontainers for real database integration tests

PostgreSQL was chosen over SQL Server partway through Phase 1. The two reasons were that it
runs natively on Apple Silicon (SQL Server has no ARM64 build and needs Rosetta emulation),
and that `SELECT ... FOR UPDATE SKIP LOCKED` is close to a purpose-built primitive for job
queues. That second point turned out to matter a great deal.

## Prerequisites

- .NET 10 SDK
- Docker
- `dotnet-ef` CLI tool: `dotnet tool install --global dotnet-ef`

## Getting started

### 1. Start the database

```bash
docker compose up -d
docker compose ps
```

Wait for the status to read `(healthy)` rather than just `Up`. The healthcheck runs
`pg_isready`, so healthy means PostgreSQL is actually accepting connections.

The container maps to host port **5434**, not the default 5432. This is deliberate: several
PostgreSQL instances on one machine competing for 5432 produces authentication errors that
look like wrong credentials but are actually connections landing on the wrong server.

### 2. Set the connection string

The connection string lives in .NET User Secrets, which stores it outside the repository
entirely so it cannot be committed by accident.

```bash
dotnet user-secrets set "ConnectionStrings:JobEngine" \
  "Host=localhost;Port=5434;Database=jobengine;Username=jobengine;Password=localdev" \
  --project src/JobEngine.Worker
```

Note this is not encryption. It is a plain JSON file under `~/.microsoft/usersecrets/`,
readable by anyone with access to your user account. It solves accidental commits, not local
disk security, and has no place in a deployed environment.

### 3. Apply migrations

```bash
dotnet ef database update \
  --project src/JobEngine.Persistence \
  --startup-project src/JobEngine.Worker
```

Two projects are needed because the migrations live in `JobEngine.Persistence` while the
configuration that builds the `DbContext` lives in `JobEngine.Worker`.

### 4. Run a worker

```bash
dotnet run --project src/JobEngine.Worker
```

The worker polls for eligible jobs once per second. A second hosted service scans for stale
claims on its own timer. Both log their effective configuration on startup, which is worth
reading: a mistyped configuration section falls back silently to code defaults, and the
startup log is the only place that shows what is actually in effect.

### 5. Queue a job

```bash
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "INSERT INTO jobs (type, payload_json, status, attempts, max_attempts, created_at, scheduled_at, version)
   VALUES ('delayed-greeting', '{\"message\":\"hello\",\"delayMilliseconds\":2000}', 'Pending', 0, 3, now(), now(), 0);"
```

### Running several workers

Each process needs its own name. Environment variables map to configuration keys with a
double underscore as the separator, because colons are not valid in environment variable names
on every platform.

```bash
# terminal 1
Worker__WorkerName=worker-a dotnet run --project src/JobEngine.Worker

# terminal 2
Worker__WorkerName=worker-b dotnet run --project src/JobEngine.Worker
```

Without an explicit name, the worker derives one from the machine name plus a random suffix,
so two processes on one machine are still distinguishable. An explicit name just makes the
logs easier to read.

Every worker process also runs its own stale claim recovery service. Several scanning at once
is safe, since recovery uses the same `SKIP LOCKED` primitive, but it does mean redundant
queries. Left as is deliberately: making it configurable would introduce a setting that could
be misconfigured to disable recovery entirely.

## Running the tests

```bash
dotnet test
```

Docker must be running. The integration tests start their own throwaway PostgreSQL container
via Testcontainers on a random free port, so they do not interfere with the development
database and can run while it is up.

Tests fall into two groups:

- **In-memory tests** for handler registration, dispatch, payload deserialisation, and retry
  policy decisions. Fast, no container needed.
- **Database tests** for optimistic concurrency, job claiming, outcome recording, stale claim
  recovery, and concurrent execution. These run against real PostgreSQL because the behaviour
  under test belongs to the database, not to the C# code.

The test fixture sets `Include Error Detail=true` on its connection string. This surfaces
PostgreSQL's `DETAIL` and `HINT` fields, which Npgsql suppresses by default because they can
contain row values. Useful in a test container full of synthetic data, wrong in production.

## Project structure

```
JobEngine.slnx
├── src/
│   ├── JobEngine.Core/            domain model, handler contracts, retry policy, store interface
│   ├── JobEngine.Persistence/     DbContext, EF configuration, migrations, PostgreSQL store
│   ├── JobEngine.SampleHandlers/  example handlers, not part of the engine
│   └── JobEngine.Worker/          the executable, DI wiring, worker loop, recovery service
└── tests/
    └── JobEngine.Tests/
```

The split is not decoration. References run one way only, and the compiler enforces it:

- `JobEngine.Core` references only contracts packages (DI abstractions, options). It cannot
  see EF Core, so domain code physically cannot depend on persistence.
- `JobEngine.Persistence` references `Core`, never the reverse.
- `JobEngine.SampleHandlers` references `Core` and is referenced by `Worker`. The engine has
  no reference to it, which demonstrates that handlers live outside the engine rather than
  merely being allowed to.

## Writing a handler

A handler implements `IJobHandler<TPayload>` and receives a deserialised payload plus an
execution context:

```csharp
public class SendEmailHandler : IJobHandler<SendEmailPayload>
{
    private readonly IEmailClient _client;

    public SendEmailHandler(IEmailClient client) => _client = client;

    public async Task HandleAsync(
        SendEmailPayload payload,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        await _client.SendAsync(payload.To, payload.Subject, cancellationToken);
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddJobHandlers(handlers => handlers
    .AddHandler<SendEmailHandler, SendEmailPayload>("send-email"));
```

`JobExecutionContext` carries `JobId`, `Attempt`, `MaxAttempts`, and a computed
`IsFinalAttempt`. It exists so a handler can log with a correlating job id, or behave
differently on its last chance, without being handed the `Job` entity and the ability to
corrupt engine state.

Things to know:

- Handlers are resolved per job from the DI container, so constructor dependencies work
  normally, including scoped ones.
- **Each handler needs a distinct payload type.** Dispatch keys on the payload type
  internally, so two handlers sharing one would silently shadow each other. Registration
  throws if you try.
- Pass the `CancellationToken` to anything you await. Shutdown is cooperative: a handler that
  ignores the token runs to completion regardless, and the host waits for it.

### Sample handlers

`delayed-greeting` waits, then logs. Payload: `message`, `delayMilliseconds`.

`flaky` throws on any attempt at or below `failUntilAttempt`, then succeeds. Payload:
`message`, `failUntilAttempt`. Set it to 2 to watch the full retry sequence, or 99 to watch a
job exhaust its attempts and dead-letter.

## Configuration

```json
{
  "Worker": {
    "PollingIntervalMilliseconds": 1000
  },
  "Retry": {
    "BaseDelaySeconds": 2,
    "MaxDelaySeconds": 300,
    "UseJitter": true
  },
  "Recovery": {
    "StaleClaimThresholdSeconds": 30,
    "ScanIntervalSeconds": 10,
    "BatchSize": 100
  }
}
```

**The recovery values here are tuned for watching it work locally, not for real use.** The
code defaults are 900 seconds and 60 seconds, and those are the safe ones. A 30 second
threshold means any handler running longer than 30 seconds will have its job reclaimed while
still executing, which is exactly the duplicate execution the rest of the design prevents.
Raise them for anything beyond local experimentation.

Defaults live in the options classes rather than only in the JSON, so a missing or misspelled
section falls back to the conservative values rather than to nothing.

All sections are validated at startup with `ValidateOnStart`, so a bad value fails the host
immediately rather than causing strange behaviour later.

## Design decisions

### Claiming is a single atomic statement

```sql
UPDATE jobs
SET status = 'Claimed', claimed_by = $1, claimed_at = now(), run_at = now(),
    attempts = attempts + 1, version = version + 1
WHERE id = (
    SELECT id FROM jobs
    WHERE status = 'Pending' AND scheduled_at <= now()
    ORDER BY scheduled_at
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
RETURNING *;
```

`FOR UPDATE SKIP LOCKED` means a worker takes a row lock and ignores rows another transaction
already holds, rather than waiting for them. Concurrent workers therefore get different rows
with no blocking and no aborted transactions.

A read-then-update pair would not work. Under PostgreSQL's default `READ COMMITTED` isolation,
two workers can both read the same row before either writes. This was verified rather than
assumed: removing `SKIP LOCKED` and running the concurrency test produced 27 claims across 20
jobs, meaning 7 jobs would have been executed twice with no error raised anywhere.

The composite index on `(status, scheduled_at)` exists specifically to serve this query.

### Attempts increment when a job is claimed, not when it fails

The intuitive alternative is to increment on failure. The problem is a job that crashes the
worker process outright: no failure handler runs, so no increment happens, and on restart the
same job is claimed again and crashes again. This is called a poison job and it can take down
a whole fleet.

Counting at claim time means every attempt is recorded the moment it starts. Stale claim
recovery deliberately leaves the count alone for the same reason.

### Backoff is just a column update

A retryable failure returns the job to `Pending` with `scheduled_at` pushed into the future.
The claim query's existing `scheduled_at <= now()` condition then skips it until the delay
elapses. No timer, no scheduler, no second mechanism.

The delay is `baseDelay * 2^(attempts - 1)`, capped at `MaxDelaySeconds`, with **full jitter**:
the actual wait is uniformly random between zero and that value. Jitter matters because fifty
jobs failing at once would otherwise all retry at the same instant, hammering whatever was
already struggling. This is the thundering herd problem.

### Five statuses, each meaning something different

| Status | Meaning | Terminal |
| --- | --- | --- |
| `Pending` | Eligible once `scheduled_at` passes | No |
| `Claimed` | A worker is running it | No |
| `Succeeded` | Completed | Yes |
| `Failed` | Permanently failed: no handler, or bad payload | Yes |
| `DeadLetter` | Exhausted `max_attempts` | Yes |

`Failed` and `DeadLetter` are both terminal but call for different responses. `Failed` means
someone made a mistake and the fix is code or data. `DeadLetter` means the work kept failing
and the fix is usually whatever downstream thing was broken.

The distinction comes from the exception classification: `HandlerNotFoundException` and
`InvalidPayloadException` are permanent and skip retries entirely, because retrying a job whose
handler does not exist only burns attempts to reach the same conclusion. Anything else is
treated as transient.

### Status is stored as text, not an integer

The column is `varchar(20)` holding `Pending`, `Claimed`, and so on, with a check constraint
listing the valid values. An integer would be marginally smaller and faster, but the table
becomes opaque when you inspect it directly, and reordering the C# enum would silently change
the meaning of existing rows.

### The execution log prevents duplicates rather than just recording them

`job_executions` holds one row per execution attempt, with a **unique constraint on
`(job_id, attempt)`**. The worker inserts that row before dispatching, so if two workers ever
held the same job and attempt, the second insert fails and the handler never runs.

Attempt numbers are assigned atomically at claim time, so each claim gets a distinct one. That
is what lets the constraint distinguish a legitimate retry, which has a new attempt number,
from a duplicate, which does not.

The constraint does not catch every case. If stale claim recovery releases a job while a worker
is still running it, the second worker gets a new attempt number and no violation occurs. That
hazard is a function of the recovery threshold, not of the claiming logic.

A null `outcome` means an execution started and never recorded an ending, which is itself
informative: the worker died mid-job.

### Optimistic concurrency as a backstop

A `version` column acts as a concurrency token. EF Core includes the original value in the
`WHERE` clause of every update, and `SaveChangesAsync` is overridden to increment it. If two
writers somehow both hold the same job, the second write matches zero rows and throws
`DbUpdateConcurrencyException` rather than silently overwriting.

Worth knowing: `IsConcurrencyToken()` alone does **not** increment the value. Only
database-generated tokens like `xmin` do that automatically. With a plain integer, incrementing
is the application's responsibility, and without it the check is wired up but permanently
inert.

### Cleanup writes are not cancellable

When shutdown interrupts a job, the store calls that record the outcome use
`CancellationToken.None` rather than the worker's stopping token. Passing the cancelled token
would cancel the very write that records what happened, leaving the job stuck in `Claimed`.
Error handling must not be cancellable by the same signal that triggered it.

### Error columns have no length limit

`last_error_message` and `last_error_detail` are `text`, deliberately unconstrained, with
truncation applied in code instead (1000 and 20000 characters).

A `varchar(500)` would be the consistent choice, but this column is written inside the error
handler. If a message exceeded the limit, the insert would fail while recording a failure, and
the job would be stuck with no record of what went wrong. An error path must not be able to
fail on its own constraints.

### Payload deserialisation happens in one place

Handlers are generic over their payload type, but dispatch has to work from a runtime string.
An adapter bridges the two: handler authors write against a typed interface, and a single
adapter class does the JSON parsing for all of them. Malformed payloads therefore fail
identically regardless of which handler they were destined for.

## Database schema

```
jobs
  id                  bigint, identity, primary key
  type                varchar(100)  not null
  payload_json        text          not null
  status              varchar(20)   not null, check constraint
  attempts            integer       not null
  max_attempts        integer       not null, default 3
  created_at          timestamptz   not null
  scheduled_at        timestamptz   not null
  run_at              timestamptz   null
  claimed_by          varchar(100)  null
  claimed_at          timestamptz   null
  completed_at        timestamptz   null
  last_error_message  text          null
  last_error_detail   text          null
  version             integer       not null

  index ix_jobs_status_scheduled_at on (status, scheduled_at)

job_executions
  id            bigint, identity, primary key
  job_id        bigint        not null, FK -> jobs(id) on delete cascade
  attempt       integer       not null
  worker_id     varchar(100)  not null
  started_at    timestamptz   not null
  completed_at  timestamptz   null
  outcome       varchar(20)   null

  unique index ux_job_executions_job_id_attempt on (job_id, attempt)
```

`scheduled_at` is when a job becomes eligible to run, and is rewritten by backoff on each
retry. `run_at` is when execution actually started, and is null until a worker claims it.

`outcome` values are `Succeeded`, `Failed`, `Retrying`, `DeadLettered`, and `Released`. These
describe a single execution, which is not the same as the job's status: a job that ends
`Succeeded` may have executions recorded as `Retrying`, `Retrying`, `Succeeded`.

All timestamps are UTC. Npgsql enforces this at the driver level: a `DateTime` whose `Kind`
is not `Utc` throws rather than being silently converted. For a scheduler this is a feature,
since a daylight saving transition can otherwise make a job run twice or not at all.

## Useful commands

```bash
# Inspect the schema
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c "\d jobs"
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c "\d job_executions"

# Queued and completed jobs
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "SELECT id, type, status, attempts, claimed_by, last_error_message FROM jobs ORDER BY id;"

# Execution history with durations
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "SELECT job_id, attempt, worker_id, outcome, completed_at - started_at AS duration
   FROM job_executions ORDER BY job_id, attempt;"

# Executions that started and never finished
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "SELECT * FROM job_executions WHERE outcome IS NULL;"

# Clear both tables (the foreign key means they must be truncated together)
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "TRUNCATE TABLE job_executions, jobs RESTART IDENTITY;"

# Create a migration after changing the model
dotnet ef migrations add SomeName \
  --project src/JobEngine.Persistence \
  --startup-project src/JobEngine.Worker
```

Always read a generated migration before applying it. EF Core infers changes by diffing your
model against a stored snapshot, and inference is a guess. It has heuristics for detecting
renames, and when `last_error` became `last_error_message` it correctly produced a
`RenameColumn` rather than a destructive drop-and-add. It does not always get this right:
rename a property and change its type in the same migration, or rename two at once, and it can
generate a drop-and-add that silently discards data.