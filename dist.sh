#!/usr/bin/env bash

## This source code is dual-licensed under the Apache License, version
## 2.0, and the Mozilla Public License, version 1.1.
##
## The APL v2.0:
##
##---------------------------------------------------------------------------
##   Copyright (C) 2007-2014 GoPivotal, Inc.
##
##   Licensed under the Apache License, Version 2.0 (the "License");
##   you may not use this file except in compliance with the License.
##   You may obtain a copy of the License at
##
##       http:##www.apache.org/licenses/LICENSE-2.0
##
##   Unless required by applicable law or agreed to in writing, software
##   distributed under the License is distributed on an "AS IS" BASIS,
##   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
##   See the License for the specific language governing permissions and
##   limitations under the License.
##---------------------------------------------------------------------------
##
## The MPL v1.1:
##
##---------------------------------------------------------------------------
##  The contents of this file are subject to the Mozilla Public License
##  Version 1.1 (the "License"); you may not use this file except in
##  compliance with the License. You may obtain a copy of the License
##  at http:##www.mozilla.org/MPL/
##
##  Software distributed under the License is distributed on an "AS IS"
##  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
##  the License for the specific language governing rights and
##  limitations under the License.
##
##  The Original Code is RabbitMQ.
##
##  The Initial Developer of the Original Code is GoPivotal, Inc.
##  Copyright (c) 2007-2014 GoPivotal, Inc.  All rights reserved.
##---------------------------------------------------------------------------

### Fail on any non-zero return
set -e
### Print command traces
set -x

### Disable sharing files by default (it causes things not to work properly)
CYGWIN=nontsec

### Change to this script's directory and copy the KEYFILE is any.
cd $(dirname "$0")
if [ -f "$KEYFILE" ]; then
    cp "$KEYFILE" .
    KEYFILE="$(basename "$KEYFILE")"
    trap "rm \"$KEYFILE\"" EXIT
fi

. dist-lib.sh

### Overrideable vars
test "$KEYFILE" || KEYFILE=rabbit-mock.snk
test "$RABBIT_VSN" || RABBIT_VSN=0.0.0.0
test "$WEB_URL" || WEB_URL=https://stage.rabbitmq.com/
test "$UNOFFICIAL_RELEASE" || UNOFFICIAL_RELEASE=
test "$MONO_DIST" || MONO_DIST=
test "$BUILD_WINRT" || BUILD_WINRT=false

### Other, general vars
NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN
RELEASE_DIR=release
if [ "$MONO_DIST" ] ; then
    BUILD=build.sh
    DOTNET_PROGRAM_PREPEND="mono"
else
    BUILD=build.bat
    DOTNET_PROGRAM_PREPEND=
fi

assembly-version $RABBIT_VSN

function main {
    ### Remove everything in the release dir and create the dir again
    safe-rm-deep-dir $RELEASE_DIR
    mkdir -p $RELEASE_DIR

    ### Check keyfile exists and generate if necessary, or exit with error if
    ### we're building an official release
    if [ ! -f "$KEYFILE" ]; then
        if [ "$UNOFFICIAL_RELEASE" ]; then
            sn -k "$KEYFILE"
        else
            echo "ERROR! Keyfile $KEYFILE not found."
            exit 1
        fi
    fi

    ### Remove everything in tmp dir and create it again
    safe-rm-deep-dir tmp
    mkdir -p tmp

    ### Generate dist zip files
    dist-zips

    ### Remove tmp
    safe-rm-deep-dir tmp

    echo "SUCCESS!"
}


function dist-zips {
    # clean & build
    dist-target-framework dotnet-4.5

    ### Source dist
    src-dist

    gendoc-dist \
        build/bin/RabbitMQ.Client.xml \
        $NAME_VSN-client-htmldoc.zip \
        "/suppress:RabbitMQ.Client.Framing \
         /suppress:RabbitMQ.Client.Framing.Impl \
         /suppress:RabbitMQ.Client.Impl \
         /suppress:RabbitMQ.Client.Apigen.Attributes" \
        $NAME_VSN-tmp-xmldoc.zip \
	projects/client/RabbitMQ.Client \
        ../../..
}


function cp-license-to {
    cp LICENSE $1
    cp LICENSE-APACHE2 $1
    cp LICENSE-MPL-RabbitMQ $1
}


