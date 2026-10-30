# Transport test process

This executable provides real stdin/stdout/stderr pipes for transport tests on Windows and POSIX systems. It uses the repository's .NET 10 runtime and has no shell or third-party dependencies.

Modes are `exit`, `wait-input`, `wait-kill`, `stdout`, and `stderr`. Output modes accept a base64 payload and either `exit` or `wait`. They write and flush the exact decoded bytes, then either exit or wait for one stdin line. `wait-kill` stays alive independently of stdin so teardown fault tests always exercise their injected process-wait failure.

The test project references this executable and invokes its copied DLL with `dotnet exec --runtimeconfig` using the test project's runtime configuration. The output scenarios cover immediate process exit and a producer held alive until the receiver confirms delivery.
