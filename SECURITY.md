# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Current release on `main` | ✅ Yes |
| Older releases | ❌ No |

Only the latest version on the `main` branch receives security updates.

## Reporting a Vulnerability

**Please do NOT open public issues for security vulnerabilities.**

Instead, use **GitHub Security Advisories** to report vulnerabilities privately:

1. Go to the [Security Advisories](https://github.com/Paregov/WinSTerm/security/advisories) page.
2. Click **"New draft security advisory"**.
3. Fill in the details and submit.

This ensures the vulnerability is disclosed privately and can be addressed before public disclosure.

## Scope

The following areas are in scope for security reports:

- **SSH key handling** — private key loading, storage, and memory management
- **Password encryption** — credential encryption/decryption at rest
- **Master password** — key derivation and protection mechanism
- **Credential storage** — encrypted connection database security
- **Session data** — protection of sensitive session information

### Encryption Details

Credentials are encrypted at rest using:

- **AES-256-CBC** symmetric encryption
- **PBKDF2** key derivation from the master password
- Encrypted data is stored locally and never transmitted to external services

## Response Timeline

| Action | Timeline |
|--------|----------|
| Acknowledge report | Within **48 hours** |
| Initial assessment | Within **7 days** |
| Fix target | Within **30 days** |

Timelines may vary depending on complexity. We will keep you informed of progress.

## Disclosure

We follow a coordinated disclosure process. Once a fix is released, we will:

1. Credit the reporter (unless they prefer anonymity).
2. Publish a security advisory on GitHub.
3. Release a patched version.

Thank you for helping keep NetSterm and its users safe!
