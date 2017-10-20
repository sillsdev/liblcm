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

if [ -z "$3" ] ; then
	FILESAVAILABLE=False
else
	FILESAVAILABLE=$3
fi

. environ
xbuild /t:$TARGET /p:Configuration=$CONFIG /p:Platform=x86 /p:UseLocalFiles=$FILESAVAILABLE LCM.sln