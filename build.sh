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

if [ -n "$3" ] ; then
	echo Usage: "build [(Debug|Release) [<target>]]"
	exit 1
fi

. environ
msbuild /t:$TARGET /p:Configuration=$CONFIG LCM.sln