# Security response headers (CSP and related headers)

This document records the HTTP security response headers added to 1CibiPlatform in August 2026, why they were placed at the YARP API Gateway, and the constraints that shaped each header value. It follows a security scan that flagged six missing or incomplete headers: `X-Frame-Options`, `X-Content-Type-Options`, `Strict-Transport-Security`, `Content-Security-Policy`, `Referrer-Policy`, and `Permissions-Policy`.

## Where the headers live and why

All headers are set in one place: the inline security-headers middleware in `ApiGateways/YarpApiGateway/Program.cs` (the block that previously set only the Content-Security-Policy).

- The gateway is the **only public entry point**. Browsers reach `oneplatform.cibi.com.ph` → YarpApiGateway (Kestrel on :443 with the production PFX), which reverse-proxies to the internal backend (`apis:8080`) and to nginx serving the Blazor WebAssembly frontend (`frontendwebassembly:8080`). Setting headers at the gateway therefore covers every response leaving the platform — frontend static files, API responses, and gateway-local endpoints such as `/__routes`.
- The middleware is registered after `app.UseRouting()` and before `app.MapReverseProxy()`, so proxied responses receive the headers too.
- **`wwwroot` and `web.config` are not usable levers here.** The deployment is Linux/Docker (nginx + Kestrel), not IIS; there is no `web.config` in the repository, and `wwwroot` contains only static application files.
- **nginx.conf was deliberately not changed.** It is reachable only on the internal Docker network, and nginx's `add_header` directive is not inherited into `location` blocks that declare their own `add_header`. Every location block in `UI/FrontendWebassembly/nginx.conf` already declares a `Cache-Control` header, so server-level security headers there would be silently dropped on most responses unless all of them were duplicated with `always` in every block — high-maintenance duplication for near-zero benefit behind the gateway.
- The backend pipeline (`BackendAPI/API/APIs/ServiceConfig/AppConfiguration.cs`) was also left unchanged for the same reason: it is internal-only and covered by the gateway.

## Before and after

| Header | Before | After |
| --- | --- | --- |
| `Content-Security-Policy` | Present, but without a `frame-ancestors` directive. | Same policy with `frame-ancestors 'self';` appended. No other directive was changed. |
| `X-Frame-Options` | Missing. | `SAMEORIGIN` |
| `X-Content-Type-Options` | Missing. | `nosniff` |
| `Referrer-Policy` | Missing. | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | Missing. | `camera=(self "https://liveness.everify.gov.ph"), microphone=(), geolocation=(), payment=()` |
| `Strict-Transport-Security` | Missing. | `max-age=31536000; includeSubDomains` — sent only over HTTPS and only outside the Development environment. |

## The CSP change in detail

The only change to the existing Content-Security-Policy was the addition of one directive at the end of the policy string:

```
frame-ancestors 'self';
```

`frame-ancestors` controls which origins may embed the site in an `<iframe>`, `<frame>`, `<embed>`, or `<object>`. It is the modern, CSP-based clickjacking protection and supersedes `X-Frame-Options` in browsers that support both (when both are present, browsers honor the CSP directive). `'self'` was chosen over `'none'` because it costs nothing and cannot break a future same-origin embed (for example, `blob:` PDF previews inherit the parent origin), while still blocking every cross-origin framing attempt.

Nothing else in the CSP was touched. The pre-existing allowances remain as they were, including:

- `'wasm-unsafe-eval'` in `script-src` — mandatory for Blazor WebAssembly;
- `'unsafe-inline'` in `script-src` and `style-src` — required by the inline bootstrap script and importmap in `wwwroot/index.html`, the inline styles across the Razor components, and MudBlazor's runtime style injection;
- the Google Fonts, go-mpulse, S3 bucket, and `liveness.everify.gov.ph` origins.

Hardening those allowances (removing `unsafe-inline`, tightening the `connect-src https:` wildcard, and so on) was intentionally left out of scope; the scan finding was about missing headers, not about the strength of the existing CSP.

## Rationale for each new header

### `X-Frame-Options: SAMEORIGIN`

