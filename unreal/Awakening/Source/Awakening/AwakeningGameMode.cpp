#include "AwakeningGameMode.h"
#include "AwakeningCharacter.h"

AAwakeningGameMode::AAwakeningGameMode()
{
    DefaultPawnClass = AAwakeningCharacter::StaticClass();
}

UClass* AAwakeningGameMode::GetDefaultPawnClassForController_Implementation(AController* InController)
{
    // Resolve at spawn: the editor bootstrap creates this asset after module load.
    if (UClass* Hero = LoadClass<APawn>(nullptr, TEXT("/Game/AwakeningPrototype/BP_Hero.BP_Hero_C"))) return Hero;
    return Super::GetDefaultPawnClassForController_Implementation(InController);
}
