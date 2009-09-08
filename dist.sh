#! /bin/bash

## This source code is dual-licensed under the Apache License, version
## 2.0, and the Mozilla Public License, version 1.1.
##
## The APL v2.0:
##
##---------------------------------------------------------------------------
##   Copyright (C) 2007-2009 LShift Ltd., Cohesive Financial
##   Technologies LLC., and Rabbit Technologies Ltd.
##
##   Licensed under the Apache License, Version 2.0 (the "License");
##   you may not use this file except in compliance with the License.
##   You may obtain a copy of the License at
##
##       http://www.apache.org/licenses/LICENSE-2.0
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
##   The contents of this file are subject to the Mozilla Public License
##   Version 1.1 (the "License"); you may not use this file except in
##   compliance with the License. You may obtain a copy of the License at
##   http://www.rabbitmq.com/mpl.html
## 
##   Software distributed under the License is distributed on an "AS IS"
##   basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
##   License for the specific language governing rights and limitations
##   under the License.
##
##   The Original Code is The RabbitMQ .NET Client.
##
##   The Initial Developers of the Original Code are LShift Ltd,
##   Cohesive Financial Technologies LLC, and Rabbit Technologies Ltd.
##
##   Portions created before 22-Nov-2008 00:00:00 GMT by LShift Ltd,
##   Cohesive Financial Technologies LLC, or Rabbit Technologies Ltd
##   are Copyright (C) 2007-2008 LShift Ltd, Cohesive Financial
##   Technologies LLC, and Rabbit Technologies Ltd.
##
##   Portions created by LShift Ltd are Copyright (C) 2007-2009 LShift
##   Ltd. Portions created by Cohesive Financial Technologies LLC are
##   Copyright (C) 2007-2009 Cohesive Financial Technologies
##   LLC. Portions created by Rabbit Technologies Ltd are Copyright
##   (C) 2007-2009 Rabbit Technologies Ltd.
##
##   All Rights Reserved.
##
##   Contributor(s): ______________________________________.
##
##---------------------------------------------------------------------------


### Disable sharing files by default (it causes things not to work properly)
CYGWIN=nontsec

### Overrideable vars
test "$KEYFILE" || KEYFILE=rabbit-mock.snk
test "$RABBIT_VSN" || RABBIT_VSN=0.0.0
test "$OFFICIAL_RELEASE" || OFFICIAL_RELEASE=
test "$MSBUILD" || MSBUILD=msbuild.exe
test "$RABBIT_WEBSITE" || RABBIT_WEBSITE=http://www.rabbitmq.com

### Other, general vars
NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN
RELEASE_DIR=releases/$NAME/v$RABBIT_VSN


function main {
    ### Remove everything in the release dir and create the dir again
    safe-rm-deep-dir $RELEASE_DIR
    mkdir -p $RELEASE_DIR

    ### Check keyfile exists and generate if necessary, or exit with error if
    ### we're building an official release
    if [ ! -f "$KEYFILE" ]; then
        if [ ! "$OFFICIAL_RELEASE" ]; then
            sn -k $KEYFILE || exit $?
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
}


function dist-zips {
    ### Source dist
    src-dist

    ### .NET 2.0 library (bin) and examples (src and bin) dist
    dist-target-framework dotnet-2.0
    ### HTML documentation for the .NET 2.0 library dist
    gendoc-dist \
        projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.xml \
        $NAME_VSN-client-htmldoc.zip \
        "/suppress:RabbitMQ.Client.Framing.v0_8 \
         /suppress:RabbitMQ.Client.Framing.v0_8qpid \
         /suppress:RabbitMQ.Client.Framing.v0_9 \
         /suppress:RabbitMQ.Client.Framing.Impl.v0_8 \
         /suppress:RabbitMQ.Client.Framing.Impl.v0_8qpid \
         /suppress:RabbitMQ.Client.Framing.Impl.v0_9 \
         /suppress:RabbitMQ.Client.Impl \
         /suppress:RabbitMQ.Client.Apigen.Attributes" \
        $NAME_VSN-tmp-xmldoc.zip

    ### .NET 3.0 library (bin), examples (src and bin), WCF bindings library (bin)
    ### and WCF examples (src) dist
    dist-target-framework dotnet-3.0
    ### HTML documentation for the WCF bindings library dist
    gendoc-dist \
        projects/wcf/RabbitMQ.ServiceModel/build/bin/RabbitMQ.ServiceModel.xml \
        $NAME_VSN-wcf-htmldoc.zip \
        "" \
        ""
}


function cp-license-to {
    cp LICENSE $1
    cp LICENSE-APACHE2 $1
    cp LICENSE-MPL-RabbitMQ $1
}


function safe-rm-deep-dir {
    ### Workaround for the path-too-long bug in cygwin
    if [ -e $1 ]; then
        mv -f $1 /tmp/del
        rm -rf /tmp/del
    fi
}


