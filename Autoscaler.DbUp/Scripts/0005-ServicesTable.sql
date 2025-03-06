START TRANSACTION;

CREATE TABLE IF NOT EXISTS Services
(
    Id UUID NOT NULL PRIMARY KEY,
    Name               varchar(255) NOT NULL UNIQUE,
    AutoscalingEnabled boolean      NOT NULL
);
COMMIT;