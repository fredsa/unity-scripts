#!/bin/bash

set -ue

$( dirname $0 )/version-check.sh


MANIFEST_PATH=Assets/Plugins/Android/AndroidManifest.xml

activity=com.unity3d.player.UnityPlayerActivity
root=$( dirname $0 )

# Determine APK name
if [ $# -ge 1 ]
then
  apk=$1
else
  # Look for APKs
  apk=$( ls -1 "$root"/*.apk )
  apk_count=$(( $( echo "$apk" | wc -l ) ))
  if [ $apk_count -ne 1 ]
  then
    echo "ERROR: Found $apk_count APKs but expecting exactly 1" 1>&2
    ls -l $apk 1>&2
    exit 1
  fi
fi

# Determine Android package name
if [ $# -ge 2 ]
then
  pkg=$2
else
  pkg=$( basename $apk | sed -e 's/.apk$//' )
fi

# Determine overriding Android activity name from AndroidManifest.xml
if [ -f "$MANIFEST_PATH" ]
then
  echo
  echo "Extracting activity name from $MANIFEST_PATH …"
  activity=$( grep '<activity android:name="' "$MANIFEST_PATH" | cut -d '"' -f 2 )
fi


# Actually start

echo
echo "Checking device is attached …"
# -d  Use only connected USB device; fail if more than one device
adb -d devices

# See http://docs.unity3d.com/Manual/Profiler.html
echo
echo "Setting up Unity debug tunnel for package $pkg …"
adb forward tcp:54999 localabstract:Unity-$pkg

echo
echo "Launching $pkg/$activity …"
adb shell am start -n $pkg/$activity
