# Jyazo Screenshot Client - Development Notes

## 🚨 CRITICAL: OAuth Port 52805 Must Be Available

Before testing OAuth uploads, kill any lingering Jyazo processes to free port 52805:

```bash
# Windows
taskkill /F /IM Jyazo.exe /T

# Or force all
wmic process where name="Jyazo.exe" delete
```

Then verify the port is free:
```bash
netstat -ano | findstr :52805
# Should return nothing if port is free
```

**Why this matters:** The server hardcodes the OAuth redirect URI to `http://127.0.0.1:52805/` for security. If the port is in use, OAuth will crash with `HttpListenerException`.

---

## Building

Build both Debug and Release configurations.

### Via MSBuild

```bash
# From project root
cd "D:\Coding - Unsynced\JyazoC#"

# Build Debug configuration (default)
"D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ScreenShot.sln

# Build Release configuration
"D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ScreenShot.sln -p:Configuration=Release
```

Executable outputs:
- Debug: `ScreenShot/bin/Debug/Jyazo.exe`
- Release: `ScreenShot/bin/Release/Jyazo.exe`

### In Visual Studio

Open `ScreenShot.sln`, select configuration (Debug/Release) from dropdown, and build (Ctrl+Shift+B).

## OAuth2 Authentication Port Details

The server is configured to redirect OAuth callbacks to `http://127.0.0.1:52805/`. This port is hardcoded in:
- Client: `ScreenShot/views/App.xaml.cs` line 349 (`const int OAUTH_CALLBACK_PORT = 52805`)
- Server: `ArkaPrime/arkapri.me/src/pages/api/auth/oauth-callback.ts` (redirect_uri parameter)

The server hardcodes this URI for security reasons. Changing this would require modifying both client and server code to coordinate port selection, which defeats the purpose of a fixed OAuth callback endpoint. The solution is proper process cleanup (see critical warning above), not port flexibility.

## Logging

Log files are stored in: `~/AppData/Roaming/Jyazo\`

**Separate log files by mode:**
- `logs-dev.txt` — Logs when dev mode is enabled (connects to `http://localhost:3000` or `DEV_SERVER` env var)
- `logs-release.txt` — Logs in normal mode (connects to production server from config)

This separation makes it easier to debug issues specific to each mode without mixing log output.
