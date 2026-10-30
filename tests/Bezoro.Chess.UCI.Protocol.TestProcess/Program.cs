// A child process for real pipe/lifecycle tests. Arguments and payloads use ASCII/base64;
// output writes raw bytes so the operating system's console encoding never changes them.
if (args.Length == 0) return 1;

switch (args[0])
{
    case "exit":
        return 0;
    case "wait-input":
        await Console.In.ReadLineAsync();
        return 0;
    case "wait-kill":
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    case "stdout":
    case "stderr":
        if (args.Length != 3) return 2;
        byte[] bytes = Convert.FromBase64String(args[1]);
        await using (var output = args[0] == "stderr" ? Console.OpenStandardError() : Console.OpenStandardOutput())
        {
            await output.WriteAsync(bytes);
            await output.FlushAsync();
            if (args[2] == "wait") await Console.In.ReadLineAsync();
        }
        return 0;
    default:
        return 3;
}
