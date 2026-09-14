using UnrealBuildTool;

public class AwakeningTarget : TargetRules
{
    public AwakeningTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Game;
        DefaultBuildSettings = BuildSettingsVersion.V5;
        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
        ExtraModuleNames.Add("Awakening");
    }
}
