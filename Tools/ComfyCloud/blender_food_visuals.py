"""Prepare actual Pixal3D food meshes for the existing recipe's actors.

No cloud requests. Preserve source GLBs and interaction/collision geometry.
Tomato visual cuts derive from the generated seeded slice; cooked egg uses the
generated folded curd. Run with Blender 5.2 --background --factory-startup.
"""
import hashlib
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_postprocess as core

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "ArtSource/KitchenAssets/Recipe/v002_food_visuals"


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def source(name):
    path = ROOT / f"ArtSource/KitchenAssets/{name}/v001/prepared/{name}_game.glb"
    meshes = core.load_source(path)
    visual = next(m for m in meshes if m.name.startswith("VIS_"))
    for mesh in meshes:
        if mesh != visual:
            bpy.data.objects.remove(mesh, do_unlink=True)
    bm = bmesh.new()
    bm.from_mesh(visual.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-8)
    bm.to_mesh(visual.data)
    bm.free()
    visual.data.update()
    return visual


def fit(obj, target):
    lo, hi = core.bounds([obj])
    target_lo, target_hi = target
    before = hi - lo
    scale = Matrix.Diagonal(Vector(tuple((target_hi[i] - target_lo[i]) / before[i] for i in range(3)) + (1,)))
    obj.data.transform(Matrix.Translation(target_lo) @ scale @ Matrix.Translation(-lo))
    obj.data.update()


def mark_materials(obj, label):
    for material in obj.data.materials:
        if material is not None:
            material.name = label


def textured_cut_faces(obj):
    # Preserve all original generated UVs. Only newly created cut faces get
    # planar UVs in the fleshy centre of the generated reference photograph.
    uv = obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        if obj.data.materials[polygon.material_index].name.startswith("MAT_Interior_"):
            for index in polygon.loop_indices:
                p = obj.data.vertices[obj.data.loops[index].vertex_index].co
                uv.data[index].uv = (.5 + p.y / .08 * .52, .5 + (p.z - .0375) / .08 * .52)


def tomato_visuals():
    clear()
    proxy = ROOT / "ArtSource/KitchenAssets/tomato/v002/prepared_recipe/tomato_pieces.glb"
    proxies = core.load_source(proxy)
    targets = {obj.name: core.bounds([obj]) for obj in proxies}
    clear()
    visual = source("tomato_slice")
    assert core.mesh_stats(visual)["closed"], "Repair source solid before prepared visual cuts"
    mark_materials(visual, "MAT_Food_TomatoGenerated")
    # The cloud slice has excess side thickness. Bake its thin axis into each
    # existing actor's bounds; retain the textured seed chambers and surface.
    visual.data.transform(Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1))))
    all_parts, info = [], []
    for i in range(4):
        name = f"VIS_Slice_{i:02d}"
        piece = core.duplicate(visual, name)
        fit(piece, targets[name])
        all_parts.append(piece)
        halves, _ = core.make_pieces(piece, {"kind": "slice", "pieces": 2, "cut_axis": "y", "interior": "TomatoFleshDetailed"}, 271031, .08)
        children = []
        for half in halves:
            quarters, _ = core.make_pieces(half, {"kind": "slice", "pieces": 2, "cut_axis": "z", "interior": "TomatoFleshDetailed"}, 271031, .08)
            for part in quarters:
                child_name = f"VIS_Dice_{i:02d}_{len(children):02d}"
                part.name = child_name
                fit(part, targets[child_name])
                textured_cut_faces(part)
                for polygon in part.data.polygons:
                    if part.data.materials[polygon.material_index].name.startswith("MAT_Interior_"):
                        part.data.materials[polygon.material_index].name = "MAT_Interior_TomatoFleshDetailed"
                children.append({"name": child_name, **core.mesh_stats(part)})
                all_parts.append(part)
            bpy.data.objects.remove(half, do_unlink=True)
        info.append({"name": name, "target_bounds": [list(v) for v in targets[name]], "children": children, **core.mesh_stats(piece)})
    bpy.data.objects.remove(visual, do_unlink=True)
    # Rendered Blender preview uses the same generated reference on new cuts.
    for cap in bpy.data.materials:
        if not cap.name.startswith("MAT_Interior_TomatoFleshDetailed"):
            continue
        shader = cap.node_tree.nodes.get("Principled BSDF")
        image = cap.node_tree.nodes.new("ShaderNodeTexImage")
        image.image = bpy.data.images.load(str(ROOT / "ArtSource/KitchenAssets/References/tomato_slice_single_v001.png"))
        cap.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])
        shader.inputs["Roughness"].default_value = .4
    core.export_fbx(OUTPUT / "tomato_detailed_visuals.fbx", all_parts)
    core.export_glb(OUTPUT / "tomato_detailed_visuals.glb", all_parts)
    core.preview([p for p in all_parts if p.name.startswith("VIS_Slice_")], OUTPUT / "tomato_slices_preview.png", .1)
    return info


def egg_visuals():
    clear()
    curd = source("scrambled_egg")
    mark_materials(curd, "MAT_Food_EggGenerated")
    core.decimate(curd, 4000)
    curd.name = "VIS_EggCurd_Detailed"
    # The curd is a soft sheet with open creases; it is only a pooled visual,
    # never fractured and never used as a dynamic collision mesh.
    coarse = core.duplicate(curd, "VIS_EggCurd_LOD1")
    core.decimate(coarse, 1200)
    core.export_fbx(OUTPUT / "egg_curd_detailed.fbx", [curd, coarse])
    core.export_glb(OUTPUT / "egg_curd_detailed.glb", [curd])
    core.preview([curd], OUTPUT / "egg_curd_preview.png", .045)
    return {"lod0": core.mesh_stats(curd), "lod1": core.mesh_stats(coarse)}


def main():
    if OUTPUT.exists() and any(OUTPUT.iterdir()):
        raise ValueError("Keep existing source versions; use a new output revision")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    report = {"schema_version": 1, "source_provider": "Comfy Cloud Pixal3D",
              "cloud_jobs": {"tomato_slice": "a85e7bdd-21ac-4286-a904-bbbd4cd317db", "scrambled_egg": "691f27f7-8e38-47e1-88d7-65d5fe2b828f"},
              "tomato": tomato_visuals(), "egg": egg_visuals(),
              "collision_and_food_quantity": "Existing recipe colliders, mass and action hierarchy retained. These are visual derivatives, not measured food volumes.",
              "source_hashes": {name: hashlib.sha256((ROOT / f"ArtSource/KitchenAssets/{name}/v001/download/master.glb").read_bytes()).hexdigest() for name in ["tomato_slice", "scrambled_egg"]}}
    report["files"] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in OUTPUT.glob("*") if p.is_file()}
    (OUTPUT / "visual_provenance.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print("DETAILED_FOOD_VISUALS_READY", json.dumps(report["egg"]))


if __name__ == "__main__":
    main()
