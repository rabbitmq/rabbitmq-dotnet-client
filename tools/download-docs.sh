#! /bin/bash

OUTPUT_DIR=../releases/rabbitmq-dotnet-client/v1.6.0

mkdir -p $OUTPUT_DIR
(cd $OUTPUT_DIR; wget http://www.rabbitmq.com/releases/rabbitmq-dotnet-client/v1.6.0/rabbitmq-dotnet-client-1.6.0-user-guide.pdf)
(cd $OUTPUT_DIR; wget http://www.rabbitmq.com/releases/rabbitmq-dotnet-client/v1.6.0/rabbitmq-dotnet-client-1.6.0-api-guide.pdf)
