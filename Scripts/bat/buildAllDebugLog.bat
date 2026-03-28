@echo off
cd ../../

call git submodule update --init --recursive
call dotnet build -c Debug /clp:ErrorsOnly > Scripts\logs\buildAllDebug.log

type Scripts\logs\buildAllDebug.log

pause