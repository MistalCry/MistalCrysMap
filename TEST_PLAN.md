# MistalCrysMap 测试计划

目标环境：Casualties Unknown Demo + BepInEx + Harmony。当前只测试单人模式。

## 1. 启动与基础设置

- 安装 `MistalCrysMap.dll` 后启动游戏，BepInEx 日志不应出现本模组加载、Harmony 补丁或反射异常。
- `Settings -> Game` 中应出现 `MistalCrysMap`、`Map Toggle Key`、`Exploration Map`、`Minimap Shape`、`Minimap Size` 五个设置。
- 新默认值应为：地图按键 `M`，小地图形状 `Square`，小地图大小 `1.25x`。
- `Minimap Size` 的最大值应能调到 `3x`，最小值仍为 `0.5x`。
- 如果玩家设置仍停留在上一版默认值 `Esc`，应自动迁移回 `M`。
- 如果玩家设置仍停留在旧默认值 `Rectangle`、`1x`，应自动迁移到新默认值；如果已经手动改过其他值，不应被无故覆盖。
- 关闭 `MistalCrysMap` 总开关后，小地图和大地图都不应显示，地图按键不应打开地图。
- 与 KrokMP 同时加载时，KrokMP 自己的 keybind 设置仍应出现在设置界面中，并且在游戏中可正常响应；MistalCrysMap 不应在插件 `Awake` 阶段主动初始化或重写游戏的 `Settings.settings` 列表。

## 2. 小地图

- 进入单人游戏后，小地图默认显示在屏幕右上角。
- 主菜单、读档前或没有本地玩家身体时，小地图不应显示。
- 按地图按键后，小地图隐藏；再次按下应重新显示。
- 左键拖动小地图时，小地图位置应跟随鼠标移动，松开后位置保存。
- 拖动小地图、右键打开大地图、在小地图上滚轮缩放时，不应触发角色攻击、投掷等鼠标输入动作。
- 重启游戏或重新进档后，小地图位置应恢复到上次保存的位置。
- 鼠标悬停在小地图上滚轮时，地图内容应缩放，不应改变小地图屏幕位置。
- 右键点击小地图应打开大地图。
- 中键点击小地图内容区域应设置或更新导航标记，并在小地图上显示从玩家到标记点的指示线。
- `Minimap Shape` 分别设置为 `Rectangle`、`Square`、`Circle` 后，边框和可视区域应切换到对应形状；圆形模式下角落不应显示地图内容，标记也不应画出圆形边界。

## 3. 大地图

- 大地图打开时应覆盖屏幕主要区域，并显示操作说明。
- 操作说明和按钮文字应使用正常大小写，不应全大写。
- 当前默认按键为 `M`，打开大地图时按 `M` 应关闭大地图并回到小地图状态。
- 大地图中单击右键不应关闭地图。
- 大地图中按住右键拖动时，地图内容应平移。
- 鼠标悬停在大地图上滚轮时，地图内容应缩放。
- 大地图打开时按 `Home` 或点击顶部 `Center` 按钮，应立即把视角居中到玩家当前位置，并让玩家标记外圈在约 3 秒内明显放大、逐渐淡出，方便重新找到自己。
- 大地图打开时，玩家移动、投掷、攻击等输入应尽量不继续落到游戏角色身上。
- 中键点击大地图内容区域应设置或更新导航标记，并在大地图和小地图上都显示玩家到标记点的指示线。
- 大地图顶部应有 `Interact`、`Traps`、`Traders`、`Enemies`、`Items`、`Thornback`、`Sprites` 七个开关。
- `Sprites` 关闭时右侧不应显示大地图精灵图大小控制；打开 `Sprites` 后才应显示 `-`、倍率、`+`，点击后大地图上的精灵图应变小或变大，设置应能保存。
- 大地图顶部应有 `Clear Mark` 按钮；点击后应删除当前导航标记和指示线。
- 点击分类开关后，对应类别应尽快显示或隐藏；允许点击瞬间做一次完整刷新，但平时后台扫描仍应保持分帧，避免重新变卡。

