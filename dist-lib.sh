#! /bin/bash

## This source code is dual-licensed under the Apache License, version
## 2.0, and the Mozilla Public License, version 1.1.
##
## The APL v2.0:
##
##---------------------------------------------------------------------------
##   Copyright (C) 2007-2012 VMware, Inc.
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
##  The Initial Developer of the Original Code is VMware, Inc.
##  Copyright (c) 2007-2012 VMware, Inc.  All rights reserved.
##---------------------------------------------------------------------------

function assembly-version {
    local RELEASE_PATTERN="^[0-9]+(\.[0-9]+){2}$"
    local NIGHTLY_PATTERN="^[0-9]+(\.[0-9]+){3}$"
    if [[ $1 =~ $RELEASE_PATTERN ]] ; then
        ASSEMBLY_VSN=$RABBIT_VSN.0
    elif [[ $1 =~ $NIGHTLY_PATTERN ]] ; then
        ASSEMBLY_VSN=$RABBIT_VSN
    else
        echo "Error: invalid version pattern: '$1'" >&2
        exit 1
    fi
}

function safe-rm-deep-dir {
    ### Workaround for the path-too-long bug in cygwin
    if [ -e "$1" ]; then
        mv -f $1 /tmp/del
        rm -rf /tmp/del
    fi
}

