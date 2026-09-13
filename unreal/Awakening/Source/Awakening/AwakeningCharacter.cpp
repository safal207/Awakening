#include "AwakeningCharacter.h"
#include "Camera/CameraComponent.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/LocalPlayer.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"

AAwakeningCharacter::AAwakeningCharacter()
{
    GetCapsuleComponent()->InitCapsuleSize(34.f, 90.f);
    GetCapsuleComponent()->SetCollisionProfileName(TEXT("Pawn"));
    bUseControllerRotationYaw = false;
    bUseControllerRotationPitch = false;
    bUseControllerRotationRoll = false;
    auto* Movement = GetCharacterMovement();
    Movement->bOrientRotationToMovement = true;
    Movement->RotationRate = FRotator(0.f, 420.f, 0.f);
    Movement->MaxWalkSpeed = 165.f;
    Movement->MaxAcceleration = 750.f;
    Movement->BrakingDecelerationWalking = 900.f;
    Movement->MaxStepHeight = 35.f;
    Movement->JumpZVelocity = 350.f;
    Movement->AirControl = .15f;
    Movement->bEnablePhysicsInteraction = true;
    GetMesh()->SetCollisionProfileName(TEXT("NoCollision"));
    GetMesh()->SetRelativeLocation(FVector(0.f, 0.f, -90.f));
    GetMesh()->SetRelativeRotation(FRotator(0.f, -90.f, 0.f));

    CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
    CameraBoom->SetupAttachment(GetRootComponent());
    CameraBoom->TargetArmLength = 320.f;
    CameraBoom->TargetOffset = FVector(0.f, 0.f, 55.f);
    CameraBoom->SocketOffset = FVector(0.f, 35.f, 0.f);
    CameraBoom->bUsePawnControlRotation = true;
    CameraBoom->bDoCollisionTest = true;
    CameraBoom->ProbeSize = 12.f;
    CameraBoom->bEnableCameraLag = true;
    CameraBoom->CameraLagSpeed = 12.f;
    CameraBoom->bUseCameraLagSubstepping = true;
    FollowCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FollowCamera"));
    FollowCamera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
    FollowCamera->FieldOfView = 75.f;
}

void AAwakeningCharacter::BeginPlay()
{
    Super::BeginPlay();
    if (!GetMesh()->GetSkeletalMeshAsset())
    {
        // Expose the collision proxy instead of presenting it as a finished human.
        GetCapsuleComponent()->SetHiddenInGame(false);
        UE_LOG(LogTemp, Warning, TEXT("Awakening: BP_Hero has no body. Assign a licensed rig and matching Animation Blueprint before hero visual QA."));
    }
}

void AAwakeningCharacter::PawnClientRestart()
{
    Super::PawnClientRestart();
    RemoveMapping();
    auto* Player = Cast<APlayerController>(GetController());
    if (!Player || !Player->GetLocalPlayer()) return;
    if (Player->PlayerCameraManager)
    {
        Player->PlayerCameraManager->ViewPitchMin = -65.f;
        Player->PlayerCameraManager->ViewPitchMax = 50.f;
    }
    auto* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(Player->GetLocalPlayer());
    if (Subsystem && Mapping)
    {
        Subsystem->AddMappingContext(Mapping, 0);
        InputSubsystem = Subsystem;
    }
}

void AAwakeningCharacter::RemoveMapping()
{
    if (InputSubsystem.IsValid() && Mapping) InputSubsystem->RemoveMappingContext(Mapping);
    InputSubsystem.Reset();
    SprintStop();
}

void AAwakeningCharacter::UnPossessed()
{
    RemoveMapping();
    Super::UnPossessed();
}

void AAwakeningCharacter::EndPlay(const EEndPlayReason::Type Reason)
{
    RemoveMapping();
    Super::EndPlay(Reason);
}

