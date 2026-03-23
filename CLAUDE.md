# Jyazo Screenshot Client - Development Notes

## OAuth2 Authentication Port

**CRITICAL: Port 52805 must ALWAYS be used for OAuth2 callbacks**

The server is configured to redirect OAuth callbacks to `http://127.0.0.1:52805/`. This port is hardcoded in:
- Client: `ScreenShot/views/App.xaml.cs` line 349 (`const int OAUTH_CALLBACK_PORT = 52805`)
- Server: `ArkaPrime/arkapri.me/src/pages/api/auth/oauth-callback.ts` (redirect_uri parameter)

**Port Conflict Issue:**

If port 52805 is already in use, the OAuth flow will crash with:
```
System.Net.HttpListenerException: Failed to listen on prefix 'http://127.0.0.1:52805/'
because it conflicts with an existing registration on the machine.
```

**Solution:**

1. **Before testing uploads**, always kill any lingering Jyazo processes:
   ```bash
   # Windows
   taskkill /F /IM Jyazo.exe /T

   # Or force all
   wmic process where name="Jyazo.exe" delete
   ```

2. **Verify port is free:**
   ```bash
   netstat -ano | findstr :52805
   # Should return nothing if port is free
   ```

3. **Only then run the app and test uploads**

**Why not dynamic ports?**

The server hardcodes the redirect URI to `http://127.0.0.1:52805/` for security reasons. Changing this would require modifying both client and server code to coordinate port selection, which defeats the purpose of a fixed OAuth callback endpoint. The solution is proper process cleanup, not port flexibility.
