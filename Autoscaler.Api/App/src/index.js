import 'bootstrap/dist/css/bootstrap.css';
import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import * as serviceWorkerRegistration from './serviceWorkerRegistration';
import reportWebVitals from './reportWebVitals';
import { Layout } from "./components/Layout";
import ServicePage from "./service/[id]/service";

const services = [
    { id: 1, name: "Service A" },
    { id: 2, name: "Service B" },
    { id: 3, name: "Service C" },
    { id: 4, name: "Service D" }
];

const ServicesGrid = () => (
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