void AAwakeningCharacter::SetupPlayerInputComponent(UInputComponent* Input)
{
    Super::SetupPlayerInputComponent(Input);
    auto* Enhanced = CastChecked<UEnhancedInputComponent>(Input);
    // Keep the transient action graph alive for this pawn, never rebuild per frame.
    if (!Mapping)
    {
        Mapping = NewObject<UInputMappingContext>(this);
        const auto AddAxis = [this](FKey Positive, FKey Negative)
        {
            auto* Action = NewObject<UInputAction>(this);
            Action->ValueType = EInputActionValueType::Axis1D;
            Actions.Add(Action);
            Mapping->MapKey(Action, Positive);
            if (Negative.IsValid())
                Mapping->MapKey(Action, Negative).Modifiers.Add(NewObject<UInputModifierNegate>(Mapping));
        };
        AddAxis(EKeys::W, EKeys::S);
        AddAxis(EKeys::D, EKeys::A);
        AddAxis(EKeys::MouseX, FKey());
        AddAxis(EKeys::MouseY, FKey());
        AddAxis(EKeys::LeftShift, FKey());
        AddAxis(EKeys::SpaceBar, FKey());
        AddAxis(EKeys::C, FKey());
        AddAxis(EKeys::MouseWheelAxis, FKey());
    }
    Enhanced->BindAction(Actions[0], ETriggerEvent::Triggered, this, &AAwakeningCharacter::MoveForward);
    Enhanced->BindAction(Actions[1], ETriggerEvent::Triggered, this, &AAwakeningCharacter::MoveRight);
    Enhanced->BindAction(Actions[2], ETriggerEvent::Triggered, this, &AAwakeningCharacter::LookYaw);
    Enhanced->BindAction(Actions[3], ETriggerEvent::Triggered, this, &AAwakeningCharacter::LookPitch);
    Enhanced->BindAction(Actions[4], ETriggerEvent::Started, this, &AAwakeningCharacter::SprintStart);
    Enhanced->BindAction(Actions[4], ETriggerEvent::Completed, this, &AAwakeningCharacter::SprintStop);
    Enhanced->BindAction(Actions[4], ETriggerEvent::Canceled, this, &AAwakeningCharacter::SprintStop);
    Enhanced->BindAction(Actions[5], ETriggerEvent::Started, this, &ACharacter::Jump);
    Enhanced->BindAction(Actions[5], ETriggerEvent::Completed, this, &ACharacter::StopJumping);
    Enhanced->BindAction(Actions[5], ETriggerEvent::Canceled, this, &ACharacter::StopJumping);
    Enhanced->BindAction(Actions[6], ETriggerEvent::Started, this, &AAwakeningCharacter::Recenter);
    Enhanced->BindAction(Actions[7], ETriggerEvent::Triggered, this, &AAwakeningCharacter::Zoom);
}

void AAwakeningCharacter::MoveForward(const FInputActionValue& Value)
{
    if (Controller) AddMovementInput(FRotationMatrix(FRotator(0.f, Controller->GetControlRotation().Yaw, 0.f)).GetUnitAxis(EAxis::X), Value.Get<float>());
}

void AAwakeningCharacter::MoveRight(const FInputActionValue& Value)
{
    if (Controller) AddMovementInput(FRotationMatrix(FRotator(0.f, Controller->GetControlRotation().Yaw, 0.f)).GetUnitAxis(EAxis::Y), Value.Get<float>());
}

void AAwakeningCharacter::LookYaw(const FInputActionValue& Value) { AddControllerYawInput(Value.Get<float>() * .65f); }
void AAwakeningCharacter::LookPitch(const FInputActionValue& Value) { AddControllerPitchInput(-Value.Get<float>() * .65f); }
void AAwakeningCharacter::SprintStart() { GetCharacterMovement()->MaxWalkSpeed = 420.f; }
void AAwakeningCharacter::SprintStop() { GetCharacterMovement()->MaxWalkSpeed = 165.f; }

void AAwakeningCharacter::Recenter()
{
    if (Controller) Controller->SetControlRotation(FRotator(-10.f, GetActorRotation().Yaw, 0.f));
}

void AAwakeningCharacter::Zoom(const FInputActionValue& Value)
{
    CameraBoom->TargetArmLength = FMath::Clamp(CameraBoom->TargetArmLength - Value.Get<float>() * 30.f, 150.f, 500.f);
}
