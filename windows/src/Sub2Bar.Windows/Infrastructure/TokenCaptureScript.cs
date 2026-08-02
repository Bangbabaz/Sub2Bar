namespace Sub2Bar.Windows.Infrastructure;

public static class TokenCaptureScript
{
    public const string Value = """
        (() => {
          if (window !== window.top || window.__sub2barTokenCaptureInstalled) return;
          window.__sub2barTokenCaptureInstalled = true;

          const sendCredentials = (accessToken, refreshToken = '') => {
            if (typeof accessToken !== 'string' || accessToken.trim().length === 0) return;
            try {
              window.chrome.webview.postMessage({
                access_token: accessToken,
                refresh_token: typeof refreshToken === 'string' ? refreshToken : ''
              });
            } catch (_) {}
          };

          const inspectPayload = (payload) => {
            try {
              const value = typeof payload === 'string' ? JSON.parse(payload) : payload;
              if (value?.code !== 0 && value?.code !== '0') return;
              const accessToken = value?.data?.access_token ?? value?.access_token ??
                value?.data?.token ?? value?.token;
              const refreshToken = value?.data?.refresh_token ?? value?.refresh_token ?? '';
              sendCredentials(accessToken, refreshToken);
            } catch (_) {}
          };

          const isLoginRequest = (value) => {
            try {
              const path = new URL(String(value), location.href).pathname;
              return path === '/api/v1/auth/login' || path.endsWith('/api/v1/auth/login');
            } catch (_) {
              return String(value).includes('/api/v1/auth/login');
            }
          };

          try {
            const originalFetch = window.fetch;
            window.fetch = async function(...args) {
              const response = await originalFetch.apply(this, args);
              const requestUrl = args[0]?.url ?? args[0] ?? '';
              if (isLoginRequest(requestUrl)) {
                response.clone().json().then(inspectPayload).catch(() => {});
              }
              return response;
            };
          } catch (_) {}

          try {
            const originalOpen = XMLHttpRequest.prototype.open;
            const originalSend = XMLHttpRequest.prototype.send;
            XMLHttpRequest.prototype.open = function(method, url, ...rest) {
              this.__sub2barRequestUrl = String(url);
              return originalOpen.call(this, method, url, ...rest);
            };
            XMLHttpRequest.prototype.send = function(...args) {
              if (isLoginRequest(this.__sub2barRequestUrl ?? '')) {
                this.addEventListener('load', () => inspectPayload(this.responseText), { once: true });
              }
              return originalSend.apply(this, args);
            };
          } catch (_) {}

          try {
            const originalSetItem = Storage.prototype.setItem;
            Storage.prototype.setItem = function(key, value) {
              const result = originalSetItem.call(this, key, value);
              if (key === 'auth_token') {
                sendCredentials(String(value), localStorage.getItem('refresh_token') ?? '');
              } else if (key === 'refresh_token') {
                sendCredentials(localStorage.getItem('auth_token') ?? '', String(value));
              }
              return result;
            };
          } catch (_) {}
        })();
        """;
}
