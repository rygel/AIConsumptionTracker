# Codex API Debug Script

A PowerShell script to fetch and inspect the Codex (OpenAI) usage API response for debugging parsing issues.

## Usage

```powershell
.\scripts\debug-codex.ps1
```

## Requirements

- PowerShell 5.1+
- Access to `~/.codex/auth.json` (Codex CLI authentication)

## Output

The script saves the raw API response to `test-fixtures/codex-api-responses.json` and prints the JSON to stdout.

## Example Output

```json
{
  "user_id": "user-xxx",
  "account_id": "user-xxx",
  "email": "user@example.com",
  "plan_type": "plus",
  "rate_limit": {
    "allowed": true,
    "limit_reached": false,
    "primary_window": {
      "used_percent": 0,
      "limit_window_seconds": 18000,
      "reset_after_seconds": 18000,
      "reset_at": 1772654666
    },
    "secondary_window": {
      "used_percent": 19,
      "limit_window_seconds": 604800,
      "reset_after_seconds": 580741,
      "reset3217407
_at": 177    }
  },
  "code_review_rate_limit": {
    "allowed": true,
    "limit_reached": false,
    "primary_window": {...},
    "secondary_window": null
  },
  "additional_rate_limits": null,
  "credits": {...}
}
```

## Troubleshooting

### "No access token found"
- Run `codex auth login` first to authenticate

### Authentication errors
- Check `~/.codex/auth.json` exists and contains valid tokens
