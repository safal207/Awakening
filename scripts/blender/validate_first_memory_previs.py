"""Validate the First Memory Blender previs structure.

Usage:

    blender --background artifacts/previs/first-memory-previs.blend \
        --python scripts/blender/validate_first_memory_previs.py

Exits non-zero when required structural invariants are missing.
This validates scene structure only; it does not replace visual/story review.
"""

from __future__ import annotations

import sys
import bpy
from bpy_extras.object_utils import world_to_camera_view

EXPECTED_CAMERAS = [
    "CAM_01_ROUTINE",
    "CAM_02_ANOMALY",
    "CAM_03_FACE",
    "CAM_04_CHOICE",
    "CAM_05_MEETING",
    "CAM_06_RESET",
    "CAM_07_MEMORY",
    "CAM_08_TITLE",
]

EXPECTED_MARKERS = {
    "01_ROUTINE": 1,
    "02_ANOMALY": 97,
    "03_RECOGNITION": 193,
    "04_CHOICE": 289,
    "05_MEETING": 409,
    "06_SVERKA": 505,
    "07_MEMORY": 577,
    "08_TITLE": 649,
}

EXPECTED_OBJECTS = [
    "HERO",
    "LIDA",
    "MARK",
    "NPC_REPEAT",
    "TRAM",
    "TRAM_GHOST",
    "SIGNAL_GREEN",
    "MEMORY_NOTE",
]


def fail(message: str, failures: list[str]) -> None:
    failures.append(message)


def main() -> None:
    scene = bpy.context.scene
    failures: list[str] = []

    if scene.frame_start != 1:
        fail(f"frame_start={scene.frame_start}, expected 1", failures)
    if scene.frame_end != 672:
        fail(f"frame_end={scene.frame_end}, expected 672", failures)
    if scene.render.fps != 24:
        fail(f"fps={scene.render.fps}, expected 24", failures)
    if scene.render.resolution_x != 1920 or scene.render.resolution_y != 1080:
        fail(
            f"resolution={scene.render.resolution_x}x{scene.render.resolution_y}, expected 1920x1080",
            failures,
        )

    for name in EXPECTED_CAMERAS:
        obj = bpy.data.objects.get(name)
        if obj is None:
            fail(f"missing camera {name}", failures)
        elif obj.type != "CAMERA":
            fail(f"{name} exists but type={obj.type}, expected CAMERA", failures)

    markers = {m.name: m for m in scene.timeline_markers}
    for name, frame in EXPECTED_MARKERS.items():
        marker = markers.get(name)
        if marker is None:
            fail(f"missing timeline marker {name}", failures)
            continue
        if marker.frame != frame:
            fail(f"marker {name} frame={marker.frame}, expected {frame}", failures)
        if marker.camera is None:
            fail(f"marker {name} is not bound to a camera", failures)

    for name in EXPECTED_OBJECTS:
        if bpy.data.objects.get(name) is None:
            fail(f"missing object {name}", failures)

    # The opening and payoff must use the same focal length by design.
    cam1 = bpy.data.objects.get("CAM_01_ROUTINE")
    cam7 = bpy.data.objects.get("CAM_07_MEMORY")
    if cam1 and cam7 and abs(cam1.data.lens - cam7.data.lens) > 0.001:
        fail(f"opening/payoff lens mismatch: {cam1.data.lens} vs {cam7.data.lens}", failures)

    if scene.get("causal_chain") != "NOTICE>INVESTIGATE>CHOOSE>ACT>WITNESS>ANCHOR>SVERKA>CONSEQUENCE":
        fail("causal_chain scene metadata missing or changed", failures)

    # Object names alone do not prove that the camera follows the visible body.
    original_frame = scene.frame_current
    try:
        for frame in (1, 96, 193, 288, 409, 480, 577, 648):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            for name in ("HERO", "LIDA", "MARK"):
                root = bpy.data.objects.get(name)
                head = bpy.data.objects.get(name + "_HEAD")
                aim = bpy.data.objects.get(name + "_AIM")
                if not all((root, head, aim)):
                    fail(f"missing character transform objects for {name}", failures)
                    continue
                if root.matrix_world.to_scale().length < 0.01:
                    continue
                offset = head.matrix_world.translation - aim.matrix_world.translation
                if offset.length > 0.5:
                    fail(f"{name} head/aim separation {offset.length:.3f}m at frame {frame}", failures)
            if frame in (1, 96, 193, 288, 577, 648):
                head = bpy.data.objects.get("HERO_HEAD")
                if head and scene.camera:
                    p = world_to_camera_view(scene, scene.camera, head.matrix_world.translation)
                    if p.z <= 0 or not (0.05 <= p.x <= 0.95 and 0.05 <= p.y <= 0.95):
                        fail(f"hero head outside camera at frame {frame}: {tuple(p)}", failures)
    finally:
        scene.frame_set(original_frame)

    if failures:
        print("FIRST_MEMORY_PREVIS_VALIDATION=FAIL")
        for item in failures:
            print(f"- {item}")
        raise SystemExit(1)

    print("FIRST_MEMORY_PREVIS_VALIDATION=PASS")
    print(f"cameras={len(EXPECTED_CAMERAS)} markers={len(EXPECTED_MARKERS)} required_objects={len(EXPECTED_OBJECTS)}")


if __name__ == "__main__":
    main()
