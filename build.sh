#!/bin/bash

cd "$(dirname "$0")"
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

if [ -z "$3" ] ; then
	FILESAVAILABLE=False
else
	FILESAVAILABLE=$3
fi

. environ
msbuild /t:$TARGET /p:Configuration=$CONFIG /p:UseLocalFiles=$FILESAVAILABLE LCM.sln