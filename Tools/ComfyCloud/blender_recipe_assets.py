"""Derive two-stage tomato cuts and author the recipe's mechanical meshes locally.

Blender 5.2; no cloud requests. The tomato retains the approved Pixal3D source.
Run --background --factory-startup --python-exit-code 1 --python this_file.
"""
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_postprocess as core

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource/KitchenAssets/Recipe/v001"
TOMATO = ROOT / "ArtSource/KitchenAssets/tomato/v002/prepared_recipe"


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def material(name, colour, metal=0, rough=.5):
    m = bpy.data.materials.new("MAT_Recipe_" + name)
    m.diffuse_color = (*colour, 1)
    m.use_nodes = True
    node = m.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*colour, 1)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return m


def box(name, centre, size, mat, bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    o = bpy.context.object; o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    if bevel:
        mod = o.modifiers.new("Rounded edges", "BEVEL"); mod.width = bevel; mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = o.modifiers.new("Panel normals", "WEIGHTED_NORMAL")
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o


def export(name, objects):
    core.export_fbx(OUT / (name + ".fbx"), objects)
    core.export_glb(OUT / (name + ".glb"), objects)


def tomato():
    clear(); TOMATO.mkdir(parents=True, exist_ok=True)
    parent = ROOT / "ArtSource/KitchenAssets/tomato/v001/prepared"
    meshes = core.load_source(parent / "tomato_game.glb")
    source = next(m for m in meshes if m.name.startswith("VIS_"))
    for o in meshes:
        if o != source: bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-8)
    bm.to_mesh(source.data); bm.free(); source.data.update()
    source.name = "VIS_Intact"
    assert core.mesh_stats(source)["closed"]
    span = max(core.bounds([source])[1] - core.bounds([source])[0])
    slices, error = core.make_pieces(source, {"kind": "slice", "pieces": 4, "cut_axis": "x", "interior": "tomato flesh"}, 271029, span)
    all_parts = []; info = []
    for i, piece in enumerate(slices):
        piece.name = f"VIS_Slice_{i:02d}"
        hull, _ = core.make_hull(piece, f"COL_Slice_{i:02d}")
        all_parts += [piece, hull]
        halves, _ = core.make_pieces(piece, {"kind": "slice", "pieces": 2, "cut_axis": "y", "interior": "tomato flesh"}, 271029, span)
        children = []
        for half in halves:
            quarters, _ = core.make_pieces(half, {"kind": "slice", "pieces": 2, "cut_axis": "z", "interior": "tomato flesh"}, 271029, span)
            for q in quarters:
                j = len(children); q.name = f"VIS_Dice_{i:02d}_{j:02d}"
                h, _ = core.make_hull(q, f"COL_Dice_{i:02d}_{j:02d}")
                all_parts += [q, h]
                children.append({"visual": q.name, "collider": h.name, **core.mesh_stats(q)})
            bpy.data.objects.remove(half, do_unlink=True)
        relative = abs(sum(p["volume_m3"] for p in children) - core.mesh_stats(piece)["volume_m3"]) / core.mesh_stats(piece)["volume_m3"]
        assert relative < .02
        info.append({"visual": piece.name, "collider": hull.name, **core.mesh_stats(piece), "children": children,
                     "child_volume_relative_error": relative})
    hull, _ = core.make_hull(source, "COL_Intact")
    core.export_fbx(TOMATO / "tomato_game.fbx", [source, hull])
    core.export_glb(TOMATO / "tomato_game.glb", [source])
    core.export_fbx(TOMATO / "tomato_pieces.fbx", all_parts)
    core.export_glb(TOMATO / "tomato_pieces.glb", [o for o in all_parts if o.name.startswith("VIS_")])
    shutil.copytree(parent / "Textures", TOMATO / "Textures", dirs_exist_ok=True)
    manifest = json.loads((parent / "unity_asset.json").read_text())
    manifest.update(version=2, pieces=info, recipe="slice", piece_count=4,
                    derived_dice_count=16, fragment_volume_relative_error=error,
                    source_input_relative="../../v001/prepared/tomato_game.glb",
                    source_input_sha256=hashlib.sha256((parent / "tomato_game.glb").read_bytes()).hexdigest())
    manifest["acceptance"] = {"geometry": "4 closed slices and 16 closed diced chunks; per-parent volume error below 2%", "unity_recipe": "pending"}
    (TOMATO / "unity_asset.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print("RECIPE_TOMATO_READY", len(info), "slices", sum(len(s["children"]) for s in info), "diced pieces")


def mechanical():
    clear(); OUT.mkdir(parents=True, exist_ok=True)
    white = material("FridgeEnamel", (.79, .81, .78), .15, .28)
    liner = material("FridgeLiner", (.87, .88, .82), 0, .44)
    steel = material("BrushedSteel", (.38, .41, .41), .85, .3)
    dark = material("Gasket", (.045, .05, .048), 0, .65)
    glass = material("ShelfEdge", (.56, .67, .65), .15, .22)
    body = []
    for x, name in [(-.34, "Left"), (.34, "Right")]:
        body.append(box("VIS_Fridge_" + name, (x, 0, .92), (.04, .66, 1.76), white, .008))
    body += [box("VIS_FridgeBack", (0, .31, .92), (.64, .04, 1.76), white),
             box("VIS_FridgeTop", (0, 0, 1.79), (.68, .66, .04), white, .008),
             box("VIS_FridgeBottom", (0, 0, .09), (.68, .66, .10), white, .008),
             box("VIS_InnerBack", (0, .281, .94), (.62, .012, 1.62), liner),
             box("VIS_Kickplate", (0, -.325, .07), (.62, .018, .075), dark)]
    for i, z in enumerate((.62, 1.0, 1.32)):
        body += [box(f"VIS_Shelf_{i}", (0, 0, z), (.625, .55, .018), glass, .003),
                 box(f"VIS_ShelfLip_{i}", (0, -.277, z), (.625, .012, .026), liner, .003)]
    for i in range(4):
        body.append(box(f"VIS_FridgeFoot_{i}", ((-.27, .27)[i % 2], (-.24, .24)[i // 2], .025), (.06, .06, .05), dark))
    export("fridge_body", body)
    # Door geometry is local to the left hinge; front is -Y (Unity +Z).
    door = [box("VIS_FridgeDoor", (.345, -.021, .88), (.69, .046, 1.73), white, .012),
            box("VIS_DoorLiner", (.345, .011, .88), (.64, .012, 1.66), liner, .008)]
    for x in (.025, .665): door.append(box("VIS_DoorGasket", (x, .02, .88), (.012, .012, 1.66), dark, .003))
    for z in (.05, 1.71): door.append(box("VIS_DoorGasket", (.345, .02, z), (.65, .012, .012), dark, .003))
    door += [box("VIS_FridgeHandle", (.605, -.088, 1.05), (.023, .028, .44), steel, .01),
             box("VIS_HandleMountLow", (.605, -.06, .88), (.025, .06, .027), steel),
             box("VIS_HandleMountHigh", (.605, -.06, 1.22), (.025, .06, .027), steel)]
    export("fridge_door", door)
    clear()
    head = box("VIS_SpatulaBlade", (0, 0, .003), (.066, .083, .006), steel, .006)
    for x in (-.018, 0, .018):
        cut = box("Slot", (x, -.002, .003), (.008, .050, .025), dark, .003)
        core.select_only([head]); mod = head.modifiers.new("Blade slots", "BOOLEAN"); mod.operation = "DIFFERENCE"; mod.object = cut
        bpy.ops.object.modifier_apply(modifier=mod.name); bpy.data.objects.remove(cut, do_unlink=True)
    spatula = [head, box("VIS_SpatulaNeck", (0, -.09, .013), (.014, .105, .009), steel, .003),
               box("VIS_SpatulaHandle", (0, -.207, .025), (.026, .15, .023), dark, .01)]
    export("spatula", spatula)
    clear()
    wood = material("BoardWood", (.48, .28, .12), 0, .65)
    board = box("VIS_ChoppingBoard", (0, 0, .011), (.42, .34, .022), wood, .012)
    cut = box("Handle slot", (.177, 0, .011), (.018, .10, .05), dark, .008)
    core.select_only([board]); mod = board.modifiers.new("Handle hole", "BOOLEAN"); mod.operation = "DIFFERENCE"; mod.object = cut
    bpy.ops.object.modifier_apply(modifier=mod.name); bpy.data.objects.remove(cut, do_unlink=True)
    export("chopping_board", [board])
    clear()
    egg = material("CookedEgg", (.98, .76, .24), 0, .73)
    curds = []
    for i in range(3):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1)
        o = bpy.context.object; o.name = f"VIS_EggCurd_{i}"
        for v in o.data.vertices:
            x, y, z = v.co
            ripple = 1 + .12 * math.sin(x * 8 + i) * math.cos(y * 7 - i)
            v.co = Vector((x * .024 * ripple, y * .015 * ripple, (z + 1) * .006 * (1 + .18 * math.sin(x * 9 + i))))
        o.data.materials.append(egg)
        for face in o.data.polygons: face.use_smooth = True
        curds.append(o)
    export("egg_curds", curds)
    sources = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob("*.fbx")}
    (OUT / "model_provenance.json").write_text(json.dumps({"source": "locally authored Blender meshes; not cloud output", "units": "metres", "front": "Blender -Y / Unity +Z", "fridge_size": [.72, .66, 1.81], "fridge_hinge_unity_fbx": [.345, .04, .37], "fbx_axis_note": "FBX mirrors Blender X; Unity uses the right hinge and +105 degree outward swing", "models": sources, "tomato_derivation": "approved Pixal3D tomato v001 -> four slices -> four diced pieces per slice"}, indent=2) + "\n")
    print("RECIPE_MECHANICAL_MODELS_READY", len(sources))


tomato()
mechanical()
