#!/usr/bin/env python3
"""Bind an exported, cloud-validated ComfyUI graph to a repeatable asset recipe.

This tool never authenticates, uploads, queues jobs or spends credits.
"""
import argparse
import copy
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def validate_graph(graph):
    nodes = {node["id"]: node for node in graph["nodes"]}
    if len(nodes) != len(graph["nodes"]):
        raise ValueError("Duplicate node IDs")
    edges = {}
    for edge in graph["links"]:
        ident, source, output, target, input_slot, _ = edge
        if ident in edges:
            raise ValueError("Duplicate link ID: " + str(ident))
        if source not in nodes or target not in nodes:
            raise ValueError("Missing link endpoint: " + str(ident))
        source_slot = nodes[source]["outputs"][output]
        target_slot = nodes[target]["inputs"][input_slot]
        if ident not in (source_slot.get("links") or []):
            raise ValueError("Missing outgoing link: " + str(ident))
        if target_slot.get("link") != ident:
            raise ValueError("Mismatched incoming link: " + str(ident))
        edges[ident] = edge
    for node in nodes.values():
        for slot in node.get("inputs", []):
            if slot.get("link") is not None and slot["link"] not in edges:
                raise ValueError("Dangling input on " + str(node["id"]))
        for slot in node.get("outputs", []):
            for link in slot.get("links") or []:
                if link not in edges:
                    raise ValueError("Dangling output on " + str(node["id"]))
    return {"nodes": len(nodes), "links": len(edges)}


def find_one(graph, node_type, title=None):
    candidates = [n for n in graph["nodes"] if n["type"] == node_type
                  and (title is None or title in n.get("title", ""))]
    if len(candidates) != 1:
        raise ValueError(f"Expected one {node_type} / {title}, found {len(candidates)}")
    return candidates[0]


def set_widget(graph, node_type, title, value, index=0):
    node = find_one(graph, node_type, title)
    node["widgets_values"][index] = value
    # Comfy Cloud exports both representations. Keep them in agreement so a
    # later import cannot silently restore an old value from the named form.
    named = node.get("widgets_values_named")
    if named is not None:
        key = list(named)[index]
        named[key] = value


def configure(template, manifest, preset, quality="standard", model="pixal3d"):
    graph = copy.deepcopy(template)
    validate_graph(graph)
    name = manifest["asset_name"]
    if not re.fullmatch(r"[a-z][a-z0-9_]{1,63}", name):
        raise ValueError("Asset name must be lowercase snake_case, 2–64 characters")
    if model not in ("pixal3d", "trellis2"):
        raise ValueError("Unsupported generation model")
    if manifest["asset_class"] != preset["asset_class"]:
        raise ValueError("Asset category does not match preset")
    if type(manifest["version"]) is not int or manifest["version"] < 1:
        raise ValueError("Version must be a positive integer")
    settings = copy.deepcopy(preset["quality"][quality])
    overrides = manifest.get("parameters", {})
    if set(overrides) - set(settings):
        raise ValueError("Unknown parameter override")
    settings.update(overrides)
    for key, value in settings.items():
        if type(value) is not int or value < 1:
            raise ValueError("Positive integer required for " + key)
        if "texture" in key or key in ("master_normal", "master_ao"):
            if value not in (256, 512, 1024, 2048, 4096):
                raise ValueError("Texture size must be 256, 512, 1024, 2048 or 4096")
    seed = manifest["seed"]
    if type(seed) is not int or not 0 <= seed <= 2**63 - 4:
        raise ValueError("Seed must leave room for four consecutive stage seeds")
    for stage in range(1, 5):
        node = find_one(graph, "KSampler", f"Seed {stage}:")
        node["widgets_values"][:2] = [seed + stage - 1, "fixed"]
        if "widgets_values_named" in node:
            node["widgets_values_named"]["seed"] = seed + stage - 1
            node["widgets_values_named"]["control_after_generate"] = "fixed"
    set_widget(graph, "PrimitiveInt", "Target Face Count", settings["master_faces"])
    set_widget(graph, "PrimitiveInt", "Texture Size", settings["master_texture"])
    set_widget(graph, "PrimitiveInt", "Normal Map Size", settings["master_normal"])
    set_widget(graph, "PrimitiveInt", "AO Map Size", settings["master_ao"])
    prefix = f"3d/kitchen/{name}/v{manifest['version']:03d}/"
    set_widget(graph, "PrimitiveString", "Output Prefix", prefix)
    multi = any(n["type"] == "Pixal3DMultiViewConditioning" for n in graph["nodes"])
    if multi and model != "pixal3d":
        raise ValueError("This multi-view workflow supports Pixal3D only")
    if not multi:
        set_widget(graph, "PrimitiveBoolean", "Switch to Trellis2", model == "trellis2")
    if manifest["input_mode"] != ("multiview" if multi else "single"):
        raise ValueError("Manifest input_mode does not match workflow")
    required = ["front", "left", "back", "right"] if multi else ["single"]
    refs = manifest.get("reference_images", {})
    if set(refs) != set(required) or any(not refs[v] for v in required):
        raise ValueError("Set reference_images exactly for: " + ", ".join(required))
    if multi:
        if len({Path(refs[v]).name for v in required}) != 4:
            raise ValueError("Give the four uploaded views distinct filenames")
        crop = manifest.get("multiview", {}).get("shared_crop", [0, 0, 1024, 1024])
        if (len(crop) != 4 or any(type(v) is not int for v in crop)
                or min(crop[:2]) < 0 or min(crop[2:]) < 1 or crop[2] != crop[3]):
            raise ValueError("Multiview shared_crop must be a square [x, y, width, height]")
        for index, value in enumerate(crop):
            set_widget(graph, "PrimitiveBoundingBox", "Shared Crop Region", value, index)
        for view in required:
            set_widget(graph, "LoadImage", "Input: " + view, Path(refs[view]).name)
    else:
        set_widget(graph, "LoadImage", None, Path(refs["single"]).name)
    graph.setdefault("extra", {})["kitchen_asset"] = {
        "schema_version": 1,
        "asset_name": name,
        "asset_class": preset["asset_class"],
        "version": manifest["version"],
        "quality": quality,
        "settings": settings,
        "seed": seed,
        "model": model,
        "input_mode": "multiview" if multi else "single",
        "reference_images": refs,
        "design_review": manifest["design_review"],
        "postprocess_recipe": manifest["postprocess_recipe"],
        "generation_status": "not_run",
        "template_sha256": hashlib.sha256(json.dumps(template, sort_keys=True).encode()).hexdigest(),
    }
    identity = {"asset": name, "version": manifest["version"], "quality": quality,
                "settings": settings, "seed": seed, "model": model, "refs": refs,
                "reference_sha256": manifest.get("reference_sha256", {})}
    graph["id"] = str(uuid.uuid5(uuid.NAMESPACE_URL, "xr-apartment-kitchen/" + json.dumps(identity, sort_keys=True)))
    validate_graph(graph)
    return graph


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--template", type=Path, required=True)
    parser.add_argument("--quality", choices=["draft", "standard", "high"])
    parser.add_argument("--model", choices=["pixal3d", "trellis2"], default="pixal3d")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    manifest = read_json(args.manifest)
    preset = read_json(ROOT / "presets" / (manifest["asset_class"] + ".json"))
    graph = configure(read_json(args.template), manifest, preset, args.quality or manifest["quality"], args.model)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(graph, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"workflow": str(args.output), "sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(),
                      "design_approved": bool(manifest["design_review"].get("approved")),
                      "generation_queued": False, **validate_graph(graph)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
