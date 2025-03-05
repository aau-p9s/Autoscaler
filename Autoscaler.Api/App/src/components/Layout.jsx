import React from 'react';
import { Container } from 'reactstrap';

const Layout = ({ children }) => {
    return (
        <div className="min-h-screen w-full">
                {children}
        </div>
    );
};

export default Layout;