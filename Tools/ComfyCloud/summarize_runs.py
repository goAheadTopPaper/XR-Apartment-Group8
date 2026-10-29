#!/usr/bin/env python3
"""Summarise recorded pilot facts. Never infer job charges or mark an acceptance passed."""
import argparse
import csv
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2] / "ArtSource/KitchenAssets")
    args = parser.parse_args()
    rows = []
    fields = ["asset_name", "asset_class", "input_mode", "job_id", "elapsed_seconds", "compute_hours_displayed",
              "credits_consumed", "game_triangles", "piece_count", "game_basecolor_size", "human_cleanup_minutes",
              "agent_authoring_minutes", "visual_quality", "unity_import", "interaction", "body_animation", "facial_lipsync", "quest_link"]
    for path in sorted((args.root / "Manifests").glob("*_v*.json")):
        asset = json.loads(path.read_text())
        if asset.get("schema_version") != 1 or "asset_name" not in asset: continue
        generation = asset.get("generation", {})
        processing = asset.get("processing", {})
        acceptance = asset.get("acceptance", {})
        row = {name: asset.get(name) for name in ("asset_name", "asset_class", "input_mode")}
        row.update({name: generation.get(name) for name in ("job_id", "elapsed_seconds", "compute_hours_displayed", "credits_consumed")})
        row.update({name: processing.get(name) for name in ("game_triangles", "piece_count")})
        row["game_basecolor_size"] = "x".join(map(str, processing.get("game_basecolor_size", [])))
        row["human_cleanup_minutes"] = asset.get("human_cleanup", {}).get("minutes")
        row["agent_authoring_minutes"] = asset.get("human_cleanup", {}).get("agent_authoring_minutes")
        row.update({name: acceptance.get(name) for name in ("visual_quality", "unity_import", "interaction", "body_animation", "facial_lipsync", "quest_link")})
        rows.append(row)
    priority = {name: i for i, name in enumerate(("plate", "tomato", "carrot", "apple", "lemon", "egg", "knife",
                                                "kitchen_character", "cabinet_module", "cabinet_multiview"))}
    rows.sort(key=lambda row: (priority.get(row["asset_name"], len(priority)), row["asset_name"]))
    with (args.root / "Manifests/pilot_runs.csv").open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=fields)
        writer.writeheader(); writer.writerows(rows)
    names = {"plate": "盘子", "tomato": "番茄", "carrot": "胡萝卜", "apple": "苹果", "lemon": "柠檬", "egg": "鸡蛋", "knife": "厨刀",
             "kitchen_character": "人物实验样例", "cabinet_module": "柜体模块", "cabinet_multiview": "四视角柜体对照",
             "kitchen_fridge_body":"冰箱箱体参考", "kitchen_fridge_door":"独立冰箱门", "kitchen_frying_pan":"煎锅",
             "kitchen_stockpot":"双耳汤锅", "kitchen_spatula":"锅铲", "kitchen_ladle":"汤勺", "kitchen_gas_hob":"燃气灶", "kitchen_whisk":"搅拌器"}
    status = {"kitchen_character": "身体动作已测；肩部与衣物待精修，口型未做", "cabinet_module": "空心柜身与独立铰链门已测", "cabinet_multiview": "技术导入通过；外观不合格，排除"}
    status["kitchen_fridge_body"] = "生成外壳平面不合格；参考其设计本地重建箱体，生成门保留"
    lines = ["# 首批生成与处理结果", "", "数据来自各资产 manifest；缺项保留为空，未核实的费用不写成零。", "",
             "| 物品 | 输入 | 生成秒数 | 游戏三角形 | 碎片 / 切片 | 状态 |", "| --- | --- | ---: | ---: | ---: | --- |"]
    for row in rows:
        name = row["asset_name"]
        seconds = "待按任务核实" if row["elapsed_seconds"] is None else f'{row["elapsed_seconds"]:.2f}'
        lines.append(f'| {names.get(name, name)} | {"四视角" if row["input_mode"] == "multiview" else "单图"} | {seconds} | {row["game_triangles"]} | {row["piece_count"]} | {status.get(name, "厨房原型可测试")} |')
    lines.extend(["", "单图与四视角均实际运行过。四视角任务输出有褶皱，未替换厨房中本地整理过的柜体。", "",
                  "人物、柜体的本地制作耗时及修改说明见各 manifest。人类未在 Blender 中拖拽修模；agent 编写和返修的总工时没有单独计时。", "",
                  "逐任务积分扣费尚未确认。云端显示的计算小时与积分余额分别记录；余额未变化不能证明零成本。", "",
                  "验证摘要：[validation_summary.json](validation_summary.json)。完整字段：[pilot_runs.csv](../Manifests/pilot_runs.csv)。"])
    target = args.root / "Validation/PilotResults.md"; target.parent.mkdir(exist_ok=True)
    target.write_text("\n".join(lines) + "\n")
    print(json.dumps({"assets": len(rows), "csv": str(args.root / "Manifests/pilot_runs.csv"), "summary": str(target)}))


if __name__ == "__main__":
    main()
