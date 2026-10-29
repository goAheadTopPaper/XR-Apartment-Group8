import copy
import json
import unittest
from pathlib import Path

import prepare_asset as pipeline

ROOT = Path(__file__).resolve().parent


class AssetWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.single = pipeline.read_json(ROOT / "workflows/KitchenAssets_v1_Single_Pixal_Trellis.json")
        self.multi = pipeline.read_json(ROOT / "workflows/KitchenAssets_v1_Multiview_Pixal.json")
        self.manifest = pipeline.read_json(ROOT.parent.parent / "ArtSource/KitchenAssets/Manifests/plate_v001.json")
        self.manifest["reference_images"] = {"single": "plate_front.png"}
        self.preset = pipeline.read_json(ROOT / "presets/prop.json")

    def test_exported_graphs_have_no_dangling_links(self):
        for graph in (self.single, self.multi):
            self.assertGreater(pipeline.validate_graph(graph)["links"], 100)
            for image in [n for n in graph["nodes"] if n["type"] == "LoadImage"]:
                self.assertFalse(image["widgets_values"][0], "Reusable templates must not run the sample image")
                self.assertFalse(image["widgets_values_named"]["image"])

    def test_single_uses_pixal_and_all_controls_change_together(self):
        graph = pipeline.configure(self.single, self.manifest, self.preset, "draft")
        for title, value in (("Target Face Count", 50000), ("Texture Size", 1024),
                             ("Normal Map Size", 1024), ("AO Map Size", 512)):
            node = pipeline.find_one(graph, "PrimitiveInt", title)
            self.assertEqual(node["widgets_values"][0], value)
            self.assertEqual(node["widgets_values_named"]["value"], value)
        self.assertFalse(pipeline.find_one(graph, "PrimitiveBoolean")["widgets_values"][0])
        self.assertEqual(graph["extra"]["kitchen_asset"]["generation_status"], "not_run")
        self.assertEqual(graph["extra"]["kitchen_asset"]["design_review"], self.manifest["design_review"])

    def test_trellis_switch_and_stage_seeds_are_reproducible(self):
        graph = pipeline.configure(self.single, self.manifest, self.preset, model="trellis2")
        self.assertTrue(pipeline.find_one(graph, "PrimitiveBoolean")["widgets_values_named"]["value"])
        for stage in range(1, 5):
            node = pipeline.find_one(graph, "KSampler", f"Seed {stage}:")
            self.assertEqual(node["widgets_values"][:2], [271027 + stage, "fixed"])
            self.assertEqual(node["widgets_values_named"]["seed"], 271027 + stage)

    def test_canonical_templates_are_not_mutated(self):
        previous = copy.deepcopy(self.single)
        prepared = pipeline.configure(self.single, self.manifest, self.preset)
        self.assertEqual(self.single, previous)
        self.assertNotEqual(prepared["id"], self.single["id"])
        self.assertEqual(prepared["id"], pipeline.configure(self.single, self.manifest, self.preset)["id"])
        self.manifest["asset_name"] = "tomato"
        self.assertNotEqual(prepared["id"], pipeline.configure(self.single, self.manifest, self.preset)["id"])

    def test_multiview_four_inputs_share_crop_without_box_normalisation(self):
        self.manifest["input_mode"] = "multiview"
        self.manifest["reference_images"] = {v: f"plate_{v}.png" for v in ("front", "left", "back", "right")}
        self.manifest["multiview"] = {"shared_crop": [0, 0, 1536, 1536]}
        graph = pipeline.configure(self.multi, self.manifest, self.preset)
        for view in self.manifest["reference_images"]:
            node = pipeline.find_one(graph, "LoadImage", "Input: " + view)
            self.assertEqual(node["widgets_values_named"]["image"], f"plate_{view}.png")
        self.assertEqual(pipeline.find_one(graph, "PrimitiveBoundingBox")["widgets_values"], [0, 0, 1536, 1536])
        self.assertFalse(any(n["type"] == "ImageCropToMask" for n in graph["nodes"]))
        with self.assertRaisesRegex(ValueError, "Pixal3D only"):
            pipeline.configure(self.multi, self.manifest, self.preset, model="trellis2")

    def test_missing_reference_rejects_accidental_sample_axe_generation(self):
        self.manifest["reference_images"] = {}
        with self.assertRaisesRegex(ValueError, "reference_images"):
            pipeline.configure(self.single, self.manifest, self.preset)

    def test_manifest_and_template_modes_must_match(self):
        with self.assertRaisesRegex(ValueError, "input_mode"):
            pipeline.configure(self.multi, self.manifest, self.preset)

    def test_invalid_names_seeds_and_category_rejected(self):
        for key, value in (("asset_name", "../secrets"), ("seed", True), ("seed", 2**64),
                           ("version", 0), ("asset_class", "character")):
            recipe = copy.deepcopy(self.manifest)
            recipe[key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                pipeline.configure(self.single, recipe, self.preset)

    def test_dangling_or_mismatched_edge_rejected(self):
        for edit in (lambda g: g["links"].pop(), lambda g: g["links"][0].__setitem__(3, "missing"),
                     lambda g: g["nodes"].append(copy.deepcopy(g["nodes"][0]))):
            graph = copy.deepcopy(self.single)
            edit(graph)
            with self.assertRaises(ValueError):
                pipeline.validate_graph(graph)

    def test_remesh_values_match_pinned_official_template(self):
        official = pipeline.read_json(ROOT / "upstream/3d_pixal3d_trellis2_image_to_model.json")
        expected = pipeline.find_one(official, "RemeshMesh")["widgets_values_named"]
        for graph in (self.single, self.multi):
            actual = pipeline.find_one(graph, "RemeshMesh")
            self.assertEqual(actual["widgets_values_named"], expected)
            self.assertEqual(actual["widgets_values"], list(expected.values()))

    def test_all_asset_categories_have_three_quality_budgets(self):
        for name in ("prop", "character", "building"):
            preset = pipeline.read_json(ROOT / "presets" / (name + ".json"))
            self.assertEqual(set(preset["quality"]), {"draft", "standard", "high"})
            for quality in preset["quality"].values():
                self.assertLess(quality["game_triangles"], quality["master_faces"])
                self.assertLessEqual(quality["game_texture"], quality["master_texture"])

    def test_per_asset_budget_overrides_are_recorded_and_invalid_sizes_rejected(self):
        self.manifest["parameters"] = {"master_faces": 150000, "master_texture": 1024, "game_triangles": 3000}
        graph = pipeline.configure(self.single, self.manifest, self.preset)
        self.assertEqual(pipeline.find_one(graph, "PrimitiveInt", "Target Face Count")["widgets_values"][0], 150000)
        self.assertEqual(graph["extra"]["kitchen_asset"]["settings"]["game_triangles"], 3000)
        for overrides in ({"master_texture": 1023}, {"master_faces": -1}, {"unrecognised": 1}):
            self.manifest["parameters"] = overrides
            with self.assertRaises(ValueError):
                pipeline.configure(self.single, self.manifest, self.preset)


if __name__ == "__main__":
    unittest.main()
