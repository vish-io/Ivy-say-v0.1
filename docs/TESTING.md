# Windows acceptance checklist

Use a test Groq account and non-sensitive sample text. Automated tests cover the WAV encoder, not the OS integration or live provider. Check current Groq model availability and costs before testing.

1. Build, run tests, and publish using the README commands. Launch the published app on a clean Windows 10/11 account with microphone permission enabled.
2. First launch opens settings. Save a valid key, close and reopen the app, and verify dictation works; confirm `groq.key` is ciphertext and contains no plaintext key. Replace and remove the key; restarting after removal should show settings. Check that another Windows account cannot decrypt the first account's key.
3. Focus Notepad; press Ctrl+Win+Space, speak, and press it again. Check output for default prose and for Code, Email, and Bullet prefixes. Exercise punctuation, Unicode, multiline output, fast repeated shortcuts and very short or silent recordings.
4. Test both models and disable polishing. Confirm disabled polish returns raw transcript and does not interpret spoken mode prefixes. Check observed timings over a range of utterances and network conditions; do not assume a fixed latency.
5. Focus another window while processing. Verify nothing is injected into it and the recovery window shows the output. Change focus during a long insertion and verify the partial-insertion warning. Copy manually and check the target before pasting.
6. Exercise invalid key, network disconnect, provider rejection and formatter failure. Verify errors reveal neither keys nor response bodies; formatting failure should preserve the raw transcript in the recovery window. Check that no audio or transcript files were written.
7. Deny microphone permission, remove/disconnect the device, and test unsupported microphone formats. Confirm a useful error and that a subsequent valid attempt works. Check automatic stop at 115 seconds and maximum memory protection.
8. Occupy the hotkey with another app before launching, open a second Ivy Say instance, and test quitting while idle, recording, and processing. Verify no lingering microphone capture or process. Test standard and elevated targets, multiple monitors, DPI scaling and screen-reader access.
9. Review unsigned binaries, dependencies, update/distribution plan, privacy policy, provider terms, threat model and accessibility with your release team before deploying to users.
