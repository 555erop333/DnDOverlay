# Unresolved Issues

1. **Deck playback still restricted to preview snippets**  
   - Dragging a YouTube track onto a deck still starts playback from the 60-second preview instead of a fully buffered or streaming source.  
   - Requirement: seamlessly upgrade decks to buffered segments or a live HLS stream so tracks play in full without looping the preview.

2. **"Download All" does not download full buffered tracks**  
   - Bulk downloads presently reuse the preview flow, so only the short snippet is cached and users never receive the complete audio.  
   - Requirement: fetch the full buffered/segment files for every listed track, surface progress, and notify on success/failure.

3. **Segment buffering remains fragile when yt-dlp hits challenge-solving errors**  
   - Logs (e.g., 20:29 session) still show `EnsureYouTubeSegment` disabling segments after yt-dlp exits with code 101 even though it produced partial output.  
   - Need more robust fallbacks (alternate clients, retries, solver integration) plus smarter handling so a single hiccup doesn’t permanently block buffering for that video.

4. **UI freeze risk during segment downloads**  
   - Deck drag/drop can stall the UI while `EnsureYouTubeSegment` waits on in-flight downloads and synchronous logging; the main thread remains blocked for up to a minute when yt-dlp hangs.  
   - Requirement: move heavy yt-dlp/segment orchestration fully off the UI thread, add cancellation/timeouts, and throttle verbose logs from hot UI events.

5. **Toolbar polish requests still outstanding**  
   - "Delete" button must be removed from the toolbar and exposed via the right-click context menu only.  
   - The "Add" button should display a + icon per UX feedback.  
   - These layout tweaks have not been implemented yet.
