# Ivy Say

A Windows-native, bring-your-own-key dictation MVP. Press **Ctrl + Win + Space** to start recording and again to stop (or choose another shortcut in settings). Ivy Say transcribes with Groq Whisper, formats with GPT-OSS 20B, and inserts Unicode text into the original foreground window. Start with **Code**, **Email**, or **Bullet** to change formatting. Drag the floating pill to reposition it; its gear opens settings.

## Build and run

Requires Windows 10/11, the .NET 8 SDK, a working microphone, and a Groq API key with access to `whisper-large-v3` and `openai/gpt-oss-20b`.

```powershell
dotnet restore IvySay/IvySay.csproj
dotnet run --project IvySay/IvySay.csproj
# Optional publish (framework-dependent)
dotnet publish IvySay/IvySay.csproj -c Release -r win-x64 --self-contained false
```

Enter the key in settings. Close the settings window, focus a text field, and use the shortcut. If Windows reports that the shortcut is unavailable, close the other application using it or choose a different shortcut in settings, then restart Ivy Say. Quit from settings. On first use, grant Windows microphone access. Recording automatically stops after 115 seconds, with a hard two-minute memory limit. Text insertion requires the target to remain foreground throughout processing and injection; elevated apps may reject input from a non-elevated Ivy Say. Some applications do not accept Unicode `SendInput` and line breaks may behave differently.

## Streaming output

After Whisper returns its transcript, AI formatting streams text into the pill as a preview. By default, Ivy Say waits for a complete, successful AI response before applying insertion security checks and typing. This is **not** live audio streaming; Whisper still receives audio only after recording stops. Preview text is visible on screen while it is generated.

An optional **Type directly as text streams** setting types each fragment into the original active app. This mode is only active with security level 0; levels 1 and 2 always use preview and their usual review rules. Direct streaming can leave incomplete text if the stream fails or focus changes, and output may contain actions or newlines that run commands or send messages. It cannot be automatically rolled back. If interrupted after typing begins, Ivy Say shows the partial response for review and does not reinsert it or fall back to raw text automatically; some or all of it may already be in the target. Focus checks still apply. Use a harmless text editor when testing.

## Controls and recovery

In settings choose the Windows default microphone or a specific recording device, the standard Whisper model or `whisper-large-v3-turbo`, and optionally disable AI polishing to skip the second request. With polishing disabled, spoken formatting prefixes are not interpreted. Microphone, language, spelling hints, model, security level, streaming mode, polish, connection warmup and comparison preferences take effect on the next dictation; shortcut changes require a restart. The pill displays elapsed processing time after successful insertion; this includes recording finalization, network calls and insertion, not the time spent speaking.

If formatting fails, the raw transcript is inserted instead and the pill reports a sanitized error category or HTTP status; no provider response body is shown. Review it carefully: speech recognition can be wrong, and spoken mode prefixes are not interpreted. Choose a **Security level** in General settings: **0 · No restrictions** skips terminal, unverified-target, multiline/tab, and unsafe-character review; **1 · Guarded review** (default) holds those results for review; **2 · Review every dictation** holds all output for review. Level 0 can type commands and line breaks directly into a terminal or send a message, and can insert unsafe control characters. Automatic insertion still checks that the original foreground window and any detectable focused control have not changed, and it may fail if Windows blocks input. All levels retain API/network limits and other protections. Existing users who had “Review every dictation” enabled are migrated to level 2. If the target changes or Windows rejects insertion, the result is also shown for manual copying. Some text may already have been inserted; check the target before pasting. Recovery text remains in memory only until the window is closed. Copying is explicit and places text on the system clipboard, where other software or clipboard history may access it. Review the destination before pasting: pasting into a terminal may execute commands. A transcription failure has no transcript to recover.

For recognition issues, set the spoken language to English, Hindi, or Punjabi rather than auto-detection where appropriate. The setting supplies a language hint to Whisper; it is not a translation setting and does not guarantee language or transcription accuracy. Auto-detect may work better when mixing languages. You can also enter optional spelling hints (such as unfamiliar names) for Whisper. Hints are saved **unencrypted** in local preferences and sent to Groq with every recording; do not use sensitive information. Hints can bias output incorrectly; remove them if they harm accuracy. Try standard Whisper large v3 if turbo misses words; compare accuracy against latency.

For a faster transcription path while keeping AI formatting, select **Whisper large v3 turbo** in settings; compare recognition quality and timings with the standard model. Optional connection warmup may help cold starts. To verify the pipeline, enable **Show transcription and AI comparison after insertion** in settings. After each successful insertion, a window shows the Whisper transcript, the actual formatter output (or a clear disabled/failed state), model names and request times, including time to the first AI fragment. Identical text does not mean the AI was skipped: the model can choose not to change it. The comparison is opt-in and visible until closed; neither text is saved by Ivy Say. Turn it off for the normal uninterrupted workflow.

The formatter attempts to resolve explicit self-corrections and likely accent-related recognition errors using context. It cannot know the speaker's intent with certainty and can introduce mistakes, especially in names, numbers, technical terms, or short ambiguous phrases. Always review important text before sending.

