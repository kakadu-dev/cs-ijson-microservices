# NET CORE Inverted JSON Microservices

![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/kakadu-dev/cs-ijson-microservices)

 - ~~Gateway entrypoint~~ (in-progress)
 - Microservice worker

Installation
------------

The preferred way to install this extension is through [composer](http://getcomposer.org/download/).

Either run

```
composer require --prefer-dist kakadu-dev/php-ijson-microservices "*"
```

or add

```
"kakadu-dev/php-ijson-microservices": "@dev"
```

to the require section of your `composer.json` file.

Usage
-----

Example microservice:
```php
use Kakadu\Microservices\Microservice;

$app = Microservice::create('my-microservice', [
    'ijson' => 'http://127.0.0.1:8001',
    'env'   => 'dev',
], true);

$app->start(function ($method, $params) {
    // Run method with params
    // Return result

    return ['hello' => 'world'];
});
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
