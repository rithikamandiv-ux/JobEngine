import { Link, useSearchParams } from 'react-router-dom';
import { useJobs } from '../api/hooks';
import { StatusBadge } from '../components/StatusBadge';
import { StatsCards } from '../components/StatsCards';
import type { JobStatus } from '../types/api';

const statuses: JobStatus[] = [
    'Pending',
    'Claimed',
    'Succeeded',
    'Failed',
    'DeadLetter',
];

function formatTime(iso: string | null) {
    if (!iso) return '-';
    return new Date(iso).toLocaleString();
}

export function JobList() {
    const [searchParams, setSearchParams] = useSearchParams();

    const status = (searchParams.get('status') as JobStatus | null) ?? undefined;
    const page = Number(searchParams.get('page') ?? '1');

    const { data, isLoading, isError, error, isPlaceholderData } = useJobs({
        status,
        page,
        pageSize: 20,
    });

    function setStatus(next: JobStatus | undefined) {
        const params = new URLSearchParams();
        if (next) params.set('status', next);
        setSearchParams(params);
    }

    function setPage(next: number) {
        const params = new URLSearchParams(searchParams);
        params.set('page', String(next));
        setSearchParams(params);
    }

    return (
        <div className="space-y-6">
            <StatsCards />

            <div className="flex flex-wrap gap-2">
                <button
                    onClick={() => setStatus(undefined)}
                    className={`rounded-md px-3 py-1 text-sm ${
                        !status
                            ? 'bg-slate-900 text-white'
                            : 'bg-white text-slate-700 ring-1 ring-slate-200'
                    }`}
                >
                    All
                </button>
                {statuses.map((s) => (
                    <button
                        key={s}
                        onClick={() => setStatus(s)}
                        className={`rounded-md px-3 py-1 text-sm ${
                            status === s
                                ? 'bg-slate-900 text-white'
                                : 'bg-white text-slate-700 ring-1 ring-slate-200'
                        }`}
                    >
                        {s}
                    </button>
                ))}
            </div>

            {isError && (
                <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">
                    {String(error)}
                </p>
            )}

            {isLoading && <p className="text-sm text-slate-500">Loading...</p>}

            {data && (
                <div className={isPlaceholderData ? 'opacity-60' : ''}>
                    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
                        <table className="min-w-full text-sm">
                            <thead className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase text-slate-500">
                            <tr>
                                <th className="px-4 py-2">Id</th>
                                <th className="px-4 py-2">Type</th>
                                <th className="px-4 py-2">Status</th>
                                <th className="px-4 py-2">Attempts</th>
                                <th className="px-4 py-2">Scheduled</th>
                                <th className="px-4 py-2">Error</th>
                            </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                            {data.items.map((job) => (
                                <tr key={job.id} className="hover:bg-slate-50">
                                    <td className="px-4 py-2">
                                        <Link
                                            to={`/jobs/${job.id}`}
                                            className="font-medium text-blue-600 hover:underline"
                                        >
                                            {job.id}
                                        </Link>
                                    </td>
                                    <td className="px-4 py-2 text-slate-700">{job.type}</td>
                                    <td className="px-4 py-2">
                                        <StatusBadge status={job.status} />
                                    </td>
                                    <td className="px-4 py-2 text-slate-600">
                                        {job.attempts}/{job.maxAttempts}
                                        {job.generation > 0 && (
                                            <span className="ml-1 text-xs text-slate-400">
                          gen {job.generation}
                        </span>
                                        )}
                                    </td>
                                    <td className="px-4 py-2 text-slate-600">
                                        {formatTime(job.scheduledAt)}
                                    </td>
                                    <td className="max-w-xs truncate px-4 py-2 text-slate-500">
                                        {job.lastErrorMessage ?? '-'}
                                    </td>
                                </tr>
                            ))}
                            </tbody>
                        </table>
                    </div>

                    {data.items.length === 0 && (
                        <p className="mt-4 text-sm text-slate-500">No jobs match this filter.</p>
                    )}

                    <div className="mt-4 flex items-center justify-between text-sm">
                        <p className="text-slate-500">
                            {data.totalCount} job{data.totalCount === 1 ? '' : 's'}
                        </p>
                        <div className="flex items-center gap-2">
                            <button
                                disabled={page <= 1}
                                onClick={() => setPage(page - 1)}
                                className="rounded-md px-3 py-1 ring-1 ring-slate-200 disabled:opacity-40"
                            >
                                Previous
                            </button>
                            <span className="text-slate-600">
                Page {data.page} of {Math.max(data.totalPages, 1)}
              </span>
                            <button
                                disabled={page >= data.totalPages}
                                onClick={() => setPage(page + 1)}
                                className="rounded-md px-3 py-1 ring-1 ring-slate-200 disabled:opacity-40"
                            >
                                Next
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}