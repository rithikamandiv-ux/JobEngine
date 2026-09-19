import type { JobStatus } from '../types/api';

const styles: Record<JobStatus, string> = {
    Pending: 'bg-slate-100 text-slate-700 ring-slate-200',
    Claimed: 'bg-blue-100 text-blue-700 ring-blue-200',
    Succeeded: 'bg-green-100 text-green-700 ring-green-200',
    Failed: 'bg-red-100 text-red-700 ring-red-200',
    DeadLetter: 'bg-orange-100 text-orange-700 ring-orange-200',
};

export function StatusBadge({ status }: { status: JobStatus }) {
    return (
        <span
            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[status]}`}
        >
      {status}
    </span>
    );
}