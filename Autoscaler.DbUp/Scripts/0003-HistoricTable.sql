START TRANSACTION;

CREATE TABLE IF NOT EXISTS HistoricData(
    Id varchar(40) NOT NULL PRIMARY KEY,
    ServiceId varchar(40) NOT NULL,
    CreatedAt timestamp NOT NULL,
    HistoricData json NOT NULL
);
COMMIT;