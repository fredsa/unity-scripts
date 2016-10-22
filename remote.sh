#!/bin/bash

set -ue

adb forward tcp:7201 tcp:7201
adb forward --list
