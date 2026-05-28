# cu-font-loader

为 Casualties: Unknown（及任何使用 TextMeshPro 的 Unity 游戏）注入外部字体的 BepInEx 插件。

## 安装

1. 确保游戏已安装 [BepInEx](https://github.com/BepInEx/BepInEx)
2. 将 `cu-font-loader` 文件夹复制到 `BepInEx/plugins/`
3. 将 `.ttf` 或 `.otf` 字体文件放入 `BepInEx/plugins/cu-font-loader/fonts/`
4. 启动游戏

```
BepInEx/
└── plugins/
    └── cu-font-loader/
        ├── cu-font-loader.dll
        └── fonts/
            ├── YourFont.ttf
            └── AnotherFont.otf
```

## 工作原理

```
游戏启动
  → BepInEx 加载 cu-font-loader.dll
  → Awake() 扫描 fonts/ 目录
  → 发现 .ttf/.otf → Font(文件路径) → TMP_FontAsset.CreateFontAsset()
  → 注册 SceneManager.sceneLoaded 回调
  → 每次场景切换 → TMP_Settings.fallbackFontAssets.Add(外部字体)
  → 游戏中 TMP 文本缺字 → 自动回退到外部字体
```

没有字体文件时插件静默跳过，不影响游戏。

## 支持格式

- `.ttf`（TrueType）
- `.otf`（OpenType）

可同时放入多个字体文件，按文件名排序加载。TMP 按回退表顺序尝试字形。

## 编译

```bash
# 1. 把 cu-font-loader.csproj 中的 YOUR_GAME 替换为你的游戏路径
# 2. 编译
dotnet build
```

编译目标：.NET 4.8 / Unity 2019.4 / TextMeshPro 2.x。

## 许可

MIT — 详见 [LICENSE](./LICENSE)
