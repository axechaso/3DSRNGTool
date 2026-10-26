# G6、TinyFinder 与 PokeReader

主工具保留原版三栏设置及完整结果表。G6「野生乱数」左下角的「TinyMT 遭遇设置…」打开独立设置窗口：勾选「计算 TinyMT 遭遇」，填写 PokeReader 种子或四段状态后，关闭设置窗口，仍使用主界面的「计算」按钮和原结果表。设置即时生效，关闭后保留输入；启用时入口会显示「已启用」。这里直接调用整合的 TinyFinder 遭遇算法和地点表，搜索不会跳转到其他窗口。

主 MT 种子、帧范围、同步性格、护符和个体值筛选继续在原位置设置。TinyMT 决定遭遇、槽位、同步、携带物及部分特殊判定，主 MT 决定 PID、个体值和性格；两者的计数不同。新增选项按当前遭遇类型显示，无关选项及标签直接隐藏：普通野生不显示交互帧、模拟器、群战、雷达或 DexNav 专用项；群战可切换走路/蜂蜜，钓鱼和碎岩显示交互帧，DexNav 显示搜索等级及主动搜索选项。NPC、背包、眨眼等参数随地点自动填入，只有勾选「手动调整地点参数」时才展开当前方式可用的校准项。索引预测和原有时间线是两种使用方式，启用其一会取消另一项。

## 功能入口

| 功能 | 位置及参数 |
| --- | --- |
| 普通野生、冲浪、花丛、洞穴 | 原野生界面的地点/地形列表；补齐 XY 和 ORAS 地点、版本、槽位、等级与形态 |
| 朋友原野 | 原类别；选择物种及二/三槽解锁、隐藏特性，采用原版概率 |
| 蜂蜜群战、走路群战 | 原群战类别；新增走路群战选项，计算五只的同步、物种、等级、携带物与 HA 位置 |
| 蜂蜜单体 | 普通野生类别勾选「蜂蜜单体」，使用能够产生单体的地点/地形 |
| 钓鱼、碎岩、洞穴落影 | 原对应类别，补充队伍数、眨眼与交互帧参数 |
| 宝可追踪 | 原类别及连锁数；新增奖励音乐；草丛图与闪草详情在遭遇搜索器查看 |
| DexNav 走路、主动搜索 | 原图鉴导航类别；新增主动搜索、搜索等级、限定种和眨眼参数；整合潜力、HA、强闪和遗传招式名称数据库 |
| TID / SID | 原 ID 界面支持 PokeReader；遭遇搜索器保留完整 ID 及日期搜索 |
| 定点等主 MT 搜索 | 「工具 → G6 MT／PID／EC／群战种子搜索（TinyFinder）」；保留 IV、PID、EC、PID 重掷、PID 重掷等于 EC、多闪群战与 MT 种子 RTC 搜索 |
| TinyMT 索引/日期搜索 | 「工具 → G6 遭遇搜索器（TinyFinder）」或「TinyMT 遭遇设置… → 遭遇搜索器…」；保留原版布局、方式、筛选、RTC、档案、NTR 入口和 Guides 菜单 |

两个搜索器使用纳入项目的原版源码，在同一程序中运行。原窗体设计文件、算法、地点/物种/招式数据和教程地址保持原样；宿主输入、结果回填及窗口关闭清理放在单独的集成文件中。TinyMT 结果右键「回填到 3DSRNGTool」可设置主工具的状态、类别、地点与参数；MT 搜索结果右键可回填主种子。回填 TinyMT 后，原时间线需要重新核对事件帧并生成。

## PokeReader 输入

时间线、G6 ID、蛋乱数状态、G6 野生新增设置和杂项的 G6 TinyMT 均提供 PokeReader 入口。输入 **Tiny u32 seed**，按 `init(seed, 0)` 初始化，不额外推进启动帧。`00000000` 是有效种子，初始化后是非零的四段状态；四段全零状态不等于种子零。

