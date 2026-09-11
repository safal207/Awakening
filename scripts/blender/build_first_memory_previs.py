"""Build the reproducible gray-box previs for Awakening: First Memory.

Blender target: 4.x (tested design target: 4.5 LTS).
No external assets are required.

Usage from repository root:

    blender --background --python scripts/blender/build_first_memory_previs.py -- \
        --output artifacts/previs/first-memory-previs.blend

Optional render (slow, 672 frames):

    blender --background --python scripts/blender/build_first_memory_previs.py -- \
        --output artifacts/previs/first-memory-previs.blend \
        --render artifacts/previs/first-memory-previs.mp4

The script intentionally builds a simple gray-box. Its job is to validate story,
blocking, timing and camera grammar before final assets or paid video generation.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector

FPS = 24
FRAME_START = 1
FRAME_END = 672

SHOT_STARTS = {
    "01_ROUTINE": 1,
    "02_ANOMALY": 97,
    "03_RECOGNITION": 193,
    "04_CHOICE": 289,
    "05_MEETING": 409,
    "06_SVERKA": 505,
    "07_MEMORY": 577,
    "08_TITLE": 649,
}


# -----------------------------------------------------------------------------
# CLI
# -----------------------------------------------------------------------------


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="artifacts/previs/first-memory-previs.blend",
        help="Output .blend path (relative to current working directory if not absolute).",
    )
    parser.add_argument(
        "--render",
        default=None,
        help="Optional output .mp4 path. If supplied, render the full animation.",
    )
    return parser.parse_args(argv)


# -----------------------------------------------------------------------------
# Scene helpers
# -----------------------------------------------------------------------------


def clean_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def ensure_collection(name: str) -> bpy.types.Collection:
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)


def material(name: str, rgba: tuple[float, float, float, float]) -> bpy.types.Material:
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = rgba
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = 0.72
    return mat


def emission_material(name: str, rgba: tuple[float, float, float, float], strength: float = 1.0) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    em = nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = rgba
    em.inputs["Strength"].default_value = strength
    links.new(em.outputs["Emission"], out.inputs["Surface"])
    return mat


def cube(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    mat: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    move_to_collection(obj, collection)
    return obj


def sphere(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    mat: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    move_to_collection(obj, collection)
    return obj


def plane(
    name: str,
    location: tuple[float, float, float],
    size: tuple[float, float],
    mat: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_plane_add(size=2, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0] * 0.5, size[1] * 0.5, 1)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    move_to_collection(obj, collection)
    return obj


def empty(name: str, location: tuple[float, float, float], collection: bpy.types.Collection) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.25
    obj.location = location
    collection.objects.link(obj)
    return obj


def set_key(obj: bpy.types.Object, frame: int, location=None, rotation_z=None, scale=None) -> None:
    if location is not None:
        obj.location = location
        obj.keyframe_insert(data_path="location", frame=frame)
    if rotation_z is not None:
        obj.rotation_euler[2] = math.radians(rotation_z)
        obj.keyframe_insert(data_path="rotation_euler", index=2, frame=frame)
    if scale is not None:
        obj.scale = scale
        obj.keyframe_insert(data_path="scale", frame=frame)


def set_linear(obj: bpy.types.Object) -> None:
    if obj.animation_data and obj.animation_data.action:
        for fcurve in obj.animation_data.action.fcurves:
            for point in fcurve.keyframe_points:
                point.interpolation = "LINEAR"


def set_bezier(obj: bpy.types.Object) -> None:
    if obj.animation_data and obj.animation_data.action:
        for fcurve in obj.animation_data.action.fcurves:
            for point in fcurve.keyframe_points:
                point.interpolation = "BEZIER"


# -----------------------------------------------------------------------------
# Character proxies
# -----------------------------------------------------------------------------


def build_character(
    name: str,
    location: tuple[float, float, float],
    body_mat: bpy.types.Material,
    skin_mat: bpy.types.Material,
    collection: bpy.types.Collection,
) -> dict[str, bpy.types.Object]:
    root = empty(name, location, collection)

    body = cube(f"{name}_BODY", (0, 0, 0.95), (0.55, 0.34, 1.1), body_mat, collection)
    head = sphere(f"{name}_HEAD", (0, 0, 1.72), 0.24, skin_mat, collection)
    left_leg = cube(f"{name}_LEG_L", (-0.16, 0, 0.35), (0.18, 0.22, 0.7), body_mat, collection)
    right_leg = cube(f"{name}_LEG_R", (0.16, 0, 0.35), (0.18, 0.22, 0.7), body_mat, collection)

    for child in (body, head, left_leg, right_leg):
        child.parent = root
        child.matrix_parent_inverse = root.matrix_world.inverted()

    aim = empty(f"{name}_AIM", (0, 0, 1.4), collection)
    aim.parent = root

    return {
        "root": root,
        "body": body,
        "head": head,
        "aim": aim,
    }


# -----------------------------------------------------------------------------
# Camera helpers
# -----------------------------------------------------------------------------


def add_camera(
    name: str,
    location: tuple[float, float, float],
    lens: float,
    target: bpy.types.Object,
    collection: bpy.types.Collection,
    orthographic: bool = False,
    ortho_scale: float = 10.0,
) -> bpy.types.Object:
    data = bpy.data.cameras.new(name)
    data.lens = lens
    if orthographic:
        data.type = "ORTHO"
        data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new(name, data)
    cam.location = location
    collection.objects.link(cam)

    con = cam.constraints.new(type="TRACK_TO")
    con.target = target
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"
    return cam


def bind_camera_marker(name: str, frame: int, camera: bpy.types.Object) -> None:
    marker = bpy.context.scene.timeline_markers.new(name, frame=frame)
    marker.camera = camera


# -----------------------------------------------------------------------------
# Environment
# -----------------------------------------------------------------------------


def build_environment(mats: dict[str, bpy.types.Material], col: bpy.types.Collection) -> dict[str, bpy.types.Object]:
    plane("GROUND", (0, 0, 0), (48, 52), mats["ground"], col)

    # Tram corridor and pedestrian crossing area.
    cube("TRAM_BED", (0, 3.5, 0.035), (34, 3.0, 0.07), mats["road"], col)
    cube("PLAZA", (-3, 4, 0.05), (8, 7, 0.1), mats["plaza"], col)
    cube("SIDEWALK_MAIN", (2, -8, 0.06), (8, 22, 0.12), mats["plaza"], col)

    # Buildings kept intentionally simple.
    buildings = [
        (-10, -7, 6, 8, 7),
        (10, -6, 7, 8, 10),
        (-11, 10, 6, 7, 8),
        (10, 10, 8, 7, 6),
    ]
    for idx, (x, y, w, d, h) in enumerate(buildings, start=1):
        cube(f"BUILDING_{idx:02d}", (x, y, h / 2), (w, d, h), mats["building"], col)

    # Stop and signal.
    cube("TRAM_STOP_BACK", (0, 0.7, 1.2), (3.2, 0.18, 2.4), mats["stop"], col)
    cube("TRAM_STOP_ROOF", (0, 0.5, 2.45), (3.5, 1.3, 0.12), mats["stop"], col)
    cube("SIGNAL_POLE", (2, -1, 1.3), (0.12, 0.12, 2.6), mats["dark"], col)
    signal_green = sphere("SIGNAL_GREEN", (2, -1, 2.25), 0.12, mats["signal_green"], col)

    # Persistent memory trace.
    note = cube("MEMORY_NOTE", (0.4, 0.8, 0.88), (0.32, 0.02, 0.22), mats["memory"], col)
    note.rotation_euler = (math.radians(86), 0, math.radians(7))

    # Tram and subtle ghost duplicate.
    tram = cube("TRAM", (13, 3.5, 1.2), (6.5, 2.1, 2.4), mats["tram"], col)
    ghost = cube("TRAM_GHOST", (11.7, 3.5, 1.2), (6.5, 2.1, 2.4), mats["ghost"], col)
    set_key(ghost, 1, scale=(0, 0, 0))
    set_key(ghost, 141, scale=(0, 0, 0))
    set_key(ghost, 142, scale=(1, 1, 1))
    set_key(ghost, 145, scale=(1, 1, 1))
    set_key(ghost, 146, scale=(0, 0, 0))

    # Signal failure: briefly disappears / returns instead of a digital glitch overlay.
    set_key(signal_green, 1, scale=(1, 1, 1))
    set_key(signal_green, 118, scale=(1, 1, 1))
    set_key(signal_green, 122, scale=(0.15, 0.15, 0.15))
    set_key(signal_green, 126, scale=(1, 1, 1))
    set_key(signal_green, 132, scale=(0.15, 0.15, 0.15))
    set_key(signal_green, 145, scale=(1, 1, 1))

    # A few street markers to make camera motion legible.
    for y in (-15, -10, -5, 0, 5, 10):
        cube(f"BOLLARD_{y:+03d}", (5.5, y, 0.45), (0.18, 0.18, 0.9), mats["dark"], col)

    return {"tram": tram, "ghost": ghost, "note": note, "signal": signal_green}


# -----------------------------------------------------------------------------
# Animation
# -----------------------------------------------------------------------------


def animate_story(chars: dict[str, dict[str, bpy.types.Object]], env: dict[str, bpy.types.Object]) -> None:
    hero = chars["HERO"]
    lida = chars["LIDA"]
    mark = chars["MARK"]
    repeat = chars["NPC_REPEAT"]

    # HERO — routine/anomaly.
    set_key(hero["root"], 1, location=(4.0, -18.0, 0), rotation_z=8)
    set_key(hero["root"], 96, location=(2.1, -5.0, 0), rotation_z=3)
    set_key(hero["root"], 140, location=(1.5, -2.8, 0), rotation_z=2)
    set_key(hero["root"], 168, location=(1.3, -2.2, 0), rotation_z=2)
    set_key(hero["root"], 288, location=(1.3, -2.2, 0), rotation_z=2)

    # Choice shot: hero starts by the signal, then walks toward Lida.
    set_key(hero["root"], 289, location=(1.8, -1.3, 0), rotation_z=185)
    set_key(hero["root"], 365, location=(1.8, -1.3, 0), rotation_z=185)
    set_key(hero["root"], 390, location=(1.4, -1.0, 0), rotation_z=215)
    set_key(hero["root"], 408, location=(-0.7, 0.0, 0), rotation_z=215)
    set_key(hero["root"], 504, location=(-0.7, 0.0, 0), rotation_z=215)

    # Following morning resets the hero to the familiar route.
    set_key(hero["root"], 576, location=(4.0, -18.0, 0), rotation_z=8)
    set_key(hero["root"], 577, location=(4.0, -18.0, 0), rotation_z=8)
    set_key(hero["root"], 648, location=(2.1, -5.0, 0), rotation_z=3)
    set_linear(hero["root"])

    # Head reaction. Body stays restrained.
    hero["head"].rotation_euler[2] = 0
    hero["head"].keyframe_insert(data_path="rotation_euler", index=2, frame=130)
    hero["head"].rotation_euler[2] = math.radians(-22)
    hero["head"].keyframe_insert(data_path="rotation_euler", index=2, frame=168)
    hero["head"].rotation_euler[2] = math.radians(-32)
    hero["head"].keyframe_insert(data_path="rotation_euler", index=2, frame=250)
    hero["head"].rotation_euler[2] = 0
    hero["head"].keyframe_insert(data_path="rotation_euler", index=2, frame=290)
    hero["head"].rotation_euler[2] = math.radians(-16)
    hero["head"].keyframe_insert(data_path="rotation_euler", index=2, frame=640)
    set_bezier(hero["head"])

    # Lida remains near the stop. A slight body turn marks recognition beats.
    set_key(lida["root"], 1, location=(-2.0, 1.0, 0), rotation_z=30)
    set_key(lida["root"], 230, location=(-2.0, 1.0, 0), rotation_z=110)
    set_key(lida["root"], 504, location=(-2.0, 1.0, 0), rotation_z=70)
    set_key(lida["root"], 577, location=(-2.0, 1.0, 0), rotation_z=30)
    set_key(lida["root"], 615, location=(-2.0, 1.0, 0), rotation_z=70)
    set_linear(lida["root"])

    # Mark enters only in the meeting; his next-morning path echoes it.
    set_key(mark["root"], 1, location=(-8.0, 8.0, 0), scale=(0, 0, 0))
    set_key(mark["root"], 408, location=(-8.0, 8.0, 0), scale=(0, 0, 0))
    set_key(mark["root"], 409, location=(-8.0, 8.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_key(mark["root"], 480, location=(-3.0, 3.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_key(mark["root"], 504, location=(-3.0, 3.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_key(mark["root"], 576, location=(-8.0, 8.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_key(mark["root"], 610, location=(-5.0, 5.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_key(mark["root"], 635, location=(-3.0, 3.0, 0), scale=(1, 1, 1), rotation_z=145)
    set_linear(mark["root"])

    # Repeating pedestrian movement in Shot 2.
    set_key(repeat["root"], 1, location=(-4.0, -1.0, 0), scale=(0, 0, 0))
    set_key(repeat["root"], 96, location=(-4.0, -1.0, 0), scale=(0, 0, 0))
    set_key(repeat["root"], 97, location=(-4.0, -1.0, 0), scale=(1, 1, 1), rotation_z=90)
    set_key(repeat["root"], 125, location=(-4.0, -1.0, 0))
    set_key(repeat["root"], 138, location=(-2.5, -1.0, 0))
    set_key(repeat["root"], 143, location=(-3.4, -1.0, 0))
    set_key(repeat["root"], 150, location=(-2.2, -1.0, 0))
    set_key(repeat["root"], 192, location=(1.0, -1.0, 0))
    set_key(repeat["root"], 193, scale=(0, 0, 0))
    set_linear(repeat["root"])

    # Tram movement cues and reset.
    tram = env["tram"]
    set_key(tram, 1, location=(13, 3.5, 1.2))
    set_key(tram, 142, location=(10, 3.5, 1.2))
    set_key(tram, 335, location=(16, 3.5, 1.2))
    set_key(tram, 408, location=(6.5, 3.5, 1.2))
    set_key(tram, 504, location=(4.5, 3.5, 1.2))
    set_key(tram, 515, location=(4.5, 3.5, 1.2))
    set_key(tram, 545, location=(13, 3.5, 1.2))
    set_key(tram, 648, location=(13, 3.5, 1.2))
    set_linear(tram)

    # Note persists. A tiny tilt late in Sverka implies wind without turning it
    # into a magical collectible.
    note = env["note"]
    note.keyframe_insert(data_path="rotation_euler", frame=505)
    note.rotation_euler[1] += math.radians(5)
    note.keyframe_insert(data_path="rotation_euler", frame=570)
    note.rotation_euler[1] -= math.radians(5)
    note.keyframe_insert(data_path="rotation_euler", frame=576)


# -----------------------------------------------------------------------------
# Lighting
# -----------------------------------------------------------------------------


def build_lighting(col: bpy.types.Collection) -> dict[str, object]:
    scene = bpy.context.scene
    world = bpy.data.worlds.new("MERA_WORLD") if not bpy.data.worlds.get("MERA_WORLD") else bpy.data.worlds["MERA_WORLD"]
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.48, 0.58, 0.72, 1)
    bg.inputs["Strength"].default_value = 0.55

    sun_data = bpy.data.lights.new("SUN_MORNING", type="SUN")
    sun_data.energy = 2.0
    sun = bpy.data.objects.new("SUN_MORNING", sun_data)
    col.objects.link(sun)
    sun.rotation_euler = (math.radians(38), 0, math.radians(-35))

    # Morning until the reset cut.
    bg.inputs["Color"].keyframe_insert("default_value", frame=1)
    bg.inputs["Strength"].keyframe_insert("default_value", frame=1)
    sun_data.keyframe_insert("energy", frame=1)

    bg.inputs["Color"].default_value = (0.035, 0.045, 0.07, 1)
    bg.inputs["Strength"].default_value = 0.18
    sun_data.energy = 0.15
    bg.inputs["Color"].keyframe_insert("default_value", frame=505)
    bg.inputs["Strength"].keyframe_insert("default_value", frame=505)
    sun_data.keyframe_insert("energy", frame=505)

    # Gradual return toward dawn through Sverka.
    bg.inputs["Color"].default_value = (0.48, 0.58, 0.72, 1)
    bg.inputs["Strength"].default_value = 0.55
    sun_data.energy = 2.0
    bg.inputs["Color"].keyframe_insert("default_value", frame=576)
    bg.inputs["Strength"].keyframe_insert("default_value", frame=576)
    sun_data.keyframe_insert("energy", frame=576)

    return {"world_bg": bg, "sun": sun, "sun_data": sun_data}


# -----------------------------------------------------------------------------
# Cameras and title card
# -----------------------------------------------------------------------------


def build_cameras(chars: dict[str, dict[str, bpy.types.Object]], mats, col: bpy.types.Collection) -> dict[str, bpy.types.Object]:
    hero = chars["HERO"]

    cam1 = add_camera("CAM_01_ROUTINE", (7.5, -22.5, 2.4), 35, hero["aim"], col)
    set_key(cam1, 1, location=(7.5, -22.5, 2.4))
    set_key(cam1, 96, location=(5.5, -9.5, 2.2))
    set_bezier(cam1)

    cam2 = add_camera("CAM_02_ANOMALY", (5.5, -9.5, 2.2), 40, hero["aim"], col)
    set_key(cam2, 97, location=(5.5, -9.5, 2.2))
    set_key(cam2, 192, location=(4.8, -2.8, 2.1))
    set_bezier(cam2)

    cam3 = add_camera("CAM_03_FACE", (2.8, -0.8, 1.75), 65, hero["aim"], col)
    set_key(cam3, 193, location=(2.8, -0.8, 1.75))
    set_key(cam3, 288, location=(2.5, -0.55, 1.75))
    set_bezier(cam3)

    choice_target = empty("TARGET_CHOICE", (0, 0.4, 1.05), col)
    cam4 = add_camera("CAM_04_CHOICE", (6, -5, 3.0), 42, choice_target, col)

    meeting_target = empty("TARGET_MEETING", (-0.7, 0.0, 1.1), col)
    set_key(meeting_target, 409, location=(-0.7, 0.0, 1.1))
    set_key(meeting_target, 504, location=(-2.5, 2.0, 1.1))
    set_bezier(meeting_target)
    cam5 = add_camera("CAM_05_MEETING", (2, -5, 2.0), 40, meeting_target, col)
    set_key(cam5, 409, location=(2, -5, 2.0))
    set_key(cam5, 504, location=(5, -1, 2.1))
    set_bezier(cam5)

    reset_target = empty("TARGET_SVERKA", (0, 2.0, 0.0), col)
    cam6 = add_camera("CAM_06_RESET", (0, -5, 18), 32, reset_target, col)
    set_key(cam6, 505, location=(0, -5, 18))
    set_key(cam6, 576, location=(0, -5, 14))
    set_bezier(cam6)

    cam7 = add_camera("CAM_07_MEMORY", (7.5, -22.5, 2.4), 35, hero["aim"], col)
    set_key(cam7, 577, location=(7.5, -22.5, 2.4))
    set_key(cam7, 648, location=(5.5, -9.5, 2.2))
    set_bezier(cam7)

    # Title card: overhead orthographic camera, independent of world.
    title_target = empty("TARGET_TITLE", (0, 0, 30), col)
    cam8 = add_camera("CAM_08_TITLE", (0, 0, 40), 50, title_target, col, orthographic=True, ortho_scale=9.0)

    title_col = ensure_collection("TITLE_CARD")
    black = material("MAT_TITLE_BLACK", (0.002, 0.002, 0.002, 1))
    white = emission_material("MAT_TITLE_WHITE", (0.92, 0.95, 1.0, 1), 1.2)
    plane("TITLE_BLACK", (0, 0, 30), (16, 9), black, title_col)

    def title_text(name: str, body: str, y: float, size: float):
        bpy.ops.object.text_add(location=(0, y, 30.03))
        txt = bpy.context.object
        txt.name = name
        txt.data.body = body
        txt.data.align_x = "CENTER"
        txt.data.align_y = "CENTER"
        txt.data.size = size
        txt.data.extrude = 0
        txt.data.materials.append(white)
        move_to_collection(txt, title_col)
        return txt

    title_text("TITLE_MAIN", "ПРОБУЖДЕНИЕ", 0.65, 0.72)
    title_text("TITLE_TAG", "ГОРОД ЗАБЫВАЕТ.  ЛЮДИ МОГУТ ПОМНИТЬ.", -0.55, 0.25)

    cameras = {
        "01_ROUTINE": cam1,
        "02_ANOMALY": cam2,
        "03_RECOGNITION": cam3,
        "04_CHOICE": cam4,
        "05_MEETING": cam5,
        "06_SVERKA": cam6,
        "07_MEMORY": cam7,
        "08_TITLE": cam8,
    }

    for shot, start in SHOT_STARTS.items():
        bind_camera_marker(shot, start, cameras[shot])

    bpy.context.scene.camera = cam1
    return cameras


# -----------------------------------------------------------------------------
# QA metadata
# -----------------------------------------------------------------------------


def add_metadata(scene: bpy.types.Scene) -> None:
    scene["awakening_previs"] = "First Memory"
    scene["purpose"] = "story/blocking/camera validation before final render"
    scene["causal_chain"] = "NOTICE>INVESTIGATE>CHOOSE>ACT>WITNESS>ANCHOR>SVERKA>CONSEQUENCE"
    scene["viewer_test_gate"] = "PASS if >=2/3 viewers infer reset + hero-caused meeting + persistent human memory"
    scene["non_goal"] = "not final trailer; not proof gameplay mechanics are implemented"


# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------


def main() -> None:
    args = parse_args()
    clean_scene()

    scene = bpy.context.scene
    scene.frame_start = FRAME_START
    scene.frame_end = FRAME_END
    scene.render.fps = FPS
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.engine = "BLENDER_EEVEE_NEXT"

    world_col = ensure_collection("WORLD")
    actor_col = ensure_collection("ACTORS")
    camera_col = ensure_collection("CAMERAS")
    light_col = ensure_collection("LIGHTS")

    mats = {
        "ground": material("MAT_GROUND", (0.30, 0.30, 0.31, 1)),
        "road": material("MAT_ROAD", (0.16, 0.17, 0.18, 1)),
        "plaza": material("MAT_PLAZA", (0.43, 0.43, 0.44, 1)),
        "building": material("MAT_BUILDING", (0.48, 0.49, 0.50, 1)),
        "stop": material("MAT_STOP", (0.36, 0.38, 0.40, 1)),
        "dark": material("MAT_DARK", (0.08, 0.09, 0.10, 1)),
        "hero": material("MAT_HERO", (0.25, 0.42, 0.58, 1)),
        "lida": material("MAT_LIDA", (0.60, 0.40, 0.32, 1)),
        "mark": material("MAT_MARK", (0.30, 0.48, 0.32, 1)),
        "ped": material("MAT_PED", (0.42, 0.42, 0.43, 1)),
        "skin": material("MAT_SKIN", (0.72, 0.57, 0.48, 1)),
        "tram": material("MAT_TRAM", (0.35, 0.35, 0.38, 1)),
        "ghost": material("MAT_GHOST", (0.66, 0.72, 0.78, 1)),
        "memory": material("MAT_MEMORY", (0.35, 0.75, 0.85, 1)),
        "signal_green": emission_material("MAT_SIGNAL_GREEN", (0.15, 0.75, 0.25, 1), 2.0),
    }

    env = build_environment(mats, world_col)

    chars = {
        "HERO": build_character("HERO", (4, -18, 0), mats["hero"], mats["skin"], actor_col),
        "LIDA": build_character("LIDA", (-2, 1, 0), mats["lida"], mats["skin"], actor_col),
        "MARK": build_character("MARK", (-8, 8, 0), mats["mark"], mats["skin"], actor_col),
        "NPC_REPEAT": build_character("NPC_REPEAT", (-4, -1, 0), mats["ped"], mats["skin"], actor_col),
    }

    animate_story(chars, env)
    build_lighting(light_col)
    build_cameras(chars, mats, camera_col)
    add_metadata(scene)

    # Keep cuts readable in viewport/render.
    scene.frame_set(1)

    output = Path(args.output)
    if not output.is_absolute():
        output = Path.cwd() / output
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print(f"PREVIS_BLEND={output}")

    if args.render:
        render_path = Path(args.render)
        if not render_path.is_absolute():
            render_path = Path.cwd() / render_path
        render_path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(render_path)
        scene.render.image_settings.file_format = "FFMPEG"
        scene.render.ffmpeg.format = "MPEG4"
        scene.render.ffmpeg.codec = "H264"
        scene.render.ffmpeg.constant_rate_factor = "MEDIUM"
        bpy.ops.render.render(animation=True)
        print(f"PREVIS_VIDEO={render_path}")


if __name__ == "__main__":
    main()
