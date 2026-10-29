"""Blender 5.x: approved GLB -> metre-scale game FBX/GLB and derived pieces.

Run with Blender --background --factory-startup --python this_file -- ...
No network access. This is not a rigging, retopology or fluid simulation tool.
"""
import argparse
import hashlib
import json
import math
import os
import random
import sys
import time
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parent


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def bounds(objects):
    # Mesh edits and parent changes do not synchronously refresh bound_box.
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (Vector(tuple(min(v[i] for v in points) for i in range(3))),
            Vector(tuple(max(v[i] for v in points) for i in range(3))))


def mesh_stats(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    closed = bool(bm.faces) and all(e.is_manifold for e in bm.edges)
    volume = abs(bm.calc_volume(signed=True)) if closed else None
    result = {"vertices": len(bm.verts), "triangles": sum(len(f.verts) - 2 for f in bm.faces),
              "closed": closed, "volume_m3": volume}
    bm.free()
    return result


def duplicate(obj, name):
    clone = obj.copy()
    clone.data = obj.data.copy()
    clone.name = name
    bpy.context.collection.objects.link(clone)
    return clone


def decimate(obj, target):
    before = mesh_stats(obj)["triangles"]
    if before > target:
        select_only([obj])
        modifier = obj.modifiers.new("GameBudget", "DECIMATE")
        modifier.ratio = target / before
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Exporters triangulate n-gons differently. Commit triangles once here.
    select_only([obj])
    triangulate = obj.modifiers.new("ExportTriangles", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=triangulate.name)


def load_source(path):
    if path.suffix.lower() == ".glb":
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif path.suffix.lower() == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    else:
        raise ValueError("Input must be GLB or FBX")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise ValueError("No meshes in input")
    if any(o.type == "ARMATURE" for o in bpy.context.scene.objects):
        raise ValueError("This static-mesh pass must run before rigging; preserve a rig in a separate file")
    # Bake import transforms before placing a shared origin. Never infer movable
    # parts from a texture; preserve the imported object names for manual authoring.
    bpy.context.view_layer.update()
    worlds = {obj: obj.matrix_world.copy() for obj in meshes}
    for obj in meshes:
        world = worlds[obj]
        obj.parent = None
        obj.data.transform(world)
        obj.matrix_world = Matrix.Identity(4)
    return meshes


def normalise(meshes, span):
    lo, hi = bounds(meshes)
    current = max(hi - lo)
    if current < 1e-8 or not math.isfinite(span) or span <= 0:
        raise ValueError("Invalid source bounds or target scale")
    factor = span / current
    origin = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    transform = Matrix.Scale(factor, 4) @ Matrix.Translation(-origin)
    for obj in meshes:
        obj.data.transform(transform)
        obj.data.update()
    return factor


def clean_source(meshes, manifest):
    """Explicit, logged geometry cleanup; never modifies the downloaded master."""
    options = manifest.get("cleanup", {})
    report = []
    if options.get("weld_coincident_vertices"):
        lo, hi = bounds(meshes)
        tolerance = max(hi - lo) * 1e-7
        for obj in meshes:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            before = len(bm.verts)
            boundaries_before = sum(e.is_boundary for e in bm.edges)
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=tolerance)
            bm.normal_update()
            report.append({"mesh": obj.name, "operation": "weld_coincident_vertices_preserve_loop_uv",
                           "tolerance_source_units": tolerance, "vertices_before": before,
                           "vertices_after": len(bm.verts), "boundaries_before": boundaries_before,
                           "boundaries_after": sum(e.is_boundary for e in bm.edges)})
            bm.to_mesh(obj.data)
            bm.free()
            obj.data.update()
    orientation = options.get("orientation")
    if orientation in {"best_fit_plane_z", "longest_axis_z", "longest_axis_x", "longest_axis_x_flat_z", "longest_axis_z_box"}:
        import numpy as np
        coordinates = np.array([tuple(v.co) for obj in meshes for v in obj.data.vertices])
        centred = coordinates - coordinates.mean(axis=0)
        _, axes = np.linalg.eigh(centred.T @ centred)
        normal = Vector(axes[:, 0 if orientation == "best_fit_plane_z" else 2].tolist())
        target = Vector((1, 0, 0)) if orientation.startswith("longest_axis_x") else Vector((0, 0, 1))
        if normal.dot(target) < 0:
            normal.negate()
        rotation = normal.rotation_difference(target).to_matrix().to_4x4()
        if orientation == "longest_axis_x_flat_z":
            thin = (rotation.to_3x3() @ Vector(axes[:, 0].tolist())).normalized()
            if thin.z < 0:
                thin.negate()
            # Rotate only around the already aligned long axis, preserving the
            # handle/blade direction while laying the blade flat on a table.
            roll = math.atan2(thin.y, thin.z)
            rotation = Matrix.Rotation(roll, 4, "X") @ rotation
        for obj in meshes:
            obj.data.transform(rotation)
            obj.data.update()
        report.append({"operation": orientation, "source_axis": list(normal)})
        if orientation == "longest_axis_z_box":
            real = imaginary = 0
            for obj in meshes:
                for face in obj.data.polygons:
                    if abs(face.normal.z) < .25:
                        angle = math.atan2(face.normal.y, face.normal.x)
                        real += face.area * math.cos(4 * angle)
                        imaginary += face.area * math.sin(4 * angle)
            yaw = .25 * math.atan2(imaginary, real)
            correction = Matrix.Rotation(-yaw, 4, "Z")
            for obj in meshes:
                obj.data.transform(correction)
                obj.data.update()
            report.append({"operation": "area_weighted_box_yaw_alignment", "radians": -yaw})
    return report