function src-dist {
    ### Copy files to be zipped to tmp/srcdist/
    mkdir -p tmp/srcdist/docs/specs tmp/srcdist/lib
    cp RabbitMQDotNetClient.sln tmp/srcdist/
    cp -r projects tmp/srcdist/
    rm -f tmp/srcdist/projects/README
    cp -r docs/specs/*.xml tmp/srcdist/docs/specs/
    cp -r lib/MSBuild.Community.Tasks tmp/srcdist/lib/
    cp -r lib/nunit tmp/srcdist/lib/
    cp Local.props.example tmp/srcdist/
    cp README.in tmp/srcdist/README
    links -dump $RABBIT_WEBSITE/build-dotnet-client.html >> tmp/srcdist/README
    cp-license-to tmp/srcdist/

    ### Zip tmp/srcdist
    cd tmp/srcdist
    zip -r ../../$RELEASE_DIR/$NAME_VSN.zip . -x \*.snk \*.resharper \*.csproj.user
    cd ../..
    
    ### Remove tmp/srcdist
    rm -rf tmp/srcdist
}


function dist-target-framework {
    TARGET_FRAMEWORK="$1"
    BUILD_WCF=
    test "$TARGET_FRAMEWORK" == "dotnet-3.0" && BUILD_WCF="true"

    ### Make sure we can use MSBuild.Community.Tasks.dll (it might be from a
    ### remote location)
    chmod +x lib/MSBuild.Community.Tasks/MSBuild.Community.Tasks.dll

    ### Save current Local.props
    LOCAL_PROPS_EXISTS=
    test -f Local.props && LOCAL_PROPS_EXISTS="true"
    test "$LOCAL_PROPS_EXISTS" && mv ./Local.props ./Local.props.user

    ### Overwrite Local.props with settings specific to dist
    gen-props Dist-$TARGET_FRAMEWORK.props.in ./Local.props

    mkdir -p tmp/dist/bin tmp/dist/projects/examples

    ### Clean
    $MSBUILD RabbitMQDotNetClient.sln /t:Clean /property:Configuration="Release" || exit $?

    ### Copy examples code to be zipped to tmp/dist/
    cp -r projects/examples/client tmp/dist/projects/examples/
    test "$BUILD_WCF" && cp -r projects/examples/wcf tmp/dist/projects/examples/

    ### Build
    $MSBUILD RabbitMQDotNetClient.sln /t:Build /property:Configuration="Release" || exit $?
    
    ### Copy bin files to be zipped to tmp/dist/
    cp projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.dll tmp/dist/bin/
    cp projects/examples/client/AddClient/build/bin/AddClient.exe tmp/dist/bin/
    cp projects/examples/client/AddServer/build/bin/AddServer.exe tmp/dist/bin/
    cp projects/examples/client/DeclareQueue/build/bin/DeclareQueue.exe tmp/dist/bin/
    cp projects/examples/client/ExceptionTest/build/bin/ExceptionTest.exe tmp/dist/bin/
    cp projects/examples/client/LogTail/build/bin/LogTail.exe tmp/dist/bin/
    cp projects/examples/client/LowlevelLogTail/build/bin/LowlevelLogTail.exe tmp/dist/bin/
    cp projects/examples/client/SendMap/build/bin/SendMap.exe tmp/dist/bin/
    cp projects/examples/client/SendString/build/bin/SendString.exe tmp/dist/bin/
    cp projects/examples/client/SingleGet/build/bin/SingleGet.exe tmp/dist/bin/
    test "$BUILD_WCF" && cp projects/wcf/RabbitMQ.ServiceModel/build/bin/RabbitMQ.ServiceModel.dll tmp/dist/bin/
    cp-license-to tmp/dist/
    
    ### Zip tmp/dist
    cd tmp/dist
    zip -r ../../$RELEASE_DIR/$NAME_VSN-$TARGET_FRAMEWORK.zip .
    cd ../..

    ### Remove tmp/dist
    rm -rf tmp/dist
    
    ### Restore Local.props
    rm -f ./Local.props
    test "$LOCAL_PROPS_EXISTS" && mv ./Local.props.user ./Local.props
}

function gen-props {
    sed -e "s:@VERSION@:$RABBIT_VSN:g" \
        -e "s:@KEYFILE@:$KEYFILE:g" \
    < $1 > $2
}

function gendoc-dist {
    XML_SOURCE_FILE="$1"
    ZIP_DESTINATION_FILENAME="$2"
    EXTRA_NDOCPROC_ARGS="$3"
    ### If this is an empty string, the intermediate xml output will not be saved in a zip
    ZIP_TMP_XML_DOC_FILENAME="$4"

    mkdir -p tmp/gendoc/xml tmp/gendoc/html

    ### Generate XMLs with ndocproc    
    lib/ndocproc-bin/bin/ndocproc.exe \
    /nosubtypes \
    $EXTRA_NDOCPROC_ARGS \
    tmp/gendoc/xml \
    $XML_SOURCE_FILE \
    docs/namespaces.xml

    ### Zip ndocproc's output
    if [ $ZIP_TMP_XML_DOC_FILENAME ]; then
        cd tmp/gendoc/xml
        zip -r ../../../$RELEASE_DIR/$ZIP_TMP_XML_DOC_FILENAME .
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
    zip -r ../../$RELEASE_DIR/$ZIP_DESTINATION_FILENAME .
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