## 4. 地图内容与分类

- `Exploration Map` 关闭时，地图应直接显示完整地形，不需要先手动切换一次选项。
- `Exploration Map` 开启时，未探索区域不应显示地形、陷阱、交互物、商人、怪物或掉落物。
- 探索半径应为以玩家为中心的 40 格圆形。
- 玩家标记应出现在当前角色位置附近，角色左右朝向变化时标记方向应变化，并带有一圈轻微呼吸光圈，方便打开大地图后快速找到自己。
- 可交互物和容器应显示为更醒目的灰蓝色标记。
- 掉落物应长期显示为黄色点，包括玩家主动扔出的物品。
- 商人应显示为绿色 `$` 标记；关闭 `Traders` 后不应继续作为普通交互物显示。
- 普通怪物应显示为红色标记，并随移动实时更新位置。
- `thornbackelder` 应由 `Thornback` 独立开关控制，不应混在普通 `Enemies` 开关里。
- `Sprites` 默认关闭。打开后应优先显示游戏对象自带的小精灵图；如果对象没有可用精灵图，应自动退回简单方块或字母标记。
- 小地图上的精灵图应比上一版更大、更容易看清。
- Trader 在 `Sprites` 开启时不应显示为空白，应显示专用绿色 `$` 地图图标。

## 5. 交互物白名单

以下对象应能作为交互物或重要资源来源显示：

- `pop`
- `hydreed`
- `BushCol` / `bush`
- `BounceShroom`
- `Glowplant`
- `Geotree`
- `Mushplant`
- `Stoneplant`
- `Leadbush`
- `Driedbush`
- `Cactus`
- `Brownshroom`
- `Sandrose`
- `Plantation`
- `Bananaplant` / `Banana-plant`
- `Corpse`
- `Animalcorpse`
- `BloodCrystal`
- `DigestionCrystal`
- `DrillPod`
- `EmissiveCrystal`
- `OxygenCrystal`
- `ReliefCrystal`
- `SoothingCrystal`
- `TurbulentCrystal`
- `Vine`

`wallflower` 不应显示为交互物，因为它只是贴图，没有实际用途。

## 6. 陷阱与危险物

以下对象应显示为陷阱或危险物：

- `MineScript`
- `GunmineScript`
- `TurretScript`
- `BearTrap`
- `CoilScript`
- `SpikeStabberScript`
- `SoundCannon`
- `JumpPadScript`
- `Spentfuel` / `Spent Fuel and Barrel`
- `radbarrel`
- `minibarrel`
- `BarbedFence`
- `Stalactite` / `StalactiteDropper`
- `skullcrusher`
- `CaveTicks` / `CaveTickSpawner`
- `GrabberPlant`

`stalagmite` 不应显示为陷阱或危险物，因为它是安全无害的石笋。

## 7. 性能回归

- 小地图开启后，原地观察至少 2 分钟，不应出现固定周期性的明显掉帧。
- 关闭小地图后继续游玩，地图采样不应造成可感知卡顿；再次打开后地图应正常显示。
- 地震、爆炸、挖掘等大量改变地形的场景中，小地图仍应可玩，不应每帧重建完整地形贴图。
- 进入怪物很多的层级，例如 `layer=4`，帧率不应因为小地图扫描全图怪物而明显掉到不可玩。
- 大地图打开或点击分类开关时可以做一次较重刷新，但关闭大地图回到普通小地图后，应恢复附近区域轻量扫描。
- 圆形小地图模式下，四周不应出现方形黑边，只保留圆形可视区域。

## 8. 构建验证

构建命令：

```powershell
dotnet build MistalCrysMap.csproj -c Release /p:GameManagedDir="E:\SteamLibrary\steamapps\common\Casualties Unknown Demo\CasualtiesUnknown_Data\Managed" /p:BepInExCoreDir="E:\SteamLibrary\steamapps\common\Casualties Unknown Demo\BepInEx\core"
```

期望结果：

- 成功生成 `bin\Release\MistalCrysMap.dll`
- 0 个错误
- 最好 0 个警告
