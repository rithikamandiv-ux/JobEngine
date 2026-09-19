import { Link, Route, Routes } from 'react-router-dom';
import JobList from './pages/JobList';
import JobDetail from './pages/JobDetail';

export default function App() {
    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto max-w-6xl px-6 py-4">
                    <Link to="/" className="text-lg font-semibold text-slate-900">
                        JobEngine
                    </Link>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-6 py-6">
                <Routes>
                    <Route path="/" element={<JobList />} />
                    <Route path="/jobs/:id" element={<JobDetail />} />
                </Routes>
            </main>
        </div>
    );
}
