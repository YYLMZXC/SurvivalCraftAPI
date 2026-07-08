# Root Motion 位移剥离：从全局后处理改为 per-source 源头剥离

**分支**：`feature/root-motion-per-layer-strip`（基于 `2d6456e6`）
**日期**：2026-07-08
**状态**：设计定稿，待实现

---

## 1. 背景

### 1.1 现状（`2d6456e6`，本设计起点）

root motion 位移剥离（清根骨骼 translation，防"动画位移 + 物理位移"双倍）目前是**全局后处理**：`AnimationController.ComputeBoneTransforms` 在 `BlendLayers` 混合完最终 `boneTransforms` 后，按控制器唯一的 `m_currentRootMotionConfig` 决定是否清根骨骼链 translation。

```csharp
// ComputeBoneTransforms 现状（将被删除）
if (m_currentRootMotionConfig != null
    && m_currentRootMotionConfig.Translation.Mode != TranslationMode.None
    && m_currentRootMotionConfig.Translation.Mode != TranslationMode.AddImpulse) {
    // 从 SourceBone 走到 RootBone，每骨 translation := rest
}
```

### 1.2 问题

config 切换（如 Override 动画 A 切出 → AddImpulse/None 动画 B）瞬间，`m_currentRootMotionConfig` 立即变为 B 的 config（不剥离），但 `AnimationTransition` 仍在混合 A 的源快照（含 A 的累计根骨骼 translation，已由 root motion 转为物理速度）→ 不剥离会瞬移。

更根本：**blend 期同时存在两段动画（源 A / 目标 B），单一控制器 config 无法表达各自剥离需求**。全局后处理对混合结果整条清根 → 当 B 不需剥离（AddImpulse）时连坐剥掉 B 的自然位移。

### 1.3 已尝试的最小修（`feature/gltf-player-bone-aliases` 分支 `f5ff10da`）

加残留计时器 `m_rootMotionStripRemain`：config 切出时武装，blend 期继续全局剥离。修了长 blend / 硬切 / SourceBone 三处缺口，但**缺口 3（Override→AddImpulse 残留误剥新动画）是全局后处理方案固有**，残留窗口（= blendDuration）内仍误剥 B。本设计是其替代方案。

---

## 2. 目标 / 非目标

### 目标
1. 根治缺口 3：blend 期源/目标各按自身 config 剥离，互不连坐。
2. 一并消除残留计时器方案的全部缺口（长 blend 提前结束、硬切误武装、SourceBone 剥错链）。
3. 删除全局后处理剥离块与残留计时器，剥离覆盖期**精确等于 blend 状态生命期**（无计时、无漂移）。
4. 保持 root motion 物理提取（`ApplyRootMotion`）行为不变。

### 非目标
- 不改变 root motion 是 **Base 层专属** 的语义；非 Base 层不剥离（config 恒 null）。
- 不引入多层（非 Base）root motion 配置机制（YAGNI）。
- 不改 `TranslationMode` 语义：`Override`/`Blend` 剥离，`AddImpulse`/`None` 不剥离。
- 不改 `ApplyRootMotion` 的速度/冲量提取逻辑，只改它读取 config 的来源。
- 不动 driver 产生 pose 的剥离（过程性动画无 root motion translation）。

---

## 3. 架构概览

**核心思想**：每个动画源（活动 player / transition 源快照 / transition 目标 / Override 渐入源 / Override 渐降 live 源 / deactivate 源）在**采样点**按**自身 config** 剥离根骨骼 translation，**再混合**。混合结果天然干净，无需后处理。

**信息传递**：剥离需求 `RootStripInfo` 在 config 切换瞬间（`layer.SetRootMotionConfig`，此时 old/new 都已知）**盖戳**到运行中的 blend 状态（transition / activate / deactivate）上。info 寿命 == blend 状态寿命，状态完成/取消时清除 → 剥离覆盖期 == blend 期，无需计时器。

```
config 切换 (old → new)
  └─ layer.SetRootMotionConfig(new)
       ├─ oldInfo = RootStripInfo.From(old)
       ├─ newInfo = RootStripInfo.From(new)
       ├─ 给运行中 blend 状态盖戳 (source=oldInfo, target=newInfo)
       └─ m_rootMotionConfig = new   ← 稳态/目标用

每帧采样 (SampleTransforms)
  └─ 各源按自身 info 剥根 → 混合
```

