# cu-font-loader

`cu-font-loader` 是给 **Casualties: Unknown** 用的 BepInEx 字体加载插件。它会从外部加载 `.ttf` / `.otf` 字体，并在运行时应用到 TextMeshPro 文本上。

这个插件主要用于把游戏里的像素字体替换成更易读的字体，尤其适合简体中文。

## 功能

- 从 `BepInEx/plugins/cu-font-loader/fonts/` 加载字体
- 支持 `.ttf` 和 `.otf`
- 可作为 TextMeshPro fallback 字体补缺字
- 可运行时替换已有文本和后续生成的文本
- 同时处理 TextMeshPro 文本和旧版 `UnityEngine.UI.Text`
- 替换时保留游戏原字体作为 fallback，避免符号、图标或缺字变成方块
- 不修改游戏资源包
- 不依赖 Harmony

## 安装

1. 确保游戏已经安装 BepInEx。
2. 把 `cu-font-loader.dll` 放到：

```text
BepInEx/plugins/cu-font-loader/cu-font-loader.dll
```

3. 把你想用的 `.ttf` 或 `.otf` 字体放到：

```text
BepInEx/plugins/cu-font-loader/fonts/
```

示例：

```text
BepInEx/
  plugins/
    cu-font-loader/
      cu-font-loader.dll
      fonts/
        YourFont.ttf
```

4. 启动游戏一次，插件会自动生成配置文件。

## 配置

配置文件路径：

```text
BepInEx/config/fenchen.cu-font-loader.cfg
```

### ReplaceMode

```ini
[General]
ReplaceMode = FallbackOnly
ScanIntervalSeconds = 1
```

可选模式：

| 模式 | 效果 | 适合情况 |
|---|---|---|
| `FallbackOnly` | 只把外部字体加入 fallback，不主动替换游戏字体 | 最安全，只想补缺字 |
| `ReplaceOnce` | 发现 TMP 文本时替换一次；后续新生成的文本会通过低频扫描补上 | 推荐的字体替换模式 |
| `Persistent` | 如果游戏把字体改回去，插件会低频再改回来 | 仍有部分文字保持像素字体时再用 |

`ReplaceOnce` 和 `Persistent` 会按 `ScanIntervalSeconds` 低频扫描新出现的文字。默认是 `1` 秒。

不建议把扫描间隔设得太小。默认值一般就够了。

### Font

```ini
[Font]
AtlasSize = 4096
SamplingPointSize = 36
AtlasPadding = 5
```

`AtlasSize` 控制外部字体的动态 TMP 图集大小。中文字符很多，如果图集太小，TMP 会因为放不下新字形而回退到游戏原字体，看起来就像仍然有部分像素字。

默认 `4096` 比旧版的 `512` 更适合中文。一般不需要改；如果你的显存非常紧张，可以降到 `2048`，但残留像素字的概率会变高。

### Debug

```ini
[Debug]
LogTextDetails = false
MaxLoggedTexts = 80
```

如果某个界面仍然没有被替换，可以临时把 `LogTextDetails` 改成 `true`。插件会在日志里输出 TMP 文本对象的名称、激活状态、字体名、材质名和文本片段，方便定位那部分 UI 到底用了什么字体链。

平时建议保持关闭。

### 旧配置兼容

旧版本使用：

```ini
ReplaceAllText = true
```

这个选项仍然保留。如果 `ReplaceMode` 还没有写入配置文件，那么 `ReplaceAllText = true` 会让插件默认进入 `ReplaceOnce` 模式。

新配置建议直接使用 `ReplaceMode`。

## 替换原理

插件不会硬改游戏资源包里的字体文件。

替换时大概是这样：

```text
某个 TMP 文本原本使用游戏字体 A
插件把它改成外部字体 B
插件同时把游戏字体 A 加入字体 B 的 fallback 列表
```

这样视觉上会优先使用你的外部字体；如果外部字体缺少某些字符、图标或特殊符号，TextMeshPro 仍然可以回退到游戏原字体。

## 为什么不直接替换游戏内置字体？

游戏字体通常是打包进 Unity 资源里的 `TMP_FontAsset`，不一定会以单独 `.ttf` 文件存在。

直接改资源包风险更高：

- 游戏更新后容易被覆盖
- 资源包编辑失败可能导致游戏资源损坏
- 卸载 mod 不方便

BepInEx 运行时替换更干净：删除插件后，游戏就会恢复原样。

## 编译

项目不会内置任何人的本机游戏路径。编译时需要告诉 MSBuild 游戏安装目录。

方式一：命令行传入 `GameDir`：
```bash
dotnet build -p:GameDir="D:\path\to\Casualties Unknown Demo"
```

方式二：设置环境变量 `CU_GAME_DIR` 后直接编译：

```powershell
$env:CU_GAME_DIR="D:\path\to\Casualties Unknown Demo"
dotnet build
```

输出文件：

```text
bin/cu-font-loader.dll
```

`GameDir` 应指向包含 `BepInEx/` 和 `CasualtiesUnknown_Data/` 的游戏根目录。

## 许可

MIT。详见 [LICENSE](./LICENSE)。
