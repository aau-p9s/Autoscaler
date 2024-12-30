#!/usr/bin/env python3
import darts.models as models
import sys

from utils import getData, format_prediction

model = models.StatsForecastAutoTheta.load("model/autotheta_model.pth")
prediction = model.predict(1440)

print(format_prediction(prediction))
