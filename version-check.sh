#!/bin/bash

set -ue

REFDIR=/fred/src/unity-scripts

diff_list=""
for file in $( cd $REFDIR; git ls-files )
do
  diff=$( diff -q "$file" "$REFDIR/$file" ) || diff_list="$diff_list $file"
done


if [ -n "$diff_list" ]
then
  for file in $diff_list
  do
    echo
    echo "ERROR:"
    diff -q "$file" "$REFDIR/$file" || true
    diff "$file" "$REFDIR/$file" || true
  done
  echo
  echo "To fix this review:"
  for file in $diff_list
  do
    echo "  diff $file $REFDIR/$file"
  done
  exit 1
fi
