# JobEngine

A generic background job processing engine built in C# and .NET 10.

Jobs are rows in a PostgreSQL table. Each row carries a type string and a JSON payload.
Worker processes claim jobs atomically, dispatch them to registered handlers, and record
the outcome. The engine itself knows nothing about what any given job does, which is what
makes it generic: adding a new kind of job means writing a handler and registering it, not
changing the engine.

This is a learning project. The goal is a realistic implementation of the concurrency and
reliability problems a job queue actually has, not a minimal demo.

## Status

| Phase | Scope | State |
| --- | --- | --- |
| 0 | C# and .NET fundamentals | Done |
| 1 | Job model and persistence | Done |
| 2 | Handler registration and dispatch | Done |
| 3 | Worker loop and atomic claiming | Done |
| 4 | Retry policy, backoff, dead-lettering | Not started |
| 5 | Multiple concurrent workers, verified | Not started |
| 6 | Minimal API and React dashboard | Not started |

### Known gaps

- **No retry policy.** A failed job is marked `Failed` and stays there. Nothing reschedules it.
- **No dead-lettering.** `MaxAttempts` is stored and incremented but not yet enforced.
- **No stale claim recovery.** A worker killed rather than shut down gracefully leaves its
  job stuck in `Claimed` indefinitely. Deferred to Phase 4.

## Stack

- C# on .NET 10
- Entity Framework Core 10 with the Npgsql provider
- PostgreSQL 17, run in Docker
- xUnit for tests, with Testcontainers for real database integration tests

PostgreSQL was chosen over SQL Server partway through Phase 1. The two reasons were that it
runs natively on Apple Silicon (SQL Server has no ARM64 build and needs Rosetta emulation),
and that `SELECT ... FOR UPDATE SKIP LOCKED` is close to a purpose-built primitive for job
queues. That second point turned out to matter a great deal in Phase 3.

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

The worker polls for eligible jobs once per second and processes any it claims.

### 5. Queue a job

```bash
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "INSERT INTO jobs (type, payload_json, status, attempts, max_attempts, created_at, scheduled_at, version)
   VALUES ('delayed-greeting', '{\"message\":\"hello\",\"delayMilliseconds\":2000}', 'Pending', 0, 3, now(), now(), 0);"
```

Watch the worker log. It should claim the job, run the handler, and mark it succeeded.

## Running the tests

```bash
dotnet test
```

Docker must be running. The integration tests start their own throwaway PostgreSQL container
via Testcontainers on a random free port, so they do not interfere with the development
database and can run while it is up.

Tests fall into two groups:

- **In-memory tests** for handler registration, dispatch, and payload deserialisation. Fast,
  no container needed.
- **Database tests** for optimistic concurrency and job claiming. These run against real
  PostgreSQL because the behaviour under test (`SKIP LOCKED`, concurrency tokens) belongs to
  the database, not to the C# code.

## Project structure

```
JobEngine.slnx
├── src/
│   ├── JobEngine.Core/            domain model, handler contracts, store interface
│   ├── JobEngine.Persistence/     DbContext, EF configuration, migrations, PostgreSQL store
│   ├── JobEngine.SampleHandlers/  example handler, not part of the engine
│   └── JobEngine.Worker/          the executable, DI wiring, worker loop
└── tests/
    └── JobEngine.Tests/
```

The split is not decoration. References run one way only, and the compiler enforces it:

- `JobEngine.Core` references no infrastructure packages at all. It cannot see EF Core, so
  domain code physically cannot depend on persistence.
- `JobEngine.Persistence` references `Core`, never the reverse.
- `JobEngine.SampleHandlers` references `Core` and is referenced by `Worker`. The engine has
  no reference to it, which demonstrates that handlers live outside the engine rather than
  merely being allowed to.

## Writing a handler

A handler implements `IJobHandler<TPayload>` and receives a deserialised payload:

