all: executable index.cgi

run: executable
	./pyle.cgi 127.0.0.1

runftp: executable
	./PyleFtp.py

index.cgi:
	ln -s pyle.cgi index.cgi

executable:
	chmod a+x pyle.cgi
	chmod a+x scripts/pyle2
	chmod a+x PyleFtp.py
	chmod a+x sublanguages/sequence-helper.sh

clean: cleancache
	rm -f $$(find . -name '*.pyc')

cleancache:
	rm -f pyledb_cache/*
