using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace CspMultiplexer.App;

/// <summary>
/// Writes, verifies and reaps <c>%LOCALAPPDATA%\CSP Suite\mux-session.json</c> (§12).
/// The file carries a live proxy credential and it is a hint, never a fact: every method
/// here is total, silent, and never logs — a diagnostic sink on this path is how the
/// pairing URL ends up in a second on-disk location nothing reaps.
/// </summary>
internal static class MuxSessionHandoff
{
    private const int StreamBufferBytes = 4096;

    /// <summary>One write per session. The whole method's error contract is "never throws".</summary>
    internal static void TryPublish(string pairingUrl)
    {
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(MuxHandoffContract.DirectoryPath);
            TryDeleteOrphanTempFiles();

            using var self = Process.GetCurrentProcess();
            var document = new MuxSessionDocument
            {
                SchemaVersion = MuxHandoffContract.SchemaVersion,
                PairingUrl = pairingUrl,
                ProcessId = Environment.ProcessId,
                ProcessStartTimeUtc = self.StartTime.ToUniversalTime(),
            };

            temporaryPath = Path.Combine(
                MuxHandoffContract.DirectoryPath,
                $"{MuxHandoffContract.TempPrefix}{Guid.NewGuid():N}{MuxHandoffContract.TempSuffix}");

            // The DACL goes on the TEMP file, not on the destination afterwards:
            //  (1) a file created and then re-ACLed is readable for the interval between
            //      the two calls;
            //  (2) File.Move within a volume preserves the source's EXPLICIT ACEs and does
            //      not re-inherit from the destination directory — verified.
            // Protection is what makes this worth writing at all: %LOCALAPPDATA% carries
            // an inherited Full-Control ACE for an AppContainer capability SID, and a
            // low-integrity process is exactly the class that could not otherwise reach a
            // credential this app holds.
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            using var identity = WindowsIdentity.GetCurrent();
            var user = identity.User!;
            security.SetAccessRule(new FileSystemAccessRule(
                user, FileSystemRights.FullControl, AccessControlType.Allow));
            security.SetAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));

            // WriteThrough so a crash leaves the file complete or absent, never half.
            using (var stream = new FileInfo(temporaryPath).Create(
                       FileMode.CreateNew,
                       FileSystemRights.WriteData | FileSystemRights.Write,
                       FileShare.None,
                       StreamBufferBytes,
                       FileOptions.WriteThrough,
                       security))
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(document, MuxHandoffContract.Json));
                stream.Flush();
            }

            // Medium mandatory label with no-read-up. Best effort: if it fails the DACL
            // still holds, and the DACL is what closes the AppContainer hole.
            NativeMethods.TrySetMediumNoReadUpLabel(temporaryPath);

            // Atomic replace, so a Companion polling the path never observes a partial
            // document — it sees the old file or the new one.
            File.Move(temporaryPath, MuxHandoffContract.FilePath, overwrite: true);
            temporaryPath = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or PlatformNotSupportedException)
        {
            // Degrade to the QR path. Never surfaced, never logged. Failing the start
            // would take the proxy down over a hint.
        }
        finally
        {
            // Without this, every failed publish leaves a credential-bearing .tmp on disk
            // forever, under a random name TryDeleteOwn does not match.
            if (temporaryPath is not null)
            {
                TryDeleteQuietly(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Deletes the published file in the two sanctioned cases: it is this process's, or
    /// the process it names is verifiably dead. Without the ownership check one instance's
    /// shutdown would silently unpublish another instance's live session.
    /// </summary>
    internal static void TryDeleteOwn()
    {
        TryDeleteOrphanTempFiles();

        try
        {
            MuxSessionDocument? document;

            // FileShare.None for the verify read: while this handle is open no other
            // instance can complete a File.Move replace, so the document that is verified
            // is the document that is deleted.
            using (var stream = new FileStream(
                       MuxHandoffContract.FilePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None))
            {
                if (stream.Length > MuxHandoffContract.MaximumFileBytes)
                {
                    return;
                }

                document = JsonSerializer.Deserialize<MuxSessionDocument>(
                    stream, MuxHandoffContract.Json);
            }

            // A document that is the JSON literal null deserialises to null even when
            // every member is required.
            if (document is null)
            {
                return;
            }

            var mine = document.ProcessId == Environment.ProcessId;
            if (mine)
            {
                using var self = Process.GetCurrentProcess();
                mine = Math.Abs(
                           (self.StartTime.ToUniversalTime() - document.ProcessStartTimeUtc).Ticks)
                       <= MuxHandoffContract.StartTimeTolerance.Ticks;
            }

            var dead = false;
            if (!mine)
            {
                try
                {
                    using var other = Process.GetProcessById(document.ProcessId);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    dead = true;
                }
            }

            if (mine || dead)
            {
                File.Delete(MuxHandoffContract.FilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or JsonException or NotSupportedException)
        {
        }
    }

    /// <summary>
    /// The LAN-scope start site's name for <see cref="TryDeleteOwn"/>: the ownership check
    /// already sanctions both "it is mine" and "its owner is dead", so the write site
    /// needs no second method.
    /// </summary>
    internal static void TryDeleteStale() => TryDeleteOwn();

    /// <summary>
    /// Runs on publish and on delete, so a crash between CreateNew and Move — the one case
    /// the publish <c>finally</c> cannot cover — is reaped at the next clean start or stop.
    /// </summary>
    private static void TryDeleteOrphanTempFiles()
    {
        try
        {
            foreach (var orphan in Directory.EnumerateFiles(
                         MuxHandoffContract.DirectoryPath,
                         $"{MuxHandoffContract.TempPrefix}*{MuxHandoffContract.TempSuffix}"))
            {
                TryDeleteQuietly(orphan);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
