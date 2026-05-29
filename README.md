# cu-font-loader

为 Casualties: Unknown（及任何使用 TextMeshPro 的 Unity 游戏）注入 / 替换外部字体的 BepInEx 插件。

> **注意**：当前 master/commit 包含 v1.1.0 源码（含全局替换功能），但尚未打包发布为 GitHub Release。
> 下载预编译 DLL 请等待 Release 页面更新。

## 安装

1. 确保游戏已安装 [BepInEx](https://github.com/BepInEx/BepInEx)
2. 将 `cu-font-loader` 文件夹复制到 `BepInEx/plugins/`
3. 将 `.ttf` 或 `.otf` 字体文件放入 `BepInEx/plugins/cu-font-loader/fonts/`
4. 启动游戏

```
BepInEx/
├── core/
│   ├── BepInEx.dll
│   └── 0Harmony.dll          ← v1.1.0 起需要
└── plugins/
    └── cu-font-loader/
        ├── cu-font-loader.dll
        └── fonts/
            ├── YourFont.ttf
            └── AnotherFont.otf
```

## 配置（v1.1.0+）

首次运行后会在 `BepInEx/config/fenchen.cu-font-loader.cfg` 生成配置文件：

```ini
[General]
## false = 回退模式（默认）：游戏缺字时才用 TTF 渲染
## true  = 全局替换模式：所有 TMP 文本（含英文数字）全部用 TTF
# Setting type: Boolean
# Default value: false
ReplaceAllText = false
```

### 运行时切换（推荐）

安装 [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，进游戏按 **F1** 打开面板，找到 `ReplaceAllText` 打勾/取消，立即生效无需重启。

## 两种模式对比

| | 回退模式 (ReplaceAllText=false) | 全局替换 (ReplaceAllText=true) |
|---|---|---|
| 英文/数字 | 游戏原字体 | 你的 TTF |
| 中日韩字符 | 你的 TTF | 你的 TTF |
| 适用场景 | TTF 补全缺字 | 统一整套字体风格 |
| 需要 Harmony | 否 | 是 (BepInEx 自带) |

## 支持格式

- `.ttf`（TrueType）
- `.otf`（OpenType）

可同时放入多个字体文件，按文件名排序加载。TMP 按回退表顺序尝试字形。

## 编译

```bash
# 1. 把 cu-font-loader.csproj 中的 YOUR_GAME 替换为你的游戏路径
# 2. 确保 BepInEx 已装（需要 BepInEx.dll + 0Harmony.dll）
# 3. 编译
dotnet build
```

编译目标：.NET 4.8 / Unity 2019.4 / TextMeshPro 2.x / Harmony 2.x。

## 许可

MIT — 详见 [LICENSE](./LICENSE)
