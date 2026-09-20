import { useState } from 'react';
import { useEnqueueJob } from '../api/hooks';

const defaultPayloads: Record<string, string> = {
    'delayed-greeting': '{ "message": "hello", "delayMilliseconds": 1000 }',
    flaky: '{ "message": "retry me", "failUntilAttempt": 2 }',
};

export function NewJobForm() {
    const [open, setOpen] = useState(false);
    const [type, setType] = useState('delayed-greeting');
    const [payloadJson, setPayloadJson] = useState(
        defaultPayloads['delayed-greeting'],
    );
    const [maxAttempts, setMaxAttempts] = useState('3');

    const enqueue = useEnqueueJob();

    function selectType(next: string) {
        setType(next);
        if (defaultPayloads[next]) {
            setPayloadJson(defaultPayloads[next]);
        }
    }

    function submit() {
        enqueue.mutate(
            {
                type,
                payloadJson,
                maxAttempts: Number(maxAttempts) || undefined,
            },
            {
                onSuccess: () => setOpen(false),
            },
        );
    }

    if (!open) {
        return (
            <button
                onClick={() => setOpen(true)}
                className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            >
                New job
            </button>
        );
    }

    return (
        <div className="rounded-lg border border-slate-200 bg-white p-6">
            <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold text-slate-900">Enqueue a job</h2>
                <button
                    onClick={() => setOpen(false)}
                    className="text-sm text-slate-500 hover:text-slate-800"
                >
                    Cancel
                </button>
            </div>

            <div className="mt-4 grid gap-4 sm:grid-cols-3">
                <label className="block">
          <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
            Type
          </span>
                    <input
                        value={type}
                        onChange={(e) => selectType(e.target.value)}
                        list="job-types"
                        className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                    />
                    <datalist id="job-types">
                        <option value="delayed-greeting" />
                        <option value="flaky" />
                    </datalist>
                </label>

                <label className="block">
          <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
            Max attempts
          </span>
                    <input
                        type="number"
                        min={1}
                        value={maxAttempts}
                        onChange={(e) => setMaxAttempts(e.target.value)}
                        className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                    />
                </label>
            </div>

            <label className="mt-4 block">
        <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
          Payload JSON
        </span>
                <textarea
                    value={payloadJson}
                    onChange={(e) => setPayloadJson(e.target.value)}
                    rows={4}
                    className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-xs"
                />
            </label>

            {enqueue.isError && (
                <p className="mt-3 rounded-md bg-red-50 p-3 text-sm text-red-700">
                    {enqueue.error instanceof Error
                        ? enqueue.error.message
                        : String(enqueue.error)}
                </p>
            )}

            <div className="mt-4 flex justify-end">
                <button
                    onClick={submit}
                    disabled={enqueue.isPending}
                    className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
                >
                    {enqueue.isPending ? 'Enqueueing...' : 'Enqueue'}
                </button>
            </div>
        </div>
    );
}