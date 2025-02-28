import React from 'react';
import TimePeriodGraph from "../../components/TimePeriodGraph";
import { useParams } from "react-router-dom";
import CpuScalingFields from "../../components/CpuScalingFields";
const ServicePage = () => {
    const params = useParams();

    return (
        <div className="container">
            <div className="row">
                <div className="col-md-3">
                    <CpuScalingFields id={params.id} />
                </div>
                <div className="col-md-9">
                    <h1 className="mt-3 mb-4">
                        Managing {"Service"}
                    </h1>
                    <TimePeriodGraph id={params.id} />
                </div>
            </div>
        </div>
    );
};

export default ServicePage;