#!/bin/bash

if `echo "$*" | grep -q Silo`; then
    echo "Spawning Orleans Silo..."
    xfce4-terminal -e 'bash -c "~/.dotnet/dotnet run --project ../Silo; bash"' -T "Silo"
fi