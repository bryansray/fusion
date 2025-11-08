set shell := ["/bin/bash", "-cu"]

# Build all projects
build:
	dotnet build src/fusion.sln

# Run analyzers/formatting
lint:
	dotnet format src/fusion.sln

# Run every test project, skipping integration category
test:
	dotnet test src/fusion.sln --filter "TestCategory!=Integration"

# Run full test suite (including integration)
test-full:
	dotnet test src/fusion.sln

# Run only the bot tests
bot-tests:
	dotnet test src/Fusion.Bot.Tests/Fusion.Bot.Tests.csproj

# Run only the persistence tests
persistence-tests:
	dotnet test src/Fusion.Persistence.Tests/Fusion.Persistence.Tests.csproj

# Run the Discord runner
run:
	dotnet run --project src/fusion.runner/fusion.runner.csproj
