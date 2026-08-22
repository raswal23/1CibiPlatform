# Static Asset Cache Busting (Blazor WebAssembly)

## Overview

This document describes how **FrontendWebassembly** busts browser caches for static assets so users never have to clear their cache after a deploy, while unchanged files stay cached for maximum performance.

Implemented August 2026 on `UI/FrontendWebassembly`. It replaces the earlier manual approach (a hand-bumped `?v=1.0.1` query string injected at runtime) with the .NET 10 built-in **content fingerprinting** pipeline plus matching nginx cache headers.

**Files involved:**

| File | Role |
|---|---|
| `UI/FrontendWebassembly/FrontendWebassembly.csproj` | Enables fingerprinting (`OverrideHtmlAssetPlaceholders`, `StaticWebAssetFingerprintPattern`) |
| `UI/FrontendWebassembly/wwwroot/index.html` | Placeholder markers in script tags, import map hook, `?v=` bump on CSS/MudBlazor links |
| `UI/FrontendWebassembly/nginx.conf` | Cache-Control headers per asset class |
| `UI/FrontendWebassembly/wwwroot/js/ats/chartExport.js` | Resolves lazy-loaded scripts through the import map |

---

## How it works

### 1. JS files — content fingerprinting (fully automatic)

At `dotnet publish`, the Static Web Assets pipeline hashes each file's **content** (SHA-256, surfaced as the short base-36 slug) and renames the file on disk:

```
js/generic/nameFilter.js  →  js/generic/nameFilter.08j5zj0alz.js
```

`index.html` contains placeholder markers that the publish step replaces with the hashed name:

```html
<!-- Source (wwwroot/index.html) -->
<script src="js/generic/nameFilter#[.{fingerprint}].js"></script>

<!-- Published output -->
<script src="js/generic/nameFilter.08j5zj0alz.js"></script>
```

Because the hash is derived purely from content:

- **File changed** → new hash → new URL → every browser downloads it (no stale cache possible).
- **File unchanged** → same hash → same URL → browsers keep using their cached copy.
- **Nobody bumps a version manually, ever.**

This is enabled by two things in `FrontendWebassembly.csproj`:

```xml
<PropertyGroup>
  <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
</PropertyGroup>

<ItemGroup>
  <StaticWebAssetFingerprintPattern Include="JS" Pattern="*.js" Expression="#[.{fingerprint}]!" />
</ItemGroup>
```

The publish also emits an **import map** into `index.html` (the `<script type="importmap"></script>` hook) mapping original names to fingerprinted names, plus SRI `integrity` hashes for framework assets. The framework's own files (`blazor.webassembly.js`, `dotnet.*.wasm`, all assembly `.wasm` files) are fingerprinted the same way automatically.

### 2. CSS files — ETag revalidation (automatic, one extra request)

