import React, { useState, useEffect, useRef } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Line } from 'react-chartjs-2';
import 'chart.js/auto';
import Chart from 'chart.js/auto';
import dragDataPlugin from 'chartjs-plugin-dragdata';
import './ServicePage.css';

Chart.register(dragDataPlugin);

const ServicePage = (name) => {
    const params = useParams();

    // Service and Autoscaling State
    const [service, setService] = useState([]);
    const [isAutoscalingEnabled, setIsAutoscalingEnabled] = useState(false);

    // CPU Scaling State
    const [currentScaleValues, setCurrentScaleValues] = useState(null);
    const [scaleUpPercentage, setScaleUpPercentage] = useState('');
    const [scaleDownPercentage, setScaleDownPercentage] = useState('');
    const [minReplicas, setMinReplicas] = useState('');
    const [maxReplicas, setMaxReplicas] = useState('');
    const [interval, setInterval] = useState('');
    const [trainInterval, setTrainInterval] = useState('');
    const [scaleError, setScaleError] = useState(null);
    const [success, setSuccess] = useState(null);

    // Graph State
    const [forecast, setForecast] = useState(null);
    const [chartData, setChartData] = useState(null);
    const [originalChartData, setOriginalChartData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [dragEnabled, setDragEnabled] = useState(false);
    const [smartModifyEnabled, setSmartModifyEnabled] = useState(false);
    const [modifyEnabled, setModifyEnabled] = useState(false);
    const [firstEndpoint, setFirstEndpoint] = useState(null);
    const chartRef = useRef(null);

    const [modelHyperParams, setModelHyperParams] = useState(null);
    const [optunaConfig, setOptunaConfig] = useState(null);
    const [modelSettingsError, setModelSettingsError] = useState(null);
    const fetchCurrentScaleValues = async () => {
        try {
            const res = await fetch(`http://localhost:8080/services/${params.id}/settings`, { method: 'GET' });
            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }
            const data = await res.json();
            setCurrentScaleValues(data);
            setScaleUpPercentage(data.scaleUp || '');
            setScaleDownPercentage(data.scaleDown || '');
            setMinReplicas(data.minReplicas || '');
            setMaxReplicas(data.maxReplicas || '');
            setInterval(data.scalePeriod || '');
            setTrainInterval(data.trainInterval || '');
            setOptunaConfig(data.optunaConfig || {});
            setModelHyperParams(data.modelHyperParams || {});
        } catch (err) {
            setScaleError('Failed to fetch current values');
        }
    };
    const handleScaleSubmit = async (e) => {
        e.preventDefault();
        const payload = {
            id: params.id,
            scaleUp: parseFloat(scaleUpPercentage),
            scaleDown: parseFloat(scaleDownPercentage),
            minReplicas: parseInt(minReplicas, 10),
            maxReplicas: parseInt(maxReplicas, 10),
            scalePeriod: parseInt(interval, 10) * 60000,
            trainInterval: parseInt(trainInterval, 10) * 3600000,
            modelHyperParams: typeof modelHyperParams === 'object' ? JSON.stringify(modelHyperParams) : modelHyperParams,
            optunaConfig: typeof optunaConfig === 'object' ? JSON.stringify(optunaConfig) : optunaConfig,
        };

        if (minReplicas < 0 || maxReplicas < 0) {
            setScaleError('Replicas cannot be negative');
            return;
        }
        if (minReplicas > maxReplicas) {
            setScaleError('Min replicas cannot be greater than max replicas');
            return;
        }

        try {
            const res = await fetch(`/services/${params.id}/settings`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
            });
            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }
            setSuccess('Settings changed successfully');
            setScaleError(null);
            fetchCurrentScaleValues();
        } catch (err) {
            setScaleError('Failed to submit the settings');
        }
    };
    const handleAutoscalingEnabled = async () => {
        const payload = {
            id: params.id,
            name: service.name,
            autoscalingEnabled: !isAutoscalingEnabled,
        };
        try {
            const res = await fetch(`/services/${params.id}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
            });
            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }
            setSuccess(isAutoscalingEnabled ? 'Autoscaling disabled' : 'Autoscaling enabled');
            setScaleError(null);
            fetchServiceInformation();
        } catch (err) {
            setScaleError('Failed to submit the settings');
        }
    };

    const handleAutoscalingChange = async () => {
        const newStatus = !isAutoscalingEnabled;
        await handleAutoscalingEnabled(newStatus);
        setIsAutoscalingEnabled(newStatus);
    };
    const fetchGraphData = async () => {
        setIsLoading(true);
        try {
            const response = await fetch(`/services/${params.id}/forecast`, { method: 'GET' });
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            const json = await response.json();
            setForecast(json);
            const forecastData = JSON.parse(json.forecast);
            console.log("Forecast Data:", forecastData);
            if (forecastData && forecastData.timestamp.length > 0) {
                const labels = forecastData.timestamp;
                const data = forecastData.value.flat();
                labels.sort((a, b) => new Date(a) - new Date(b));
                const newChartData = {
                    labels,
                    datasets: [
                        {
                            label: 'Current forecast data',
                            data,
                            fill: false,
                            backgroundColor: 'rgb(10,88,202)',
                            borderColor: 'rgb(13,110,253)',
                        },
                    ],
                };
                setChartData(newChartData);
                setOriginalChartData(newChartData);
            } else {
                const fallbackData = await generateFallbackData("hour");
                setChartData(fallbackData);
                setOriginalChartData(fallbackData);
            }
        } catch (error) {
            console.error("Error fetching data:", error);
            const fallbackData = await generateFallbackData("hour");
            setChartData(fallbackData);
            setOriginalChartData(fallbackData);
        } finally {
            setIsLoading(false);
        }
    };
    const fetchServiceInformation = async () => {
        try {
            const response = await fetch(`/services/${params.id}`, { method: 'GET' });
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            const data = await response.json();
            setService(data);
            setIsAutoscalingEnabled(data.autoscalingEnabled);
        } catch (err) {
            console.error("Error fetching data:", err);
        }
    };
    const generateFallbackData = async (interval) => {
        let labels = Array.from({ length: 12 }, (_, i) => `${i * 5} min`);
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
    const handleGraphSave = async () => {
        try {
            const forecastPayload = JSON.stringify({
                columns: ["cpu"],
                timestamp: chartData.labels,
                value: chartData.datasets[0].data.map(val => [val])
            });
            const payload = {
                id: forecast.id,
                serviceId: params.id,
                createdAt: forecast.createdAt,
                modelId: forecast.modelId,
                forecast: forecastPayload,
                hasManualChange: true,
            };
            const response = await fetch(`/services/${params.id}/forecast`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
            });
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            setSuccess('Forecast saved successfully');
            await fetchGraphData();
        } catch (error) {
            console.error('Error:', error);
        }
    };
    const handleReset = () => {
        if (originalChartData) {
            setChartData(originalChartData);
            setFirstEndpoint(null);
        }
    };
    const parseStringifiedJson = (jsonString) => {
        try {
            return typeof jsonString === 'string' ? JSON.parse(jsonString) : jsonString;
        } catch (error) {
            console.error('Failed to parse JSON:', error);
            return {};
        }
    };

    const renderJson = (obj) => {
        const parsedObj = parseStringifiedJson(obj);
        return Object.entries(parsedObj).map(([key, value]) => (
            <div key={key} className="mb-1">
                <strong>{key}:</strong> {typeof value === 'object' ? JSON.stringify(value) : value.toString()}
            </div>
        ));
    };

    useEffect(() => {
        if (scaleError) {
            const timer = setTimeout(() => setScaleError(null), 2000);
            return () => clearTimeout(timer);
        }
    }, [scaleError]);

    useEffect(() => {
        if (success) {
            const timer = setTimeout(() => setSuccess(null), 2000);
            return () => clearTimeout(timer);
        }
    }, [success]);

    useEffect(() => {
        fetchCurrentScaleValues();
        fetchGraphData();
        fetchServiceInformation();
    }, []);
    const handleNormalModifyToggle = () => {

        if (!modifyEnabled)
        {
            setDragEnabled(true);
            setModifyEnabled(true);
        }
        if(smartModifyEnabled)
        {
            setSmartModifyEnabled(false);
        }
        if(modifyEnabled)
        {
            setDragEnabled(false);
            setModifyEnabled(false);
        }
    };

    const handleSmartModifyToggle = () => {
        if(!smartModifyEnabled)
        {
            setDragEnabled(true);
            setSmartModifyEnabled(true)
            setFirstEndpoint(null);
        }
        if(modifyEnabled){
            setModifyEnabled(false);
        }
        if(smartModifyEnabled){
            setDragEnabled(false);
            setSmartModifyEnabled(false);
        }
    };

    const dragDataConfig = smartModifyEnabled
        ? {
            onDrag: (e, datasetIndex, index, value) => {
                if (firstEndpoint !== null) {
                    const start = Math.min(firstEndpoint.index, index);
                    const end = Math.max(firstEndpoint.index, index);
                    const newData = [...chartData.datasets[datasetIndex].data];
                    for (let i = start; i <= end; i++) {
                        if(i=== start){
                            continue;
                        }
                        newData[i] = value;
                    }
                    setChartData((prev) => ({
                        ...prev,
                        datasets: prev.datasets.map((ds, dsIndex) =>
                            dsIndex === datasetIndex ? { ...ds, data: newData } : ds
                        ),
                    }));
                } else {
                    // If no endpoint stored, update only the dragged point.
                    const newData = [...chartData.datasets[datasetIndex].data];
                    newData[index] = value;
                    setChartData((prev) => ({
                        ...prev,
                        datasets: prev.datasets.map((ds, dsIndex) =>
                            dsIndex === datasetIndex ? { ...ds, data: newData } : ds
                        ),
                    }));
                }
            },
            onDragEnd: (e, datasetIndex, index, value) => {
                // On first drag, store the endpoint; on second, clear it.
                if (firstEndpoint === null) {
                    setFirstEndpoint({ index, value });
                } else {
                    setFirstEndpoint(null);
                }
            },
        }
        : {
            // Normal modify: update only the dragged point.
            onDrag: (e, datasetIndex, index, value) => {
                const newData = [...chartData.datasets[datasetIndex].data];
                newData[index] = value;
                setChartData((prev) => ({
                    ...prev,
                    datasets: prev.datasets.map((ds, dsIndex) =>
                        dsIndex === datasetIndex ? { ...ds, data: newData } : ds
                    ),
                }));
            },
            onDragEnd: () => {
                console.log('Drag ended, data saved.');
            },
        };

    const graphOptions = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            x: { title: { display: true, text: 'Time' } },
            y: { title: { display: true, text: 'Value' } },
        },
        plugins: {
            dragData: dragEnabled ? dragDataConfig : false,
            legend: { display: true },
        },
    };

    return (
        <div className="min-h-screen w-full">
            <div className="nav-bar">
                <Link to="/" className="secondary-button">
                    Go to Overview
                </Link>
            </div>

            <div style={{ height: "120vh", width: "120vw" }} className="row">
                {/* Left Sidebar - CPU Scaling */}
                <div className="col-md-2 sidebar">
                    <h3>Scale Settings</h3>
                    {currentScaleValues && (
                        <div className="current-values">
                            <h4>Current Settings</h4>
                            <p><strong>Scale Up at:</strong> {currentScaleValues.scaleUp}%</p>
                            <p><strong>Scale Down at:</strong> {currentScaleValues.scaleDown}%</p>
                            <p><strong>Min Replicas:</strong> {currentScaleValues.minReplicas}</p>
                            <p><strong>Max Replicas:</strong> {currentScaleValues.maxReplicas}</p>
                            <p><strong>Interval:</strong> {currentScaleValues.scalePeriod} ms</p>
                            <p><strong>Train Interval:</strong> {currentScaleValues.trainInterval} ms</p>
                        </div>
                    )}
                    <h4>Update Settings</h4>
                    <button
                        onClick={handleAutoscalingChange}
                        className={isAutoscalingEnabled ? "secondary-button" : "primary-button"}
                    >
                        {isAutoscalingEnabled ? "Disable Autoscaling" : "Enable Autoscaling"}
                    </button>
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
                            <label>Min replicas</label>
                            <input
                                type="number"
                                className="form-control"
                                value={minReplicas}
                                onChange={(e) => setMinReplicas(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Max replicas</label>
                            <input
                                type="number"
                                className="form-control"
                                value={maxReplicas}
                                onChange={(e) => setMaxReplicas(e.target.value)}
                                required
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Interval (min 1 minute)</label>
                            <input
                                type="number"
                                className="form-control"
                                value={interval}
                                onChange={(e) => setInterval(e.target.value)}
                                required
                                min={1}
                                step={1}
                            />
                        </div>
                        <div className="form-group mb-2">
                            <label>Train Interval (min 1 hour)</label>
                            <input
                                type="number"
                                className="form-control"
                                value={trainInterval}
                                onChange={(e) => setTrainInterval(e.target.value)}
                                required
                                min={1}
                                step={1}
                            />
                        </div>
                        <button type="submit" className="submit-button">Submit</button>
                        {scaleError && <div className="response error mt-2">{scaleError}</div>}
                        {success && <div className="response success mt-2">{success}</div>}
                    </form>
                </div>

                {/* Center - Graph */}
                <div className="col-md-8 p-3">
                    <h1 className="text-center mb-4">Managing {service.name}</h1>
                    {isLoading ? (
                        <div>Loading chart data...</div>
                    ) : (
                        <div style={{ height: '500px' }}>
                            <Line ref={chartRef} data={chartData} options={graphOptions} />
                            <div className="d-flex justify-content-between mt-3">
                                <div className="d-flex">
                                    <button onClick={handleNormalModifyToggle} className="btn btn-secondary me-2">
                                        {modifyEnabled ? 'Disable Modify' : 'Modify'}
                                    </button>
                                    <button onClick={handleSmartModifyToggle} className="btn btn-secondary me-2">
                                        {smartModifyEnabled ? 'Disable Smart Modify' : 'Smart Modify'}
                                    </button>
                                </div>
                                <div className="d-flex">
                                    <button onClick={handleReset} className="btn btn-secondary me-2">
                                        Reset modifications
                                    </button>
                                    <button onClick={handleGraphSave} disabled={!dragEnabled} className="primary-button">
                                        Save Forecast
                                    </button>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default ServicePage;
