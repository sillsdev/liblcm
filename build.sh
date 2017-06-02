#!/bin/bash

if [ -z "$1" ] ; then
    CONFIG=Debug
else
    CONFIG=$1
fi

if [ -z "$2" ] ; then
    TARGET=Build
else
    TARGET=$2
fi

. environ
xbuild /t:$TARGET /p:Configuration=$CONFIG /p:Platform=x86 LCM.sln