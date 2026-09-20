#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "InputActionValue.h"
#include "AwakeningCharacter.generated.h"

class UCameraComponent;
class USpringArmComponent;
class UInputMappingContext;
class UInputAction;

UCLASS(Blueprintable)
class AWAKENING_API AAwakeningCharacter : public ACharacter
{
    GENERATED_BODY()

public:
    AAwakeningCharacter();
    virtual void PawnClientRestart() override;
    virtual void UnPossessed() override;
    virtual void EndPlay(const EEndPlayReason::Type Reason) override;
    virtual void SetupPlayerInputComponent(UInputComponent* Input) override;

protected:
    virtual void BeginPlay() override;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Camera")
    TObjectPtr<USpringArmComponent> CameraBoom;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Camera")
    TObjectPtr<UCameraComponent> FollowCamera;

private:
    UPROPERTY(Transient) TObjectPtr<UInputMappingContext> Mapping;
    UPROPERTY(Transient) TArray<TObjectPtr<UInputAction>> Actions;
    TWeakObjectPtr<class UEnhancedInputLocalPlayerSubsystem> InputSubsystem;
    void RemoveMapping();
    void MoveForward(const FInputActionValue& Value);
    void MoveRight(const FInputActionValue& Value);
    void LookYaw(const FInputActionValue& Value);
    void LookPitch(const FInputActionValue& Value);
    void SprintStart();
    void SprintStop();
    void Recenter();
    void Zoom(const FInputActionValue& Value);
};
