#!/bin/sh

RELEASE_DIR=releases/rabbitmq-dotnet/v${RABBIT_VSN}

rm -rf build/tmpdoc
mkdir -p build/tmpdoc/html build/tmpdoc/xml
unzip -j ${RELEASE_DIR}/rabbitmq-dotnet-${RABBIT_VSN}-net-2.0-htmldoc.zip -d build/tmpdoc/html
unzip -j ${RELEASE_DIR}/rabbitmq-dotnet-${RABBIT_VSN}-tmp-xmldoc.zip -d build/tmpdoc/xml
cd docs/
./api-guide.sh
mv api-guide.pdf ../${RELEASE_DIR}/rabbitmq-dotnet-${RABBIT_VSN}-api-guide.pdf
make
mv ../build/doc/userguide/user-guide.pdf ../${RELEASE_DIR}/rabbitmq-dotnet-${RABBIT_VSN}-user-guide.pdf
rm -rf ../build/doc/userguide
cp "RabbitMQ Service Model.pdf" ../${RELEASE_DIR}/rabbitmq-dotnet-${RABBIT_VSN}-wcf-service-model.pdf
cd ../${RELEASE_DIR}
unzip rabbitmq-dotnet-${RABBIT_VSN}-net-2.0-htmldoc.zip
unzip rabbitmq-dotnet-${RABBIT_VSN}-net-3.0-wcf-htmldoc.zip
cd ../../..

