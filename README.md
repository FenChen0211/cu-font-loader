# CU Font Loader — Custom Font & Fallback Framework for Casualties: Unknown

`cu-font-loader` 是一个面向 **Casualties: Unknown** 的独立 BepInEx 插件，提供自定义字体替换与 CJK 字形回退（glyph fallback）框架，用于改善游戏内文字的可读性与无障碍体验。

- **Custom font replacement**：从外部加载 `.ttf` / `.otf`，运行时替换 TextMeshPro 字体
- **CJK glyph fallback**：缺字自动回退到游戏原字体，图标、符号不变成方块
- **Mod UI compatibility**：保守的兼容性策略，尽量不碰其他 Mod 的自定义 UI、字体和材质
- **No game asset modification**：不修改任何游戏资源文件和 AssetBundle，卸载即恢复原样
- **Independent**：标准 BepInEx 插件，无 Harmony，不依赖 CUCoreLib / ScavLib / RshLib，也不引用游戏内部程序集类型

> **v1.4.0 是 Compatibility Update（兼容性更新）。**
> 本版本重点是与现有 BepInEx Mod 生态和平共处。由于尚未对大型 Modpack 做充分测试，
> 如果你遇到其他 Mod 的 UI 被误伤、字体显示异常或替换不生效，请到
> [Issues](https://github.com/FenChen0211/cu-font-loader/issues) 反馈（附上 BepInEx 日志更佳）。

## 安装

1. 确保游戏已经安装 BepInEx（5.x）。
2. 把插件解压到游戏根目录，得到如下结构：

```text
BepInEx/
  plugins/
    cu-font-loader/
      cu-font-loader.dll
      fonts/
        README.txt   <- 占位文件，把字体和它放在一起
```

3. 把你想使用的 `.ttf` 或 `.otf` 字体放进 `fonts/` 文件夹。
4. 启动游戏一次，插件会自动生成配置文件。

配置文件路径：

```text
BepInEx/config/fenchen.cu-font-loader.cfg
```

## 替换模式（ReplaceMode）

```ini
[General]
ReplaceMode = Normal
ScanIntervalSeconds = 1
```

| 模式 | 行为 | 适合情况 |
|---|---|---|
| `FallbackOnly` | 只把外部字体加入 TMP fallback，不主动替换任何字体 | 最安全；只想补中文缺字，不改变原有字形风格 |
| `Safe` | 只替换"普通"文本：跳过含 `<sprite>` 标签、自定义材质、自定义 Shader 的文本 | 想替换字体但装了不少其他 Mod |
| `Normal` | 替换大多数文本，仍保留其他 Mod 的自定义 UI 与材质（默认） | 推荐的日常模式 |
| `Aggressive` | 接近旧版 `Persistent`：每轮扫描强制替换所有未排除的文本，包括自定义材质 | 明确需要强制全局替换时使用 |

`Safe` / `Normal` 对每个文本和 TMP 输入框只替换一次，之后不会强行改回；`Aggressive` 会持续对抗游戏对字体或材质的重新赋值。

### 从旧版本迁移

| 旧配置 | 新行为 | 说明 |
|---|---|---|
| `ReplaceMode = FallbackOnly` | 保持 `FallbackOnly` | 不变 |
| `ReplaceMode = ReplaceOnce` | 自动迁移为 `Normal` | 首次启动时自动改写配置文件并写日志 |
| `ReplaceMode = Persistent` | 自动迁移为 `Aggressive` | 同上 |
| `ReplaceAllText = true` | 已废弃 | 不再产生作用；对应的旧需求由 `ReplaceMode` 承接 |

注意：迁移会把配置文件里的旧值改写成新值。如果之后回退到 v1.3.2，旧版本无法识别 `Normal` / `Aggressive`，会按默认值 `FallbackOnly` 处理（重新设置即可）。

## 兼容性保护（Compatibility）

其他 Mod 可能创建自己的 TMP UI，使用自定义 `TMP_FontAsset`、Glow / Outline 材质、自定义 Shader 或图标字体。本插件的处理原则是：**宁可少替换，也不破坏其他 Mod 的 UI**。

```ini
[Compatibility]
PreserveCustomMaterials = true
ExcludedFontNames =
ExcludedMaterialNames =
ExcludedObjectNames =
```

### PreserveCustomMaterials

- `true`（默认）：`Safe` / `Normal` 模式下，材质不是当前字体默认材质的文本（描边、辉光、自定义 Shader 等）会被**整体跳过**——不替换字体，也不动材质
- `false`：`Safe` / `Normal` 也会替换这些文本的字体和材质
- `Aggressive`：无论此选项为何值，都会恢复 v1.3.2 的强制行为并持续替换字体和材质

字体与材质替换是分离的：默认的 `Safe` / `Normal` 替换只改 `text.font`，只有当原材质就是原字体的默认材质时才会同步替换成新字体的默认材质；关闭保护或使用 `Aggressive` 时会强制同步材质。

### 排除规则

三个选项都是**逗号分隔的关键字列表**，不区分大小写，**包含匹配**（名称里含有关键字即命中），留空表示不排除：

```ini
ExcludedFontNames = FancyModFont, IconFont
ExcludedMaterialNames = FancyGlow
ExcludedObjectNames = SomeModMenuTitle, SomeModIcon
```

- `ExcludedFontNames`：按当前字体（FontAsset）名称匹配
- `ExcludedMaterialNames`：按当前材质名称匹配
- `ExcludedObjectNames`：按 GameObject 名称匹配（TMP 与旧版 `UnityEngine.UI.Text` 都生效）

排除规则在**所有替换模式（包括 Aggressive）**下都生效。改变配置后立即生效，无需重启游戏。

### 已知限制

- 插件无法可靠地识别"其他 Mod 创建的字体"——Mod 字体和游戏字体在运行时没有可靠区别，需要排除规则兜底
- 含 `<sprite>` 标签的文本在 `Safe` 模式会被跳过；`Normal` / `Aggressive` 会替换其字体（精灵图渲染不受字体替换影响）
- 如果某些游戏 UI 使用了非默认材质，`Normal` 模式会跳过它们，看起来像"没替换到"——这通常是保护机制在工作，可用 CompatibilityLog 确认

## 调试（Debug）

```ini
[Debug]
CompatibilityLog = false
MaxCompatibilityLogs = 200
LogTextDetails = false
MaxLoggedTexts = 80
```

排查"某个 UI 为什么没被替换 / 被误替换"时，把 `CompatibilityLog` 改成 `true`。插件会输出单行决策记录：

```text
[Compat] Scene=MainMenu Object=SomeModMenuTitle Font=FancyModFont Material=FancyGlowMaterial Action=Skipped Reason=CustomMaterial
[Compat] Scene=Game Object=DamageText Font=LibSans SDF Material=LibSans SDF Atlas Action=Replaced From=PixelFont
[Compat] Scene=Game Object=DamageText Font=LibSans SDF Material=LibSans SDF Atlas Action=MaterialFixed From=TemporaryGlow
```

- `Action=Replaced` 时 `From=` 显示替换前的字体
- `Action=MaterialFixed` 表示 `Aggressive` 模式只修复了材质，`From=` 显示替换前的材质
- `Reason=` 说明跳过原因：`Excluded`（命中排除规则）、`ExcludedMaterial`、`CustomMaterial`、`CustomShader`、`SpriteTag`、`MaterialMissing`、`FontMissing`
- 同一对象的同一决策只记录一次，不会刷屏；整个会话最多记录 `MaxCompatibilityLogs` 条

`LogTextDetails` 是旧的诊断开关，会把扫描到的 TMP 文本对象完整列出来，仅在深度排查时使用。

## 字体（Font）

```ini
[General]
PrimaryFont =

[Font]
AtlasSize = 4096
SamplingPointSize = 36
AtlasPadding = 5
```

- 加载了多个字体时，`PrimaryFont`（按文件名，不含扩展名）决定用哪一个作为替换字体，其余字体只作为 fallback；留空使用按文件名排序后的第一个
- `AtlasSize` 控制外部字体的动态 TMP 图集大小。中文字符很多，图集太小会频繁回退到游戏原字体（看起来像残留像素字）。默认 `4096` 适合中文；显存紧张可降到 `2048`

## 语言切换刷新

部分本地化 UI 会在启动早期生成，需要游戏重载本地化界面后才会完全刷新。如果安装插件后仍看到少量字体没变，可以在游戏内切换一次语言（中文 → English → 中文），场景重载后插件会重新扫描。

## 替换原理

插件不会修改游戏资源包：

```text
某个 TMP 文本原本使用游戏字体 A
插件把它改成外部字体 B（只在原材质是默认材质时同步默认材质）
插件把字体 A 加入字体 B 的 fallback 列表
外部字体同时加入 TMP 全局 fallback
```

缺字、图标和特殊符号仍然可以回退到游戏原字体渲染。删除插件目录即可完全恢复原样。

## 编译

项目不会内置任何人的本机游戏路径。编译时需要告诉 MSBuild 游戏安装目录。

方式一：命令行传入 `GameDir`

```powershell
dotnet build -p:GameDir="D:\path\to\Casualties Unknown Demo"
```

方式二：设置环境变量 `CU_GAME_DIR` 后直接编译

```powershell
$env:CU_GAME_DIR="D:\path\to\Casualties Unknown Demo"
dotnet build
```

输出文件：

```text
bin/cu-font-loader.dll
```

`GameDir` 应指向包含 `BepInEx/` 和 `CasualtiesUnknown_Data/` 的游戏根目录。

发布包（dist/*.zip）结构即上文安装部分的结构：`BepInEx/plugins/cu-font-loader/` 下只有 dll 和空的 `fonts/`（含占位说明文件），不打包任何字体文件。

## 许可

MIT。详见 [LICENSE](./LICENSE)。
