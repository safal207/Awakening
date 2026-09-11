using UnrealBuildTool;

public class AwakeningEditorTarget : TargetRules
{
    public AwakeningEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.V5;
        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
        ExtraModuleNames.Add("Awakening");
    }
}