```csharp
public class SendEmailHandler : IJobHandler<SendEmailPayload>
{
    private readonly IEmailClient _client;

    public SendEmailHandler(IEmailClient client) => _client = client;

    public async Task HandleAsync(SendEmailPayload payload, CancellationToken cancellationToken)
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

Two things to know:

- Handlers are resolved per job from the DI container, so constructor dependencies work
  normally, including scoped ones.
- **Each handler needs a distinct payload type.** Dispatch keys on the payload type
  internally, so two handlers sharing one would silently shadow each other. Registration
  throws if you try.

Handlers never see the `Job` entity. They get their payload and nothing else, so they cannot
read or modify the engine's bookkeeping.

## Configuration

`appsettings.json`:

```json
{
  "Worker": {
    "PollingIntervalMilliseconds": 1000
  }
}
```

`WorkerName` is optional. When absent, the worker derives an identity from the machine name
plus a short random suffix, for example `Rithikas-MacBook-Pro-ede4f396`. That value is written
to `claimed_by` on every job it takes, which is what makes it possible to tell which process
ran what.

Options are validated at startup with `ValidateOnStart`, so a bad value fails the host
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

Counting at claim time means every attempt is recorded the moment it starts. The cost is that
a job interrupted by graceful shutdown burns an attempt, which is a reasonable trade for
bounded retries under every failure mode.

### Status is stored as text, not an integer

The column is `varchar(20)` holding `Pending`, `Claimed`, and so on, with a check constraint
listing the valid values. An integer would be marginally smaller and faster, but the table
becomes opaque when you inspect it directly, and reordering the C# enum would silently change
the meaning of existing rows.

### Optimistic concurrency as a backstop

A `version` column acts as a concurrency token. EF Core includes the original value in the
`WHERE` clause of every update, and `SaveChangesAsync` is overridden to increment it. If two
writers somehow both hold the same job, the second write matches zero rows and throws
`DbUpdateConcurrencyException` rather than silently overwriting.

Worth knowing: `IsConcurrencyToken()` alone does **not** increment the value. Only
database-generated tokens like `xmin` do that automatically. With a plain integer, incrementing
is the application's responsibility, and without it the check is wired up but permanently
inert.

`SKIP LOCKED` is the primary defence. This is defence in depth.

### Cleanup writes are not cancellable

When shutdown interrupts a job, the store calls that record the outcome use
`CancellationToken.None` rather than the worker's stopping token. Passing the cancelled token
would cancel the very write that records what happened, leaving the job stuck in `Claimed`.
Error handling must not be cancellable by the same signal that triggered it.

### Payload deserialisation happens in one place

Handlers are generic over their payload type, but dispatch has to work from a runtime string.
An adapter bridges the two: handler authors write against a typed interface, and a single
adapter class does the JSON parsing for all of them. Malformed payloads therefore fail
identically regardless of which handler they were destined for, rather than every handler
reimplementing the same parse and check.

### Failure classification

`HandlerNotFoundException` and `InvalidPayloadException` represent permanent failures. A
missing handler will not appear on retry and malformed JSON will not become valid. Anything
else escaping a handler is treated as transient.

Phase 3 does not yet act on this distinction. It exists so that Phase 4's retry policy can
send permanent failures straight to dead-letter instead of burning attempts to reach the same
conclusion.

## Database schema

```
jobs
  id            bigint, identity, primary key
  type          varchar(100)  not null
  payload_json  text          not null
  status        varchar(20)   not null, check constraint
  attempts      integer       not null
  max_attempts  integer       not null, default 3
  created_at    timestamptz   not null
  scheduled_at  timestamptz   not null
  run_at        timestamptz   null
  claimed_by    varchar(100)  null
  claimed_at    timestamptz   null
  completed_at  timestamptz   null
  last_error    text          null
  version       integer       not null

  index ix_jobs_status_scheduled_at on (status, scheduled_at)
```

`scheduled_at` is when a job becomes eligible to run. `run_at` is when execution actually
started, and is null until a worker claims it. Phase 4's exponential backoff will work by
pushing `scheduled_at` forward after a failure.

All timestamps are UTC. Npgsql enforces this at the driver level: a `DateTime` whose `Kind`
is not `Utc` throws rather than being silently converted. For a scheduler this is a feature,
since a daylight saving transition can otherwise make a job run twice or not at all.

## Useful commands

```bash
# Inspect the schema
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c "\d jobs"

# See queued and completed jobs
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "SELECT id, type, status, attempts, claimed_by, completed_at FROM jobs ORDER BY id;"

# Clear the table
docker exec -it jobengine-postgres psql -U jobengine -d jobengine -c \
  "TRUNCATE TABLE jobs RESTART IDENTITY;"

# Create a migration after changing the model
dotnet ef migrations add SomeName \
  --project src/JobEngine.Persistence \
  --startup-project src/JobEngine.Worker
```

Always read a generated migration before applying it. EF Core infers changes from a model
diff, and some inferences are destructive: a property rename looks identical to dropping one
column and adding another, so that is exactly what it generates.
