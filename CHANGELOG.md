# MistalCrysMap 更新日志

## 1.0.1 - 2026-06-28

### CN

- 大地图新增 `Center` 按钮，可直接把视角居中到玩家当前位置。（也可以按键盘home键）
- 居中到玩家后，玩家标记外圈会在约 3 秒内明显放大并逐渐淡出，方便快速找回自己。
- 不再在插件 `Awake` 阶段主动初始化或重写游戏原生 `Settings.settings` 列表。

### EN

- The main map has added a "Center" button, which enables you to directly center the view at the player's current location. (You can also press the home key on the keyboard)
- Once centered on the player, the player's marker ring will significantly enlarge and gradually fade out within approximately 3 seconds, making it easy to quickly locate yourself.
- No longer actively initialize or rewrite the game's native `Settings.settings` list during the `Awake` plugin stage.