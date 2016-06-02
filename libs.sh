#!/bin/bash
#

set -ue

$( dirname $0 )/version-check.sh

dir="Assets/Plugins"

commits=$(
  git ls-files $dir | while read file
  do
    echo "$( git log --diff-filter=A --format=%H -- "$file" )"
  done \
  | sort -u
)

echo "Unique commits:"
for commit in $commits
do
  echo $commit
done

for commit in $commits
do
  echo
  echo
  git --no-pager show --name-only --format=short $commit -- $dir
done
