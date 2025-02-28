START TRANSACTION;

CREATE TABLE IF NOT EXISTS Forecasts
(
    Id        varchar(40) NOT NULL PRIMARY KEY,
    ServiceId varchar(40) NOT NULL,
    CreatedAt timestamp   NOT NULL,
    ModelId   varchar(40) NOT NULL,
    Forecast  json        NOT NULL
);
COMMIT;