#!/usr/bin/env bash

(\
  ([ -f ./bin/Debug/tap ] && dotnet build -c Debug Package -v:q)\
  || (dotnet build -c Debug Tap.Upgrader -v:q && ./bin/Debug/tap sdk translate OpenTAP)\
) && clear && yes 0 | ./bin/Debug/tap sdk translate OpenTAP --test