The .NET 10 placeholder pass **does not support stylesheets yet** — a known gap tracked in [dotnet/aspnetcore discussion #64170](https://github.com/dotnet/aspnetcore/discussions/64170). If you put a fingerprint marker on a `<link rel="stylesheet">`, publish renames the file on disk but writes the *original* name into the HTML, producing a 404. Placing the marker inside a query string (`app.css?v=#[{fingerprint}]`) also passes through as literal text — both variants were tested and failed.

So CSS keeps stable file names, and nginx serves `*.css` with:

```nginx
add_header Cache-Control "no-cache";
```

`no-cache` does **not** mean "don't cache" — it means *cache, but revalidate before use*. The browser keeps the file and asks nginx "has this changed?" (`If-None-Match` + ETag) on each page load:

- **Unchanged** → nginx answers `304 Not Modified` (a few hundred bytes) → cached copy used.
- **Changed by a deploy** → ETag differs → full download of the new file.

Users always get fresh CSS on the next load after a deploy; the cost is one cheap conditional request per stylesheet.

### 3. `_content/` package assets (MudBlazor) — same as CSS

Package static assets are **not fingerprinted** in standalone Blazor WASM publish output — `_content/MudBlazor/MudBlazor.min.css` and `.min.js` keep stable names. nginx therefore serves `_content/` with `no-cache` (ETag revalidation) so a MudBlazor package upgrade reaches users on their next visit.

### 4. Lazily loaded scripts — resolved through the import map

`js/ats/chartExport.js` injects `html2canvas.min.js` and `jspdf.umd.min.js` on first use. Those files get fingerprinted names on disk, but import maps only apply to ES module imports — not to dynamically created `<script src>` elements. `chartExport.js` therefore reads the import map from the DOM and translates the original path to the fingerprinted one before injecting. **Any future script that injects a `<script>` tag or `fetch()`es a local JS asset by path must do the same** (reuse `resolveAssetSrc` from `chartExport.js`).

Scripts loaded via Blazor's `IJSRuntime.InvokeAsync("import", "./js/....js")` (e.g. `safeSignaturePad.js`) need **no change** — module imports go through the import map natively.

---

## nginx cache policy summary

| Asset class | Header | Why |
|---|---|---|
| `index.html` | `no-store` | Must always be fresh — it carries the fingerprinted URLs and import map |
| `/_framework/` | `immutable, max-age=1y` | Every file is content-fingerprinted; URL changes on every deploy that changes it |
| `*.js` | `immutable, max-age=1y` | App JS is fingerprinted via `StaticWebAssetFingerprintPattern` |
| `*.css`, `*.json` | `no-cache` (ETag revalidation) | Stable names — CSS can't be fingerprinted yet; `appsettings.json` is fetched by name at boot |
| `/_content/` | `no-cache` (ETag revalidation) | Package assets keep stable names in standalone WASM output |
| Images, fonts, misc | `max-age=1M` | Rarely change; acceptable staleness window |

---

## The one-time `?v=2` bump — do not touch

The CSS links and `MudBlazor.min.js` in `index.html` carry a literal `?v=2`:

```html
<link rel="stylesheet" href="css/app.css?v=2" />
<script src="_content/MudBlazor/MudBlazor.min.js?v=2"></script>
```

This exists only to evict copies cached under the **old** nginx config, which served CSS with `max-age=1M` and `_content/` with `immutable, max-age=1y` and no revalidation. Browsers holding those entries would not re-check for up to a month (CSS) or a year (MudBlazor). Changing the URL once forces everyone past the old cache entry; from then on ETag revalidation governs freshness.

**Do not bump `?v=2` again** — it is not the cache-busting mechanism and incrementing it does nothing useful. It can be removed entirely once the old cache lifetimes have safely expired (any time after ~August 2027).

---

## Gotchas

1. **Never write the literal placeholder marker in an HTML comment.** The placeholder scanner matches the marker text inside comments and then silently skips the next *real* occurrence, leaving an unreplaced `#[.{fingerprint}]` in the published HTML (which 404s). This actually happened during implementation with `xlsx.full.min.js`.

2. **Do not add a CSS `StaticWebAssetFingerprintPattern`.** It would rename CSS files on disk while `index.html` keeps referencing the original names → 404 on every stylesheet. Revisit when [#64170](https://github.com/dotnet/aspnetcore/discussions/64170) is fixed; at that point the CSS links can adopt the same `#[.{fingerprint}]` markers as the scripts and the nginx `*.css` block can move to `immutable`.

3. **`dotnet watch` hot reload** is known to misbehave with `OverrideHtmlAssetPlaceholders` ([dotnet/sdk#53331](https://github.com/dotnet/sdk/issues/53331)). If hot reload stops applying, restart the watch session or run without watch.

4. **Local publishes** warn that the `wasm-tools` workload is missing — install it with `dotnet workload install wasm-tools` for optimized local output. The Dockerfile already installs it, so CI/production builds are unaffected.

---

## Verifying after a deploy

1. Open DevTools → Network, load the site.
2. `index.html` → status 200 every time (never from cache).
3. App JS files → names contain a hash slug (e.g. `nameFilter.08j5zj0alz.js`); repeat loads show *(memory cache)* / *(disk cache)*.
4. CSS files → first load 200, repeat loads `304 Not Modified`.
5. After deploying a JS change: the changed file appears under a **new** hashed name; unchanged files keep their old names and still come from cache.
