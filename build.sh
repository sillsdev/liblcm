#!/bin/bash

if [ -z "$1" ] ; then
    CONFIG=Debug
else
    CONFIG=$1
fi

. environ
xbuild /t:Rebuild /p:Configuration=$CONFIG /p:Platform=x86 LCM.sln