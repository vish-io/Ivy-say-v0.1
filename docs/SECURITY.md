# Security and trust boundaries

Ivy Say is an unsigned MVP, not a security-certified product. Do not use it as an autonomous agent or with sensitive accounts without an independent review and testing.

## Prompt injection and generated text

The transcript is sent as a user message separate from the formatter's system instructions. The formatter has no tools or ability to run code, and the prompt asks it not to follow instructions in dictated content. These are mitigations, **not** guarantees: a model can still obey, hallucinate, or alter text. Treat both transcript and model output as untrusted, and review before sending, executing, or relying on them.

Ivy Say checks the foreground window and detectable Win32 focused control, and requires manual review for known terminal processes, unverified targets, output with line breaks or tabs, or when the user enables review for every dictation. Some IDE terminals, browsers, remote desktops, password fields, inaccessible controls, app-internal focus changes, and renamed terminal processes cannot be reliably recognized. Focus can also change between checks. This is a best-effort blocklist, not a sandbox or allowlist. For sensitive work, turn on **Review every dictation before inserting** and check the destination and content. Pasting into a terminal can execute commands, especially with line breaks. Clipboard history and other applications may read copied content.

Unexpected control characters, bidirectional override/isolate characters, and malformed surrogate pairs are not automatically inserted or copied by the app; the user can review/edit the result. Other visually deceptive Unicode and malicious instructions can still pass through. Multi-line and tabbed results are not automatically typed even in ordinary apps. The review window is local and is not automatically saved by Ivy Say.

## Provider and local data

API requests use HTTPS with standard certificate validation, no automatic redirects, a shared connection pool, bounded recording size, response-size limits (256 KiB), and bounded request times including reading the body. Error bodies are not displayed. Prompt injection is not prevented by these network bounds. Groq receives recorded audio and transcripts; review its terms and retention policies.

The Groq key and optional personal vocabulary/corrections are protected with DPAPI bound to the Windows user account. It is not a defense against malware already running as that user. General spelling hints are unencrypted in `preferences.json` and sent to Groq. No transcript history or recordings are intentionally written to disk. Windows clipboard history, paging, crash dumps, target apps and external software can retain sensitive data. Avoid dictating secrets into untrusted apps.

## Remaining release work

The cloud tests use fake HTTP responses and unit-test insertion decisions; they do not validate actual terminal detection or UI behavior. Before release: run the Windows manual checklist, test cross-process focus and terminals/IDEs/RDP, evaluate adversarial dictations with the live model, benchmark latency, audit and pin dependencies and build provenance, conduct an independent security review, sign installers and updates, and design a secure update mechanism and vulnerability disclosure process. No defense prevents all attacks or guarantees zero latency overhead.
