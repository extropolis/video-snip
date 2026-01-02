# Contributing to Video Snip

Thank you for your interest in contributing to Video Snip! This document provides guidelines and information for contributors.

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Setting Up the Development Environment

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/yourusername/video-snip.git
   cd video-snip
   ```

2. Build the project:
   ```bash
   dotnet build -p:Platform=x64
   ```

3. Run the application:
   ```bash
   dotnet run --project VideoSnip -p:Platform=x64
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

## How to Contribute

### Reporting Bugs

- Check if the issue already exists in the [Issues](../../issues) page
- If not, create a new issue with:
  - Clear, descriptive title
  - Steps to reproduce the bug
  - Expected vs actual behavior
  - Windows version and .NET version
  - Screenshots if applicable

### Suggesting Features

- Open a new issue with the `enhancement` label
- Describe the feature and its use case
- Explain why it would benefit users

### Submitting Pull Requests

1. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following the code style guidelines below

3. Test your changes:
   ```bash
   dotnet test
   dotnet build -c Release -p:Platform=x64
   ```

4. Commit with a clear message:
   ```bash
   git commit -m "Add feature: description of your changes"
   ```

5. Push and create a Pull Request:
   ```bash
   git push origin feature/your-feature-name
   ```

6. Fill out the PR template with:
   - Description of changes
   - Related issue numbers
   - Testing performed

## Code Style Guidelines

### General

- Use C# 12 features where appropriate
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Keep methods focused and reasonably sized

### Formatting

- Use 4 spaces for indentation (not tabs)
- Use `var` when the type is obvious from the right side
- Place braces on their own lines for types and methods
- Use file-scoped namespaces

### Comments

- Add XML documentation for public APIs
- Use comments sparingly - prefer self-documenting code
- Keep comments up-to-date with code changes

### XAML

- Use consistent indentation (4 spaces)
- Group related properties together
- Use meaningful `x:Name` values

## Project Structure

```
VideoSnip/
├── Helpers/       # Win32 interop and utilities
├── Models/        # Data models and presets
├── Services/      # Core business logic
└── Views/         # XAML views and code-behind
```

### Key Classes

- `RecordingController` - Orchestrates the recording process
- `RegionSelector` - Handles region/window selection UI
- `RecordingBorderService` - Manages the recording border overlay
- `NativeMethods` - Win32 API declarations

## Testing

- Add unit tests for new functionality
- Ensure existing tests pass before submitting PR
- Test on both Windows 10 and Windows 11 if possible

## Questions?

Feel free to open an issue for any questions about contributing.

Thank you for helping make Video Snip better!
