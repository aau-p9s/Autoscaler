import 'bootstrap/dist/css/bootstrap.css';
import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import * as serviceWorkerRegistration from './serviceWorkerRegistration';
import reportWebVitals from './reportWebVitals';
import Layout from "./components/Layout";
import ServicePage from "./service/[id]/service";

const ServicesGrid = () => {
    const [services, setServices] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchServices = async () => {
            try {
                const response = await fetch(`http://${window.location.hostname}:8080/services`, { method: 'GET' });
                if (!response.ok) {
                    throw new Error('Failed to fetch services');
                }
                const data = await response.json();
                setServices(data);
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };
        fetchServices();
    }, []);

    if (loading) return <div className="container mt-4"><p>Loading...</p></div>;
    if (error) return <div className="container mt-4"><p className="text-danger">Error: {error}</p></div>;

    return (
        <div className="container mt-4">
            <h2>Service Control Panel</h2>
            <div className="row">
                {services.map(service => (
                    <div key={service.id} className="col-md-3 mb-4">
                        <div className="card text-center p-3">
                            <h5>{service.name}</h5>
                            <Link to={`/service/${service.id}`} className="btn btn-primary">Manage</Link>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};

const App = () => (
    <BrowserRouter>
        <Layout>
            <Routes>
                <Route path="/" element={<ServicesGrid />} />
                <Route path="/service/:id" element={<ServicePage />} />
            </Routes>
        </Layout>
    </BrowserRouter>
);

const rootElement = document.getElementById('root');
const root = createRoot(rootElement);
root.render(<App />);

serviceWorkerRegistration.unregister();
reportWebVitals();
