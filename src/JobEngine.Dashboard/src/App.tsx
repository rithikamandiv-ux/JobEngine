import { useEffect, useState } from 'react'
import { api } from './api/client'
import type { Stats } from './types/api'

export default function App() {
    const [stats, setStats] = useState<Stats | null>(null)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        api.getStats().then(setStats).catch(e => setError(String(e)))
    }, [])

    return (
        <div className="min-h-screen bg-slate-50 p-8">
            <h1 className="text-2xl font-semibold text-slate-900">JobEngine</h1>
            {error && <p className="mt-4 text-red-600">{error}</p>}
            <pre className="mt-4 text-sm">{JSON.stringify(stats, null, 2)}</pre>
        </div>
    )
}