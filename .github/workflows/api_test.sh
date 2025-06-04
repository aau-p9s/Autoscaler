#!/usr/bin/env bash

TARGET=$1
TEST_DIR=$(mktemp -d)
CONTENT_TYPE="Content-Type: application/json"

set -e

error() {
    printf "\033[38;2;255;0;0m%s\033[0m\n" "$@"
    return 1
}

pass() {
    printf "\033[38;2;0;255;0m%s\033[0m\n" "$@"
    return 0
}

assert() {
    echo "$1" | jq -C
    if ! [[ "$(echo $1 | jq)" == "$(echo $2 | jq)" ]]; then
        error "assertion failed"
    fi
    pass "assertion passed"
}

sleep 200

echo test get services
echo "GET $TARGET/services"
curl -f -X "GET" $TARGET/services > $TEST_DIR/services.json 2>/dev/null
assert "$(cat $TEST_DIR/services.json)" '[{"id":"1a2b3c4d-1111-2222-3333-444455556666","name":"Image Recognition","autoscalingEnabled":true},{"id":"2b3c4d5e-1111-2222-3333-444455556666","name":"Speech-to-Text","autoscalingEnabled":true},{"id":"3c4d5e6f-1111-2222-3333-444455556666","name":"Recommendation System","autoscalingEnabled":true}]'
SERVICE_ID=$(jq -r ".[0].id" $TEST_DIR/services.json)

echo test get one service
echo "GET $TARGET/services/$SERVICE_ID"
curl -f -X "GET" $TARGET/services/$SERVICE_ID > $TEST_DIR/service.json 2>/dev/null
assert "$(cat $TEST_DIR/service.json)" '{"id":"1a2b3c4d-1111-2222-3333-444455556666","name":"Image Recognition","autoscalingEnabled":true}'

echo test get settings
echo "GET $TARGET/services/$SERVICE_ID/settings"
curl -f -X "GET" $TARGET/services/$SERVICE_ID/settings > $TEST_DIR/settings.json 2>/dev/null
assert "$(cat $TEST_DIR/settings.json)" '{
                                           "id": "a1b2c3d4-aaaa-bbbb-cccc-ddddeeeeffff",
                                           "serviceId": "1a2b3c4d-1111-2222-3333-444455556666",
                                           "scaleUp": 5,
                                           "scaleDown": 2,
                                           "minReplicas": 1,
                                           "maxReplicas": 10,
                                           "scalePeriod": 10,
                                           "trainInterval": 30
                                         }'

echo test upsert service
echo "POST $TARGET/services/$SERVICE_ID"
curl -f -X "POST" $TARGET/services/$SERVICE_ID -d "$(jq '.name = "test"' $TEST_DIR/service.json)" --header "$CONTENT_TYPE" > $TEST_DIR/service_upsert.json 2>/dev/null
assert "$(cat $TEST_DIR/service_upsert.json)" "true"

echo test upsert settings
echo "POST $TARGET/services/$SERVICE_ID/settings"
curl -f -X "POST" $TARGET/services/$SERVICE_ID/settings -d "$(jq '.scaleUp = 0' $TEST_DIR/settings.json)" --header "$CONTENT_TYPE" > $TEST_DIR/settings_upsert.json 2>/dev/null
assert "$(cat $TEST_DIR/settings_upsert.json)" "true"

echo test get forecast
echo "GET $TARGET/services/$SERVICE_ID/forecast"
curl -f -X "GET" $TARGET/services/$SERVICE_ID/forecast > $TEST_DIR/forecast.json 2>/dev/null
assert "$(cat $TEST_DIR/forecast.json)" '{
                                          "id": "f1a2b3c4-aaaa-bbbb-cccc-ddddeeeeffff",
                                          "serviceId": "1a2b3c4d-1111-2222-3333-444455556666",
                                          "createdAt": "2025-03-06T12:00:00",
                                          "modelId": "1a2b3c4d-aaaa-bbbb-cccc-ddddeeeeffff",
                                          "forecast": "{\"value\": [[0.38], [0.39], [0.40]], \"columns\": [\"CPU\"], \"timestamp\": [\"2025-03-28T13:30:00.00\", \"2025-03-28T13:31:00.00\", \"2025-03-28T13:32:00.00\"]}",
                                          "hasManualChange": false
                                        }'

echo test upsert forecast
echo "POST $TARGET/services/$SERVICE_ID/forecast"
curl -f -X "POST" $TARGET/services/$SERVICE_ID/forecast -d "$(jq ".hasManualChange = true" $TEST_DIR/forecast.json)" --header "$CONTENT_TYPE" > $TEST_DIR/forecast_upsert.json 2>/dev/null
assert "$(cat $TEST_DIR/forecast_upsert.json)" "true"
