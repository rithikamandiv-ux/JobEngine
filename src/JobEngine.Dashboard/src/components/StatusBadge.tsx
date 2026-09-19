import type { ExecutionOutcome, JobStatus } from '../types/api';

const base =
    'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset';

const styles: Record<JobStatus, string> = {
    Pending: 'bg-slate-100 text-slate-700 ring-slate-200',
    Claimed: 'bg-blue-100 text-blue-700 ring-blue-200',
    Succeeded: 'bg-green-100 text-green-700 ring-green-200',
    Failed: 'bg-red-100 text-red-700 ring-red-200',
    DeadLetter: 'bg-orange-100 text-orange-700 ring-orange-200',
};

const outcomeStyles: Record<ExecutionOutcome, string> = {
    Succeeded: 'bg-green-100 text-green-700 ring-green-200',
    Failed: 'bg-red-100 text-red-700 ring-red-200',
    Retrying: 'bg-amber-100 text-amber-700 ring-amber-200',
    DeadLettered: 'bg-orange-100 text-orange-700 ring-orange-200',
    Released: 'bg-violet-100 text-violet-700 ring-violet-200',
};

export function StatusBadge({ status }: { status: JobStatus }) {
    return <span className={`${base} ${styles[status]}`}>{status}</span>;
}

export function OutcomeBadge({ outcome }: { outcome: ExecutionOutcome | null }) {
    if (outcome === null) {
        return (
            <span className={`${base} bg-slate-100 text-slate-500 ring-slate-200`}>
                Never finished
            </span>
        );
    }

    return <span className={`${base} ${outcomeStyles[outcome]}`}>{outcome}</span>;
}