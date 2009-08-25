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

### Run commands in local.dist
if [ ! -f local.dist ]; then
    echo "Could not find local.dist"
    exit 1
fi
while read line; do $line || exit $?; done < local.dist


### Some general vars
RABBIT_WEBSITE=http://www.rabbitmq.com

NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN

RELEASE_DIR=releases/$NAME/v$RABBIT_VSN

DEVENV=devenv.com



function main {
    ### Remove everything in the release dir and create the dir again
    safe-rm-deep-dir $RELEASE_DIR
    mkdir -p $RELEASE_DIR

    ### Remove tmp dir
    safe-rm-deep-dir tmp

    ### Get specified version snapshot from hg
    take-hg-snapshot

    ### Copy keyfile in the hg snapshot
    cp $KEYFILE tmp/hg-snapshot/projects/client/RabbitMQ.Client/

    ### Generate dist zip files
    cd tmp/hg-snapshot
    dist-zips
    cd ../../

    ### Remove tmp and containing hg snapshot
    safe-rm-deep-dir tmp
}


function dist-zips {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

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
         /suppress:RabbitMQ.Client.Apigen.Attributes"

    ### .NET 3.0 library (bin), examples (src and bin), WCF bindings library (bin)
    ### and WCF examples (src) dist
    dist-target-framework dotnet-3.0
    ### HTML documentation for the WCF bindings library dist
    gendoc-dist \
        projects/wcf/RabbitMQ.ServiceModel/build/bin/RabbitMQ.ServiceModel.xml \
        $NAME_VSN-wcf-htmldoc.zip \
        ""
}


function cp-license-to {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

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


function take-hg-snapshot {
    mkdir -p tmp
    hg clone -r $RABBIT_VSN $HG_REPO tmp/hg-snapshot || exit $?
}


function src-dist {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    ### Copy files to be zipped to tmp/srcdist/
    mkdir -p ../srcdist/docs/specs ../srcdist/lib
    cp RabbitMQDotNetClient.sln ../srcdist/
    cp -r projects ../srcdist/
    rm -f ../srcdist/projects/README
    cp -r docs/specs/*.xml ../srcdist/docs/specs/
    cp -r lib/MSBuild.Community.Tasks ../srcdist/lib/
    cp -r lib/nunit ../srcdist/lib/
    cp Local.props.example ../srcdist/
    cp README.in ../srcdist/README
    links -dump $RABBIT_WEBSITE/build-dotnet-client.html >> ../srcdist/README
    cp-license-to ../srcdist/

    ### Zip tmp/srcdist
    cd ../srcdist
    zip -r ../../$RELEASE_DIR/$NAME_VSN.zip . -x \*.snk \*.resharper \*.csproj.user
    cd ../hg-snapshot
    
    ### Remove tmp/srcdist
    rm -rf ../srcdist
}


function dist-target-framework {
    TARGET_FRAMEWORK="$1"
    BUILD_WCF=
    test "$TARGET_FRAMEWORK" == "dotnet-3.0" && BUILD_WCF="true"

    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    ### Copy Local.props specific to dist
    cp ../../Dist-$TARGET_FRAMEWORK.props ./Local.props

    mkdir -p ../dist/bin ../dist/projects/examples

    ### Clean
    $DEVENV RabbitMQDotNetClient.sln /Clean "Release|AnyCPU"

    ### Copy examples code to be zipped to tmp/dist/
    cp -r projects/examples/client ../dist/projects/examples/
    test "$BUILD_WCF" && cp -r projects/examples/wcf ../dist/projects/examples/

    ### Build
    $DEVENV RabbitMQDotNetClient.sln /Build "Release|AnyCPU"
    
    ### Copy bin files to be zipped to tmp/dist/
    cp projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.dll ../dist/bin/
    cp projects/examples/client/AddClient/build/bin/AddClient.exe ../dist/bin/
    cp projects/examples/client/AddServer/build/bin/AddServer.exe ../dist/bin/
    cp projects/examples/client/DeclareQueue/build/bin/DeclareQueue.exe ../dist/bin/
    cp projects/examples/client/ExceptionTest/build/bin/ExceptionTest.exe ../dist/bin/
    cp projects/examples/client/LogTail/build/bin/LogTail.exe ../dist/bin/
    cp projects/examples/client/LowlevelLogTail/build/bin/LowlevelLogTail.exe ../dist/bin/
    cp projects/examples/client/SendMap/build/bin/SendMap.exe ../dist/bin/
    cp projects/examples/client/SendString/build/bin/SendString.exe ../dist/bin/
    cp projects/examples/client/SingleGet/build/bin/SingleGet.exe ../dist/bin/
    test "$BUILD_WCF" && cp projects/wcf/RabbitMQ.ServiceModel/build/bin/RabbitMQ.ServiceModel.dll ../dist/bin/
    cp-license-to ../dist/
    
    ### Zip tmp/dist
    cd ../dist
    zip -r ../../$RELEASE_DIR/$NAME_VSN-$TARGET_FRAMEWORK.zip .
    cd ../hg-snapshot

    ### Remove tmp/dist
    rm -rf ../dist
}


function gendoc-dist {
    XML_SOURCE_FILE="$1"
    ZIP_DESTINATION_FILENAME="$2"
    EXTRA_NDOCPROC_ARGS="$3"

    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    mkdir -p ../gendoc/xml ../gendoc/html

    cat $XML_SOURCE_FILE

    ### Generate xml's with ndocproc    
    lib/ndocproc-bin/bin/ndocproc.exe \
    /nosubtypes \
    $EXTRA_NDOCPROC_ARGS \
    ../gendoc/xml \
    $XML_SOURCE_FILE \
    docs/namespaces.xml
    
    ### Transform to html, using xsltproc
    genhtml index index
    genhtml namespace- namespace
    genhtml type- type
    
    ### Remove generated xml's and copy remaining files to be added to the .zip
    rm -rf ../gendoc/xml
    cp  lib/ndocproc-bin/xsl/style.css ../gendoc/html/
    cp-license-to ../gendoc/
    
    ### Zip tmp/gendoc
    cd ../gendoc
    zip -r ../../$RELEASE_DIR/$ZIP_DESTINATION_FILENAME .
    cd ../hg-snapshot

    ### Remove tmp/gendoc
    rm -rf ../gendoc
}


function genhtml {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot
    
    for f in $(ls ../gendoc/xml/$1*.xml); do
        f_base_name="${f##*/}"
        echo "(xsltproc) Processing $f_base_name ..."
        xsltproc -o ../gendoc/html/${f_base_name%.xml}.html --param config ../gendoc/xml/config.xml lib/ndocproc-bin/xsl/$2.xsl $f
    done
}


function test-dir-tmp-hgsnapshot {
    last_dir="${PWD##/*/}"
    pwd_wo_last_dir="${PWD%/$last_dir}"
    prev_dir="${pwd_wo_last_dir##/*/}"
    if [ "$last_dir" != "hg-snapshot" ]; then
        fail="true"
    fi
    if [ "$prev_dir" != 'tmp' ]; then
        fail="true"
    fi
    if [ $fail ]; then
        echo "FAILED! Expected working dir tmp/hg-snapshot"
        exit 1
    fi
}


main $@