def export_glb(path, objects):
    select_only(objects)
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True,
                              export_yup=True, export_materials="EXPORT")


def export_fbx(path, objects):
    select_only(objects)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             bake_anim=False, path_mode="RELATIVE", embed_textures=False)


def retain_embedded_images(directory, size):
    """Keep individual embedded images. Packed ORM is deliberately not relabelled."""
    images = []
    for index, image in enumerate(list(bpy.data.images)):
        if image.source not in {"FILE", "GENERATED"} or not image.has_data or image.name == "Render Result":
            continue
        # Blender glTF material connections retain the correct channel semantics.
        # This directory is a fallback; the five cloud maps remain the canonical PBR set.
        filename = f"embedded_{index:02d}.png"
        if max(image.size) > size:
            scale = size / max(image.size)
            image.scale(max(1, round(image.size[0] * scale)), max(1, round(image.size[1] * scale)))
        image.filepath_raw = str(directory / filename)
        image.file_format = "PNG"
        image.save()
        images.append({"file": filename, "original_name": image.name,
                       "width": image.size[0], "height": image.size[1],
                       "color_space": image.colorspace_settings.name})
    return images


def prepare_pbr(maps, directory, size):
    """Resize exported maps and create Built-in Standard metallic/smoothness RGBA.

    Load data maps as Non-Color before sampling. No green-channel inversion is
    assumed; a raised-normal check in Unity is a separate acceptance item.
    """
    names = ("basecolor", "metallic", "roughness", "normal", "ao")
    paths = {}
    data = {}
    for name in names:
        source = maps / (name + ".png")
        if not source.is_file():
            raise ValueError("Missing cloud PBR export: " + str(source))
        image = bpy.data.images.load(str(source), check_existing=False)
        image.colorspace_settings.name = "sRGB" if name == "basecolor" else "Non-Color"
        image.scale(size, size)
        output = directory / (name + ".png")
        image.filepath_raw = str(output)
        image.file_format = "PNG"
        image.save()
        paths[name] = output.name
        data[name] = image
    packed = bpy.data.images.new("UnityMetallicSmoothness", width=size, height=size, alpha=True)
    packed.colorspace_settings.name = "Non-Color"
    metal = list(data["metallic"].pixels)
    rough = list(data["roughness"].pixels)
    pixels = [0.0] * (size * size * 4)
    for i in range(0, len(pixels), 4):
        pixels[i:i + 4] = [metal[i], metal[i], metal[i], 1 - rough[i]]
    packed.pixels.foreach_set(pixels)
    packed.filepath_raw = str(directory / "metallic_smoothness.png")
    packed.file_format = "PNG"
    packed.save()
    paths["metallic_smoothness"] = "metallic_smoothness.png"
    return paths


