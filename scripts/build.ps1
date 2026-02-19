param (
    [string]$Action = "build",
    [string]$Configuration = "Release"
)

$ProjectFile = "KeyBard.csproj"

switch ($Action) {
    "restore" {
        Write-Host "Restoring dependencies..." -ForegroundColor Cyan
        dotnet restore $ProjectFile
    }
    "build" {
        Write-Host "Building project ($Configuration)..." -ForegroundColor Cyan
        dotnet build $ProjectFile -c $Configuration
    }
    "clean" {
        Write-Host "Cleaning project..." -ForegroundColor Cyan
        dotnet clean $ProjectFile
        Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue
    }
    "publish" {
        Write-Host "Publishing project ($Configuration)..." -ForegroundColor Cyan
        $PublishPath = "publish"
        dotnet publish $ProjectFile -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishPath
        Write-Host "Published to $PublishPath" -ForegroundColor Green
    }
    "run" {
        Write-Host "Running project..." -ForegroundColor Cyan
        dotnet run --project $ProjectFile
    }
    "test" {
        Write-Host "Running tests..." -ForegroundColor Cyan
        dotnet test "KeyBard.Tests/KeyBard.Tests.csproj" -c $Configuration
    }
    default {
        Write-Host "Unknown action: $Action" -ForegroundColor Red
        Write-Host "Usage: .\build.ps1 -Action [restore|build|clean|publish|run|test] [-Configuration Release|Debug]"
    }
}
