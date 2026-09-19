import type {
    EnqueueJobRequest,
    JobDetail,
    JobExecution,
    JobStatus,
    JobSummary,
    PagedResponse,
    Stats,
} from '../types/api';

export class ApiError extends Error {
    constructor(
        message: string,
        public readonly status: number,
        public readonly details?: unknown,
    ) {
        super(message);
        this.name = 'ApiError';
    }
}

interface ErrorBody {
    error?: string;
    validValues?: string[];
    registeredTypes?: string[];
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(path, {
        ...init,
        headers: {
            'Content-Type': 'application/json',
            ...init?.headers,
        },
    });

    if (!response.ok) {
        throw new ApiError(
            await extractErrorMessage(response),
            response.status,
            await safeJson(response.clone()),
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return (await response.json()) as T;
}

async function extractErrorMessage(response: Response): Promise<string> {
    const body = await safeJson<ErrorBody>(response.clone());

    if (body?.error) {
        const hint = body.validValues ?? body.registeredTypes;
        return hint ? `${body.error} Valid values: ${hint.join(', ')}` : body.error;
    }

    return `Request failed with status ${response.status}.`;
}

async function safeJson<T>(response: Response): Promise<T | null> {
    try {
        return (await response.json()) as T;
    } catch {
        return null;
    }
}

export interface JobListParams {
    status?: JobStatus;
    type?: string;
    page?: number;
    pageSize?: number;
}

export const api = {
    listJobs(params: JobListParams = {}): Promise<PagedResponse<JobSummary>> {
        const query = new URLSearchParams();

        if (params.status) query.set('status', params.status);
        if (params.type) query.set('type', params.type);
        if (params.page) query.set('page', String(params.page));
        if (params.pageSize) query.set('pageSize', String(params.pageSize));

        const suffix = query.toString();
        return request<PagedResponse<JobSummary>>(
            suffix ? `/api/jobs?${suffix}` : '/api/jobs',
        );
    },

    getJob(id: number): Promise<JobDetail> {
        return request<JobDetail>(`/api/jobs/${id}`);
    },

    getExecutions(id: number): Promise<JobExecution[]> {
        return request<JobExecution[]>(`/api/jobs/${id}/executions`);
    },

    getStats(): Promise<Stats> {
        return request<Stats>('/api/stats');
    },

    enqueueJob(body: EnqueueJobRequest): Promise<JobDetail> {
        return request<JobDetail>('/api/jobs', {
            method: 'POST',
            body: JSON.stringify(body),
        });
    },

    retryJob(id: number): Promise<JobDetail> {
        return request<JobDetail>(`/api/jobs/${id}/retry`, { method: 'POST' });
    },
};