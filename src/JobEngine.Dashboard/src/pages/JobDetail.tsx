import { Link, useParams } from 'react-router-dom';
import { useExecutions, useJob, useRetryJob } from '../api/hooks';
import { OutcomeBadge, StatusBadge } from '../components/StatusBadge';
import type { JobExecution } from '../types/api';

function formatTime(iso: string | null): string {
    if (!iso) return '-';
    return new Date(iso).toLocaleString();
}

function formatDuration(ms: number | null): string {
    if (ms === null) return '-';
    if (ms < 1000) return `${ms.toFixed(0)} ms`;
    return `${(ms / 1000).toFixed(2)} s`;
}

function groupByGeneration(
    executions: JobExecution[],
): Map<number, JobExecution[]> {
    const groups = new Map<number, JobExecution[]>();

    for (const execution of executions) {
        const existing = groups.get(execution.generation);
        if (existing) {
            existing.push(execution);
        } else {
            groups.set(execution.generation, [execution]);
        }
    }

    return groups;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div>
            <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </dt>
            <dd className="mt-1 text-sm text-slate-900">{children}</dd>
        </div>
    );
}

export default function JobDetail() {
    const { id } = useParams<{ id: string }>();
    const jobId = Number(id);

    const job = useJob(jobId);
    const executions = useExecutions(jobId);
    const retry = useRetryJob();

    if (job.isLoading) {
        return <p className="text-slate-500">Loading...</p>;
    }

    if (job.isError || !job.data) {
        return (
            <div className="space-y-4">
                <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
                    {String(job.error ?? 'Job not found.')}
                </p>
                <Link to="/" className="text-sm text-blue-700 hover:underline">
                    Back to jobs
                </Link>
            </div>
        );
    }

    const data = job.data;
    const canRetry = data.status === 'Failed' || data.status === 'DeadLetter';
    const groups = groupByGeneration(executions.data ?? []);

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <Link to="/" className="text-sm text-blue-700 hover:underline">
                        Back to jobs
                    </Link>
                    <h1 className="mt-1 text-xl font-semibold text-slate-900">
                        Job {data.id}
                    </h1>
                </div>

                {canRetry && (
                    <button
                        onClick={() => retry.mutate(jobId)}
                        disabled={retry.isPending}
                        className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
                    >
                        {retry.isPending ? 'Retrying...' : 'Retry job'}
                    </button>
                )}
            </div>

            {retry.isError && (
                <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
                    {String(retry.error)}
                </p>
            )}

            <div className="rounded-lg border border-slate-200 bg-white p-6">
                <dl className="grid grid-cols-2 gap-x-6 gap-y-5 sm:grid-cols-4">
                    <Field label="Type">{data.type}</Field>
                    <Field label="Status">
                        <StatusBadge status={data.status} />
                    </Field>
                    <Field label="Attempts">
                        {data.attempts} / {data.maxAttempts}
                    </Field>
                    <Field label="Generation">{data.generation}</Field>
                    <Field label="Created">{formatTime(data.createdAt)}</Field>
                    <Field label="Scheduled">{formatTime(data.scheduledAt)}</Field>
                    <Field label="Started">{formatTime(data.runAt)}</Field>
                    <Field label="Completed">{formatTime(data.completedAt)}</Field>
                    <Field label="Claimed by">{data.claimedBy ?? '-'}</Field>
                    <Field label="Claimed at">{formatTime(data.claimedAt)}</Field>
                </dl>
            </div>

            <section className="rounded-lg border border-slate-200 bg-white p-6">
                <h2 className="text-sm font-semibold text-slate-900">Payload</h2>
                <pre className="mt-3 overflow-x-auto rounded-md bg-slate-50 p-4 text-xs text-slate-700">
          {data.payloadJson}
        </pre>
            </section>

            {data.lastErrorMessage && (
                <section className="rounded-lg border border-red-200 bg-white p-6">
                    <h2 className="text-sm font-semibold text-red-900">Last error</h2>
                    <p className="mt-2 text-sm text-red-800">{data.lastErrorMessage}</p>

                    {data.lastErrorDetail && (
                        <details className="mt-3">
                            <summary className="cursor-pointer text-xs font-medium text-slate-600 hover:text-slate-900">
                                Show stack trace
                            </summary>
                            <pre className="mt-2 overflow-x-auto rounded-md bg-slate-50 p-4 text-xs text-slate-600">
                {data.lastErrorDetail}
              </pre>
                        </details>
                    )}
                </section>
            )}

            <section className="space-y-4">
                <h2 className="text-sm font-semibold text-slate-900">
                    Execution history
                </h2>

                {executions.isLoading && (
                    <p className="text-sm text-slate-500">Loading executions...</p>
                )}

                {!executions.isLoading && groups.size === 0 && (
                    <p className="text-sm text-slate-500">
                        This job has not been executed yet.
                    </p>
                )}

                {[...groups.entries()].map(([generation, runs]) => (
                    <div
                        key={generation}
                        className="overflow-hidden rounded-lg border border-slate-200 bg-white"
                    >
                        <div className="border-b border-slate-100 bg-slate-50 px-4 py-2">
              <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {generation === 0 ? 'Original run' : `Retry ${generation}`}
              </span>
                        </div>

                        <table className="w-full text-sm">
                            <thead className="text-left text-xs uppercase tracking-wide text-slate-500">
                            <tr>
                                <th className="px-4 py-2 font-medium">Attempt</th>
                                <th className="px-4 py-2 font-medium">Worker</th>
                                <th className="px-4 py-2 font-medium">Started</th>
                                <th className="px-4 py-2 font-medium">Duration</th>
                                <th className="px-4 py-2 font-medium">Outcome</th>
                            </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                            {runs.map((run) => (
                                <tr key={run.id}>
                                    <td className="px-4 py-2 text-slate-700">{run.attempt}</td>
                                    <td className="px-4 py-2 font-mono text-xs text-slate-600">
                                        {run.workerId}
                                    </td>
                                    <td className="px-4 py-2 text-slate-600">
                                        {formatTime(run.startedAt)}
                                    </td>
                                    <td className="px-4 py-2 text-slate-600">
                                        {formatDuration(run.durationMilliseconds)}
                                    </td>
                                    <td className="px-4 py-2">
                                        <OutcomeBadge outcome={run.outcome} />
                                    </td>
                                </tr>
                            ))}
                            </tbody>
                        </table>
                    </div>
                ))}
            </section>
        </div>
    );
}