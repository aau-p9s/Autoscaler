TARGET=$1

set -e

# test get services
curl -X "GET" $TARGET/services

# test 
