# 厨房资产参考图提示词规范

工具：内置 ImageGen。九格板为本轮生成的外观讨论稿，状态见 `design-review.json`，尚不是经过批准的单件 3D 输入。以下是可重用的提示词规范，不声称随机生成能逐像素重现同一张图。

风格板规范：3×3 产品设计板，同一干净、写实、适合 Unity 厨房游戏的风格，中等细节，柔和均匀光照，米白背景，每格只放一项且完整可见。依次为：1 温白色无花纹陶瓷盘，2 带梗红番茄，3 修剪叶柄的胡萝卜，4 红绿色苹果，5 黄色柠檬，6 浅棕鸡蛋，7 不锈钢厨刀配深灰把手，8 白衬衫 / 深色围裙与裤子的虚构成年厨房角色，9 温白色柜体配浅木台面和独立门板。人物全身中立姿态、双手无遮挡；柜体只是外观参考。禁止品牌、水印、独立生成的碎片和不相关装饰。

单件参考图规范（确认风格后）：

> Generate one isolated [ASSET] matching the approved kitchen style board. Clean realistic game asset, complete silhouette, moderate surface detail, neutral material colour and even soft lighting. One object only, centred with generous padding on a plain off-white background, square canvas. No labels, no ruler, no extra objects, no strong cast shadow, no depth of field. Show a useful three-quarter view while keeping the whole object visible. Do not add cracks, shards or cut parts; these will be derived from the approved intact mesh later.

四视角规范（同一轮参考图设计中保持一致，不靠四次独立文字提示猜形状）：

> Create a consistent four-view turnaround of exactly the same approved [ASSET], in front, object-left, back, object-right order. Fixed horizontal camera orbit at 90-degree intervals, identical distance, focal length, pose, light and square framing. Keep the same world-space scale across all four panels: narrow side views stay narrow. Preserve all asymmetrical details correctly. Full object, no occlusion, no labels or extra objects. Flat plain background suitable for segmentation. This is a consistency reference; geometry and camera calibration will be checked before image-to-3D generation.

四格图必须先以 Comfy 裁切 / 预览拆成四张独立图，使用相同裁切尺度；不能把各视角各自放大到铺满画布。参考输入和裁切结果都保留。人物、建筑与物品的专门要求见 `Docs/KitchenAssetPipeline.md` 和 `presets/`。
