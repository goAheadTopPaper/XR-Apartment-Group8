#!/usr/bin/env python3
"""Collect the seven canonical outputs from a Comfy Cloud job ZIP.

The job ID and runtime come from the visible Cloud task. No authentication or
queueing is performed. Extra previews stay in the ignored local download cache.
"""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path
from zipfile import ZipFile


def collect(manifest_path, archive):
    manifest_path = Path(manifest_path).resolve()
    archive = Path(archive).resolve()
    manifest = json.loads(manifest_path.read_text())
    asset = manifest["asset_name"]
    version = manifest["version"]
    if not re.fullmatch(r"[a-z][a-z0-9_]{1,63}", asset):
        raise ValueError("Invalid asset name")
    root = manifest_path.parent.parent
    directory = root / asset / f"v{version:03}"
    maps = directory / "TexturesSource"
    download = directory / "download"
    maps.mkdir(parents=True, exist_ok=True)
    download.mkdir(parents=True, exist_ok=True)
    prefix = f"3d/kitchen/{asset}/v{version:03}/"
    outputs = {}
    with ZipFile(archive) as bundle:
        for name in ("master_glb", "basecolor", "metallic", "roughness", "normal", "ao", "render_preview"):
            extension = "glb" if name == "master_glb" else "png"
            matches = [n for n in bundle.namelist()
                       if re.fullmatch(re.escape(prefix + name) + r"_\d+\." + extension, n)]
            if len(matches) != 1:
                raise ValueError(f"Expected exactly one {name} output for {asset}, got {matches}")
            data = bundle.read(matches[0])
            target = (download / "master.glb" if name == "master_glb" else
                      download / "cloud_render_preview.png" if name == "render_preview" else maps / (name + ".png"))
            if target.exists() and target.read_bytes() != data:
                raise ValueError("Keep earlier source outputs; bump the asset version: " + str(target))
            target.write_bytes(data)
            outputs[name] = {"cloud_file": matches[0], "bytes": len(data),
                             "sha256": hashlib.sha256(data).hexdigest(),
                             "file": str(target.relative_to(root))}
    archive_hash = hashlib.sha256(archive.read_bytes()).hexdigest()
    (directory / "cloud_outputs.json").write_text(json.dumps({"job_id": manifest["generation"]["job_id"],
        "archive_sha256": archive_hash, "files": outputs}, ensure_ascii=False, indent=2) + "\n")
    manifest["generation"]["status"] = "generated_downloaded_requires_mesh_review"
    manifest["acceptance"]["cloud_generation"] = "passed_output_downloaded"
    manifest["outputs"].update(master_glb="../" + outputs["master_glb"]["file"],
        preview_png="../" + outputs["render_preview"]["file"],
        pbr_textures={name: "../" + outputs[name]["file"] for name in ("basecolor", "metallic", "roughness", "normal", "ao")})
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    cache = Path("Logs/KitchenAssetPipeline/CloudDownloads").resolve()
    cache.mkdir(parents=True, exist_ok=True)
    cached = cache / f"{asset}-{manifest['generation']['job_id']}.zip"
    if archive != cached:
        if cached.exists():
            if hashlib.sha256(cached.read_bytes()).hexdigest() != archive_hash:
                raise ValueError("Different archive already cached for this job")
        else:
            shutil.move(str(archive), cached)
    print(json.dumps({"asset": asset, "job_id": manifest["generation"]["job_id"],
                      "canonical_outputs": len(outputs), "archive_sha256": archive_hash}, ensure_ascii=False))
    return outputs


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("archive", type=Path)
    arguments = parser.parse_args()
    collect(arguments.manifest, arguments.archive)
