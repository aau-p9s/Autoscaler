import React from 'react';
import TimePeriodGraph from "../../components/TimePeriodGraph";
import { useParams } from "react-router-dom";
import CpuScalingFields from "../../components/CpuScalingFields";

const services = [
    { id: 1, name: "Service A" },
    { id: 2, name: "Service B" },
    { id: 3, name: "Service C" },
    { id: 4, name: "Service D" }
];

const ServicePage = () => {
    const { id } = useParams();
    const service = services.find(s => s.id === Number(id));

    return (
        <div className="container">
            <div className="row">
                <div className="col-md-3">
                    <CpuScalingFields />
                </div>
                <div className="col-md-9">
                    <h1 className="mt-3 mb-4">
                        Managing {service?.name || "Service"}
                    </h1>
                    <TimePeriodGraph />
                </div>
            </div>
        </div>
    );
};

export default ServicePage;