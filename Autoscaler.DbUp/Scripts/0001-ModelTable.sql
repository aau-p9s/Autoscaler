START TRANSACTION;

CREATE TABLE IF NOT EXISTS Models(
    Id varchar(40) NOT NULL PRIMARY KEY,
    Name varchar(40) NOT NULL,
    ServiceId varchar(40) NOT NULL,
    Bin BLOB NOT NULL,
    TrainedAt timestamp NOT NULL
);
COMMIT;