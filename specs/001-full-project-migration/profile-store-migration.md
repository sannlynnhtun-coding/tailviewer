# T012 — bounded profile IO and atomic saves

## Slice 1: shared bounded reads

ProfileStore.Read is shared by modern ApplicationSettings and Bookmarks.
It opens a read handle that allows replacement/deletion, so an in-progress read
can retain its coherent old file handle while a writer publishes a new file.
Reads reject files above 16 MiB, XML reader depth above 64 and document character
counts above 16 Mi characters. DTD processing is explicitly prohibited and the
resolver is null. A bounded preflight depth scan precedes tree allocation;
the second bounded pass preserves XML whitespace/document nodes. Only an
unqualified xml document root is accepted. Limits are safety policy, not a
claim that every historical plugin payload fits; oversized/deep input remains
unchanged and saves stay blocked until the user supplies a valid profile.

The bookmark snapshot now keeps an XDocument, so processing instructions and
comments outside its root survive clone/save alongside opaque bookmark payloads.
Modern bookmark errors report exception types without configuration contents.
Legacy IO remains conditional and unchanged. No real user profile is inspected.

Executed: initial seven public-model behavior cases **5 failed / 2 passed**;
DTD rejection already passed, while size/depth and outer document metadata failed.
After the reader port, **51 affected profile/bookmark/original-host cases passed**,
no failures/skips. Expanded net452 source/API build passed with the existing
legacy log4net advisory warnings. Logs: artifacts/migration/t012-read-red.log,
t012-read-green.log, t012-read-legacy.log.

T012 remains open: saves still use the old direct writer in this slice.
Candidate validation, flush/replacement/failure/concurrent-reader tests follow
before this task is marked complete or modern UI persistence is connected.

Implementation reference: [Microsoft XML reader settings](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings?view=net-10.0).
