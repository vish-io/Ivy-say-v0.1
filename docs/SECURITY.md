# Security and trust boundaries

Ivy Say is an unsigned MVP, not a security-certified product. Do not use it as an autonomous agent or with sensitive accounts without an independent review and testing.

## Prompt injection and generated text

The transcript is JSON-encoded as data in a user message separate from the formatter's system instructions. The prompt restricts it to editing and explicitly forbids answering questions or generating code from requests, including in Code mode. The formatter has no tools or ability to run code. JSON encoding protects the API message structure, not the model from instructions inside the data. These are mitigations, **not** guarantees: a model can still obey, hallucinate, or alter text. Treat both transcript and model output as untrusted, and review before sending, executing, or relying on them.

Ivy Say checks the foreground window and detectable Win32 focused control at insertion. Settings offer three security levels:

- **0 · No restrictions:** no automatic content, terminal, or unverified-target review, including unsafe controls and multiline output. Commands or line breaks can execute actions or send messages. The original foreground window and any detectable focused control must still match; operating system failures can still prevent insertion. Review-window copying is not blocked for unsafe characters in this mode.
- **1 · Guarded review (default):** known terminal processes, unverified targets, and output with line breaks, tabs or unsafe characters are held for manual review.
- **2 · Review every dictation:** all output is held for manual review.

Some IDE terminals, browsers, remote desktops, password fields, inaccessible controls, app-internal focus changes, and renamed terminal processes cannot be reliably recognized. Focus can also change between checks. Guarded mode is a best-effort blocklist, not a sandbox or allowlist. For sensitive work, use level 2 and check the destination and content. Pasting into a terminal can execute commands, especially with line breaks. Clipboard history and other applications may read copied content.

In guarded and review-everything modes, unexpected control characters, bidirectional override/isolate characters, and malformed surrogate pairs are not automatically inserted or copied by the app; the user can review/edit the result. Other visually deceptive Unicode and malicious instructions can still pass through. Level 0 does not apply this check. The review window is local and is not automatically saved by Ivy Say.

In the default streaming-preview mode, fragments are shown in the pill but are not inserted until the response completes and the selected insertion policy has been checked. An explicitly enabled direct-streaming mode (level 0 only) types unvalidated partial model output immediately; no automatic rollback is possible. It is unsafe for terminals, sending messages, or sensitive applications. A failed stream after insertion begins shows the partial text without inserting it again or trying another model. Before direct insertion, eligible provider errors may trigger one fallback request to the other formatter model. Focus checks are best-effort and do not guarantee safety.

## Provider and local data

API requests use HTTPS with standard certificate validation, no automatic redirects, a shared connection pool, bounded recording size, response-size limits (256 KiB), and bounded request times including reading the body. Error bodies are not displayed. Prompt injection is not prevented by these network bounds. Groq receives recorded audio and transcripts; review its terms and retention policies.

The Groq key and optional personal vocabulary/corrections are protected with DPAPI bound to the Windows user account. It is not a defense against malware already running as that user. General spelling hints are unencrypted in `preferences.json` and sent to Groq. No transcript history or recordings are intentionally written to disk. Daily word counts and recording durations are stored unencrypted as local aggregate statistics and can be cleared in settings. Windows clipboard history, paging, crash dumps, target apps and external software can retain sensitive data. Avoid dictating secrets into untrusted apps.

## Remaining release work

The cloud tests use fake HTTP responses and unit-test insertion decisions; they do not validate actual terminal detection or UI behavior. Before release: run the Windows manual checklist, test cross-process focus and terminals/IDEs/RDP, evaluate adversarial dictations with the live model, benchmark latency, audit and pin dependencies and build provenance, conduct an independent security review, sign installers and updates, and design a secure update mechanism and vulnerability disclosure process. No defense prevents all attacks or guarantees zero latency overhead.

Background chunking, enabled by default, sends completed audio segments to Groq while recording is still active once the threshold is reached. Disable it in General settings to defer audio uploads until stopping. Multiple uploads and a possible full-recording retry can increase provider retention exposure and quota use. Audio remains in process memory rather than being intentionally written to disk; OS paging and crash dumps are outside this guarantee. Optional Opus is lossy, not a privacy or encryption feature.

When comparison is enabled, the original recorded WAV is retained in memory until the comparison window closes, for optional local playback. Clicking play sends it to the default Windows output device, where others nearby may hear it. Ivy Say does not intentionally write it to disk. OS paging, dumps and output-device behavior are outside this guarantee.

Ivy Say can dictate while Settings is open. For editable fields within its own Settings window, WPF controls share an HWND, so the app additionally checks that the same editor remains focused. It does not permit dictation into the API key field or non-editable settings controls. An in-progress recording uses the preferences and key captured at start; removing or changing the saved key affects subsequent dictations, not a request already in progress. The key remains in memory for the in-progress request, never intentionally written in plaintext.
