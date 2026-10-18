# 厨房餐具与食品布置

用户指定的两个免费资源包已于 2026-10-16 经 Unity Package Manager 下载，并选择性导入 `feature/hzc` 的厨房评审场景。保持 Unity 2022.3.62f3、Built-in / Linear、XRI 3.5 和 StandaloneWindows64。

## 资源与处理

| 来源 | 本次使用 | 项目处理 |
| --- | --- | --- |
| [Kitchen Props Free 1.0 — Jake Sullivan](https://assetstore.unity.com/packages/3d/props/interior/kitchen-props-free-80208) | 盘、碗、马克杯、沥水架、烤面包机 | 保留原始模型和贴图；派生 Standard 材质统一白瓷与金属表现；没有导入其台面、吧台、凳子或演示场景 |
| [Food Pack Mixed 1.0 — Brain Fail Productions](https://assetstore.unity.com/packages/3d/props/food/food-pack-mixed-154349) | 番茄、洋葱、柠檬、香蕉、鸡蛋、奶酪、果汁盒、麦片盒、黑胡椒与牛至粉 | 从源 Prefab 取单个中等细节模型；保留果汁瓶盖和番茄蒂，去掉场景中的切片及重复细节档位；食品与纸盒取消不合理金属度 |

原始 GUID、材质及其贴图依赖分别在 `Assets/ThirdParty/KitchenPropsFree/`、`Assets/ThirdParty/FoodPackMixed/`。来源、包校验值、许可链接和导入清单记录在各目录的 `PROVENANCE.txt`。源 FBX 内仍保留原作者的多个模型档位，项目 Prefab 只实例化选中的中等档位；没有复制源演示场景或引入供应商脚本。

贴图导入上限为 1024；用于固定摆件的源模型生成 UV2。没有更改源图像与 FBX 的文件内容。项目材质在 `Assets/_Project/Art/Props/`，16 个派生道具 Prefab 在 `Assets/_Project/Prefabs/Props/`。原作者的展示偏移被消除，项目轴心为底部居中，缩放后的尺寸登记在 `ASM_KitchenParameters` 的 `prop_*` 参数中，均为 prototype assumption，不是客户尺寸或实际重量。

## 共同布局

`PREF_Kitchen_Dressing` 是独立的公共布置 Prefab，位于冰箱方案槽位之外：

- `SET_Crockery`：窗边侧台的沥水架、三只盘与备用碗。
- `SET_Preparation`：后台面的备餐盘、番茄、洋葱及两只装调料的小碗。原包中的调料是粉末堆，装进小碗作为固定摆设。
- `SET_Breakfast`：侧台的烤面包机与麦片盒。
- `SET_Dining`：已有餐桌上的餐盘、奶酪、鸡蛋、空马克杯、果汁盒及柠檬碗，香蕉放在碗旁。

布局使用实际环境 MeshCollider 向下射线取得落点；侧台与后台面高度不同。旋转后的落点使用网格顶点计算，避免弯曲模型的包围盒导致悬空。固定餐具、沥水架、烤面包机和调料参加 GI 烘焙，但用 Light Probes 接收间接光，避免细碗沿和沥水架木条的光照 UV 重叠。9 件活动食品/包装及空马克杯使用 Light Probes 和反射探针，保留实时碰撞与 XRI 动态抓取点。抓取质量是统一的操作代理值，没有实际重量、破碎、食品切割或烹饪模拟。

原有 `PREF_Prop_Cup` / `PREF_Prop_Pot`、接水盆、龙头及其 `LiquidContainer` 配置保留。新马克杯和果汁盒目前只支持抓取，不支持液体灌注、开盖或倒果汁。

## 维护与验证

在场景和 Prefab 编辑器里调整布置即可。`XR Apartment/Props/Configure Kitchen Dressing` 可重建上述道具与默认摆放；只改公共布置及其派生资源，不重建房间、不切换冰箱方案、不覆盖原有水杯与锅。已存在的派生材质和登记尺寸会保留，执行配置会恢复默认摆放。连续运行两次后仍只有一个布置实例。移动固定摆件后需重新烘焙光照和反射；移动食品不需要重烘焙。

- EditMode 63 条测试通过，新增检查归一化尺寸、底部轴心、材质与抓取碰撞体绑定。
- Play 中通过 XRI Interaction Manager 对 9 件新增活动道具逐件选择、释放，均成功；物体能在台面、盘碗上落稳。
- Play 中切换 B1 / B2 / B3，公共布置、水系统和天空保持同一实例；场景只替换方案冰箱及围板。
- 布置完成后复核原接水杯：在组件 API 控制的龙头下，从约 204.827 mL 增至登记容量 341.378 mL；场景仍为 1 套水系统与 3 个液体容器。
- 新摆件重新烘焙 GI，并更新两个室内反射探针。光照探针由 72 个扩至 102 个，覆盖侧台、冰箱侧及低位操作区；修复了麦片盒在原探针范围之外采到全黑间接光的问题，材质仍为 Standard。
- 检查在 macOS 编辑器和组件 API 上完成；Quest 3 经 Quest Link 的手感、双眼显示及持续帧率仍需真机验证。

本机验证截图与诊断文件保存在忽略入库的 `Logs/PropsImport/`。
