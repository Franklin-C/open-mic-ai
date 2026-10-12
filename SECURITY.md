# Security and privacy

Report vulnerabilities privately using GitHub's private vulnerability reporting
when enabled. Do not include real dictations, clipboard contents, audio, model
server keys or local preferences in public issues.

Speech and writing inference run on the user's computer. Models download from
the pinned sources in the catalog; SHA-256 is checked before a downloaded model
is loaded. Model downloads are the only required internet access. Do not expose
the local engine ports on a network.

The app captures text from the focused control through operating-system
accessibility APIs. It avoids password fields and checks the target and selection
before replacing text. Unsupported controls use the Copy fallback. No app can
promise support for every third-party control.

History is local and optional. It contains plain text and is not encrypted.
Audio is processed in memory and not retained. The initial Mac build is a preview:
permission prompts, audio devices and paste behavior need real-device testing.
