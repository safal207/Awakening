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
    bool functionalOk = FunctionalTests.Run(out string functionalMessage);
    bool memoryOk = MemoryAnchorTests.Run(out string memoryMessage);
    bool sverkaOk = SverkaEngineTests.Run(out string sverkaMessage);
    bool storyOk = FirstDistrictStoryTests.Run(out string storyMessage);
    Console.WriteLine(functionalMessage);
    Console.WriteLine(memoryMessage);
    Console.WriteLine(sverkaMessage);
    Console.WriteLine(storyMessage);
    Environment.Exit(functionalOk && memoryOk && sverkaOk && storyOk ? 0 : 1);
}

RuntimeProfileOptions? profileOptions = RuntimeProfileOptions.TryParse(args);

using var game = new Game(profileOptions);
game.Run();
