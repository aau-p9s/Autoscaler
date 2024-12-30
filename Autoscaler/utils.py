import sys
import datetime
from random import randint
from json import loads, dumps

# get data from stdin, otherwise generate random data
def getData() -> dict[str, list[datetime.datetime] | list[int]]:
    if sys.stdin.isatty():
        now = datetime.datetime.now()
        return {
            "time": [
                now + datetime.timedelta(seconds=i) for i in range(100)
            ], 
            "value": [
                randint(0, 100) for _ in range(100)
            ]
        }
    else:
        return loads(sys.stdin.readline())

def format_prediction(prediction):
    return dumps({
        "time": loads(prediction.to_json())["index"],
        "amount": [item[0] for item in loads(prediction.to_json())["data"]]
    })
