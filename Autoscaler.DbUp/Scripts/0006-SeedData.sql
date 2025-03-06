START TRANSACTION;

-- Insert sample data into Services table
INSERT INTO Services (Id, Name, AutoscalingEnabled)
VALUES ('1a2b3c4d-1111-2222-3333-444455556666', 'Image Recognition', true),
       ('2b3c4d5e-1111-2222-3333-444455556666', 'Speech-to-Text', true),
       ('3c4d5e6f-1111-2222-3333-444455556666', 'Recommendation System', true);

-- Insert sample data into Settings table
INSERT INTO Settings (Id, ServiceId, ScaleUp, ScaleDown, ScalePeriod, TrainInterval, ModelHyperParams,
                      OptunaConfig)
VALUES ('a1b2c3d4-aaaa-bbbb-cccc-ddddeeeeffff', '1a2b3c4d-1111-2222-3333-444455556666', 5, 2, 10, 30,
        '{"learning_rate": 0.01, "batch_size": 32, "epochs": 50}',
        '{"n_trials": 100, "direction": "maximize"}'),

       ('b2c3d4e5-aaaa-bbbb-cccc-ddddeeeeffff', '2b3c4d5e-1111-2222-3333-444455556666', 3, 1, 15, 45,
        '{"learning_rate": 0.005, "batch_size": 64, "epochs": 30}',
        '{"n_trials": 50, "direction": "minimize"}'),

       ('c3d4e5f6-aaaa-bbbb-cccc-ddddeeeeffff', '3c4d5e6f-1111-2222-3333-444455556666', 10, 5, 20, 60,
        '{"learning_rate": 0.001, "batch_size": 128, "epochs": 100}',
        '{"n_trials": 200, "direction": "maximize"}');

-- Insert sample data into Forecasts table
INSERT INTO Forecasts (Id, ServiceId, CreatedAt, ModelId, Forecast)
VALUES ('f1a2b3c4-aaaa-bbbb-cccc-ddddeeeeffff', '1a2b3c4d-1111-2222-3333-444455556666', '2025-03-06 12:00:00', '1a2b3c4d-aaaa-bbbb-cccc-ddddeeeeffff',
        '[{"timestamp": "2025-03-06T12:00:00Z", "cpu_percentage": 45.2},
          {"timestamp": "2025-03-06T12:05:00Z", "cpu_percentage": 47.8},
          {"timestamp": "2025-03-06T12:10:00Z", "cpu_percentage": 50.3}]'),
       ('f2b3c4d5-aaaa-bbbb-cccc-ddddeeeeffff', '2b3c4d5e-1111-2222-3333-444455556666', '2025-03-06 12:00:00', '2b3c4d5e-aaaa-bbbb-cccc-ddddeeeeffff',
        '[{"timestamp": "2025-03-06T12:00:00Z", "cpu_percentage": 30.1},
          {"timestamp": "2025-03-06T12:05:00Z", "cpu_percentage": 32.5},
          {"timestamp": "2025-03-06T12:10:00Z", "cpu_percentage": 34.7}]'),
       ('f3c4d5e6-aaaa-bbbb-cccc-ddddeeeeffff', '3c4d5e6f-1111-2222-3333-444455556666', '2025-03-06 12:00:00', '3c4d5e6f-aaaa-bbbb-cccc-ddddeeeeffff',
        '[{"timestamp": "2025-03-06T12:00:00Z", "cpu_percentage": 60.0},
          {"timestamp": "2025-03-06T12:05:00Z", "cpu_percentage": 62.3},
          {"timestamp": "2025-03-06T12:10:00Z", "cpu_percentage": 64.8}]');

COMMIT;