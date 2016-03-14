#!/bin/bash
#

set -ue

$( dirname $0 )/version-check.sh

devices=$(adb devices | sort | grep device\$ | cut -f1 | tr '\n' ' ')
for device in $devices
do
echo "- Device            : $device"
done

ANDROID_SERIAL=$( echo $devices | cut -d' ' -f1 ) adb logcat -s Unity
