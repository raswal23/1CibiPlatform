# OMS TLS Handshake Failure — Root Cause and Fix

Connections from the containerised API to the legacy OMS database failed at the TLS
handshake with:

```
A connection was successfully established with the server, but then an error occurred
during the pre-login handshake.
(provider: SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed)
```

Branch: `feature/OMS-Generic-API-Integration`.

---

## 1. Root cause

The OMS host (`SQL Server 2008 R2 SP3-GDR`, `10.50.6560.0`) presents a TLS certificate
signed with **sha1RSA**. OpenSSL 3.x — used by .NET on Linux — rejects SHA-1 signatures at
its default security level, so it aborts the handshake before login ever happens.

Captured from an instrumented handshake against the live server:

```
NEGOTIATED PROTOCOL : Tls12
CIPHER              : TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384
SERVER CERT SIGALG  : sha1RSA        <-- the blocker
```

### What it is not

**Not a TLS version problem.** The server has TLS 1.2 enabled, which
`xp_instance_regread` against
`SYSTEM\CurrentControlSet\Control\SecurityProviders\Schannel\Protocols\TLS 1.2\Server`
correctly reports. Once the certificate is accepted the connection negotiates `Tls12`
normally, as the capture above shows.

**Not a driver version problem.** Both driver versions fail identically. Measured against
the live server:

| Configuration                          | Result      |
| -------------------------------------- | ----------- |
| `Microsoft.Data.SqlClient` **6.1.1**, stock OpenSSL | FAIL |
| `Microsoft.Data.SqlClient` **5.2.3**, stock OpenSSL | FAIL |
| 6.1.1 + `MinProtocol=TLSv1.2` + `SECLEVEL=0`        | **SUCCESS** |
| 6.1.1 + `MinProtocol=TLSv1.2` + `SECLEVEL=2`        | FAIL |

The bottom two rows isolate the variable: holding the protocol floor at TLS 1.2 and
changing *only* the security level flips the result. That is the signature-policy check,
not the protocol.

A `6.1.1 → 5.2.3` downgrade in `OMS.csproj` was therefore reverted — it had no effect.
`OMSSqlConnectionFactory.cs` was never at fault. **No C# code was changed.**

---

## 2. The fix

### `BackendAPI/API/APIs/openssl-legacy-oms.cnf` (new)

A scoped OpenSSL policy that `.include`s the system config rather than editing
`/etc/ssl/openssl.cnf`, leaving the image default intact:

```ini
.include /etc/ssl/openssl.cnf

[system_default_sect]
MinProtocol  = TLSv1.2
MaxProtocol  = TLSv1.3
CipherString = HIGH:!aNULL:!eNULL:!EXPORT:!DES:!3DES:!RC4:!MD5:!PSK:!SRP:!CAMELLIA@SECLEVEL=0
```

`@SECLEVEL=0` is the part that re-admits the SHA-1 certificate signature.

### `BackendAPI/API/APIs/Dockerfile`

Two lines in the `base` stage — copy the policy in, and point OpenSSL at it:

```dockerfile
COPY BackendAPI/API/APIs/openssl-legacy-oms.cnf /etc/ssl/oms/openssl-legacy-oms.cnf
ENV OPENSSL_CONF=/etc/ssl/oms/openssl-legacy-oms.cnf
```

`USER $APP_UID` was moved to sit *after* the `COPY`, so the copy runs as root while
writing into `/etc/ssl/`.

---

## 3. Blast radius — this is not OMS-only

`OPENSSL_CONF` is **process-wide**. It applies to every outbound TLS connection the API
container makes, not just OMS. That is why the policy is written as a tight allow-list
instead of a blanket `DEFAULT@SECLEVEL=0`:

- **`MinProtocol = TLSv1.2`** — hard floor. TLS 1.0/1.1 and SSLv3 stay refused for every
  connection, OMS included.
- **`MaxProtocol = TLSv1.3`** — TLS 1.3 remains the preferred default everywhere it is
  available. It is the ceiling, not the floor.