function src-dist {
    ### Copy files to be zipped to tmp/srcdist/
    mkdir -p tmp/srcdist/docs/specs tmp/srcdist/lib
    cp RabbitMQDotNetClient.sln tmp/srcdist/
    cp -r projects tmp/srcdist/
    rm -f tmp/srcdist/projects/README
    cp -r docs/specs/*.xml tmp/srcdist/docs/specs/
    cp README.in tmp/srcdist/README
    if [ -n "$NO_LINKS" ]; then
        touch tmp/srcdist/README
    elif [ -f "$BUILD_DOC" ]; then
        cp "$BUILD_DOC" tmp/srcdist/README
    else
        links -dump ${WEB_URL}build-dotnet-client.html >> tmp/srcdist/README
    fi
    cp-license-to tmp/srcdist/

    ### Zip tmp/srcdist making $NAME_VSN the root dir in the archive
    mv tmp/srcdist tmp/$NAME_VSN
    mkdir tmp/srcdist
    mv tmp/$NAME_VSN tmp/srcdist/
    cd tmp/srcdist
    zip -q -r ../../$RELEASE_DIR/$NAME_VSN.zip . -x \*.snk \*.resharper \*.csproj.user
    cd ../..

    ### Remove tmp/srcdist
    rm -rf tmp/srcdist
}


function dist-target-framework {
    TARGET_FRAMEWORK="$1"

    mkdir -p tmp/dist/bin

    ### Clean
    #$MSBUILD /verbosity:quiet RabbitMQDotNetClient.sln /t:Clean /property:Configuration="Release"

    ### Build
    #$MSBUILD /verbosity:quiet RabbitMQDotNetClient.sln /t:Build /property:Configuration="Release"

    if [ "$MONO_DIST" ] ; then
	mono .paket/paket.bootstrapper.exe
	mono .paket/paket.exe restore
	mono ./packages/FAKE/tools/FAKE.exe build.fsx
    else
	.paket/paket.bootstrapper.exe
	.paket/paket.exe restore
	./packages/FAKE/tools/FAKE.exe build.fsx
    fi

    ### Copy bin files to be zipped to tmp/dist/
    cp projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.xml tmp/dist/bin/
    cp projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.dll tmp/dist/bin/
    cp-license-to tmp/dist/

    ### Zip tmp/dist
    cd tmp/dist
    zip -q -r ../../$RELEASE_DIR/$NAME_VSN-$TARGET_FRAMEWORK.zip .
    cd ../..

    ### Remove tmp/dist
    rm -rf tmp/dist

}

function gen-props {
    if [ "$MONO_DIST" ]; then
        USING_MONO="true"
    else
        USING_MONO="false"
    fi
    # once our build infra is on Windows 8.1,
    # we can enable WinRT builds
    sed -e "s:@VERSION@:$ASSEMBLY_VSN:g" -e "s:@KEYFILE@:$KEYFILE:g" -e "s:@USINGMONO@:$USING_MONO:g" -e "s:@BUILDWINRT@:$BUILD_WINRT:g" < $1 > $2

}

function gendoc-dist {
    XML_SOURCE_FILE="$1"
    ZIP_DESTINATION_FILENAME="$2"
    EXTRA_NDOCPROC_ARGS="$3"
    ### If this is an empty string, the intermediate xml output will not be saved in a zip
    ZIP_TMP_XML_DOC_FILENAME="$4"
    PROJECT_DIR="$5"
    RELATIVE_DIR="$6"

    mkdir -p tmp/gendoc/xml tmp/gendoc/html

    ### Make sure we can use ndocproc (it might be from a remote location)
    chmod +x lib/ndocproc-bin/bin/ndocproc.exe

    cd $PROJECT_DIR

    ### Generate XMLs with ndocproc
    $DOTNET_PROGRAM_PREPEND $RELATIVE_DIR/lib/ndocproc-bin/bin/ndocproc.exe \
    $EXTRA_NDOCPROC_ARGS \
    $RELATIVE_DIR/tmp/gendoc/xml \
    $XML_SOURCE_FILE \
    $RELATIVE_DIR/docs/namespaces.xml

    cd $RELATIVE_DIR

    ### Zip ndocproc's output
    if [ "$ZIP_TMP_XML_DOC_FILENAME" ]; then
        cd tmp/gendoc/xml
        zip -q -r ../../../$RELEASE_DIR/$ZIP_TMP_XML_DOC_FILENAME .
        cd ../../..
    fi

    ### Transform to html, using xsltproc
    genhtml index index
    genhtml namespace- namespace
    genhtml type- type

    ### Remove generated XMLs and copy remaining files to be added to the .zip
    rm -rf tmp/gendoc/xml
    cp  lib/ndocproc-bin/xsl/style.css tmp/gendoc/html/
    cp-license-to tmp/gendoc/

    ### Zip tmp/gendoc
    cd tmp/gendoc
    zip -q -r ../../$RELEASE_DIR/$ZIP_DESTINATION_FILENAME .
    cd ../..

    ### Remove tmp/gendoc
    rm -rf tmp/gendoc
}


function genhtml {
    for f in $(ls tmp/gendoc/xml/$1*.xml); do
        f_base_name="${f##*/}"
        echo "(xsltproc) Processing $f_base_name ..."
        xsltproc -o tmp/gendoc/html/${f_base_name%.xml}.html --param config tmp/gendoc/xml/config.xml lib/ndocproc-bin/xsl/$2.xsl $f
    done
}


main $@
