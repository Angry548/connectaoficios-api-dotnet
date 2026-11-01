#!/bin/sh
set -eu
dotnet /app/init/DatabaseInit.dll
exec dotnet /app/ConnectaOficios.Api.dll
