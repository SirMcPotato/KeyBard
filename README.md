# KeyBard

KeyBard is a C# WPF application designed to play MIDI files and map MIDI events to keyboard key presses. It uses the NAudio library for MIDI input/output and features a visualizer for MIDI events.

## Features

- **MIDI Playback**: Load and play MIDI files using NAudio.
- **Visualizer**: See MIDI notes as they are played.
- **Channel Filtering**: Enable or disable specific MIDI channels.
- **Playback Control**: Play, pause, stop, restart, seek, and loop support.
- **Volume & Speed Control**: Adjust playback volume and tempo.
- **Profile Support**: Save and load configurations for different songs or setups.
- **Midi-to-Keys**: Map MIDI events to keyboard actions (based on project name and source code references).

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later.

## Getting Started

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/SirMcPotato/KeyBard.git
   cd KeyBard
   ```

2. Restore dependencies and build the project:
   ```bash
   scripts\build.ps1 build
   ```

### Usage

1. Run the application:
   ```bash
   scripts\build.ps1 run
   ```
2. Click **Browse** to load a MIDI file.
3. Use the playback controls to start/stop the MIDI.
4. (Optional) Configure channel filters or key bindings as needed.

## Building and Releasing

To create a standalone release build:
```bash
scripts\build.ps1 publish
```

## Testing

This project uses xUnit for unit testing.

- Run all tests:
  ```bash
  dotnet test KeyBard.Tests/KeyBard.Tests.csproj
  ```
- Or via the helper script:
  ```bash
  scripts\build.ps1 test
  ```

### TDD workflow (example)
1. Write a failing test in `KeyBard.Tests` (e.g., for `KeyBindingsStore`).
2. Run the tests (`scripts\build.ps1 test`) and observe the failure.
3. Implement the minimal code change in `KeyBard` to make the test pass.
4. Re-run the tests and refactor as needed.

Notes:
- The main WPF project excludes `KeyBard.Tests` from its compilation items to avoid design-time build issues.
- Tests target `net8.0-windows` to match the app's target.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) for MIDI processing.
