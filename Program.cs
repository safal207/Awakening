using System;
using Probuzhdenie;
using Probuzhdenie.FreeCity;

if (args.Length > 0 && args[0] == "--self-test")
{
    bool ok = SaveSystem.RunSelfTest(out string message);
    bool recoveryOk = SaveSystem.RunRecoveryTests(out string recoveryMessage);
    Console.WriteLine(message);
    Console.WriteLine(recoveryMessage);
    Environment.Exit(ok && recoveryOk ? 0 : 1);
}

if (args.Length > 0 && args[0] == "--functional-test")
{
    bool functionalOk = FunctionalTests.Run(out string functionalMessage);
    bool memoryOk = MemoryAnchorTests.Run(out string memoryMessage);
    bool sverkaOk = SverkaEngineTests.Run(out string sverkaMessage);
    bool storyOk = FirstDistrictStoryTests.Run(out string storyMessage);
    bool ledgerOk = MemoryLedgerSelfTest.Run(out string ledgerMessage);
    bool integrationOk = MemoryIntegrationTests.Run(out string integrationMessage);
    bool roundTripOk = SaveSystem.RunMemoryRoundTripTest(out string roundTripMessage);
    bool districtOk = DistrictEpisodeTests.Run(out string districtMessage);
    bool navigationOk = NavigationTests.Run(out string navigationMessage);
    bool archiveOk = DistrictArchiveTests.Run(out string archiveMessage);
    Console.WriteLine(functionalMessage);
    Console.WriteLine(memoryMessage);
    Console.WriteLine(sverkaMessage);
    Console.WriteLine(storyMessage);
    Console.WriteLine(ledgerMessage);
    Console.WriteLine(integrationMessage);
    Console.WriteLine(roundTripMessage);
    Console.WriteLine(districtMessage);
    Console.WriteLine(navigationMessage);
    Console.WriteLine(archiveMessage);
    Environment.Exit(functionalOk && memoryOk && sverkaOk && storyOk && ledgerOk && integrationOk && roundTripOk && districtOk && navigationOk && archiveOk ? 0 : 1);
}

if (args.Length == 1 && args[0] == "--memory-test")
{
    bool ok = MemoryLedgerSelfTest.Run(out string message);
    Console.WriteLine(message);
    Environment.Exit(ok ? 0 : 1);
}

if (args.Length > 0 && Array.IndexOf(args, "--runtime-profile") < 0)
{
    Console.Error.WriteLine("Unknown arguments. Use --self-test, --functional-test, --memory-test or --runtime-profile.");
    Environment.Exit(2);
}

RuntimeProfileOptions? profileOptions = RuntimeProfileOptions.TryParse(args);

using var game = new Game(profileOptions);
game.Run();
