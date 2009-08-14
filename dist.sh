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


while read line; do $line; done < local.dist

RABBIT_WEBSITE=http://www.rabbitmq.com

NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN

RELEASE_DIR=releases/$NAME/v$RABBIT_VSN

DEVENV=devenv.com



function main {
    ### Remove everything in the release dir and create the dir again
    rm -v -rf $RELEASE_DIR
    mkdir -p $RELEASE_DIR

    ### Get specified version snapshot from hg
    take-hg-snapshot

    ### Copy keyfile in the hg snapshot
    cp -v $KEYFILE tmp/hg-snapshot/projects/client/RabbitMQ.Client/

    ### Generate dist zip files
    cd tmp/hg-snapshot
    src-dist
    dist-target-framework dotnet-2.0
    dist-target-framework dotnet-3.0
    gendoc-dist
    cd ../../

    ### Remove tmp and containing hg snapshot
    rm -v -rf tmp   
}


function test-dir-tmp-hgsnapshot {
    LAST_DIR="${PWD##/*/}"
    PWD_WO_LAST_DIR="${PWD%/$LAST_DIR}"
    PREV_DIR="${PWD_WO_LAST_DIR##/*/}"
    if [ "$LAST_DIR" != "hg-snapshot" ]; then
        FAIL="true"
    fi
    if [ "$PREV_DIR" != 'tmp' ]; then
        FAIL="true"
    fi
    if [ $FAIL ]; then
        echo "FAILED! Expected to be in tmp/hg-snapshot"
        exit 1
    fi
}


function cp-license-to {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    cp -v LICENSE $1
    cp -v LICENSE-APACHE2 $1
    cp -v LICENSE-MPL-RabbitMQ $1
}


function take-hg-snapshot {
    mkdir -p tmp
    hg clone hg.rabbitmq.com/rabbitmq-dotnet-client -r $RABBIT_VSN tmp/hg-snapshot
}


function src-dist {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    ### Copy files to be zipped to tmp/srcdist/
    mkdir -p ../srcdist/docs/specs ../srcdist/lib
    cp -v RabbitMQDotNetClient.sln ../srcdist/
    cp -v -r projects ../srcdist/
    rm -v -f ../srcdist/projects/README
    cp -v -r docs/specs/*.xml ../srcdist/docs/specs/
    cp -v -r lib/MSBuild.Community.Tasks ../srcdist/lib/
    cp -v -r lib/nunit ../srcdist/lib/
    cp -v Local.props.example ../srcdist/
    cp -v README.in ../srcdist/README
    links -dump $RABBIT_WEBSITE/build-dotnet-client.html >> ../srcdist/README
    cp-license-to ../srcdist/

    ### Zip tmp/srcdist
    cd ../srcdist
    zip -r ../../$RELEASE_DIR/$NAME_VSN.zip . -x \*.snk \*.resharper \*.csproj.user
    cd ../hg-snapshot
    
    ### Remove tmp/srcdist
    rm -v -rf ../srcdist
}


function dist-target-framework {
    TARGET_FRAMEWORK="$1"

    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    ### Copy Local.props specific to dist
    cp -v ../../Dist-$TARGET_FRAMEWORK.props ./Local.props

    ### Clean build
    $DEVENV RabbitMQDotNetClient.sln /Clean "Release|AnyCPU"
    $DEVENV RabbitMQDotNetClient.sln /Build "Release|AnyCPU"
    
    ### Copy files to be zipped to tmp/dist/
    mkdir -p ../dist/dll ../dist/projects/examples
    cp -v projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.dll ../dist/dll/
    cp -v -r projects/examples/client ../dist/projects/examples/
    rm -v -rf ../dist/projects/examples/client/AddClient/obj
    rm -v -rf ../dist/projects/examples/client/AddServer/obj
    rm -v -rf ../dist/projects/examples/client/DeclareQueue/obj
    rm -v -rf ../dist/projects/examples/client/ExceptionTest/obj
    rm -v -rf ../dist/projects/examples/client/LogTail/obj
    rm -v -rf ../dist/projects/examples/client/LowlevelLogTail/obj
    rm -v -rf ../dist/projects/examples/client/SendMap/obj
    rm -v -rf ../dist/projects/examples/client/SendString/obj
    rm -v -rf ../dist/projects/examples/client/SingleGet/obj
    cp-license-to ../dist/
    
    ### Zip tmp/dist
    cd ../dist
    zip -r ../../$RELEASE_DIR/$NAME_VSN-$TARGET_FRAMEWORK.zip .
    cd ../hg-snapshot

    ### Remove tmp/dist
    rm -v -rf ../dist
}


function gendoc-dist {
    ### Assuming we're in tmp/hg-snapshot/
    test-dir-tmp-hgsnapshot

    mkdir -p ../gendoc/xml ../gendoc/html

    ### Generate xml's with ndocproc    
    lib/ndocproc-bin/bin/ndocproc.exe \
    /nosubtypes \
    /suppress:RabbitMQ.Client.Framing.v0_8 \
    /suppress:RabbitMQ.Client.Framing.v0_8qpid \
    /suppress:RabbitMQ.Client.Framing.v0_9 \
    /suppress:RabbitMQ.Client.Framing.Impl.v0_8 \
    /suppress:RabbitMQ.Client.Framing.Impl.v0_8qpid \
    /suppress:RabbitMQ.Client.Framing.Impl.v0_9 \
    /suppress:RabbitMQ.Client.Impl \
    /suppress:RabbitMQ.Client.Apigen.Attributes \
    ../gendoc/xml \
    projects/client/RabbitMQ.Client/build/bin/RabbitMQ.Client.xml \
    docs/namespaces.xml
    
    ### Transform to html, using xsltproc
    genhtml index index
    genhtml namespace- namespace
    genhtml type- type
    
    ### Remove generated xml's and copy remaining files to be added to the .zip
    rm -v -rf ../gendoc/xml
    cp -v lib/ndocproc-bin/xsl/style.css ../gendoc/html/
    cp-license-to ../gendoc/
    
    ### Zip tmp/gendoc
    cd ../gendoc
    zip -r ../../$RELEASE_DIR/$NAME_VSN-$TARGET_FRAMEWORK-htmldoc.zip .
    cd ../hg-snapshot

    ### Remove tmp/gendoc
    rm -v -rf ../gendoc
}


function genhtml {
    for f in $(ls ../gendoc/xml/$1*.xml); do
        f_base_name="${f##*/}"
        echo "(xsltproc) Processing $f_base_name ..."
        xsltproc -o ../gendoc/html/${f_base_name%.xml}.html --param config ../gendoc/xml/config.xml lib/ndocproc-bin/xsl/$2.xsl $f
    done
}


main $@
exit $?
