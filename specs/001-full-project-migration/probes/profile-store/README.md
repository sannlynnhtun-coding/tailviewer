# Profile-store interruption probe

Run from the repository root on Windows x64:

```powershell
dotnet run --project specs/001-full-project-migration/probes/profile-store/ProfileStoreProbe.csproj -c Release
```

This test-only console uses the existing signed Tailviewer host identity to call
Core's internal production ProfileStore; no production visibility is widened.
The parent creates an owned GUID directory under the OS temporary directory and
starts a hidden child dotnet process using structured ArgumentList values.
The child writes through the real store and signals either after candidate
flush/validation (before publication) or after publication returns, then waits
on its redirected stdin. The parent terminates only that owned process handle.

For both interruption points the parent reopens the destination, verifies the
old/new committed value, saves again and verifies recovery. A staged abandoned
private directory/candidate is left untouched by the subsequent save. The probe validates that
its cleanup directory remains a direct child of the original temporary parent
before recursive cleanup. It never accesses actual user profiles or logs.

The bundle cases additionally terminate after the first member, after both members but before commit, and after commit. Pending journals restore both original snapshots before a normal store read; committed pairs remain new. Direct physical-file observations bypass the folder mutex only in this probe, while the child deliberately holds it.

Expected output: five PASS lines. It does not simulate physical power loss,
termination inside an individual kernel rename call, EFS certificate/key setup,
SMB/network filesystems or Linux/macOS. The regular NUnit tests separately cover
read-only/sharing failures, candidate IO/validation errors, metadata preservation,
input bounds, XML values and concurrent/first-save races.
