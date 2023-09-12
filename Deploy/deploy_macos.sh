#!/bin/bash

echo "Script name: $0"

help_="--help"
param1="$1"

if [ "$param1" = "$help_" ]; then
    echo "It is expected that the script runs in the project's root folder."
    exit 1
fi

var1=1
current_dir=$(pwd)
#echo $x

osascript -e 'tell app "Terminal"
    do script "cd '$current_dir' && dotnet run --project Silo"
end tell'