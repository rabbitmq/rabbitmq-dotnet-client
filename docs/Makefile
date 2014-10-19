BUILDDIR=$(CURDIR)/../build
DOCDIR=$(BUILDDIR)/doc
MANDIR=$(BUILDDIR)/doc/userguide
TMPDIR=$(BUILDDIR)/doc/tmp
PYLEDIR=pyle2-fcfcf7e
BASEURL=http://localhost:8080

PAGES=UserGuide ApiOverview MessagingPatterns ShutdownProtocols TroubleShooting Examples BuildingTheClient ImplementationGuide ApiGen

TARGET=$(MANDIR)/user-guide.pdf

DB2PDF_OPTIONS=\
	-V %generate-article-toc% \
	-V %generate-article-titlepage-on-separate-page%

all: prepare-pyle stop-pyle start-pyle
	OK=true && \
	{ make targets || OK=false; } && \
	cat pyle.log && \
	make stop-pyle && \
	$$OK

bundle:
	make all
	./api-guide.sh
	mkdir bundle
	cp $(TARGET) bundle/.
	cp ./api-guide.pdf bundle/.
	cp "RabbitMQ Service Model.pdf" bundle/.

targets: $(TARGET)

clean:
	rm -rf $(TMPDIR)
	rm -rf $(MANDIR)
	rm -rf bundle
	rm -f pyle.log

$(MANDIR)/%.pdf: %-skeleton.xml
	mkdir -p $(TMPDIR)
	mkdir -p $(MANDIR)
	(unset http_proxy; \
	 python extractdocbook.py \
		-s $< \
		-b "$(BASEURL)" \
		-o $(TMPDIR)/$*.xml \
		-t $(TMPDIR)/$*-temp \
		$(PAGES))
	(cd $(TMPDIR); db2pdf $(DB2PDF_OPTIONS) $*.xml)
	mv $(TMPDIR)/$*.pdf $(MANDIR)

prepare-pyle:
	cp pyle2-config.py $(PYLEDIR)/Config.py

start-pyle: stop-pyle
	( cd $(PYLEDIR); make run >../pyle.log 2>&1 & echo $$! > ../pyle.pid )
	sleep 5

stop-pyle:
	-[ -f pyle.pid ] && kill `cat pyle.pid`
	rm -f pyle.pid
