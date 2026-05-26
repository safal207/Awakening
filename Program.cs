using System;
using Probuzhdenie;
using Probuzhdenie.FreeCity;

if (args.Length > 0 && args[0] == "--self-test")
{
    bool ok = SaveSystem.RunSelfTest(out string message);
    Console.WriteLine(message);
    Environment.Exit(ok ? 0 : 1);
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
