using System;
using Probuzhdenie;
using Probuzhdenie.FreeCity;

PlaytestTrace.Configure(args);

bool HasArg(string value) => Array.Exists(args, arg =>
    string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));

if (HasArg("--self-test"))
{
    bool saveOk = SaveSystem.RunSelfTest(out string saveMessage);
    bool ledgerOk = MemoryLedgerSelfTest.Run(out string ledgerMessage);
    bool sliceOk = FirstMemorySliceSelfTest.Run(out string sliceMessage);

    Console.WriteLine(saveMessage);
    Console.WriteLine(ledgerMessage);
    Console.WriteLine(sliceMessage);
    bool ok = saveOk && ledgerOk && sliceOk;
    PlaytestTrace.Record("self_test_exit", value: ok ? "pass" : "fail");
    PlaytestTrace.Close();
    Environment.Exit(ok ? 0 : 1);
}

if (HasArg("--functional-test"))
{
    bool ok = FunctionalTests.Run(out string message);
    Console.WriteLine(message);
    PlaytestTrace.Record("functional_test_exit", value: ok ? "pass" : "fail");
    PlaytestTrace.Close();
    Environment.Exit(ok ? 0 : 1);
}

RuntimeProfileOptions? profileOptions = RuntimeProfileOptions.TryParse(args);

using var game = new Game(profileOptions);
game.Run();
