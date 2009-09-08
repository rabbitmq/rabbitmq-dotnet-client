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

### Fail on any non-zero return
set -e

### Disable sharing files by default (it causes things not to work properly)
CYGWIN=nontsec

### Overrideable vars
test "$RABBIT_VSN" || RABBIT_VSN=0.0.0

### Other, general vars
NAME=rabbitmq-dotnet-client
NAME_VSN=$NAME-$RABBIT_VSN
RELEASE_DIR=releases/$NAME/v$RABBIT_VSN


function main {
    get-sources
    gen-license-rtf
    gen-wxs

    build-msm-msi

    rm -f wix/dotnet-client-merge-module.wxs
    rm -f wix/dotnet-client-product.wxs
    rm -f wix/License.rtf

    safe-rm-deep-dir tmp
}


function build-msm-msi {
    safe-rm-deep-dir tmp/wix
    mkdir -p tmp/wix

    cd wix

    candle -out ../tmp/wix/rabbitmq-dotnet-client-msm.wixobj dotnet-client-merge-module.wxs
    light -out ../tmp/wix/rabbitmq-dotnet-client.msm ../tmp/wix/rabbitmq-dotnet-client-msm.wixobj
    MsiVal2.exe ../tmp/wix/rabbitmq-dotnet-client.msm ../lib/wix/mergemod.cub -f

    candle -out ../tmp/wix/rabbitmq-dotnet-client-msi.wixobj dotnet-client-product.wxs
    light -out ../tmp/wix/rabbitmq-dotnet-client.msi \
        ../tmp/wix/rabbitmq-dotnet-client-msi.wixobj \
        ../lib/wix/wixui.wixlib \
        -loc WixUI_en-us.wxl
    MsiVal2.exe ../tmp/wix/rabbitmq-dotnet-client.msi ../lib/wix/darice.cub -f

    cd ..

    cp tmp/wix/rabbitmq-dotnet-client.msm $RELEASE_DIR/$NAME_VSN.msm
    cp tmp/wix/rabbitmq-dotnet-client.msi $RELEASE_DIR/$NAME_VSN.msi

    safe-rm-deep-dir tmp/wix
}


function get-sources {
    safe-rm-deep-dir tmp/unzip
    mkdir -p tmp/unzip
    unzip $RELEASE_DIR/$NAME_VSN-dotnet-2.0.zip -d tmp/unzip/$NAME_VSN-dotnet-2.0
    unzip $RELEASE_DIR/$NAME_VSN-client-htmldoc.zip -d tmp/unzip/$NAME_VSN-client-htmldoc
    cp $RELEASE_DIR/$NAME_VSN-api-guide.pdf tmp/unzip/
    cp $RELEASE_DIR/$NAME_VSN-user-guide.pdf tmp/unzip/
}


function gen-wxs {
    sed -e "s:@VERSION@:$RABBIT_VSN:g" \
        < wix/dotnet-client-merge-module.wxs.in \
        > wix/dotnet-client-merge-module.wxs
      
    sed -e "s:@VERSION@:$RABBIT_VSN:g" \
        < wix/dotnet-client-product.wxs.in \
        > wix/dotnet-client-product.wxs
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


function safe-rm-deep-dir {
    ### Workaround for the path-too-long bug in cygwin
    if [ -e "$1" ]; then
        mv -f $1 /tmp/del
        rm -rf /tmp/del
    fi
}


main $@
