import React, { useState, useEffect, useRef } from 'react';
import { Line } from 'react-chartjs-2';
import 'chart.js/auto';
import Chart from 'chart.js/auto';
import dragDataPlugin from 'chartjs-plugin-dragdata';

Chart.register(dragDataPlugin);

const TimePeriodGraph = (id) => {
    const [chartData, setChartData] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [dragEnabled, setDragEnabled] = useState(false);
    const chartRef = useRef(null);

    const generateData = async (interval) => {
        let labels;
        let data;

        if (interval === 'hour') {
            labels = Array.from({ length: 12 }, (_, i) => `${i * 5} min`);
            data = [45, 50, 55, 60, 58, 63, 70, 68, 75, 80, 85, 90];
        } else if (interval === 'day') {
            labels = Array.from({ length: 24 }, (_, i) => `${i}:00`);
            data = [30, 35, 28, 40, 45, 50, 55, 60, 62, 65, 70, 72, 75, 78, 80, 82, 85, 87, 90, 92, 95, 98, 100, 105];
        } else if (interval === 'week') {
            labels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
        }

        return {
            labels,
            datasets: [
                {
                    label: 'No data from server, using generated data',
                    data,
                    fill: false,
                    backgroundColor: 'rgba(75, 192, 192, 0.6)',
                    borderColor: 'rgba(75, 192, 192, 1)',
                },
            ],
        };
    };

    const fetchData = async () => {
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
                const fallbackData = await generateData("hour");
                setChartData(fallbackData);
            }
        } catch (error) {
            console.error("Error fetching data:", error);
            const fallbackData = await generateData("hour");
            setChartData(fallbackData);
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    if (isLoading) {
        return <div>Loading chart data...</div>;
    }

    const options = {
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

    const handleSave = async () => {
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

            await fetchData();
        } catch (error) {
            console.error('Error:', error);
        }
    };

    return (
        <div style={{ width: '80%', height: '800px', margin: '0 auto' }}>
            <Line ref={chartRef} data={chartData} options={options} />
            <div style={{
                marginBottom: '20px',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
            }}>
                <div>
                    <button onClick={() => setDragEnabled(!dragEnabled)} style={buttonStyle}>
                        {dragEnabled ? 'Disable Modify' : 'Modify'}
                    </button>
                    <button onClick={handleSave} style={buttonStyle}>
                        Save Forecast
                    </button>
                </div>
            </div>
        </div>
    );
};

const buttonStyle = {
    backgroundColor: 'rgba(75, 192, 192, 0.6)',
    borderColor: 'rgba(255,255,255)',
    borderRadius: '5px',
    color: '#000',
    padding: '10px 20px',
    margin: '0 10px',
    cursor: 'pointer',
    fontSize: '16px',
    transition: 'all 0.3s ease',
};

export default TimePeriodGraph;