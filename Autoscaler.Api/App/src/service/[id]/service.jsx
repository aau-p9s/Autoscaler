import React, {useState, useEffect, useRef} from 'react';
import {Link, useParams} from 'react-router-dom';
import {Line} from 'react-chartjs-2';
import 'chart.js/auto';
import Chart from 'chart.js/auto';
import dragDataPlugin from 'chartjs-plugin-dragdata';
import './ServicePage.css';
import {Navbar, NavbarBrand} from "reactstrap";

Chart.register(dragDataPlugin);

const ServicePage = (name) => {
    const params = useParams();

    const [serviceName, setServiceName] = useState([]);

    // CPU Scaling State
    const [currentScaleValues, setCurrentScaleValues] = useState(null);
    const [scaleUpPercentage, setScaleUpPercentage] = useState('');
    const [scaleDownPercentage, setScaleDownPercentage] = useState('');
    const [interval, setInterval] = useState('');
    const [trainInterval, setTrainInterval] = useState('');
    const [scaleError, setScaleError] = useState(null);

    // Graph State
    const [chartData, setChartData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [dragEnabled, setDragEnabled] = useState(false);
    const chartRef = useRef(null);

    // Model Settings State
    const [modelHyperParams, setModelHyperParams] = useState(null);
    const [optunaConfig, setOptunaConfig] = useState(null);
    const [modelSettingsError, setModelSettingsError] = useState(null);

    // Fetch Current Scale Values
    const fetchCurrentScaleValues = async () => {
        try {
            const res = await fetch(`http://${window.location.hostname}:8080/services/${params.id}/settings`, {method: 'GET'});
            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }
            const data = await res.json();
            setCurrentScaleValues(data);
            setScaleUpPercentage(data.scaleUp || '');
            setScaleDownPercentage(data.scaleDown || '');
            setInterval(data.scalePeriod || '');
            setTrainInterval(data.trainInterval || '');
            setOptunaConfig(data.optunaConfig || {})
            setModelHyperParams(data.modelHyperParams || {})
        } catch (err) {
            setScaleError('Failed to fetch current values');
        }
    };

    // Handle Scale Settings Submission
    const handleScaleSubmit = async (e) => {
        e.preventDefault();

        const payload = {
            id: params.id,
            scaleUp: parseFloat(scaleUpPercentage),
            scaleDown: parseFloat(scaleDownPercentage),
            scalePeriod: parseInt(interval, 10),
            trainInterval: parseInt(trainInterval, 10),
        };

        try {
            const res = await fetch(`http://${window.location.hostname}:8080/settings`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(payload),
            });

            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }

            setScaleError(null);
            fetchCurrentScaleValues();
        } catch (err) {
            setScaleError('Failed to submit the settings');
        }
    };

    // Fetch Graph Data
    const fetchGraphData = async () => {
        setIsLoading(true);

        try {
            const response = await fetch(`http://${window.location.hostname}:8080/forecast`);
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }

            const json = await response.json();

            if (json && Object.keys(json).length > 0) {
                const labels = Object.keys(json);
                const data = Object.values(json);
                labels.sort((a, b) => new Date(a) - new Date(b));
                setChartData({
                    labels,
                    datasets: [
                        {
                            label: 'Current forecast data',
                            data,
                            fill: false,
                            backgroundColor: 'rgba(75, 192, 192, 0.6)',
                            borderColor: 'rgba(75, 192, 192, 1)',
                        },
                    ],
                });
            } else {
                const fallbackData = await generateFallbackData("hour");
                setChartData(fallbackData);
            }
        } catch (error) {
            console.error("Error fetching data:", error);
            const fallbackData = await generateFallbackData("hour");
            setChartData(fallbackData);
        } finally {
            setIsLoading(false);
        }
    };

    const fetchServiceInformation = async () => {
        try {
            const response = await fetch(`http://${window.location.hostname}:8080/services/${params.id}`, {method: 'GET'});
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            const data = await response.json();
            setServiceName(data.name);
        } catch (err) {
            console.error("Error fetching data:", err);
        }
    };

    // Generate Fallback Graph Data
    const generateFallbackData = async (interval) => {
        let labels = Array.from({length: 12}, (_, i) => `${i * 5} min`);
        let data = [45, 50, 55, 60, 58, 63, 70, 68, 75, 80, 85, 90];

        return {
            labels,
            datasets: [
                {
                    label: 'No data from server, using generated data',
                    data,
                    fill: false,
                    backgroundColor: 'rgb(10,88,202)',
                    borderColor: 'rgb(13,110,253)',
                },
            ],
        };
    };

    // Handle Graph Save
    const handleGraphSave = async () => {
        try {
            const payload = chartData.labels.reduce((acc, label, index) => {
                acc[label] = chartData.datasets[0].data[index];
                return acc;
            }, {});

            const response = await fetch(`http://${window.location.hostname}:8080/forecast`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }

            await fetchGraphData();
        } catch (error) {
            console.error('Error:', error);
        }
    };

    // Render JSON for Model Settings
    const parseStringifiedJson = (jsonString) => {
        try {
            return typeof jsonString === 'string' ? JSON.parse(jsonString) : jsonString;
        } catch (error) {
            console.error('Failed to parse JSON:', error);
            return {};
        }
    };

    // Render JSON for Model Settings
    const renderJson = (obj) => {
        const parsedObj = parseStringifiedJson(obj);
        return Object.entries(parsedObj).map(([key, value]) => (
            <div key={key} className="mb-1">
                <strong>{key}:</strong> {typeof value === 'object' ? JSON.stringify(value) : value.toString()}
            </div>
        ));
    };

    // Initial Data Fetching
    useEffect(() => {
        fetchCurrentScaleValues();
        fetchGraphData();
        fetchServiceInformation();
    }, []);

    // Graph Options
    const graphOptions = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            x: {
                title: {
                    display: true,
                    text: 'Time',
                },
            },
            y: {
                title: {
                    display: true,
                    text: 'Value',
                },
            },
        },
        plugins: {
            dragData: dragEnabled
                ? {
                    onDrag: (e, datasetIndex, index, value) => {
                        const updatedData = [...chartData.datasets[datasetIndex].data];
                        updatedData[index] = value;
                        setChartData((prev) => ({
                            ...prev,
                            datasets: [
                                {
                                    ...prev.datasets[datasetIndex],
                                    data: updatedData,
                                },
                            ],
                        }));
                    },
                    onDragEnd: () => {
                        console.log('Drag ended, data saved.');
                    },
                }
                : false,
            legend: {
                display: true,
            },
        },
    };

    return (
        <div className="min-h-screen w-full">
            <div className="nav-bar">
                <Link to="/" className="secondary-button">
                    Go to Overview
                </Link>
                
            </div>

            <div style={{height: "100vh", width: "100vw"}} className="row">
                {/* Left Sidebar - CPU Scaling */}
                <div className="col-md-2 sidebar">
                    <h3>Scale Settings</h3>
                    {currentScaleValues && (
                        <div className="current-values">
                            <h4>Current Settings</h4>
                            <p><strong>Scale Up:</strong> {currentScaleValues.scaleUp}%</p>
                            <p><strong>Scale Down:</strong> {currentScaleValues.scaleDown}%</p>
                            <p><strong>Interval:</strong> {currentScaleValues.scalePeriod} ms</p>
                            <p><strong>Train Interval:</strong> {currentScaleValues.trainInterval} ms</p>
                        </div>
                    )}
                    <form onSubmit={handleScaleSubmit}>
                        <div className="form-group mb-2">
                            <label>Scale Up Percentage</label>
                            <input
                                type="number"
                                className="form-control"
                                value={scaleUpPercentage}
                                onChange={(e) => setScaleUpPercentage(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Scale Down Percentage</label>
                            <input
                                type="number"
                                className="form-control"
                                value={scaleDownPercentage}
                                onChange={(e) => setScaleDownPercentage(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Interval (ms)</label>
                            <input
                                type="number"
                                className="form-control"
                                value={interval}
                                onChange={(e) => setInterval(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Train Interval (ms)</label>
                            <input
                                type="number"
                                className="form-control"
                                value={trainInterval}
                                onChange={(e) => setTrainInterval(e.target.value)}
                                required
                            />
                        </div>
                        <button type="submit" className="submit-button">Submit</button>
                        {scaleError && <div className="response error mt-2">{scaleError}</div>}
                    </form>
                </div>

                {/* Center - Graph */}
                <div className="col-md-8 p-3">
                    <h1 className="text-center mb-4">Managing {serviceName}</h1>
                    {isLoading ? (
                        <div>Loading chart data...</div>
                    ) : (
                        <div style={{height: '500px'}}>
                            <Line ref={chartRef} data={chartData} options={graphOptions}/>
                            <div className="d-flex justify-content-center mt-3">
                                <button
                                    onClick={() => setDragEnabled(!dragEnabled)}
                                    className="btn btn-secondary me-2"
                                >
                                    {dragEnabled ? 'Disable Modify' : 'Modify'}
                                </button>
                                <button
                                    onClick={handleGraphSave}
                                    className="btn btn-primary"
                                >
                                    Save Forecast
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Right Sidebar - Model Settings */}
                <div className="col-md-2 sidebar-right">
                    <h3>Model Settings</h3>
                    {modelSettingsError && <div className="response error">{modelSettingsError}</div>}

                    <div className="current-values mb-3">
                        <h4>Model Hyper Parameters</h4>
                        {modelHyperParams ? renderJson(modelHyperParams) : <p>Loading...</p>}
                    </div>

                    <div className="current-values mb-3">
                        <h4>Optuna Config</h4>
                        {optunaConfig ? renderJson(optunaConfig) : <p>Loading...</p>}
                    </div>
                </div>
            </div>
        </div>
    );
};


export default ServicePage;