export type JobStatus =
    | 'Pending'
    | 'Claimed'
    | 'Succeeded'
    | 'Failed'
    | 'DeadLetter';

export type ExecutionOutcome =
    | 'Succeeded'
    | 'Failed'
    | 'Retrying'
    | 'DeadLettered'
    | 'Released';

export interface JobSummary {
    id: number;
    type: string;
    status: JobStatus;
    attempts: number;
    maxAttempts: number;
    generation: number;
    createdAt: string;
    scheduledAt: string;
    completedAt: string | null;
    lastErrorMessage: string | null;
}

export interface JobDetail {
    id: number;
    type: string;
    payloadJson: string;
    status: JobStatus;
    attempts: number;
    maxAttempts: number;
    generation: number;
    createdAt: string;
    scheduledAt: string;
    runAt: string | null;
    claimedBy: string | null;
    claimedAt: string | null;
    completedAt: string | null;
    lastErrorMessage: string | null;
    lastErrorDetail: string | null;
}

export interface JobExecution {
    id: number;
    generation: number;
    attempt: number;
    workerId: string;
    startedAt: string;
    completedAt: string | null;
    outcome: ExecutionOutcome | null;
    durationMilliseconds: number | null;
}

export interface PagedResponse<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export interface Stats {
    countsByStatus: Record<JobStatus, number>;
    totalJobs: number;
}

export interface EnqueueJobRequest {
    type: string;
    payloadJson?: string;
    maxAttempts?: number;
    scheduledAt?: string;
}