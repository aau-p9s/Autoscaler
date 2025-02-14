import React from 'react';
import { Container } from 'reactstrap';
import NavMenu from './NavMenu';

const Layout = ({ children }) => {
    return (
        <div>
            <NavMenu />
            <Container tag="main">
                {children}
            </Container>
        </div>
    );
};

export default Layout;