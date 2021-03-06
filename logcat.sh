#!/bin/bash
#

set -ue

devices=$(adb devices | sort | grep device\$ | cut -f1 | tr '\n' ' ')
for device in $devices
do
echo "- Device            : $device"
done

ANDROID_SERIAL=$( echo $devices | cut -d' ' -f1 )

while [ $# -gt 0 ]
do
  if [ "$1" == "-c" ]
  then
    adb logcat -c
  fi
  shift
done

adb logcat -s Unity
