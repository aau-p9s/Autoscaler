START TRANSACTION;

CREATE TABLE IF NOT EXISTS Settings
(
    Id               UUID NOT NULL PRIMARY KEY,
    ServiceId        UUID NOT NULL UNIQUE,
    ScaleUp          integer     NOT NULL,
    ScaleDown        integer     NOT NULL,
    ScalePeriod      integer     NOT NULL,
    TrainInterval    integer     NOT NULL,
    ModelHyperParams jsonb        NOT NULL,
    OptunaConfig     jsonb        NOT NULL
);

COMMIT;