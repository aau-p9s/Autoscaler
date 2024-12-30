#!/usr/bin/env python3
import darts.models as models
import darts
import pandas as pd
import sys

from utils import getData, format_prediction

data = getData()
dataframe = pd.DataFrame(data)
dataframe['time'] = pd.to_datetime(dataframe['time'])  # Ensure 'time' column is datetime
ts = darts.TimeSeries.from_dataframe(dataframe, "time", "value", freq='min')

# model
model = models.StatsForecastAutoTheta(season_length=120)
model.fit(ts)
model.save("model/autotheta_model.pth")