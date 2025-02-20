START TRANSACTION;

CREATE TABLE IF NOT EXISTS Historics(
    Id varchar(40) NOT NULL PRIMARY KEY,
    ServiceId varchar(40) NOT NULL,
    Created timestamp NOT NULL,
    HistoricData json NOT NULL
)
COMMIT;