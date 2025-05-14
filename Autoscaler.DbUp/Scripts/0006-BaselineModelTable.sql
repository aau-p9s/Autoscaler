START TRANSACTION;

CREATE TABLE IF NOT EXISTS BaselineModels
(
    Id        UUID        NOT NULL PRIMARY KEY,
    Name      varchar(40) NOT NULL,
    Bin       bytea       NOT NULL,
    Ckpt      bytea,
    TrainedAt timestamp   NOT NULL
);
COMMIT;