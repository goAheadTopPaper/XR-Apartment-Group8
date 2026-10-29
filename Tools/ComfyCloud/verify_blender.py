"""Run with Blender --background --factory-startup --python-exit-code 1 --python this_file.

These are algorithm fixtures, never Cloud-generation samples.
"""
import json
import sys
import unittest
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_postprocess as pipeline


class BlenderPipelineTests(unittest.TestCase):
    def setUp(self):
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        self.directory = Path("Logs/KitchenAssetPipeline/pbr_fixture").resolve()
        self.directory.mkdir(parents=True, exist_ok=True)

    def cube(self):
        bpy.ops.mesh.primitive_cube_add(size=.2)
        return bpy.context.object

    def test_prefracture_preserves_volume_materials_and_shared_origin(self):
        source = self.cube()
        material = bpy.data.materials.new("Exterior_UV_Test")
        source.data.materials.append(material)
        pieces, error = pipeline.make_pieces(source, {"kind": "fracture", "fragment_count": 8}, 45, .2)
        self.assertEqual(len(pieces), 8)
        self.assertLess(error, .0001)
        for piece in pieces:
            self.assertEqual(piece.location, Vector((0, 0, 0)))
            self.assertTrue(pipeline.mesh_stats(piece)["closed"])
            self.assertEqual(piece.data.materials[0], material)
            self.assertTrue(any(p.material_index == 1 for p in piece.data.polygons))
            self.assertTrue(any(p.material_index == 0 for p in piece.data.polygons))
            self.assertIsNotNone(piece.data.uv_layers.active)

    def test_six_slice_recipe_produces_six_closed_pieces(self):
        pieces, error = pipeline.make_pieces(self.cube(), {"kind": "slice", "pieces": 6}, 99, .2)
        self.assertEqual(len(pieces), 6)
        self.assertLess(error, .0001)
        self.assertTrue(all(pipeline.mesh_stats(p)["closed"] for p in pieces))

    def test_egg_halves_split_across_equator_and_keep_shell_opening(self):
        source = self.cube()
        shell = source.modifiers.new("Diagnostic shell", "SOLIDIFY")
        shell.thickness = .002
        bpy.context.view_layer.objects.active = source
        bpy.ops.object.modifier_apply(modifier=shell.name)
        pieces, error = pipeline.make_pieces(source, {"kind": "egg", "shell_halves": 2}, 99, .2)
        self.assertLess(error, .0001)
        lo0, hi0 = pipeline.bounds([pieces[0]])
        lo1, hi1 = pipeline.bounds([pieces[1]])
        self.assertAlmostEqual(hi0.z, 0, places=5)
        self.assertAlmostEqual(lo1.z, 0, places=5)
        self.assertLess(lo0.z, -.09)
        self.assertGreater(hi1.z, .09)

    def test_dense_convex_shape_gets_a_small_convex_mesh_not_coarse_box(self):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=.1)
        hull, method = pipeline.make_hull(bpy.context.object, "COL_Test")
        self.assertEqual(method, "simplified_convex_hull")
        self.assertLessEqual(len(hull.data.polygons), 200)
        self.assertGreater(len(hull.data.polygons), 12)
        self.assertTrue(pipeline.mesh_stats(hull)["closed"])

    def test_open_mesh_is_rejected_instead_of_auto_filling_unknown_holes(self):
        bpy.ops.mesh.primitive_plane_add(size=.2)
        with self.assertRaisesRegex(ValueError, "closed solid"):
            pipeline.make_pieces(bpy.context.object, {"kind": "fracture", "fragment_count": 8}, 99, .2)

    def test_normalisation_sets_bottom_centre_and_requested_span(self):
        source = self.cube()
        source.location = Vector((4, 5, 6))
        # load_source normally bakes import transforms before normalise.
        source.data.transform(source.matrix_world)
        source.location = Vector((0, 0, 0))
        pipeline.normalise([source], .27)
        lo, hi = pipeline.bounds([source])
        self.assertAlmostEqual(max(hi - lo), .27, places=5)
        self.assertAlmostEqual(lo.z, 0, places=5)
        self.assertAlmostEqual((lo.x + hi.x) / 2, 0, places=5)
        self.assertAlmostEqual((lo.y + hi.y) / 2, 0, places=5)

    def test_standard_packing_retains_linear_data_and_inverts_roughness(self):
        raw = self.directory / "input"
        output = self.directory / "output"
        raw.mkdir(exist_ok=True)
        output.mkdir(exist_ok=True)
        values = {"basecolor": (.25, .5, .75, 1), "metallic": (.25, .25, .25, 1),
                  "roughness": (.75, .75, .75, 1), "normal": (.5, .5, 1, 1), "ao": (.9, .9, .9, 1)}
        for name, value in values.items():
            image = bpy.data.images.new("Diagnostic_" + name, width=16, height=16)
            image.colorspace_settings.name = "sRGB" if name == "basecolor" else "Non-Color"
            image.pixels.foreach_set(list(value) * (16 * 16))
            image.filepath_raw = str(raw / (name + ".png"))
            image.file_format = "PNG"
            image.save()
        paths = pipeline.prepare_pbr(raw, output, 32)
        self.assertEqual(set(paths), set(values) | {"metallic_smoothness"})
        image = bpy.data.images.load(str(output / paths["metallic_smoothness"]), check_existing=False)
        image.colorspace_settings.name = "Non-Color"
        pixel = tuple(image.pixels[:4])
        self.assertAlmostEqual(pixel[0], .25, delta=.005)
        self.assertAlmostEqual(pixel[3], .25, delta=.005)
        (self.directory / "pixel_check.json").write_text(json.dumps({"expected_metallic": .25,
            "expected_smoothness": .25, "actual_rgba": pixel, "diagnostic_fixture": True}, indent=2))


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(BlenderPipelineTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise RuntimeError("Blender pipeline verification failed")
