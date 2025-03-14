START TRANSACTION;

CREATE TABLE IF NOT EXISTS Forecasts
(
    Id UUID NOT NULL PRIMARY KEY,
    ServiceId UUID NOT NULL,
    CreatedAt       timestamp NOT NULL,
    ModelId UUID NOT NULL,
    Forecast jsonb NOT NULL,
    HasManualChange boolean   NOT NULL DEFAULT false
);
COMMIT;