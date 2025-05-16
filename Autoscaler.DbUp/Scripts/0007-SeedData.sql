START TRANSACTION;

-- Insert sample data into Services table
INSERT INTO Services (Id, Name, AutoscalingEnabled)
VALUES ('1a2b3c4d-1111-2222-3333-444455556666', 'Image Recognition', true),
       ('2b3c4d5e-1111-2222-3333-444455556666', 'Speech-to-Text', true),
       ('3c4d5e6f-1111-2222-3333-444455556666', 'Recommendation System', true);

-- Insert sample data into Settings table
INSERT INTO Settings (Id, ServiceId, ScaleUp, ScaleDown, MinReplicas, MaxReplicas, ScalePeriod, TrainInterval)
VALUES ('a1b2c3d4-aaaa-bbbb-cccc-ddddeeeeffff', '1a2b3c4d-1111-2222-3333-444455556666', 5, 2, 1, 10, 10, 30),

       ('b2c3d4e5-aaaa-bbbb-cccc-ddddeeeeffff', '2b3c4d5e-1111-2222-3333-444455556666', 3, 1, 1, 10, 15, 45),

       ('c3d4e5f6-aaaa-bbbb-cccc-ddddeeeeffff', '3c4d5e6f-1111-2222-3333-444455556666', 10, 5, 1, 10, 20, 60)

-- Insert sample data into Forecasts table
INSERT INTO Forecasts (Id, ServiceId, CreatedAt, ModelId, Forecast)
VALUES ('f1a2b3c4-aaaa-bbbb-cccc-ddddeeeeffff', '1a2b3c4d-1111-2222-3333-444455556666', '2025-03-06 12:00:00',
        '1a2b3c4d-aaaa-bbbb-cccc-ddddeeeeffff',
        '{"columns": ["CPU"], "timestamp":["2025-03-28T13:30:00.00", "2025-03-28T13:31:00.00", "2025-03-28T13:32:00.00"], "value":[[0.38], [0.39], [0.40]]}'),
       ('f2b3c4d5-aaaa-bbbb-cccc-ddddeeeeffff', '2b3c4d5e-1111-2222-3333-444455556666', '2025-03-06 12:00:00',
        '2b3c4d5e-aaaa-bbbb-cccc-ddddeeeeffff',
        '{"columns": ["CPU"], "timestamp":["2025-03-28T13:30:00.00", "2025-03-28T13:31:00.00", "2025-03-28T13:32:00.00"], "value":[[0.50], [0.55], [0.60]]}'),
       ('f3c4d5e6-aaaa-bbbb-cccc-ddddeeeeffff', '3c4d5e6f-1111-2222-3333-444455556666', '2025-03-06 12:00:00',
        '3c4d5e6f-aaaa-bbbb-cccc-ddddeeeeffff',
        '{"columns": ["CPU"], "timestamp":["2025-03-28T13:30:00.00", "2025-03-28T13:31:00.00", "2025-03-28T13:32:00.00"], "value":[[0.50], [0.55], [0.60]]}');

COMMIT;
