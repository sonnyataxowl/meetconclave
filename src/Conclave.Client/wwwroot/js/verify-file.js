// Verify page: hash the recording the visitor picked, in this browser only (chunked, with progress).
// Nothing here talks to the network; the file is read through Blob.slice and dropped.
import { hashBlob } from './sha256.js';

const MIN_PROGRESS_GAP_MS = 100;

/** Hashes a File/Blob. onProgress(doneBytes, totalBytes) is called at most every 100 ms, and always for the final chunk. */
export async function hashFile(file, onProgress) {
  let last = -Infinity;
  const sha256 = await hashBlob(file, (done, total) => {
    if (!onProgress) return;
    const now = Date.now();
    if (done === total || now - last >= MIN_PROGRESS_GAP_MS) { last = now; onProgress(done, total); }
  });
  return { sha256, bytes: file.size, name: file.name };
}

/** Hashes the file picked in <input type=file id=inputId>; reports to dotnetRef.OnHashProgress(done, total). Null when nothing is picked. */
export async function hashFileInput(inputId, dotnetRef) {
  const input = document.getElementById(inputId);
  const f = input && input.files && input.files[0];
  if (!f) return null;
  return hashFile(f, (done, total) => { if (dotnetRef) dotnetRef.invokeMethodAsync('OnHashProgress', done, total).catch(() => {}); });
}
