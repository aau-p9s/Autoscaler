import 'bootstrap/dist/css/bootstrap.css';
import React, {useEffect, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {BrowserRouter, Routes, Route, Link} from 'react-router-dom';
import * as serviceWorkerRegistration from './serviceWorkerRegistration';
import reportWebVitals from './reportWebVitals';
import Layout from "./components/Layout";
import ServicePage from "./service/[id]/service";
import './service/[id]/ServicePage.css';
import './custom.css';

const ServicesGrid = () => {
    const [services, setServices] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchServices = async () => {
            try {
                const response = await fetch(`http://${window.location.hostname}:8080/services`, {method: 'GET'});
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
        <div>
            <div className="nav-bar">
                <h2>Service Control Panel</h2>
            </div>
            <div className="row">
                {/* Column for services with autoscalingEnables = true */}
                <div className="col-md-6 d-flex flex-column align-items-center divider-column colm p-5">
                    <h3 className="text-center mb-4">Services with autoscaling enabled</h3>
                    {services.filter(service => service.autoscalingEnabled === true).map(service => (
                        <div key={service.id} className="col-md-6 mb-5">
                            <div className="card text-center p-3">
                                <h5>{service.name}</h5>
                                <Link to={`/service/${service.id}`} className="btn btn-primary">Manage</Link>
                            </div>
                        </div>
                    ))}
                </div>

                {/* Column for services with autoscalingEnables = false */}
                <div className="col-md-6 d-flex flex-column align-items-center colm p-5">
                    <h3 className="text-center mb-4">Services with autoscaling disabled</h3>
                    {services.filter(service => service.autoscalingEnabled === false).map(service => (
                        <div key={service.id} className="col-md-6 mb-5">
                            <div className="card text-center p-3">
                                <h5>{service.name}</h5>
                                <Link to={`/service/${service.id}`} className="btn btn-primary">Manage</Link>
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

const App = () => (
    <BrowserRouter>
        <Layout>
            <Routes>
                <Route path="/" element={<ServicesGrid/>}/>
                <Route path="/service/:id" element={<ServicePage/>}/>
            </Routes>
        </Layout>
    </BrowserRouter>
);

const rootElement = document.getElementById('root');
const root = createRoot(rootElement);
root.render(<App/>);

serviceWorkerRegistration.unregister();
reportWebVitals();
