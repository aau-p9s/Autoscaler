START TRANSACTION;

CREATE TABLE IF NOT EXISTS Forecasts
(
    Id        UUID NOT NULL PRIMARY KEY,
    ServiceId UUID NOT NULL,
    CreatedAt timestamp   NOT NULL,
    ModelId   UUID NOT NULL,
    Forecast  json        NOT NULL
);
COMMIT;