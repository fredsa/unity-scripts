#!/bin/bash

set -ue

REFDIR=../unity-scripts

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
  echo "To review diffs:"
  for file in $diff_list
  do
    echo "  diff $file $REFDIR/$file"
  done

  echo
  echo "To push changes back to $REFDIR:"
  for file in $diff_list
  do
    dname=$(dirname $REFDIR/$file)
    [ ! -d $dname ] && echo "  mkdir -p $dname"
    echo "  cp $file $dname/"
  done

  echo
  echo "To fix the local repo:"
  for file in $diff_list
  do
    dname=$(dirname $file)
    [ ! -d $dname ] && echo "  mkdir -p $dname"
    echo " # cp $REFDIR/$file $dname/"
  done

  exit 1
fi