---

## 4. 组件

### 4.1 `RootStripInfo`（新，值类型）

**新文件** `Engine/Engine.Animation/RootStripInfo.cs`：

```csharp
namespace Engine.Animation {
    /// <summary>
    /// 根骨骼位移剥离需求（值类型，default = 不剥离）。
    /// 由 RootMotionConfig 在 config 切换时派生，盖戳到运行中 blend 状态。
    /// </summary>
    public readonly struct RootStripInfo {
        public readonly bool NeedsStrip;
        public readonly string SourceBone;   // 剥离链起点；config.SourceBone 或 rootBoneName 回退

        public RootStripInfo(bool needsStrip, string sourceBone) {
            NeedsStrip = needsStrip;
            SourceBone = sourceBone;
        }

        /// <summary>从 root motion 配置派生剥离需求。None/AddImpulse/null → 不剥离。</summary>
        public static RootStripInfo From(RootMotionConfig config, string fallbackBone) {
            if (config?.Translation is { Mode: var mode }
                && mode != TranslationMode.None
                && mode != TranslationMode.AddImpulse) {
                string bone = !string.IsNullOrEmpty(config.SourceBone)
                    ? config.SourceBone
                    : fallbackBone;
                return new RootStripInfo(true, bone);
            }
            return default;
        }
    }
}
```

`default`（`NeedsStrip=false`）= 不剥离。非 Base 层 config 恒 null → `From` 返回 `default` → 天然不剥离，无需特判。

### 4.2 `RootMotionStrip`（新，静态剥离原语）

把现 `ComputeBoneTransforms` 内那段循环抽成静态方法（与 `RootStripInfo` 同文件或新文件 `RootMotionStrip.cs`）：

```csharp
public static class RootMotionStrip {
    /// <summary>
    /// 从 SourceBone 走到模型根骨骼，每骨 translation := rest（保留 rotation/scale）。
    /// NeedsStrip=false 直接返回。骨骼缺失回退 model.RootBone；cur==null 跳过（无 NPE）。
    /// </summary>
    public static void StripRootTranslation(Matrix?[] boneTransforms, Model model, RootStripInfo info) {
        if (!info.NeedsStrip || boneTransforms == null || model == null) return;
        ModelBone found = !string.IsNullOrEmpty(info.SourceBone)
            ? model.FindBone(info.SourceBone, throwIfNotFound: false)
            : null;
        ModelBone cur = found ?? model.RootBone;
        while (cur != null) {
            if (boneTransforms[cur.Index].HasValue) {
                Matrix t = boneTransforms[cur.Index].Value;
                t.Decompose(out _, out Quaternion rot, out _);
                Vector3 restTrans = cur.Transform.Translation;
                boneTransforms[cur.Index] = Matrix.CreateFromQuaternion(rot) * Matrix.CreateTranslation(restTrans);
            }
            if (cur == model.RootBone) break;
            cur = cur.ParentBone;
        }
    }
}
```

层、transition 共用。幂等（translation 已是 rest 时再剥无变化）。

### 4.3 `AnimationTransition`（改）

**新增字段**：
```csharp
RootStripInfo m_sourceStripInfo;   // 源快照剥离需求（crossfade 源 / deactivate 源）
RootStripInfo m_targetStripInfo;   // 目标 player 剥离需求（仅 crossfade；deactivate 不用）
```

**新增方法**：
```csharp
public void SetStripInfo(RootStripInfo source, RootStripInfo target) {
    m_sourceStripInfo = source;
    m_targetStripInfo = target;
}
```

**`SampleTransforms` 改**：
- **入口统一剥源快照（幂等）**：活动时先 `StripRootTranslation(m_sourceTransforms, model, m_sourceStripInfo)`。源冻结，首帧剥后 translation=rest，后续帧再剥无变化（原地改写安全）。
- **deactivate 分支**：源已剥，`BlendTransforms(stripped_source, Matrix.Identity, progress)` 淡出到 Identity。
- **crossfade 分支**：目标采样进临时 `targetTransforms` 后 `StripRootTranslation(targetTransforms, model, m_targetStripInfo)`；与已剥源按 progress `BlendTransforms` 写入 `boneTransforms`。
- **进度 0/1 早返回分支**（纯目标采样，无源混合）：采样后剥 `m_targetStripInfo`。

