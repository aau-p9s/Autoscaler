START TRANSACTION;

CREATE TABLE IF NOT EXISTS Settings
(
    Id               varchar(40) NOT NULL PRIMARY KEY,
    ServiceId        varchar(40) NOT NULL,
    ScaleUp          integer     NOT NULL,
    ScaleDown        integer     NOT NULL,
    ScalePeriod      integer     NOT NULL,
    TrainInterval    integer     NOT NULL,
    ModelHyperParams json        NOT NULL,
    OptunaConfig     json        NOT NULL
);
COMMIT;