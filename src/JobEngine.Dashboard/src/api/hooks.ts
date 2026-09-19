import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type JobListParams } from './client';
import type { EnqueueJobRequest } from '../types/api';

export const queryKeys = {
    jobs: (params: JobListParams) => ['jobs', params] as const,
    job: (id: number) => ['job', id] as const,
    executions: (id: number) => ['executions', id] as const,
    stats: () => ['stats'] as const,
};

export function useJobs(params: JobListParams) {
    return useQuery({
        queryKey: queryKeys.jobs(params),
        queryFn: () => api.listJobs(params),
        refetchInterval: 2000,
        placeholderData: (previous) => previous,
    });
}

export function useJob(id: number) {
    return useQuery({
        queryKey: queryKeys.job(id),
        queryFn: () => api.getJob(id),
    });
}

export function useExecutions(id: number) {
    return useQuery({
        queryKey: queryKeys.executions(id),
        queryFn: () => api.getExecutions(id),
    });
}

export function useStats() {
    return useQuery({
        queryKey: queryKeys.stats(),
        queryFn: () => api.getStats(),
        refetchInterval: 2000,
    });
}

export function useEnqueueJob() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (body: EnqueueJobRequest) => api.enqueueJob(body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['jobs'] });
            queryClient.invalidateQueries({ queryKey: ['stats'] });
        },
    });
}

export function useRetryJob() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (id: number) => api.retryJob(id),
        onSuccess: (_data, id) => {
            queryClient.invalidateQueries({ queryKey: ['jobs'] });
            queryClient.invalidateQueries({ queryKey: ['stats'] });
            queryClient.invalidateQueries({ queryKey: queryKeys.job(id) });
            queryClient.invalidateQueries({ queryKey: queryKeys.executions(id) });
        },
    });
}