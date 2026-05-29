# cu-font-loader

为 Casualties: Unknown（及任何使用 TextMeshPro 的 Unity 游戏）注入 / 替换外部字体的 BepInEx 插件。

> **v1.2.0** 重写了全局替换机制（移除 Harmony，改用 scene callback），之前的 v1.0~v1.1.x 因 `TMP_Text.OnEnable` 继承链 Harmony 解析失败，回退和全局替换均无法正常工作。请使用 v1.2.0+。

## 安装

1. 确保游戏已安装 [BepInEx](https://github.com/BepInEx/BepInEx)
2. 将 `cu-font-loader` 文件夹复制到 `BepInEx/plugins/`
3. 将 `.ttf` 或 `.otf` 字体文件放入 `BepInEx/plugins/cu-font-loader/fonts/`
4. 启动游戏

```
BepInEx/
├── core/
│   └── BepInEx.dll
└── plugins/
    └── cu-font-loader/
        ├── cu-font-loader.dll
        └── fonts/
            ├── YourFont.ttf
            └── AnotherFont.otf
```

> **v1.2.0 起不再依赖 Harmony / 0Harmony.dll**，纯 BepInEx 即跑。

## 配置

首次运行后会在 `BepInEx/config/fenchen.cu-font-loader.cfg` 生成配置文件：

```ini
[General]
## false = 回退模式（默认）：缺字时才用 TTF
## true  = 全局替换：所有文字（含英文数字）全用 TTF
# Setting type: Boolean
# Default value: false
ReplaceAllText = false
```

改为 `true` 后**切换场景**生效。配合 [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) 可按 F1 实时切换。

## 两种模式

| | 回退模式 (false) | 全局替换 (true) |
|---|---|---|
| 英文/数字 | 游戏原字体 | TTF |
| 中日韩字符 | TTF 回退 | TTF |
| 触发时机 | TMP 缺字时 | 每次场景加载 |
| 性能 | 无额外开销 | 场景切换时遍历 TMP 组件，毫秒级 |

## 工作原理

```
游戏启动 → Awake() → 扫描 fonts/ → 加载 .ttf/.otf → TMP_FontAsset

场景加载 → OnSceneLoaded()
  ├─ 回退: 注入 TMP_Settings.fallbackFontAssets
  └─ 全局替换: Resources.FindObjectsOfTypeAll<TMP_Text>().font = 外部字体
```

无字体文件时插件静默跳过，不影响游戏。

## 支持格式

- `.ttf`（TrueType）
- `.otf`（OpenType）

可同时放入多个文件，按文件名排序加载。回退模式按顺序尝试字形，全局替换模式用第一个。

## 编译

```bash
# 1. 把 cu-font-loader.csproj 中的 YOUR_GAME 替换为你的游戏路径
# 2. 编译
dotnet build
```

编译目标：.NET 4.8 / Unity 2019.4+ / TextMeshPro 2.x。

## 许可

MIT — 详见 [LICENSE](./LICENSE)
