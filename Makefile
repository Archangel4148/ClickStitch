# ClickStitch local build helpers. Run from the repository root.
#   make
#   make test
#   make bench
#   make manual
#   make clean
#   make run
#   make CONFIG=Debug test

CONFIG ?= Release
SOLUTION := ClickStitch.sln
SETTINGS := tests.runsettings
APP := src/ClickStitch.App/ClickStitch.App.csproj

.DEFAULT_GOAL := build
.PHONY: build test bench manual clean run help

help:
	@echo Targets: build (default), test, bench, manual, clean, run, help
	@echo Set CONFIG=Debug or CONFIG=Release. The default is Release.
	@echo bench submits real pointer moves of 0,0 and is not part of test. About 4 minutes.
	@echo manual clicks and scrolls only inside its own window, then restores the pointer.

build:
	dotnet build $(SOLUTION) -c $(CONFIG)

test:
	dotnet test $(SOLUTION) -c $(CONFIG) --settings $(SETTINGS) --filter "Category!=Manual&Category!=Performance"

bench:
	dotnet run --project tests/ClickStitch.Bench/ClickStitch.Bench.csproj -c $(CONFIG) --nologo -v q

manual:
	powershell -NoProfile -Command "$$env:CLICKSTITCH_MANUAL_INPUT='1'; dotnet test tests/ClickStitch.Input.Tests/ClickStitch.Input.Tests.csproj -c $(CONFIG) --filter Category=Manual --nologo"

clean:
	dotnet clean $(SOLUTION) -c Debug
	dotnet clean $(SOLUTION) -c Release
	powershell -NoProfile -Command "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue 'src/ClickStitch.App/bin','src/ClickStitch.App/obj','src/ClickStitch.Core/bin','src/ClickStitch.Core/obj','src/ClickStitch.Input/bin','src/ClickStitch.Input/obj','tests/ClickStitch.Core.Tests/bin','tests/ClickStitch.Core.Tests/obj','tests/ClickStitch.Input.Tests/bin','tests/ClickStitch.Input.Tests/obj','tests/ClickStitch.Performance.Tests/bin','tests/ClickStitch.Performance.Tests/obj','tests/ClickStitch.Bench/bin','tests/ClickStitch.Bench/obj'"

run:
	dotnet run --project $(APP) -c $(CONFIG)