## Personal vocabulary and optional personalization

In settings under **Personalization**, add vocabulary terms (one per line) and/or corrections (`heard phrase => intended phrase`, one per line). Check **Use my vocabulary and corrections** and click **Save personalization**. This is opt-in and is separate from the general Save preferences button. Turn it off and save to stop using it, or use **Delete data** to remove the stored profile. There is no automatic learning, audio collection for training, or model fine-tuning.

When enabled, whole vocabulary terms are prioritized into the existing Whisper prompt, up to 160 characters total including any regular spelling hints. Terms that do not fit are skipped; put important terms first. These terms are sent to Groq with each recording. Explicit corrections match whole phrases, case-insensitively, and are applied once locally after transcription and before AI formatting; they can still replace a word in the wrong context, so review results. The corrected transcript is sent to Groq for polishing when enabled. The comparison window shows Whisper's result, the locally corrected text, and the AI output separately. Personalization cannot guarantee accent recognition and does not adapt to every new error.

Vocabulary and corrections are encrypted at rest using Windows DPAPI for the current account in `%LOCALAPPDATA%\IvySay\personalization.dat`; disabling the feature does not delete them. They are held in memory while used. Windows account malware can access them, and vocabulary and corrected text are sent to Groq as described above; avoid secrets. The older ordinary spelling hints remain separately stored unencrypted in `preferences.json`.

## Tests and delivery

Run `dotnet test IvySay.Tests/IvySay.Tests.csproj` to test the WAV encoder and Groq request handling with a fake HTTP handler. The Windows GitHub Actions workflow builds, tests, and publishes a self-contained x64 artifact. The artifact is not signed and is not a production release; see [security and limitations](docs/SECURITY.md). Follow [the manual test checklist](docs/TESTING.md) and [formatter evaluation guide](docs/FORMATTER_EVALUATION.md) on a Windows machine before distributing it.

## Privacy and security

The key is encrypted with Windows DPAPI for the current user and stored in `%LOCALAPPDATA%\IvySay\groq.key`; it is never intentionally written in plaintext. Remove it from settings. DPAPI is not a defense against malware already running as your Windows user. Audio is held in memory as 16 kHz mono PCM/WAV and sent to Groq, along with the transcript for formatting; Groq's own terms, retention policies, account billing, and network transit apply. No transcription history or audio files are saved by Ivy Say. Optional spelling hints are saved unencrypted in preferences and sent to Groq with the audio. Like any LLM, the formatter can misunderstand or follow instructions embedded in dictated content; review output before sending or running code. The app does not execute generated code.

## Latency and design

WPF/.NET keeps the native Windows shell small and uses an OS-wide registered shortcut. One long-lived HttpClient pools HTTPS connections; audio is recorded directly to memory, without audio disk I/O, at 16 kHz mono. WAV avoids encoding overhead but is larger than Opus; benchmark in-memory Opus on real networks before adding encoder complexity. Groq requests run sequentially (transcription then formatting), using low reasoning effort and hidden reasoning for GPT-OSS 20B, and service/network latency varies: sub-450 ms cannot be guaranteed. Groq's file transcription endpoint is not treated as a streaming API; sending speculative chunks while speaking requires careful overlap, ordering, and correction handling and is not implemented. Hardcoding Groq IPs is intentionally avoided: IPs can change, and bypassing DNS may break routing, failover, or TLS; connection pooling already avoids repeated lookups on reused connections. The turbo model and skipping polishing are optional speed/quality trade-offs. HTTP redirects are disabled so audio and credentials are not forwarded to a redirected endpoint. Optional connection warmup sends an extra authenticated GET to Groq’s models endpoint as recording starts, overlapping connection setup with speech. It is off by default, uses network and may count against API rate limits; failure does not block dictation. It may help a cold connection but will not reliably improve an already warm one. Audio is never sent in this warmup request.

## MVP limitations

No signed installer, automatic updates, tray icon, arbitrary custom shortcut, streaming, compressed encoding, persistent history, offline mode, or full Wispr Flow feature parity. This is not yet a production-validated release. The model may interpret a prefix differently than intended. Switching the foreground window or a detectable Win32 focused control cancels insertion rather than risking delivery to the wrong app; some applications expose only a top-level focus handle, so changes within those apps may not be detectable, and a focus change away and back cannot be detected; if focus changes during injection, part of the output may already have been inserted. Do not dictate secrets into untrusted target apps. Test with your actual microphone, keyboard layout, accessibility setup, and target applications before production deployment.

## Optional Windows installer

On Windows with Inno Setup 6 installed, from the repository root run:

```powershell
dotnet publish IvySay/IvySay.csproj -c Release -r win-x64 --self-contained true -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/IvySay.iss
```

The unsigned per-user installer is written to `installer/output`. Windows may warn about unsigned software. Uninstall removes application files, but intentionally leaves the encrypted key and preferences in `%LOCALAPPDATA%\IvySay`; remove the key in settings first or delete that directory if you want to erase local settings. Do not ship the installer to customers until it has been tested and code-signed.