- **Explicit `HIGH:!RC4:!3DES:!MD5:!aNULL:…`** — lowering the security level relaxes
  signature and key-strength policy only; it does not quietly re-enable NULL, RC4, 3DES,
  MD5 or export suites.

Verified by handshake against a local test server under the shipped policy:

```
client offers everything -> TLSv1.3     # ordinary traffic still lands on 1.3
client forced to TLS 1.3 -> TLSv1.3
client forced to TLS 1.2 -> TLSv1.2     # what OMS uses
client forced to TLS 1.1 -> refused
client forced to TLS 1.0 -> refused
```

Weak suites offered under the policy: **0**.

**Certificate chain validation is unaffected.** Security level governs crypto strength,
not chain trust. Confirmed under the override with default validation and no bypass:
`expired.badssl.com` and `wrong.host.badssl.com` are both still REJECTED, identical to
baseline. OMS skips chain validation only through `TrustServerCertificate=True` in its own
connection string, which is scoped to that one connection.

---

## 4. Why the fix appeared to "come back"

The two lines were originally placed in the Dockerfile's `final` stage. That is correct
for compose and production — but **Visual Studio Fast Mode builds only the `base` stage**
and bind-mounts the source tree into `/app`. Anything added to `final` is silently absent
when debugging from VS, so the original error returned unchanged.

Diagnosis of the running container:

- `docker history apis:dev` showed **only base-image layers** — no app, no `.cnf`
- `/app` contained `Program.cs` and `APIs.csproj` but **no `APIs.dll`** — a bind mount of
  the source directory, not a published app
- Labels showed `com.microsoft.created-by=visual-studio`, entrypoint
  `DistrolessHelper.dll --wait`
- Inside the container: `OPENSSL_CONF=(unset)`, `cnf present: NO`

**Resolution:** both lines now live in the `base` stage. Because `final` is declared
`FROM base`, it inherits them — one definition covers the VS debug path and the
compose/production path alike.

### Verification

Building `--target base` (exactly what VS Fast Mode builds) and running the real
`OMSSqlConnectionFactory` and `OMSRepository` types against the live server:

```
OPENSSL_CONF = /etc/ssl/oms/openssl-legacy-oms.cnf
OMSSqlConnectionFactory.OpenConnectionAsync -> OPEN (Open)
  server version: 10.50.6560
OMSRepository.ValidateRequestorAsync -> executed, isValid=False

VERDICT: TLS handshake to OMS SUCCEEDED
```

Control run, same binary and image with `OPENSSL_CONF` unset:

```
OMSSqlConnectionFactory.OpenConnectionAsync -> FAILED
  SqlException: ... (provider: SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed)
```

`isValid=False` is the expected verdict for a dummy requestor — the point is that the
stored procedure was reached over TLS.

---

## 5. Applying the change

In Visual Studio: stop debugging, delete the `apis:dev` image (or use Rebuild), then F5.
VS caches the base image and will not pick up the Dockerfile change otherwise. The `base`
stage requires no NuGet access, so it builds despite the restore issue in §6.

---

## 6. Known unrelated issue: `docker compose build` fails

`docker compose build apis` currently fails during `dotnet restore`:

```
error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.
  The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot
```

This is a corporate TLS-intercepting proxy blocking NuGet from inside the build container.
It is **pre-existing and unrelated** — it reproduces identically with the Dockerfile change
stashed, and it occurs in the `build` stage, whereas this fix lives in `base`.

It does not affect VS debugging. The durable fix is to install the proxy's root CA into the
`build` stage. Until then, `docker compose up --build` will fail its rebuild and silently
leave the previous container running on a stale image — which is exactly how a stale
Aug-19 image masked this fix once already.

---

## 7. This is a workaround

The correct long-term fix is to issue the OMS server a **SHA-256 signed certificate**. Once
that is done, delete `openssl-legacy-oms.cnf` and the two Dockerfile lines. The `.cnf` file
carries this note in its own header comment.
