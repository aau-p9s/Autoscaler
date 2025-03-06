# autoscaler-frontend
## docker compose
to run docker compose, run `docker compose -f Autoscaler.Persistence/docker-compose.yml` up -d

then the database will not be populated, so populate it using dbup:
```sh
dotnet run --project Autoscaler.DbUp
```
then open your browser at `http://localhost:8080`

## environment variables
| VARIABLE                     | DEFAULT           |
|------------------------------|-------------------|
| AUTOSCALER__HOST             | http://0.0.0.0    |
| AUTOSCALER__PORT             | 8080              |
| AUTOSCALER__APIS__KUBERNETES | http://kubernetes |
| AUTOSCALER__APIS__PROMETHEUS | http://prometheus |
| AUTOSCALER__APIS__FORECASTER | http://forecaster |
| AUTOSCALER__PGSQL__ADDR      | pgsql             |
| AUTOSCALER__PGSQL__PORT      | 5432              |
| AUTOSCALER__PGSQL__DATABASE  | autoscaler        |
| AUTOSCALER__PGSQL__PASSWORD  | password          |