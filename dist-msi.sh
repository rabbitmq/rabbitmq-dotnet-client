#! /bin/bash

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

. dist-lib.sh

### Overrideable vars
test "$RABBIT_VSN" || RABBIT_VSN=0.0.0.0
test "$SKIP_MSIVAL2" || SKIP_MSIVAL2=

### Other, general vars
NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN
RELEASE_DIR=release

function main {
    ensure-tmp-unzip
    get-binaries
    get-sources
    gen-license-rtf

    build-msm-msi

    safe-rm-deep-dir tmp

    echo "SUCCESS!"
}


function build-msm-msi {
    safe-rm-deep-dir tmp/wix
    mkdir -p tmp/wix

    cd wix

    gen-wxs dotnet-client-merge-module.wxs
    candle -out ../tmp/wix/rabbitmq-dotnet-client-msm.wixobj dotnet-client-merge-module.wxs
    light -out ../tmp/wix/rabbitmq-dotnet-client.msm ../tmp/wix/rabbitmq-dotnet-client-msm.wixobj
    test "$SKIP_MSIVAL2" || MsiVal2.exe ../tmp/wix/rabbitmq-dotnet-client.msm ../lib/wix/mergemod.cub -f

    gen-wxs dotnet-client-product.wxs
    candle -out ../tmp/wix/rabbitmq-dotnet-client-msi.wixobj dotnet-client-product.wxs
    light -out ../tmp/wix/rabbitmq-dotnet-client.msi \
        ../tmp/wix/rabbitmq-dotnet-client-msi.wixobj \
        ../lib/wix/wixui.wixlib \
        -loc WixUI_en-us.wxl
    test "$SKIP_MSIVAL2" || MsiVal2.exe ../tmp/wix/rabbitmq-dotnet-client.msi ../lib/wix/darice.cub -f

    cd ..

    cp tmp/wix/rabbitmq-dotnet-client.msm $RELEASE_DIR/$NAME_VSN.msm
    cp tmp/wix/rabbitmq-dotnet-client.msi $RELEASE_DIR/$NAME_VSN.msi

    safe-rm-deep-dir tmp/wix
}

function ensure-tmp-unzip {
    safe-rm-deep-dir tmp/unzip
    mkdir -p tmp/unzip
}

function get-sources {
    unzip -q $RELEASE_DIR/$NAME_VSN-client-htmldoc.zip -d tmp/unzip/$NAME_VSN-client-htmldoc
    cp $RELEASE_DIR/$NAME_VSN-api-guide.pdf tmp/unzip/
    cp $RELEASE_DIR/$NAME_VSN-user-guide.pdf tmp/unzip/
}

function get-binaries {
    unzip -q $RELEASE_DIR/$NAME_VSN.zip -d tmp/unzip/$NAME_VSN
}

function gen-wxs {
    set +x
    f=$1
    local IFS=''
    sed -e "s:@VERSION@:$RABBIT_VSN:g" <${f}.in | while read -r l ; do
        if [ -z "$l" -o -n "${l##@FILES *}" ] ; then
            echo "$l"
        else
            # This is fairly hairy, due to the fact that .wxs file
            # lists are quite far from being simple lists of
            # filenames.  Wix comes with a tool (tallow) for creating
            # these lists, but it seems to be principally intended for
            # creating an initial .wxs file, which you then hand-edit.
            # So it's not a good fit for our needs here.  Instead, we
            # use find+awk to generate the File elements.
            echo "$l" | (
                IFS=' '
                read -r intro args
                # Windows has its own find command.  Make sure we get
                # the cygwin one.
                /bin/find $args -printf "%h %f\n" | awk '
{
    path = $1 "/" $2;
    gsub("/", "\\", path);

    id = $2;
    gsub("[^a-zA-Z0-9]", "", id);

    split($2, shorta, "\\.");
    short = substr(shorta[1], 0, 8);
    ext = substr(shorta[2], 0, 3);
    count = 0;
    while (short in shorts) {
        count++;
        short = substr(short, 0, 7-length(count)) "_" count;
    }
    shorts[short] = 1;
    short = toupper(short) "." toupper(ext);

    print "<File Id=\"" id "\" Name=\"" short "\" LongName=\"" $2 "\" Source=\"" path "\" Vital=\"yes\"/>"
}'
            )
        fi
    done >$f
    set -x
}


function gen-license-rtf {
    sed -e "s:""For the Apache License, please see the file LICENSE-APACHE2.""::" \
        -e "s:""For the Mozilla Public License, please see the file LICENSE-MPL-RabbitMQ.""::" \
        -e "s:$:\n\\\par:" \
        < LICENSE \
        > tmp/LICENSE.out

    sed -e "s:$:\n\\\par:" < LICENSE-APACHE2 > tmp/LICENSE-APACHE2.out
    sed -e "s:$:\n\\\par:" < LICENSE-MPL-RabbitMQ > tmp/LICENSE-MPL-RabbitMQ.out

    sed -e '\:@LICENSE@: {
            r tmp/LICENSE.out
            d
        }' \
        -e '\:@LICENSEAPACHE2@: {
            r tmp/LICENSE-APACHE2.out
            d
        }' \
        -e '\:@LICENSEMPL@: {
            r tmp/LICENSE-MPL-RabbitMQ.out
            d
        }' \
        < wix/License.rtf.in \
        > wix/License.rtf

    rm -f tmp/LICENSE.out tmp/LICENSE-APACHE2.out tmp/LICENSE-MPL_RabbitMQ.out
}


main $@
