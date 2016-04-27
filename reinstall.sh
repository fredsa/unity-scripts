#!/bin/bash

set -ue

$( dirname $0 )/version-check.sh

ANDROID_SERIAL_FILE=android-serial.txt
MANIFEST_PATH=Assets/Plugins/Android/AndroidManifest.xml
PROJECT_SETTINGS=ProjectSettings/ProjectSettings.asset

activity=com.unity3d.player.UnityPlayerActivity
root=$( dirname $0 )

# Determine Android package name
if [ -n "$( grep bundleIdentifier $PROJECT_SETTINGS | egrep 'Binary file .* matches' )" ]
then
  echo "ERROR: $PROJECT_SETTINGS is binary" 1>&2
  exit 1
fi

pkg=$( grep bundleIdentifier $PROJECT_SETTINGS | sed 's/ //g' | cut -d: -f2 )

devices=$(adb devices | sort | grep device\$ | cut -f1 | tr '\n' ' ')

if [ ! -r "$ANDROID_SERIAL_FILE" ]
then
  echo "ERROR: Missing $ANDROID_SERIAL_FILE" 1>&2
  exit 1
fi

if [ -z "$devices" ]
then
  echo "ERROR: no adb devices" 1>&2
  exit 1
fi

# Determine APK filename
apk="$pkg.apk"
if [ ! -r "$pkg.apk" ]
then
  apk=$( ls -1 "$root"/*.apk )
  apk_count=$(( $( echo "$apk" | wc -l ) ))
  if [ $apk_count -ne 1 ]
  then
    echo "ERROR: Found $apk_count APKs but expecting exactly 1" 1>&2
    ls -l $apk 1>&2
    exit 1
  fi
fi

# Determine overriding Android activity name from AndroidManifest.xml
if [ -f "$MANIFEST_PATH" ]
then
  echo "Extracting activity name from $MANIFEST_PATH"
  activity=$( grep '<activity android:name="' "$MANIFEST_PATH" | cut -d '"' -f 2 )
fi

echo "Using:"
echo "- Package identifier: $pkg"
echo "- APK filename      : $apk"
echo "- Android Activity  : $activity"
for device in $devices
do
echo "- Device            : $device"
done

uninstall_pkg()
{
  echo
  echo "$ANDROID_SERIAL Uninstalling $*"
  adb shell pm uninstall $* || true
}

install_pkg()
{
  echo "$ANDROID_SERIAL adb install $*"
  output=$( adb install $* 2>&1 | grep -v 'KB/s' | grep -v 'pkg:' | grep -v 'Success' )
  [ -z "$output" ]
}

# Begin actual un/re-install and launch
pids=""
device_num=0
for serial in $devices
do
  export ANDROID_SERIAL=$serial
  device_num=$(( $device_num + 1))
  (
    # Send ESC to keep alive after idle
    adb shell input keyevent 111
    adb shell input keyevent 111

    # Home screen launcher
    adb shell am start -a android.intent.action.MAIN -c android.intent.category.HOME >/dev/null

    # Kill app if running in background
    adb shell am force-stop $pkg

    #user_count=$(( $( adb shell pm list users | grep UserInfo | wc -l ) ))
    #user=$( adb shell pm list users | grep UserInfo | awk "NR == $device_num % ($user_count + 1)" | sed -E 's/.*UserInfo.([0-9]+).*/\1/' )
    adb_args=$( cat $ANDROID_SERIAL_FILE | grep $ANDROID_SERIAL | cut -d' ' -f2- )

    if [ "${1:-}" != "-n" ]
    then
      android_version="$( adb shell getprop ro.build.version.release | cut -d. -f1 )"
      if [ $android_version == "N" ]
      then
        grant_flag="-g"
      elif [ $android_version -ge 6 ]
      then
        grant_flag="-g"
      else
        grant_flag=""
      fi
      # adb install
      #   -l: forward lock application
      #   -r: replace existing application
      #   -t: allow test packages
      #   -s: install application on sdcard
      #   -d: allow version code downgrade
      #   -g: grant all runtime permissions
      install_pkg -r -d $grant_flag $adb_args $apk ||
      (
        echo " and reinstalling on $serial"
        uninstall_pkg $pkg \
         && install_pkg -r -d $grant_flag $adb_args $apk
      )
    fi

    echo "$ANDROID_SERIAL Launching $pkg/$activity"
    adb shell am start $adb_args -n $pkg/$activity | ( grep -v 'Starting: Intent' || true )
  ) &
  pid=$!
  echo "$ANDROID_SERIAL PID $pid"
  pids="$pids $pid"
done

echo "Waiting for PIDS $pids …"
for pid in $pids
do
  wait $pid || echo "ERROR: PID $pid failed!" 1>&2
done

echo "DONE"