Legacy-browser companion to `frame-ancestors 'self'`. A repository-wide search found nothing that frames the application (no `<iframe>`, `<embed>`, or `<object>` of the app's own pages), so same-origin framing is not currently used — but `SAMEORIGIN` is equivalent to `DENY` for clickjacking protection while remaining safe if a same-origin embed is ever added. The value must stay consistent with the CSP `frame-ancestors` directive.

### `X-Content-Type-Options: nosniff`

Instructs browsers not to MIME-sniff responses away from their declared `Content-Type`. Safe to enable because nginx already serves correct MIME types for the frontend assets, including `application/wasm` for the Blazor runtime (WebAssembly streaming compilation requires the correct type, so a wrong type would already have been visible).

### `Referrer-Policy: strict-origin-when-cross-origin`

Makes the modern browser default explicit, which is what scanners check for. Same-origin navigation keeps the full referrer (analytics unaffected); cross-origin requests receive only the origin, never the path or query string, so tokens, user IDs, and internal endpoint paths in URLs cannot leak to third-party domains; nothing is sent on an HTTPS→HTTP downgrade.

### `Permissions-Policy: camera=(self "https://liveness.everify.gov.ph"), microphone=(), geolocation=(), payment=()`

**The `camera` allowlist entry for `https://liveness.everify.gov.ph` is load-bearing.** The PhilSys eKYC face-liveness SDK (`UI/FrontendWebassembly/wwwroot/js/philsys/everify-liveness-sdk.min.js`) creates a full-screen overlay iframe with `allow="camera"` pointing at that origin. The iframe `allow` attribute can only *delegate* a feature that the top-level document's `Permissions-Policy` grants, so:

- `camera=(self "https://liveness.everify.gov.ph")` — the page may use the camera and may delegate it to the everify iframe. **Changing this to `camera=(self)` or `camera=()` silently breaks the PhilSys face-liveness flow** — the iframe simply gets no camera prompt, with no obvious error.
- `microphone=()`, `geolocation=()`, `payment=()` — disabled outright; a repository search found no first-party or third-party use of these features.
- Features not listed (fullscreen, clipboard, and so on) keep their safe browser defaults; listing them is unnecessary and would add maintenance burden.

If a future feature needs another browser capability (for example microphone recording), add the corresponding directive here rather than removing the header.

### `Strict-Transport-Security: max-age=31536000; includeSubDomains`

Tells browsers to refuse plain-HTTP connections to the domain for one year, defeating SSL-stripping downgrade attacks. Two deliberate constraints on when it is sent:

- **Only over HTTPS** — RFC 6797 requires HSTS to be ignored on HTTP responses, and sending it there is meaningless.
- **Never in the Development environment** — otherwise a developer's browser would cache an HSTS entry for `localhost`, breaking plain-HTTP access to every other local project on that machine until the entry is manually cleared.

`preload` was deliberately **not** added. Submitting to the Chrome HSTS preload list is a semi-permanent commitment covering the entire registrable domain (`cibi.com.ph`) and all its subdomains; it should be a separate, explicit business decision, not a side effect of a scan remediation.

## Verification

Local (HTTP, Development — HSTS correctly absent):

```powershell
dotnet run --project ApiGateways/YarpApiGateway --launch-profile http
curl.exe -sI http://localhost:5115/__routes
```

Expected: `Content-Security-Policy` ending in `frame-ancestors 'self';`, plus `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, and `Permissions-Policy` with the values in the table above. The headers appear even on error responses (a 502 while backends are down still carries them), which confirms they are applied at the gateway rather than copied from an upstream.

Deployed (HSTS requires the production HTTPS listener):

```powershell
curl.exe -sI https://oneplatform.cibi.com.ph/ | findstr /i "strict-transport"
```

Functional regression check after any change to `Permissions-Policy` or the CSP `frame-src`/`frame-ancestors` directives: run the PhilSys liveness flow once and confirm the camera prompt appears inside the everify iframe, and check the browser console for CSP violation reports during normal navigation.

## Maintenance notes

- All six headers live in the single middleware block in `ApiGateways/YarpApiGateway/Program.cs`. When the CSP needs a new origin (a recurring event historically — Google Fonts, the S3 buckets, and go-mpulse were each added over time), edit only that block.
- Today no upstream (nginx or the APIs) emits any of these headers, so setting them before `await next()` works. YARP copies upstream response headers with append semantics, and browsers enforce the **intersection** of duplicate CSP headers — so if an upstream ever starts emitting its own CSP, the client would receive two and the effective policy would tighten unexpectedly. The fix at that point is to move the assignments into `context.Response.OnStarting(...)`, which runs after YARP has copied upstream headers and whose indexer assignments replace rather than append.
