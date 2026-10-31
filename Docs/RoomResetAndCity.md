# 整屋重置与城市窗景

独立烹饪场景 `SCN_Kitchen_Cooking` 的台面按钮现在显示 **RESET ROOM**。用手柄选择按钮后，原有食材 / 菜谱重置与房间其余物品一起恢复，无需重新加载场景。玩家当前位置保持不变。

## 恢复范围

- 食材、切片、碎盘与蛋壳回到初始状态；菜谱进度、受热与烧焦状态清空，炉火关闭。
- 锅、杯及同场景其他可抓物回到原位置；已持有物先取消抓取，再恢复刚体、插值与抓取设置，避免重置后产生虚假抛掷。
- 水量恢复场景初始值，龙头关闭，空中水量 / 水流 / 水花清空；蒸汽及动作音效停止。
- 冰箱与其他运动学门 / 抽屉恢复初始开合状态。暂停时仍可完成重置；连续触发和重置后再次抓取也受到检查。

`KitchenRoomReset` 挂在 `PREF_Cooking_RoomReset` 实例上，启动时缓存同场景中工作台之外的道具与水体；工作台仍负责自己的食材、菜谱和冰箱。`KitchenCookingStation.ResetStarting` 让外部道具先归位，再执行原工作台重置。其他加载场景、玩家追踪、正式 Core / 反馈 / CSV 不在这个重置范围内。

## 城市远景

使用 Poly Haven 的 [Joburg Central Sunset](https://polyhaven.com/a/sunset_jhbcentral)。摄影为 Dimitrios Savva，处理为 Greg Zaal；资产为 [CC0](https://polyhaven.com/license)。使用官方 4K OpenEXR 原文件，保存在 `Assets/ThirdParty/PolyHaven/JoburgCentralSunset/`，附 `SOURCE.json`、下载地址、哈希与授权说明。

`MAT_Sky_CookingCity` 使用 Built-in 的 `Skybox/Panoramic`，固定旋转 120°，让厨房窗户朝向楼群和附近高楼。该素材从屋顶高度拍摄，可作为公寓某层室内的窗外背景。它是静态全景照片，不提供近景视差或可进入街区；不能用于判定实际楼层高度或邻楼间距。

仅应用于独立烹饪场景。原正式评审的环境、AllSky 材质、场景和烘焙结果保留，B1/B2 的环境保持一致。

## 制作与检查

菜单 **XR Apartment → Cooking → Apply Room Reset and City View** 可重新应用配置，完整 `Rebuild Authored Workbench` 也会接入。修改天空后需重烘焙烹饪场景；批处理入口为 `KitchenCookingAuthoring.ApplyRoomAndBakeBatch`。其光照资产、光照贴图和反射探针均在烹饪场景自己的目录中。

2026-10-31 最终重置专项 **2/2** 通过，覆盖实际 XRI 持物取消、遗失锅杯归位、烧焦蛋液 / 切碎番茄 / 盘子碎片恢复、初始水量、关闭龙头 / 炉火、清理流动水、暂停 / 连续触发、再次抓取、禁用过程与门 / 抽屉初始状态恢复。另一加载场景的杯子和水量保持原样，玩家位置保持原样。

EditMode **145/145**；整批 PlayMode **51/52**，新增两项均通过，完整菜谱自动化在放锅检查失败（锅偏离炉心）。本轮未修复这一物理操作测试波动，不记作整批通过。烹饪场景城市光照已独立重烘焙；226 个受保护的原文件校验不变。仅原有 3 个运行时文件为此功能做了小幅接线调整。

精选画面、结果和文件校验在 `ArtSource/KitchenAssets/Validation/RoomResetAndCity/`，完整日志在忽略入库的 `Logs/RoomResetAndCity/`。随后键盘碰撞与移动模式整合回归通过 EditMode 145/145、PlayMode 61/61，包含重置和完整菜谱；这不改变上面的历史测试记录，也不表示旧物理操作波动已被修复。同步状态以 Git 为准；Windows Player / Quest Link 尚未实测。
