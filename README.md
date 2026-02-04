# AI Consumption Tracker

A streamlined Windows dashboard and tray utility to monitor AI API usage, costs, and quotas across multiple providers.

## Key Features

- **Multi-Provider Support**: Track usage for Anthropic, Gemini, OpenRouter, OpenCode, Kilo Code, and more.
- **Smart Discovery**: Automatically scans environment variables and application config files for existing API keys.
- **Minimalist Dashboard**: A compact, topmost window providing a quick overview of your current spend and token usage.
- **Dynamic Tray Integration**:
  - **Auto-Hide**: Dashboard hides automatically when focus is lost.
  - **Individual Tracking**: Option to spawn separate tray icons for specific providers.
  - **Live Progress Bars**: Tray icons feature "Core Temp" style bars that reflect usage levels in real-time.
- **Secure Management**: Manage all keys and preferences through a refined, dark-themed settings menu.

## Installation

### Manual
1. Download the latest `AIConsumptionTracker.zip` from releases.
2. Extract to any folder and run `AIConsumptionTracker.UI.exe`.

### Winget (Coming Soon)
`winget install Alexander.AIConsumptionTracker`

## Configuration

AI Consumption Tracker automatically discovers keys from:
- `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, and other standard environment variables.
- OpenCode, Kilo Code, and Zai local configuration files.
- Manual entry via the **Settings** menu.

## License
MIT

