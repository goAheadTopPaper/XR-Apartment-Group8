# 食材模型精修参考图提示词

2026-10-28，使用内置 Image Gen。两张参考图随后分别上传至官方 Comfy Cloud，运行本仓库的单图 Pixal3D 工作流；不是把图片当作三维生成完成的证据。用户已授权直接生成并替换简陋食材，具体设计由 agent 在已确认的写实厨房风格内选择。

## tomato_slice_single_v001.png

Use case: photorealistic-natural. Asset type: single image reference for Pixal3D image-to-3D, a realistic ingredient for a Unity VR cooking game. Subject: exactly ONE fresh thick round tomato slice (about 8 cm diameter and 16 mm thick), no other objects. Three-quarter top view, entire slice visible centred, 75 percent of frame. The exposed round cut face looks delicious and anatomically convincing: translucent glossy red flesh, four irregular seed chambers with yellow ivory seeds embedded in glistening amber-red gel, pale cream central star core and branching membranes, thin deep red smooth outer skin, tiny natural pores and slight knife-cut unevenness. Soft realistic studio illumination, very faint contact shadow, plain pure white background. High-detail photogrammetry-style food reference, accurate natural proportions. No plate, no garnish, no leaves, no green stalk, no text, no labels, no diagram, no grid, no multiple slices, no cartoon, no faceted geometry. Slice is a single continuous solid with real side thickness, not a flat image.

## scrambled_egg_single_v001.png

Use case: photorealistic-natural. Asset type: single image reference for Pixal3D image-to-3D, cooked scrambled egg for Unity VR tomato-and-egg cooking. Subject: exactly ONE irregular bite-size curd of Chinese-style stir-fried beaten egg, approx 4 cm long and 2.6 cm wide, sitting alone, entire piece visible centred on pure white seamless background, fills 75 percent of image. It is a small naturally folded sheet of cooked egg with soft ruffles and layered creases, irregular torn feathery edges, rich golden-yellow with lighter pale-yellow inner folds, subtle tiny pores and glossy moist oil highlights, a few subtle toasted golden edges. Three-quarter overhead food-photography view, soft diffuse studio light, extremely faint contact shadow. Photorealistic, appetizing natural food, volumetric soft folded shape. No plate, no fork, no tomato, no green garnish, no text, no labels, no multiple pieces, no pile, no spherical blob, no flat yellow polygon, no cartoon.

参考图尺寸是制作意图；实际模型需按版本清单和 Unity 代理范围处理。具体随机种子、面数、贴图档位及任务编号见 `../Manifests/tomato_slice_v001.json` 与 `../Manifests/scrambled_egg_v001.json`。