def cap_clip(obj, point, normal, interior_index, uv_scale):
    """Keep the negative half-space and cap only newly cut boundary edges."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if not bm.faces:
        bm.free()
        return False
    result = bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                                   dist=1e-7, plane_co=point, plane_no=normal,
                                   clear_outer=True, clear_inner=False)
    cut_edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
    if cut_edges:
        # Scan-fill supports nested loops (for example the annular rim of an
        # eggshell). Filling each loop separately would create overlapping disks.
        filled = bmesh.ops.triangle_fill(bm, edges=cut_edges, normal=normal,
                                         use_beauty=True, use_dissolve=True)["geom"]
        new_faces = [f for f in filled if isinstance(f, bmesh.types.BMFace)]
        uv = bm.loops.layers.uv.verify()
        tangent = normal.cross(Vector((0, 0, 1)))
        if tangent.length < .01:
            tangent = normal.cross(Vector((0, 1, 0)))
        tangent.normalize()
        bitangent = normal.cross(tangent).normalized()
        for face in new_faces:
            face.normal_update()
            if face.normal.dot(normal) < 0:
                face.normal_flip()
            face.material_index = interior_index
            face.smooth = False
            for loop in face.loops:
                loop[uv].uv = (loop.vert.co.dot(tangent) / uv_scale,
                               loop.vert.co.dot(bitangent) / uv_scale)
    # Preserve inward normals on a hollow shell; globally recalculating every
    # disconnected component would turn the inner surface outward.
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return bool(obj.data.polygons)


def interior_material(label):
    material = bpy.data.materials.new("MAT_Interior_" + label.replace(" ", "_"))
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    colours = {"unglazed ceramic": (.65, .61, .53, 1), "tomato flesh": (.6, .035, .015, 1),
               "carrot flesh": (.9, .22, .025, 1), "apple flesh": (.85, .79, .53, 1),
               "lemon flesh": (.9, .71, .12, 1), "eggshell interior": (.8, .76, .64, 1)}
    shader.inputs["Base Color"].default_value = colours.get(label, (.55, .5, .4, 1))
    shader.inputs["Roughness"].default_value = .78
    return material


def make_pieces(source, recipe, seed, span):
    kind = recipe["kind"]
    count = int(recipe.get("fragment_count", recipe.get("pieces", recipe.get("shell_halves", 2))))
    if not 2 <= count <= 24:
        raise ValueError("Keep the prototype within 2–24 pieces")
    source_stats = mesh_stats(source)
    if not source_stats["closed"] or source_stats["volume_m3"] < 1e-10:
        raise ValueError("Fracture requires a closed solid. Repair the approved intact mesh first.")
    lo, hi = bounds([source])
    interior = interior_material(recipe.get("interior", "eggshell interior"))
    rng = random.Random(seed)
    pieces = []
    # The planar Voronoi seeds suit thin crockery. Slice planes are fixed in local
    # X; this is a prepared cut, not an arbitrary runtime knife plane.
    seeds = [Vector(((lo.x + hi.x) / 2 + (hi.x - lo.x) * .32 * math.cos(2 * math.pi * i / count),
                     (lo.y + hi.y) / 2 + (hi.y - lo.y) * .32 * math.sin(2 * math.pi * i / count),
                     (lo.z + hi.z) / 2)) for i in range(count)]
    angle = rng.uniform(-math.pi / count * .3, math.pi / count * .3)
    if kind == "fracture":
        rotation = Matrix.Rotation(angle, 3, "Z")
        centre = (lo + hi) / 2
        seeds = [centre + rotation @ (s - centre) for s in seeds]
    for index in range(count):
        obj = duplicate(source, f"VIS_Piece_{index:02d}")
        obj.data.materials.append(interior)
        slot = len(obj.data.materials) - 1
        valid = True
        if kind == "fracture":
            for other in range(count):
                if index == other:
                    continue
                normal = (seeds[other] - seeds[index]).normalized()
                point = (seeds[other] + seeds[index]) / 2
                if not cap_clip(obj, point, normal, slot, span):
                    valid = False
                    break
        elif kind in {"slice", "shell_halves", "egg"}:
            axis = {"x": 0, "y": 1, "z": 2}[recipe.get("cut_axis", "z" if kind == "egg" else "x")]
            step = (hi[axis] - lo[axis]) / count
            point = Vector((0, 0, 0))
            normal = Vector((0, 0, 0))
            if index > 0:
                point[axis] = lo[axis] + step * index
                normal[axis] = -1
                valid = cap_clip(obj, point, normal, slot, span)
            if valid and index < count - 1:
                point[axis] = lo[axis] + step * (index + 1)
                normal[axis] = 1
                valid = cap_clip(obj, point, normal, slot, span)
        else:
            raise ValueError("Unknown piece recipe: " + kind)
        if not valid:
            bpy.data.objects.remove(obj, do_unlink=True)
            raise ValueError("A cut produced an empty piece; change seeds or repair the source")
        decimate(obj, 1000000)  # triangulate without additional simplification
        stats = mesh_stats(obj)
        if not stats["closed"] or stats["volume_m3"] < 1e-10:
            raise ValueError("A cap failed its manifold/volume check: " + obj.name)
        # Shared origin is intentionally retained on every piece. They assemble
        # exactly at (0,0,0) below the same parent in Unity.
        pieces.append(obj)
    volume = sum(mesh_stats(p)["volume_m3"] for p in pieces)
    relative_error = abs(volume - source_stats["volume_m3"]) / source_stats["volume_m3"]
    if relative_error > .02:
        raise ValueError(f"Fragment volume mismatch {relative_error:.3%}; do not ship this fracture")
    return pieces, relative_error


def make_hull(source, name):
    def convex_mesh(points):
        shape = bmesh.new()
        for point in points:
            shape.verts.new(point)
        result = bmesh.ops.convex_hull(shape, input=list(shape.verts), use_existing_faces=False)
        unused = [v for v in result.get("geom_interior", []) + result.get("geom_unused", [])
                  if isinstance(v, bmesh.types.BMVert) and v.is_valid]
        if unused:
            bmesh.ops.delete(shape, geom=list(set(unused)), context="VERTS")
        bmesh.ops.triangulate(shape, faces=list(shape.faces))
        bmesh.ops.recalc_face_normals(shape, faces=list(shape.faces))
        return shape

    bm = convex_mesh([v.co for v in source.data.vertices])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    method = "convex_hull"
    if len(mesh.polygons) > 200:
        decimate(obj, 128)
        bm = convex_mesh([v.co for v in obj.data.vertices])
        bm.to_mesh(obj.data)
        bm.free()
        method = "simplified_convex_hull"
    if len(obj.data.polygons) <= 200:
        return obj, method
    # Only pathological hulls take the coarse fallback; preserve this in QA.
    bpy.data.objects.remove(obj, do_unlink=True)
    lo, hi = bounds([source])
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    obj = bpy.context.object
    for v in obj.data.vertices:
        v.co = (lo + hi) / 2 + Vector((v.co.x * (hi.x - lo.x), v.co.y * (hi.y - lo.y), v.co.z * (hi.z - lo.z)))
    obj.name = name
    return obj, "box_fallback"


def preview(objects, output, span, exploded=False):
    locations = {obj: obj.location.copy() for obj in objects}
    if exploded:
        for obj in objects:
            lo, hi = bounds([obj])
            obj.location += Vector(((lo.x + hi.x) * .12, (lo.y + hi.y) * .12, 0))
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.hide_render = obj not in objects
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (.25, .25, .25)
    target = Vector((0, 0, span * .35))
    bpy.ops.object.camera_add(location=(span * 1.9, -span * 2.4, span * 1.7))
    camera = bpy.context.object
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = span * 1.5
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(span * 1.5, -span, span * 3))
    lamp = bpy.context.object
    lamp.data.energy = 100 * span * span
    lamp.data.shape = "DISK"
    lamp.data.size = span * 2
    lamp.rotation_euler = (target - lamp.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    for obj, position in locations.items():
        obj.location = position


def process(args):
    started = time.monotonic()
    args.output = args.output.resolve()
    if args.input:
        args.input = args.input.resolve()
    if args.maps:
        args.maps = args.maps.resolve()
    manifest = json.loads(args.manifest.read_text())
    if not args.fixture and not manifest["design_review"].get("approved"):
        raise ValueError("Design approval is required before processing a production asset")
    if args.output.exists() and any(args.output.iterdir()):
        raise ValueError("Output directory must be new or empty; keep earlier versions")
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    if args.fixture:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16)
        meshes = [bpy.context.object]
        meshes[0].scale = (1, 1, .15)
        select_only(meshes)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        meshes[0].data.materials.append(interior_material("unglazed ceramic"))
        # Exercise UV/material preservation on a closed, explicitly synthetic fixture.
        meshes[0].name = "DiagnosticFixture_NotCloudGenerated"
    else:
        if args.input is None or not args.input.is_file():
            raise ValueError("Provide the downloaded cloud master with --input")
        meshes = load_source(args.input)
    raw_stats = [mesh_stats(obj) for obj in meshes]
    cleanup = clean_source(meshes, manifest)
    span = manifest["scale"]["reference_span_metres"]
    scale_factor = normalise(meshes, span)
    master_stats = [mesh_stats(obj) for obj in meshes]
    name = manifest["asset_name"]
    preset = json.loads((ROOT / "presets" / (manifest["asset_class"] + ".json")).read_text())
    settings = preset["quality"][manifest["quality"]]
    settings.update(manifest.get("parameters", {}))
    export_glb(args.output / (name + "_master.glb"), meshes)
    game = [duplicate(obj, f"VIS_Intact_{i:02d}") for i, obj in enumerate(meshes)]
    total = sum(mesh_stats(o)["triangles"] for o in game)
    for obj in game:
        budget = max(12, round(settings["game_triangles"] * mesh_stats(obj)["triangles"] / total))
        decimate(obj, budget)
    texture_dir = args.output / "Textures"
    texture_dir.mkdir()
    embedded = retain_embedded_images(texture_dir, settings["game_texture"])
    pbr = prepare_pbr(args.maps, texture_dir, settings["game_texture"]) if args.maps else {}
    export_glb(args.output / (name + "_game.glb"), game)
    recipe = manifest["postprocess_recipe"]
    pieces = []
    piece_hulls = []
    piece_info = []
    volume_error = None
    if not args.inspect_only and recipe["kind"] in {"fracture", "slice", "shell_halves", "egg"}:
        if len(game) != 1:
            raise ValueError("Join the approved solid for this fracture, while retaining a separate original master")
        cutting_source = game[0]
        if recipe["kind"] == "egg":
            cutting_source = duplicate(game[0], "Eggshell_CuttingSource")
            cutting_source.data.materials.append(interior_material("eggshell interior"))
            select_only([cutting_source])
            shell = cutting_source.modifiers.new("EggshellThickness", "SOLIDIFY")
            shell.thickness = float(recipe.get("shell_thickness_metres", .0008))
            shell.offset = -1
            shell.use_even_offset = True
            shell.material_offset = len(cutting_source.data.materials) - 1
            bpy.ops.object.modifier_apply(modifier=shell.name)
        pieces, volume_error = make_pieces(cutting_source, recipe, manifest["seed"], span)
        hulls = []
        for i, obj in enumerate(pieces):
            hull, method = make_hull(obj, f"COL_Piece_{i:02d}")
            hulls.append(hull)
            piece_info.append({"visual": obj.name, "collider": hull.name, "collider_method": method,
                               **mesh_stats(obj)})
        export_fbx(args.output / (name + "_pieces.fbx"), pieces + hulls)
        export_glb(args.output / (name + "_pieces.glb"), pieces)
        piece_hulls = hulls
    if piece_hulls and recipe.get("collider_mode") == "derived_compound":
        intact_hulls = [duplicate(hull, f"COL_Intact_{i:02d}") for i, hull in enumerate(piece_hulls)]
    else:
        intact_hulls = [make_hull(obj, f"COL_Intact_{i:02d}")[0] for i, obj in enumerate(game)]
    export_fbx(args.output / (name + "_game.fbx"), game + intact_hulls)
    preview(game, args.output / (name + "_preview.png"), span)
    if pieces:
        preview(pieces, args.output / (name + "_pieces_preview.png"), span, exploded=True)
    lo, hi = bounds(game)
    result = {"schema_version": 1, "asset_name": name, "asset_class": manifest["asset_class"],
              "version": manifest["version"], "diagnostic_fixture": args.fixture,
              "source_provider": "synthetic algorithm fixture" if args.fixture else manifest["source"]["provider"],
              "source_sha256": None if args.fixture else hashlib.sha256(args.input.read_bytes()).hexdigest(),
              "source_input_relative": None if args.fixture else Path(os.path.relpath(args.input, args.output)).as_posix(),
              "design_approved": bool(manifest["design_review"].get("approved")),
              "blender_version": bpy.app.version_string, "scale_factor": scale_factor,
              "stage": "intact_inspection" if args.inspect_only else "prepared",
              "master_stats": master_stats,
              "raw_master_stats": raw_stats, "cleanup": cleanup,
              "unity_units": "metres", "unity_up": "Y", "shared_pivot": "bottom centre",
              "size_blender_xyz": list(hi - lo), "intact_fbx": name + "_game.fbx",
              "pieces_fbx": name + "_pieces.fbx" if pieces else "",
              "recipe": recipe["kind"], "piece_count": len(pieces), "pieces": piece_info,
              "triangle_budget": settings["game_triangles"],
              "game_triangles": sum(mesh_stats(o)["triangles"] for o in game),
              "pbr_textures": pbr, "embedded_images": embedded,
              "fragment_volume_relative_error": volume_error,
              "elapsed_seconds": round(time.monotonic() - started, 3),
              "acceptance": {"unity_import": "pending", "normal_direction": "pending",
                             "body_rig": "pending" if manifest["asset_class"] == "character" else "not_applicable",
                             "facial_lipsync": "pending_separate" if manifest["asset_class"] == "character" else "not_applicable",
                             "movable_part_pivots": "manual_check_required"}}
    (args.output / "unity_asset.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print("KITCHEN_POSTPROCESS " + json.dumps(result, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--input", type=Path)
    parser.add_argument("--maps", type=Path, help="Cloud exports renamed basecolor/metallic/roughness/normal/ao.png")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--fixture", action="store_true", help="Synthetic diagnostics only; never claim as a cloud pilot")
    parser.add_argument("--inspect-only", action="store_true", help="Export intact model and preview for review before deriving pieces")
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    process(parser.parse_args(arguments))


if __name__ == "__main__":
    main()
