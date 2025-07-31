# Run in development mode with hot reload
dotnet run Program.cs --environment Development

# Watch for file changes (when converted to project)
dotnet watch run

# Build for production
dotnet publish Program.cs --configuration Release




CONVERTING TO PROJECT

# Convert file-based app to traditional project
dotnet project convert Program.cs

# This creates:
# - PorcelAIn.csproj
# - Moves code to Program.cs
# - Converts all #: directives to MSBuild properties



TROUBLESHOOTING
# Clear NuGet cache if packages fail to restore
dotnet nuget locals all --clear

# Reset DNX packages (if using file-based apps)
rm -r $env:USERPROFILE\.dnx\packages

# Verify preview features are enabled
dotnet --version | Select-String "preview"

PERF MONITORING
# Install ANTS Performance Profiler (supports DNX/file-based apps)
# Or use built-in tools
dotnet-counters monitor --process-id [PID]
dotnet-trace collect --process-id [PID]