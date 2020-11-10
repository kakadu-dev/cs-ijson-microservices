# NET CORE Inverted JSON Microservices

![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/kakadu-dev/cs-ijson-microservices)

 - ~~Gateway entrypoint~~ (in-progress)
 - Microservice worker

Installation
------------

Either run

```
dotnet add package cs-ijson-microservices -s https://nuget.pkg.github.com/kakadu-dev/index.json
```

or

```
dotnet restore
```

Usage
-----

Example microservice:
```c#
using cs_ijson_microservice;
using static cs_ijson_microservice.Helpers;

static void Main(string[] args)
{
    //create options for microservices
    Options options = new Options("1.0.0", APP_ENV, IJSON_HOST, 1000 * 60 * 5);
    // create microservices
    Microservice.getInstance.create(
                string.Format("{0}:{1}", PROJECT_ALIAS, SERVICE_NAME), options);

    // configure microservices
    MicroserviceConfig microserviceConfig = Configure();
    
    // configure database
    DatabaseContext database = new Program().CreateDbContext(
                new string[] {microserviceConfig.mysql.getStringConnection});

    // database check migration
    database.Database.Migrate();

    // create handler
    handler = new Handler(database);

    //add worker to microservices
    Microservice.getInstance.worker = handler.Worker;

    // start microservices
    Microservice.getInstance.start();
}

```

Start Inverted JSON:
```
version: '3.7'

services:
  ijson:
    image: lega911/ijson
    container_name: base-ijson
    ports:
      - 8001:8001
```

Send POST request directly to: http://localhost:8001
```bash
curl http://127.0.0.1:8001/my-microservice -d '{"id": 1, "params":{"test":1}}'
```

**If you run [gateway](https://github.com/kakadu-dev/nodejs-ijson-microservices).** Run POST request to: http://localhost:3000
```json
{
  "id": 1,
  "method": "my-service.test-method",
  "params": {
    "test": 1
  }
}
```

That's all. Check it.
