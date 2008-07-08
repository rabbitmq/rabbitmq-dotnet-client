#!/bin/sh

rm -rf build/tmpdoc
mkdir -p build/tmpdoc/html build/tmpdoc/xml
unzip -j ${RELEASE_DIR}/${NAME_VSN}-net-2.0-htmldoc.zip -d build/tmpdoc/html
unzip -j ${RELEASE_DIR}/${NAME_VSN}-tmp-xmldoc.zip -d build/tmpdoc/xml
cd docs/
./api-guide.sh
mv api-guide.pdf ../${RELEASE_DIR}/${NAME_VSN}-api-guide.pdf
make
mv ../build/doc/userguide/user-guide.pdf ../${RELEASE_DIR}/${NAME_VSN}-user-guide.pdf
rm -rf ../build/doc/userguide
cp "RabbitMQ Service Model.pdf" ../${RELEASE_DIR}/${NAME_VSN}-wcf-service-model.pdf
cd ../${RELEASE_DIR}
unzip ${NAME_VSN}-net-2.0-htmldoc.zip
unzip ${NAME_VSN}-net-3.0-wcf-htmldoc.zip
cd ../../..

