#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "AwakeningGameMode.generated.h"

UCLASS()
class AWAKENING_API AAwakeningGameMode : public AGameModeBase
{
    GENERATED_BODY()
public:
    AAwakeningGameMode();
    virtual UClass* GetDefaultPawnClassForController_Implementation(AController* InController) override;
};
