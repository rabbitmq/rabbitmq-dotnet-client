NAME=rabbitmq-dotnet-client
NAME_VSN=${NAME}-${RABBIT_VSN}

RELEASE_DIR=release
GENSRC_DIR=gensrc

TMPXMLZIP=${NAME_VSN}-tmp-xmldoc.zip

ifeq "$(RABBIT_VSN)" ""
rabbit-vsn:
	@echo "RABBIT_VSN is not set"
	@false
else
rabbit-vsn:
endif

dist: rabbit-vsn ensure-deliverables ensure-universally-readable
	rm -f $(RELEASE_DIR)/$(TMPXMLZIP)

test-xbuild-units:
	xbuild /nologo /t:RunUnitTests projects/client/Unit/RabbitMQ.Client.Unit.csproj | grep -v "warning CS2002"

test-xbuild: test-xbuild-units

retest-xbuild: clean test-xbuild

ensure-universally-readable:
	chmod -R a+rX release

ensure-deliverables: rabbit-vsn
	file ${RELEASE_DIR}/${NAME_VSN}.zip
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc
	file ${RELEASE_DIR}/${NAME_VSN}-dotnet-4.0.zip
	file ${RELEASE_DIR}/${NAME_VSN}-wcf-htmldoc.zip
	file ${RELEASE_DIR}/${NAME_VSN}-wcf-htmldoc
	file ${RELEASE_DIR}/${NAME_VSN}.msm

ensure-release-dir: rabbit-vsn
	touch ${RELEASE_DIR}/

ensure-docs: rabbit-vsn
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip
	file ${RELEASE_DIR}/${TMPXMLZIP}

doc: rabbit-vsn ensure-release-dir ensure-docs
	rm -rf build/tmpdoc build/doc
	mkdir -p build/tmpdoc/html build/tmpdoc/xml
	unzip -q -j ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip -d build/tmpdoc/html
	unzip -q -j ${RELEASE_DIR}/${NAME_VSN}-tmp-xmldoc.zip -d build/tmpdoc/xml
	cd ${RELEASE_DIR} && \
	  rm -rf ${NAME_VSN}-htmldoc && \
	  unzip -q ${NAME_VSN}-client-htmldoc.zip -d ${NAME_VSN}-client-htmldoc && \
	  rm -rf ${NAME_VSN}-wcf-htmldoc && \
	  unzip -q ${NAME_VSN}-wcf-htmldoc.zip -d ${NAME_VSN}-wcf-htmldoc

clean:
	rm -rf $(GENSRC_DIR) $(RELEASE_DIR)/*