时间线、ID、蛋乱数和野生输入接受 1～8 位十六进制及可选 `0x` 前缀。四段状态的显示/粘贴顺序为 **3、2、1、0**；NTR 和结果导入会切回四段状态模式。杂项和原版搜索器使用其八位十六进制框。

手动时间线可填写种子、主乱数帧和消耗类型后点击「生成」；「标定」用于 NTR 自动校准。种子或模式改变会清除旧结果。PokeReader 种子不能代替事件帧校准。

## 原版 Guides

完整保留以下五项菜单与链接：

- [Initial Seed RNG](https://github.com/Bambo-Rambo/RNG-Guides/blob/main/Gen6MTSeedRNG.md#gen-6-main-mt-seed-rng)
- [TID / SID RNG](https://github.com/Bambo-Rambo/RNG-Guides/blob/main/Gen6TidSidRNG.md#gen-6-tidsid-rng-on-citra)
- [Normal Wild / FS RNG](https://github.com/Bambo-Rambo/RNG-Guides/blob/main/NormalWild-FS-RNG.md#normal-wild---friend-safari-rng)
- [Horde RNG](https://github.com/Bambo-Rambo/RNG-Guides/blob/main/HordeRNG.md#horde-encounter-rng-abuse-guide)
- [DexNav RNG](https://github.com/Bambo-Rambo/RNG-Guides/blob/main/DexNavRNG.md#dexnav-rng-abuse-guide)

## 原时间线修复

- 重复生成使用校准快照，处理空事件队列、短眨眼、负延迟、无效碎岩延迟和长延迟。
- 朋友原野使用 50/50 或 34/33/33 概率。
- 主 MT 按实际帧差推进，修正奇数起点、区间空隙、终点裁剪和遭遇延迟。
- 打开、翻译和隐藏窗口保留参数；修复未连接停止校准和非群战读取 HA 的空引用。
- 遭遇开始后停止 fidget，使主搜索与时间线预览一致。
- 补齐时间线消耗类型、遭遇方式、音乐/雷达提示及 NTR 提示框汉化；首次启动默认中文。

## 验证与来源

固定来源为 [Bambo-Rambo/TinyFinder 的 2133fe7](https://github.com/Bambo-Rambo/TinyFinder/tree/2133fe761958094ed69709aead9d7b162673e737)，说明和原许可通知位于 `ThirdParty/TinyFinder`。保持 .NET Framework 4.5.2。

`TinyFinder-vectors.json` 包含从原仓库独立构建导出的 **456 组**输入/输出，覆盖 12 种 TinyMT 方式。另核对种子 0、1、`12345678`、`FFFFFFFF` 的初始化状态。

回归检查涵盖时间线、RTC、输入切换、所有地点与各类遭遇生成、主界面按版本/方式搜索、槽位筛选、主动 DexNav、雷达进草同步/强闪、原搜索器生成/回填、全部 MT 类别及实际 IV 搜索。蓝色海兔兽、蓝条纹野蛮鲈鱼、花色和南瓜大小的扩展编号转换为主工具的物种/形态编码。原设计文件另经字节核对与本地窗体绘制检查。

界面回归检查覆盖重复切换全部游戏版本与分页时结果表尺寸不变，各遭遇类型/子模式的选项与标签显隐，以及设置窗口关闭/重开保留参数、与时间线互斥、切换模式自动隐藏和随主窗口释放。

目前属于代码和对照数据验证，尚未逐场景完成实机/模拟器 NTR 与交互时机验证。主工具的延迟仍按所选模板和场景设置；雷达连锁产生草丛后，进入草丛时的同步还需另行确定。

```powershell
msbuild 3DSRNGTool.sln /p:Configuration=Release
.\Tests\Gen6\bin\Release\Gen6.Tests.exe
.\Tests\EncounterTables\bin\Release\EncounterTables.Tests.exe
```

缺少 4.5.2 引用程序集时，参照 [遭遇表检查说明](../EncounterTables/README.md)。
