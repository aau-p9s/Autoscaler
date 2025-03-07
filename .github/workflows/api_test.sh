TARGET=$1
TEST_DIR=$(mktemp -d)
CONTENT_TYPE="Content-Type: application/json"

set -e

echo test get services
echo "GET $TARGET/services"
curl -f -X "GET" $TARGET/services > $TEST_DIR/services.json
SERVICE_ID=$(jq -r ".[0].id" $TEST_DIR/services.json)

echo test get one service
echo "GET $TARGET/services/$SERVICE_ID"
curl -f -X "GET" $TARGET/services/$SERVICE_ID > $TEST_DIR/service.json

echo test get settings
echo "GET $TARGET/services/$SERVICE_ID/settings"
curl -f -X "GET" $TARGET/services/$SERVICE_ID/settings > $TEST_DIR/settings.json

echo test upsert service
echo "POST $TARGET/services/$SERVICE_ID"
curl -f -X "POST" $TARGET/services/$SERVICE_ID -d "$(jq '.name = "test"' $TEST_DIR/service.json)" --header "$CONTENT_TYPE"

echo test upsert settings
echo "POST $TARGET/services/$SERVICE_ID/settings"
curl -f -X "POST" $TARGET/services/$SERVICE_ID/settings -d "$(jq '.scaleUp = 0' $TEST_DIR/settings.json)" --header "$CONTENT_TYPE"

echo test get forecast
echo "GET $TARGET/services/$SERVICE_ID/forecast"
curl -f -X "GET" $TARGET/services/$SERVICE_ID/forecast > $TEST_DIR/forecast.json

echo test upsert forecast
echo "POST $TARGET/services/$SERVICE_ID/forecast"
curl -f -X "POST" $TARGET/services/$SERVICE_ID/forecast -d "$(jq ".hasManualChange = true" $TEST_DIR/forecast.json)" --header "$CONTENT_TYPE"