**清除**：`CompleteTransition` 与 `CancelTransition` 内 `m_sourceStripInfo = m_targetStripInfo = default;`

### 4.4 `AnimationLayer`（改）

**新增字段**：
```csharp
public RootMotionConfig m_rootMotionConfig;           // 当前/目标 config（Base 专属；非 Base 恒 null）
RootStripInfo m_activateSourceStripInfo;              // Override 渐入冻结源剥离需求
RootStripInfo m_deactivateStripInfo;                  // Override 权重渐降 live 源剥离需求
```

**新增编排方法**（config 切换的唯一入口）：
```csharp
public void SetRootMotionConfig(RootMotionConfig newConfig, Model model, string rootBoneName) {
    RootStripInfo oldInfo = RootStripInfo.From(m_rootMotionConfig, rootBoneName);
    RootStripInfo newInfo = RootStripInfo.From(newConfig, rootBoneName);

    if (m_transition?.IsActive == true) {
        // crossfade：源=old，目标=new；deactivate 变体：源=old，目标=None
        m_transition.SetStripInfo(oldInfo, m_transition.IsDeactivateTransition ? default : newInfo);
    }
    else if (m_activating) {
        m_activateSourceStripInfo = oldInfo;   // 目标(主player)用 new(=m_rootMotionConfig 更新后)
    }
    else if (m_deactivating) {
        m_deactivateStripInfo = oldInfo;       // 渐降 live 源用旧 config
    }
    // else 稳态：活动 player 采样按 new(=m_rootMotionConfig 更新后)剥

    m_rootMotionConfig = newConfig;
}
```

**`SampleTransforms` 改**（各分支加剥离）：
- `m_activating` 分支：采样目标主 player 入 `boneTransforms` 后 `StripRootTranslation(boneTransforms, model, RootStripInfo.From(m_rootMotionConfig, m_rootBoneName))`；源 `m_activateSourceTransforms` 用 `m_activateSourceStripInfo` 剥（幂等，可原地）；再 `BlendTransforms` 混合。
- `m_transition?.IsActive` 分支：交由 `transition.SampleTransforms` 内部剥（已带 info）。
- 普通 player 采样分支（含 `m_deactivating`、preservePose、holdPose）：
  ```csharp
  RootStripInfo info = m_deactivating ? m_deactivateStripInfo
                                      : RootStripInfo.From(m_rootMotionConfig, m_rootBoneName);
  // 采样后 StripRootTranslation(boneTransforms, model, info)
  ```
- driver 分支：不剥。

**清除 info**：`CancelTransitioning`（清 `m_activateSourceStripInfo`/`m_deactivateStripInfo` 对应项）、渐入完成（清 `m_activateSourceStripInfo`）、渐降完成（清 `m_deactivateStripInfo`）。

### 4.5 `AnimationController`（改）

- **删**：`ComputeBoneTransforms` 内的全局后处理剥离块（本分支 `2d6456e6` 上的 `isAddImpulse` 版本，约 1151-1179 行）。
  - 注：残留计时器相关符号（`m_rootMotionStripRemain`/`RootMotionStripGuardTime`/`Update` 步骤 6.5/`NeedsStrip`/`m_stripSourceBone`）属于**另一分支** `feature/gltf-player-bone-aliases` 的最小修（`f5ff10da`），**本分支不存在**，无须删除。
- **`m_currentRootMotionConfig` 改为 getter**：
  ```csharp
  AnimationLayer BaseLayer => m_layers?.FirstOrDefault(l => l.Index == 0);
  public RootMotionConfig m_currentRootMotionConfig => BaseLayer?.m_rootMotionConfig;
  public bool HasRootMotion => m_currentRootMotionConfig != null;
  ```
  （保留 `m_currentRootMotionConfig` 名为字段→属性，最小化 `ApplyRootMotion` 等读点改动；若有编译冲突改属性名或加转发。）
- **`SetRootMotionConfig(RootMotionConfig config)`**：
  ```csharp
  public void SetRootMotionConfig(RootMotionConfig config) {
      if (BaseLayer != null) {
          BaseLayer.SetRootMotionConfig(config, m_model, RootBoneName);  // 存 config + 盖戳 blend 状态
      }
      m_currentAnimationName = null;
      m_parentChainRotation = Quaternion.Identity;
      m_translationApplier.Reset();
  }
  ```
