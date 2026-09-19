import { useStats } from '../api/hooks';
import type { JobStatus } from '../types/api';

const order: JobStatus[] = [
    'Pending',
    'Claimed',
    'Succeeded',
    'Failed',
    'DeadLetter',
];

const accents: Record<JobStatus, string> = {
    Pending: 'text-slate-700',
    Claimed: 'text-blue-700',
    Succeeded: 'text-green-700',
    Failed: 'text-red-700',
    DeadLetter: 'text-orange-700',
};

export function StatsCards() {
    const { data, isLoading } = useStats();

    if (isLoading || !data) {
        return <div className="h-20 animate-pulse rounded-lg bg-slate-100" />;
    }

    return (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            {order.map((status) => (
                <div key={status} className="rounded-lg border border-slate-200 bg-white p-3">
                    <p className="text-xs text-slate-500">{status}</p>
                    <p className={`mt-1 text-xl font-semibold ${accents[status]}`}>
                        {data.countsByStatus[status]}
                    </p>
                </div>
            ))}
            <div className="rounded-lg border border-slate-200 bg-white p-3">
                <p className="text-xs text-slate-500">Total</p>
                <p className="mt-1 text-xl font-semibold text-slate-900">{data.totalJobs}</p>
            </div>
        </div>
    );
}