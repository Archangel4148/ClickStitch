# ClickStitch local build helpers. Run from the repository root.
#   make
#   make test
#   make clean
#   make run
#   make CONFIG=Debug test

CONFIG ?= Release
SOLUTION := ClickStitch.sln
SETTINGS := tests.runsettings
APP := src/ClickStitch.App/ClickStitch.App.csproj

.DEFAULT_GOAL := build
.PHONY: build test clean run help

help:
	@echo Targets: build (default), test, clean, run, help
	@echo Set CONFIG=Debug or CONFIG=Release. The default is Release.

build:
	dotnet build $(SOLUTION) -c $(CONFIG)

test:
	dotnet test $(SOLUTION) -c $(CONFIG) --settings $(SETTINGS)

clean:
	dotnet clean $(SOLUTION) -c Debug
	dotnet clean $(SOLUTION) -c Release
	powershell -NoProfile -Command "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue 'src/ClickStitch.App/bin','src/ClickStitch.App/obj','src/ClickStitch.Core/bin','src/ClickStitch.Core/obj','tests/ClickStitch.Core.Tests/bin','tests/ClickStitch.Core.Tests/obj'"

run:
	dotnet run --project $(APP) -c $(CONFIG)