- `ApplyRootMotion` / `HasRootMotion` 读 `m_currentRootMotionConfig`（现 getter）逻辑不变。

---

## 5. 数据流（逐采样路径）

| 路径 | 源 | 源剥离 info | 目标 | 目标剥离 info |
|---|---|---|---|---|
| 稳态活动 player | player pose | `From(m_rootMotionConfig)` | — | — |
| transition crossfade | `m_sourceTransforms`（冻结源） | `m_sourceStripInfo`(old) | 目标 player 采样 | `m_targetStripInfo`(new) |
| transition deactivate | `m_sourceTransforms` | `m_sourceStripInfo`(old) | —（淡出到 Identity） | — |
| Override 渐入（m_activating） | `m_activateSourceTransforms`（冻结源） | `m_activateSourceStripInfo`(old) | 主 player 采样 | `From(m_rootMotionConfig)`(new) |
| Override 权重渐降（m_deactivating） | 主 player 采样（live 源） | `m_deactivateStripInfo`(old) | —（靠 Weight fade） | — |
| preservePose / holdPose | player pose | `From(m_rootMotionConfig)` | — | — |
| driver | driver pose | —（不剥） | — | — |

每路径：**采样 → 按各自 info 剥根 → 混合**。

---

## 6. 盖戳规则与边界

### 6.1 盖戳时机

config 切换发生在 blend 状态**已启动之后**：
- `ApplyAnimationToLayer`：先 `layer.PlayAnimationWithTransition`（启动 transition / activate，捕获源快照），后 `SetRootMotionConfig`（盖戳）。
- `EvaluateStateRules` Base 停用：先 `DeactivateAndClear`（启动渐降 / deactivate transition），后 `SetRootMotionConfig(null)`（盖戳）。

故 `layer.SetRootMotionConfig` 内能见到运行中状态，正确盖戳。

### 6.2 `rootBoneName` 来源

`RootStripInfo.From` 与 `StripRootTranslation` 需要 fallback 骨名（=控制器 `RootBoneName`）。方案：
- `layer.SetRootMotionConfig(newConfig, model, rootBoneName)` 由控制器传入 `RootBoneName`，层缓存为 `m_rootBoneName` 字段供 `SampleTransforms` 用。
- `SampleTransforms` 当前签名 `(Matrix?[], Model)`，不传骨名 → 层存 `m_rootBoneName`（在 `SetRootMotionConfig` 时同步更新）。

### 6.3 边界 / 错误处理

- **config null / Translation null**：`RootStripInfo.From` 返回 `default`，不剥。
- **骨骼未找到**：`StripRootTranslation` 回退 `model.RootBone`；`cur==null` 跳过循环，无 NPE。
- **transition 中断重采源**（`PlayAnimationWithTransition` 中断现有 transition，采当前渲染姿态作新源）：随后 `SetRootMotionConfig` 用当前 `m_rootMotionConfig`（被中断 anim 的 config）盖源戳——近似合理（中断是边界场景）。
- **多次连续切换**：每次 `SetRootMotionConfig` 用当前 `m_rootMotionConfig`(old) 重新盖戳，覆盖旧 info。无残留累积。
- **blend 完成清除**：transition `Complete/Cancel`、渐入完成、渐降完成、`CancelTransitioning` 各清对应 info，防陈旧 info 跨动画泄漏。
- **非 Base 层**：永不调 `SetRootMotionConfig` → `m_rootMotionConfig` 恒 null → `From` 返回 `default` → 不剥。
- **硬切（blendDuration=0，`PlayAnimation`）**：无 blend 状态，`SetRootMotionConfig` 走稳态分支，`m_rootMotionConfig=new`，活动 player 按 new 剥；旧动画无快照残留 → 无误剥。

### 6.4 覆盖期 == blend 期（缺口对照）

| 缺口 | 计时器方案 | 本设计 |
|---|---|---|
| 1 长 blend(>0.4s) 残留提前结束 | 计时固定 0.4s | info 寿命 == transition 寿命，blend 多长剥多长 ✓ |
| 2 SourceBone≠RootBoneName 剥错链 | 残留期回退 RootBoneName | info 携带各自 SourceBone，源/目标各剥各链 ✓ |
| 3 Override→AddImpulse 残留误剥新动画 | 全局剥离连坐 B | 源按 old 剥、目标按 new(None)不剥，互不连坐 ✓ |
| 4 硬切误武装残留 | blendDuration=0 仍武装 | 无 blend 状态则无 info，不剥 ✓ |

