"""Offline structural checks, NOT Unreal compilation, physics or visual QA."""

import ast
import configparser
import json
import math
from pathlib import Path
import sys
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "Awakening"
sys.path.insert(0, str(PROJECT / "Content/Python"))
from street_plan import MATERIALS, SPAWN, build_plan


class ScaffoldTests(unittest.TestCase):
    def test_project_and_targets_agree(self):
        project = json.loads((PROJECT / "Awakening.uproject").read_text(encoding="utf-8"))
        self.assertEqual(project["EngineAssociation"], "5.7")
        self.assertEqual(project["Modules"][0]["Name"], "Awakening")
        for target in (PROJECT / "Source").glob("*.Target.cs"):
            self.assertIn("Unreal5_7", target.read_text())

    def test_unreal_build_rules_are_not_dotnet_game_sources(self):
        project = ET.parse(ROOT.parent / "Probuzhdenie.csproj")
        self.assertTrue(any(item.attrib.get("Remove") == "unreal/**/*.cs" for item in project.iter("Compile")))

    def test_editor_plugins_are_not_runtime_dependencies(self):
        project = json.loads((PROJECT / "Awakening.uproject").read_text())
        for plugin in project["Plugins"]:
            if plugin["Name"] != "EnhancedInput":
                self.assertEqual(plugin["TargetAllowList"], ["Editor"])

    def test_python_syntax(self):
        for script in (PROJECT / "Content/Python").glob("*.py"):
            ast.parse(script.read_text(encoding="utf-8"), filename=str(script))

    def test_layout_is_deterministic_finite_and_bounded(self):
        plan = build_plan()
        self.assertEqual(plan, build_plan())
        self.assertLess(len(plan), 500)
        self.assertEqual(len(plan), len({box.name for box in plan}))
        for box in plan:
            self.assertTrue(all(math.isfinite(v) for v in (*box.center, *box.size)))
            self.assertTrue(all(v > 0 for v in box.size))
            self.assertIn(box.material, MATERIALS)

    def test_pbr_ranges(self):
        for color, roughness, metallic in MATERIALS.values():
            self.assertTrue(all(0 <= value <= 1 for value in (*color, roughness, metallic)))

    def test_carriageway_and_walkway_dimensions(self):
        plan = build_plan()
        walks = [box for box in plan if box.name.startswith("Sidewalk_")]
        self.assertEqual(len(walks), 2)
        self.assertEqual(walks[1].center[1] - walks[1].size[1] / 2, 600)
        for box in walks:
            self.assertEqual(box.size[1], 300)
            self.assertEqual(box.center[2] + box.size[2] / 2, 12)

    def test_spawn_and_walk_routes_have_capsule_clearance(self):
        # Static AABB clearance is only a design check, not a swept-physics test.
        plan = build_plan()
        positions = [SPAWN] + [(x, y, 103) for x in range(-5000, 5001, 100) for y in (-730, 730)]
        for position in positions:
            for box in plan:
                if not box.solid:
                    continue
                expanded = (box.size[0] / 2 + 34, box.size[1] / 2 + 34, box.size[2] / 2 + 90)
                self.assertFalse(all(abs(position[i] - box.center[i]) < expanded[i] for i in range(3)),
                                 (position, box.name))

    def test_solid_props_and_decorations_are_separated(self):
        for box in build_plan():
            decorative = "_Window_" in box.name or box.name.startswith("Lane_")
            self.assertEqual(box.solid, not decorative, box.name)

    def test_renderer_and_cook_configuration(self):
        config = configparser.ConfigParser(strict=False)
        config.read(PROJECT / "Config/DefaultEngine.ini")
        renderer = config["/Script/Engine.RendererSettings"]
        self.assertEqual(renderer["r.RayTracing"], "False")
        self.assertEqual(renderer["r.DynamicGlobalIlluminationMethod"], "1")
        self.assertEqual(renderer["r.AntiAliasingMethod"], "4")
        config.read(PROJECT / "Config/DefaultGame.ini")
        cook = config["/Script/UnrealEd.ProjectPackagingSettings"]
        self.assertIn("MeraStreet", cook["+MapsToCook"])
        self.assertIn("AwakeningPrototype", cook["+DirectoriesToAlwaysCook"])


if __name__ == "__main__":
    unittest.main()
