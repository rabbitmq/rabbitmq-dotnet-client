NAME=rabbitmq-dotnet-client
NAME_VSN=${NAME}-${RABBIT_VSN}

RELEASE_DIR=release

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

ensure-universally-readable:
	chmod -R a+rX release

ensure-deliverables: rabbit-vsn
	file ${RELEASE_DIR}/${NAME_VSN}.zip
	file ${RELEASE_DIR}/${NAME_VSN}-api-guide.pdf
	file ${RELEASE_DIR}/${NAME_VSN}-user-guide.pdf
	file ${RELEASE_DIR}/${NAME_VSN}-wcf-service-model.pdf
	file ${RELEASE_DIR}/${NAME_VSN}-dotnet-2.0.zip
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc
	file ${RELEASE_DIR}/${NAME_VSN}-dotnet-3.0.zip
	file ${RELEASE_DIR}/${NAME_VSN}-wcf-htmldoc.zip
	file ${RELEASE_DIR}/${NAME_VSN}-wcf-htmldoc
	file ${RELEASE_DIR}/${NAME_VSN}.msi
	file ${RELEASE_DIR}/${NAME_VSN}.msm

ensure-prerequisites: rabbit-vsn
	[ -f "/etc/debian_version" ] && dpkg -L htmldoc plotutils transfig graphviz docbook-utils xmlstarlet || true > /dev/null

ensure-release-dir: rabbit-vsn
	touch ${RELEASE_DIR}/

ensure-docs: rabbit-vsn
	file ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip
	file ${RELEASE_DIR}/${TMPXMLZIP}

doc: rabbit-vsn ensure-prerequisites ensure-release-dir ensure-docs
	rm -rf build/tmpdoc build/doc
	mkdir -p build/tmpdoc/html build/tmpdoc/xml
	unzip -q -j ${RELEASE_DIR}/${NAME_VSN}-client-htmldoc.zip -d build/tmpdoc/html
	unzip -q -j ${RELEASE_DIR}/${NAME_VSN}-tmp-xmldoc.zip -d build/tmpdoc/xml
	cd docs && ./api-guide.sh && \
	  mv api-guide.pdf ../${RELEASE_DIR}/${NAME_VSN}-api-guide.pdf
	$(MAKE) -C docs
	mv build/doc/userguide/user-guide.pdf ${RELEASE_DIR}/${NAME_VSN}-user-guide.pdf
	cp docs/"RabbitMQ Service Model.pdf" \
	  ${RELEASE_DIR}/${NAME_VSN}-wcf-service-model.pdf
	cd ${RELEASE_DIR} && \
	  rm -rf ${NAME_VSN}-htmldoc && \
	  unzip -q ${NAME_VSN}-client-htmldoc.zip -d ${NAME_VSN}-client-htmldoc && \
	  rm -rf ${NAME_VSN}-wcf-htmldoc && \
	  unzip -q ${NAME_VSN}-wcf-htmldoc.zip -d ${NAME_VSN}-wcf-htmldoc

clean:
	rm -rf $(RELEASE_DIR)/*
