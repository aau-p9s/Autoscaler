START TRANSACTION;

CREATE TABLE IF NOT EXISTS HistoricData
(
    Id           UUID NOT NULL PRIMARY KEY,
    ServiceId    UUID NOT NULL,
    CreatedAt    timestamp   NOT NULL,
    HistoricData jsonb        NOT NULL
);
COMMIT;