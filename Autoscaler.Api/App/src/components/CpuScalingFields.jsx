import React, { useState, useEffect } from 'react';
import './ScalesettingsSidebar.css';

const CpuScalingFields = () => {
    const [currentValues, setCurrentValues] = useState(null);
    const [scaleUpPercentage, setScaleUpPercentage] = useState('');
    const [scaleDownPercentage, setScaleDownPercentage] = useState('');
    const [interval, setInterval] = useState('');
    const [id, setId] = useState('');
    const [response, setResponse] = useState(null);
    const [error, setError] = useState(null);

    const fetchCurrentValues = async () => {
        try {
            const res = await fetch(`http://${window.location.hostname}:8080/settings`);
            if (!res.ok) {
                throw new Error(`HTTP error! Status: ${res.status}`);
            }
            const data = await res.json();
            setCurrentValues(data);
            setScaleUpPercentage(data.scaleUp || '');
            setScaleDownPercentage(data.scaleDown || '');
            setId(data.id);
            setInterval(data.scalePeriod || '');
        } catch (err) {
            setError('Failed to fetch current values');
        }
    };

    useEffect(() => {
        fetchCurrentValues();
    }, []);

    const handleSubmit = async (e) => {
        e.preventDefault();

        const payload = {
            id,
            scaleUp: parseFloat(scaleUpPercentage),
            scaleDown: parseFloat(scaleDownPercentage),
            scalePeriod: parseInt(interval, 10),
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

            setError(null);
            fetchCurrentValues();
        } catch (err) {
            setError('Failed to submit the settings');
            setResponse(null);
        }
    };

    return (
        <div className="sidebar">
            <h3>Scale Settings</h3>

            <div className="current-values">
                <h4>Current Values</h4>
                {currentValues ? (
                    <>
                        <p><strong>Current Scale Up:</strong> {currentValues.scaleUp}%</p>
                        <p><strong>Current Scale Down:</strong> {currentValues.scaleDown}%</p>
                        <p><strong>Current Interval:</strong> {currentValues.scalePeriod} ms</p>
                    </>
                ) : (
                    <p>Loading current values...</p>
                )}
            </div>

            <form onSubmit={handleSubmit}>
                <div className="form-group">
                    <label>
                        Scale Up Percentage:
                        <input
                            type="number"
                            step="0.01"
                            value={scaleUpPercentage}
                            onChange={(e) => setScaleUpPercentage(e.target.value)}
                            required
                        />
                    </label>
                </div>
                <div className="form-group">
                    <label>
                        Scale Down Percentage:
                        <input
                            type="number"
                            step="0.01"
                            value={scaleDownPercentage}
                            onChange={(e) => setScaleDownPercentage(e.target.value)}
                            required
                        />
                    </label>
                </div>
                <div className="form-group">
                    <label>
                        Interval (ms):
                        <input
                            type="number"
                            value={interval}
                            onChange={(e) => setInterval(e.target.value)}
                            required
                        />
                    </label>
                </div>
                <button type="submit" className="submit-button" style={buttonStyle}>
                    Submit
                </button>
            </form>

            {response && (
                <div className="response success">
                    <h4>Response:</h4>
                    <pre>{JSON.stringify(response, null, 2)}</pre>
                </div>
            )}
            {error && (
                <div className="response error">
                    <h4>Error:</h4>
                    <p>{error}</p>
                </div>
            )}
        </div>
    );
};

const buttonStyle = {
    backgroundColor: 'rgba(75, 192, 192, 0.6)',
    borderColor: 'rgba(255,255,255)',
    borderRadius: '5px',
    color: '#000',
    padding: '10px 20px',
    cursor: 'pointer',
    fontSize: '16px',
    transition: 'all 0.3s ease',
};

export default CpuScalingFields;