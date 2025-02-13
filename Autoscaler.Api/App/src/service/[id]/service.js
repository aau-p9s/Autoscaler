import TimePeriodGraph from "../../components/TimePeriodGraph";
import {useParams} from "react-router-dom";
import services from "bootstrap/js/src/dom/selector-engine";
import CpuScalingFields from "../../components/CpuScalingFields";


const services1 = [
    { id: 1, name: "Service A" },
    { id: 2, name: "Service B" },
    { id: 3, name: "Service C" },
    { id: 4, name: "Service D" }
];

const ServicePage = () => {
    const { id } = useParams();
    const service = services1.find(s => s.id === Number(id));
    return (
        <div className="container">
            <div className="row row-cols-md-6">
                <div className="col-md-2">
                    <CpuScalingFields />
                </div>

                <div className="col-md-10">
                    <h1>
                        Managing {service?.name || "Service"}
                    </h1>
                    <TimePeriodGraph />
                </div>
            </div>
        </div>
    );
};

export default ServicePage;