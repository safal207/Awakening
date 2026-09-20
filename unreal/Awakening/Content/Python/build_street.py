"""Run through prepare.ps1 -BuildScene in a separate Unreal commandlet process."""

import json
import sys
from pathlib import Path

import unreal

sys.path.insert(0, str(Path(__file__).resolve().parent))
from street_plan import MATERIALS, SPAWN, build_plan

ROOT = "/Game/AwakeningPrototype"
MAP = ROOT + "/Maps/MeraStreet"


def require(value, message):
    if not value:
        raise RuntimeError(message)
    return value


def make_material(name, color, roughness, metallic):
    material = require(unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "M_" + name, ROOT + "/Materials", unreal.Material, unreal.MaterialFactoryNew()), name)
    library = unreal.MaterialEditingLibrary
    base = library.create_material_expression(material, unreal.MaterialExpressionConstant3Vector)
    base.set_editor_property("constant", unreal.LinearColor(*color, 1.0))
    require(library.connect_material_property(base, "", unreal.MaterialProperty.MP_BASE_COLOR), "Base color")
    for value, prop in ((roughness, unreal.MaterialProperty.MP_ROUGHNESS), (metallic, unreal.MaterialProperty.MP_METALLIC)):
        node = library.create_material_expression(material, unreal.MaterialExpressionConstant)
        node.set_editor_property("r", value)
        require(library.connect_material_property(node, "", prop), "Material input")
    library.recompile_material(material)
    require(unreal.EditorAssetLibrary.save_loaded_asset(material), "Save material")
    return material


def main():
    # Never discard an open map or overwrite artist-authored assets on a rerun.
    if unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages() or unreal.EditorLoadingAndSavingUtils.get_dirty_content_packages():
        raise RuntimeError("Unsaved editor work detected. Run prepare.ps1 -BuildScene in its separate process.")
    if unreal.EditorAssetLibrary.does_directory_exist(ROOT):
        raise RuntimeError("AwakeningPrototype already exists. Open and edit it; generation will not overwrite it.")
    hero_class = require(unreal.load_class(None, "/Script/Awakening.AwakeningCharacter"), "Build AwakeningEditor first")
    cube = require(unreal.load_asset("/Engine/BasicShapes/Cube"), "Missing engine cube")
    levels = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    require(levels.new_level(MAP), "Cannot create level")
    materials = {name: make_material(name, *values) for name, values in MATERIALS.items()}

    for box in build_plan():
        actor = require(actors.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(*box.center)), box.name)
        actor.set_actor_label(box.name)
        actor.set_folder_path("Street")
        component = actor.static_mesh_component
        component.set_static_mesh(cube)
        component.set_material(0, materials[box.material])
        component.set_collision_profile_name("BlockAll" if box.solid else "NoCollision")
        actor.set_actor_scale3d(unreal.Vector(*(value / 100.0 for value in box.size)))
        component.set_mobility(unreal.ComponentMobility.STATIC)

    sun = actors.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 3000), unreal.Rotator(-35, -28, 0))
    sunlight = sun.get_component_by_class(unreal.DirectionalLightComponent)
    sunlight.set_mobility(unreal.ComponentMobility.MOVABLE)
    sunlight.set_editor_property("atmosphere_sun_light", True)
    sunlight.set_editor_property("intensity", 75000.0)
    sunlight.set_editor_property("use_temperature", True)
    sunlight.set_editor_property("temperature", 5600.0)
    sunlight.set_editor_property("light_source_angle", 0.5357)
    actors.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector())
    sky = actors.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 1000))
    sky_component = sky.get_component_by_class(unreal.SkyLightComponent)
    sky_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    sky_component.set_editor_property("real_time_capture", True)
    fog = actors.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector())
    fog_component = fog.get_component_by_class(unreal.ExponentialHeightFogComponent)
    fog_component.set_editor_property("fog_density", 0.008)
    fog_component.set_editor_property("enable_volumetric_fog", True)

    volume = actors.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector())
    volume.set_editor_property("unbound", True)
    settings = volume.get_editor_property("settings")
    for name, value in (("auto_exposure_min_brightness", 13.0), ("auto_exposure_max_brightness", 13.0),
                        ("motion_blur_amount", 0.0), ("bloom_intensity", 0.15)):
        settings.set_editor_property("override_" + name, True)
        settings.set_editor_property(name, value)
    volume.set_editor_property("settings", settings)
    actors.spawn_actor_from_class(unreal.PlayerStart, unreal.Vector(*SPAWN))

    factory = unreal.BlueprintFactory()
    factory.set_editor_property("parent_class", hero_class)
    blueprint = require(unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "BP_Hero", ROOT, unreal.Blueprint, factory), "Cannot create hero Blueprint")
    unreal.BlueprintEditorLibrary.compile_blueprint(blueprint)
    require(unreal.EditorAssetLibrary.save_loaded_asset(blueprint), "Cannot save hero Blueprint")
    require(levels.save_current_level(), "Cannot save level")
    report = Path(unreal.Paths.project_saved_dir()) / "AwakeningBootstrap.json"
    report.write_text(json.dumps({"map": MAP, "boxes": len(build_plan()), "materials": len(materials),
                                  "hero_body": "NOT_ASSIGNED", "visual_qa": "NOT_RUN"}, indent=2), encoding="utf-8")
    unreal.log("Awakening blockout saved. BP_Hero still needs a licensed human rig and animation setup.")


if __name__ == "__main__":
    main()
