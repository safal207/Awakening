using System;
using Probuzhdenie;
using Probuzhdenie.FreeCity;

if (args.Length > 0 && args[0] == "--self-test")
{
    bool saveOk = SaveSystem.RunSelfTest(out string saveMessage);
    bool ledgerOk = MemoryLedgerSelfTest.Run(out string ledgerMessage);
    bool sliceOk = FirstMemorySliceSelfTest.Run(out string sliceMessage);

    Console.WriteLine(saveMessage);
    Console.WriteLine(ledgerMessage);
    Console.WriteLine(sliceMessage);
    Environment.Exit(saveOk && ledgerOk && sliceOk ? 0 : 1);
}

if (args.Length > 0 && args[0] == "--functional-test")
{
    bool ok = FunctionalTests.Run(out string message);
    Console.WriteLine(message);
    Environment.Exit(ok ? 0 : 1);
}

RuntimeProfileOptions? profileOptions = RuntimeProfileOptions.TryParse(args);

using var game = new Game(profileOptions);
game.Run();