---

## 7. 文件改动清单

| 文件 | 改动 |
|---|---|
| `Engine/Engine.Animation/RootStripInfo.cs` | **新**：`RootStripInfo` 值类型 + `RootMotionStrip.StripRootTranslation` 静态方法（或拆 `RootMotionStrip.cs`） |
| `Engine/Engine.Animation/AnimationTransition.cs` | 加 `m_sourceStripInfo`/`m_targetStripInfo` + `SetStripInfo`；`SampleTransforms` 各分支剥；`Complete`/`Cancel` 清除 |
| `Engine/Engine.Animation/AnimationLayer.cs` | 加 `m_rootMotionConfig`/`m_activateSourceStripInfo`/`m_deactivateStripInfo`/`m_rootBoneName` + `SetRootMotionConfig`；`SampleTransforms` 各分支剥；完成/取消清 info |
| `Engine/Engine.Animation/AnimationController.cs` | 删全局剥离块 + 残留计时相关；`m_currentRootMotionConfig`→getter；`SetRootMotionConfig` 委派层；`BaseLayer` 访问器 |

依赖确认（不改，仅引用）：
- `RootMotionConfig`（`Translation`、`SourceBone`）位于 `Engine/Engine.Animation/RootMotion/RootMotionConfig.cs`
- `TranslationMode` 枚举值：`None`/`Blend`/`AddImpulse`/`Override`

---

## 8. 测试

### 8.1 单元（若有测试工程）
1. `RootStripInfo.From`：四种 `TranslationMode` + null config → 只有 `Blend`/`Override` 返回 `NeedsStrip=true`；`SourceBone` 空时回退 fallbackBone。
2. `RootMotionStrip.StripRootTranslation`：构造含根 translation 的 pose，剥后 translation==rest、rotation/scale 不变；`NeedsStrip=false` 不改；幂等。

### 8.2 集成场景（手动 / 脚本驱动 `Update`+`ComputeBoneTransforms`）
1. **稳态 Override**：根每帧被剥，无瞬移。
2. **Override(A) → AddImpulse(B)**：过渡期 B 根 translation 保留（不被连坐），A 源被剥；过渡完成 B 自然。**缺口 3 验证**。
3. **Override(A) → None(B)**：A 源剥、B 不剥，无瞬移无连坐。
4. **Override(A) → Override(B)**：源/目标都被剥。
5. **长 blend（blendDuration=0.6s）**：整段过渡都剥，尾部无瞬移。**缺口 1 验证**。
6. **硬切（blendDuration=0）**：无误剥、无残留。**缺口 4 验证**。
7. **SourceBone="Hips"≠RootBoneName**：源/目标在正确链剥离。**缺口 2 验证**。
8. **Base 停用（Override 权重渐降）**：渐降期 fading 源按旧 config 剥，无瞬移；完成清除 info。
9. **Additive deactivate transition**：源快照按旧 config 剥至淡出。
10. **连续快速切换 / 中断**：无 info 陈旧泄漏、无崩溃。

### 8.3 回归
- root motion 物理速度/冲量（`ApplyRootMotion`）行为不变（实体位移轨迹与改前一致）。
- 非 Base 层（Head/Arms）pose 不受影响。

---

## 9. 风险 / 迁移

- **`m_currentRootMotionConfig` 字段→属性**：若有外部代码以字段方式写它，编译期暴露。读点（`ApplyRootMotion`/`HasRootMotion`）改读 getter。外部写 config 须改走 `SetRootMotionConfig`（语义本应如此）。
- **API 兼容**：`AnimationController.SetRootMotionConfig(RootMotionConfig)` 公共签名保留（仅内部委派层）。
- **回归面**：剥离逻辑从 1 处（后处理）移到 4+ 处（各采样点），实现复杂度上升；靠 §8 场景全覆盖验证。
- **替代关系**：本分支是 `feature/gltf-player-bone-aliases` 上残留计时器最小修（`f5ff10da`）的**替代**，不合并；若本设计落地，最小修 commit 弃用。
- **内存**：每层多 3 个 `RootStripInfo`（值类型，小）+ 1 个 `RootMotionConfig` 引用 + 1 个 string；可忽略。
