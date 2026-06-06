# 家具多纹理支持设计

## 目标

让家具中的不同体素方块可以使用不同的纹理图集，通过 `Block.GetDefaultTexture` 在地形渲染和 UI 渲染中正确路由到对应纹理。

## 前置工作（已完成）

- `Block.GetDefaultTexture(int value)` — 虚方法，默认返回 `null`
- `CubeBlock`、`CrossBlock`、`FlatBlock` 的 `GenerateTerrainVertices` 和 `DrawBlock` 已接入 `GetDefaultTexture`
- `null` = 使用默认地形图集，非 null = 路由到 `geometry.GetGeometry(texture)`

## 设计

### 1. FurnitureGeometry 数据结构

```cs
public class FurnitureGeometry {
    // null 纹理（默认地形图集）— 保持不变
    public BlockMesh[] SubsetOpaqueByFace = new BlockMesh[6];
    public BlockMesh[] SubsetAlphaTestByFace = new BlockMesh[6];

    // 非 null 纹理 — 按纹理分组
    public Dictionary<Texture2D, FurnitureGeometry> Draws;
}
```

与 `TerrainGeometry.Draws` 对称。`Draws` 延迟初始化，无自定义纹理时为 `null`。

### 2. FurnitureDesign.CreateGeometry() 改造

**UV 计算**：将硬编码 `/16f` 改为 `block.GetTextureSlotCount(value2)`：

```cs
int slotCount = block.GetTextureSlotCount(value2);
int num15 = num14 % slotCount;
int num16 = num14 / slotCount;
float x5 = ((n + 0.01f) / m_resolution + num15) / slotCount;
// ... 同理其余三个 UV 坐标
```

**纹理分组**：根据 `block.GetDefaultTexture(value2)` 选择目标 BlockMesh：

- `texture == null` → 写入现有的 `blockMesh` / `blockMesh2`（与当前逻辑一致）
- `texture != null` → 延迟初始化 `Draws`，获取或创建 `Draws[texture]` 的 `FurnitureGeometry`，写入其 `SubsetOpaqueByFace[i]` 或 `SubsetAlphaTestByFace[i]`

面完成后，null 纹理的 mesh 写入 `m_geometry.SubsetOpaqueByFace[i]` 等（保持不变），Draws 中的 mesh 已就地写入。

### 3. FurnitureBlock.GenerateTerrainVertices() 改造

在现有 null 纹理遍历之后，增加 Draws 遍历：

```cs
if (geometry2.Draws != null) {
    foreach (var kv in geometry2.Draws) {
        TerrainGeometry targetGeo = geometry.GetGeometry(kv.Key);
        for (int i = 0; i < 6; i++) {
            // 与 null 纹理逻辑相同，但 target subset 取自 targetGeo
        }
    }
}
```

通过 `geometry.GetGeometry(tex)` 将顶点路由到地形 Geometry 中对应纹理的子集。

### 4. FurnitureBlock.DrawBlock() 改造

在现有 null 纹理遍历之后，增加 Draws 遍历：

```cs
if (geometry.Draws != null) {
    foreach (var kv in geometry.Draws) {
        Texture2D tex = kv.Key;
        FurnitureGeometry subGeo = kv.Value;
        for (int i = 0; i < 6; i++) {
            // 使用 BlocksManager.DrawMeshBlock(mesh, color, size, ref matrix, env, tex)
        }
    }
}
```

使用 `BlocksManager.DrawMeshBlock` 的 Texture2D 重载（第 870 行）。

### 5. 边界情况

- **GC/清理**：`FurnitureDesign.SetValues()` 重置 `m_geometry = null`，Draws 随旧 FurnitureGeometry 一起被 GC 回收
- **存档兼容**：`m_values[]` 存方块值，纹理信息运行时从 `GetDefaultTexture` 动态获取，存档格式不变
- **性能**：无自定义纹理时 Draws 为 null，零开销；有自定义纹理时按纹理数增加 draw call，与地形多纹理机制一致

## 改动文件清单

| 文件 | 改动内容 |
|------|----------|
| `FurnitureGeometry.cs` | 新增 `Draws` 字段 |
| `FurnitureDesign.cs` | `CreateGeometry()` 按 texture 分组，UV 用 `GetTextureSlotCount` |
| `FurnitureBlock.cs` | `GenerateTerrainVertices()` 和 `DrawBlock()` 遍历 Draws |
