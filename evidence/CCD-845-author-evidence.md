# CCD-845 Assessment F1/F2 作者证据

日期：2026-09-13 UTC

## 结论

Verdict：**PASS（产品实现 + 有界离线回归） / actual-process terminal receipt unverified**。

- F1 已在 `aspx-acquisition` 的认证/网络 continuation 之前加入 Assessment 自有、可审查的 exact-build registry authority gate。
- F2 已按官方 `DownstreamDisposition` 输出 registry observation，并保留完整 `HandlerOrArtifactType`、`ExpectedAvailability`、`ContentOrigin` 与 virtual-handler authority/source provenance。
- 完整官方 1,161-entry registry bytes、官方 schema bytes、F1/F2 正负例和既有 ASPX 回归均已进入永久测试。
- 有界 synthetic provider 已生成五个 official output role 和 terminal digest binding；registry 分布为 1,150 `ReferenceOnlyAvailable` + 11 `ReferenceUnavailable`。
- 新 apphost/DLL 可编译，但在本宿主从 CLI host 启动时连 `aspx-acquisition --help` 都在 10 秒内未返回，因此 actual-process terminal receipt 明确为 `unverified`。不把该项称为 runtime PASS。
- tenant scan、认证、source/target tenant 读写和 CUPCollect first/resume 均为 0。

## 冻结输入

- Assessment base commit/tree：`ccc655b68fc6be9cdf107e6d8fe7d9f56266182a` / `ffdd8db6de296516aea8ebbc61f3b12ff6f6c965`。
- task branch：`paperclip/ccd-845-exact-registry-gate`。
- PnP official registry commit/tree：`1d4f103edd278fd205f095464a2c55dcd52b8fe9` / `da41af6980d9e1d7bb917a6e9761a8c74740911f`，只读消费。
- official registry file SHA-256：`203eec0e6b1d69242589c2d430ba52c2bd3fe973c80f6659d6a2176757e5a5dc`。
- official schema file SHA-256：`6a22625b33961ea20069b541b09c459d21db23db1548da4cce4a5538f9f4c32d`。
- canonical registry hash：`3138b6d0b1e1af1e170801939d2c1e00793e61cb17f53c6f7d01e61cf3507830`。
- exact build/revision：`16.0.27709.12001` / `spo-online-16.0.27709.12001-r1`。

## KB 查询

使用已安装 CCD KB 工具执行 scenario query：`ASPX registry`、`classic page runtime verification`；读取：

- `#a903cc` / `spo.page-family.expand-coverage.v1`：要求冻结 exact authority/revision、直接绑定 catalog revision、不可用或矛盾证据不得以默认值替换。
- `#c6bd62` / `personal.classic-page-repro.integrate-and-verify-runtime.v1`：contract-only 不得描述为 runtime supported，运行证据必须绑定 immutable inputs 与可复算 artifacts。

执行证据与 KB 一致；本票没有新的已验证 KB 更正，因此未修改 KB。

## 实现

### F1 exact registry authority gate

`AspxPlatformRegistryAuthorityGate` 在 `AspxAcquisitionCommandHandler` 的认证和所有网络操作之前执行：

1. 从 Core embedded resource 读取官方 schema，并先验证 schema resource ID 与固定 SHA-256。
2. 用官方 schema 的全部实际 validation keyword 子集验证 registry：`$ref`、`type`、`const`、`enum`、`required`、`properties`、`additionalProperties`、`min/maxItems`、`prefixItems/items`、`uniqueItems`、`pattern`、`minimum`、`allOf/if/then` 等。
3. 拒绝 duplicate JSON properties、未知字段和未知版本。
4. 重算 Python producer 同义 canonical JSON：UTF-8、recursive ordinal key sort、compact separators、root `registryHash` 排除；结果必须等于固定 canonical hash。
5. 固定 registry raw file SHA、schema SHA/ID、revision、canonical hash、authority kind/source/artifact hash、review refs、platform family、exact min=max build 与 entry count。
6. 只有 gate 成功后才调用 continuation；永久负例直接观测 `networkCalls == 0`。

负例覆盖：unknown field/version/disposition、prior/future/unknown build、widened range、revision drift、declared hash drift、canonical drift、authority drift、schema locator drift、自洽 rehash drift。

### F2 unavailable virtual disposition

- 不再按 `HandlerOrArtifactType == "VirtualHandler"` 猜测 disposition。
- 直接消费并 fail-closed 验证 `DownstreamDisposition`。
- 对 11 个 `VirtualHandler.SPLayoutsMappedFile` 保持：
  - `Disposition = ReferenceUnavailable`
  - `ReasonCode/ExpectedAvailability = ReferenceTargetAbsentAtFrozenBuild`
  - `ContentOrigin = VirtualHandler`
  - `HandlerOrArtifactType = VirtualHandler.SPLayoutsMappedFile`
  - physical identity 为 null
  - evidence refs 含 registry entry、authority map/blob、handler type、mapped target state 与 source path。
- unknown disposition 转为 `Unknown` 并增加 gap，不会提升为 available/copied/equal/M5。

## 变更路径

- `src/PnP.Scanning/PnP.Scanning.Core/Discovery/AspxPlatformRegistryAuthority.cs`
- `src/PnP.Scanning/PnP.Scanning.Core/Discovery/AspxAcquisitionContracts.cs`
- `src/PnP.Scanning/PnP.Scanning.Core/Discovery/AspxReferenceStore.cs`
- `src/PnP.Scanning/PnP.Scanning.Process/Commands/Handlers/AspxAcquisitionCommandHandler.cs`
- `src/PnP.Scanning/PnP.Scanning.Core/Discovery/RegistryAuthority/spo-online-16.0.27709.12001.registry.schema.json`
- `src/PnP.Scanning/PnP.Scanning.Core/PnP.Scanning.Core.csproj`
- `src/PnP.Scanning/PnP.Scanning.Core.Tests/Discovery/AspxAcquisitionV3Tests.cs`
- `src/PnP.Scanning/PnP.Scanning.Core.Tests/Fixtures/Discovery/RegistryAuthority/spo-online-16.0.27709.12001.registry.json`
- `src/PnP.Scanning/PnP.Scanning.Core.Tests/PnP.Scanning.Core.Tests.csproj`

## 命令与结果

Restore（首次不带 TFM 的 probe 因共享 multi-target `pnpcore` 选择 net9.0 而报 `NETSDK1045`；随后按既有 runbook 固定 net8.0）：

```text
dotnet restore src/PnP.Scanning/PnP.Scanning.Core.Tests/PnP.Scanning.Core.Tests.csproj \
  -p:TargetFramework=net8.0 -p:NuGetAudit=false --verbosity minimal
```

结果：PASS，6 个 project restored，约 6 秒 wall time。

完整相关 ASPX regression：

```text
dotnet test src/PnP.Scanning/PnP.Scanning.Core.Tests/PnP.Scanning.Core.Tests.csproj \
  --no-restore -p:TargetFramework=net8.0 -p:NuGetAudit=false \
  --filter FullyQualifiedName~Aspx \
  --logger 'trx;LogFileName=aspx-full.trx' \
  --results-directory evidence/CCD-845/test-results
```

结果：PASS；70 passed / 0 failed / 0 skipped；test duration 6 秒，wall time 27 秒。TRX SHA-256：`8b5d43721cba199ceff3f54ff69faf7aff70de3bc0ed5e43216b8bd0a0997bcf`。

保留五卷 fixture 的单测：PASS；1 passed / 0 failed。

actual-process probes：

```text
timeout 30 dotnet .../microsoft365-assessment.dll aspx-acquisition <offline-invalid-registry args>
timeout 10 cmd.exe /c <run-apphost.cmd> aspx-acquisition --help
```

结果：两条路径分别 exit `124`，未进入可观察 terminal writer；无 terminal receipt、无 acquisition output。因为 `--help` 也超时，证据只支持“CLI host/apphost startup unverified”，不支持“registry gate runtime FAIL/PASS”。产品 gate continuation 的注入测试已独立证明所有负例 `network callback=0`。

## Bounded official outputs

terminal receipt：`aspx-acquisition-terminal-receipt/v1`，exit 0，五个 role 完整且 fresh-readback PASS；其 executable binding 是本次构建的 `microsoft365-assessment.dll`。

| Role/file | Length | SHA-256 |
|---|---:|---|
| `aggregate.json` | 1,736 | `6c73ecdaf10f1671adfa8918599731c4b3575802e3731ac6f796fa2cda5fd2a6` |
| `physical.json` | 16,265 | `cffb04df0ed887a0170fc21f586bc8e9e1b51175e4a6ece8fc00b894a04cc903` |
| `physical.sqlite` | 126,976 | `2e54f77bc0df42e0b409bca4794a0d7212cb85b3c3535b52ab6404a6b5a492ed` |
| `reference.json` | 1,834,798 | `139537ff342d63e876a79d1182a3925018b5d9d4fb84f3b67724a8cd5bb2549b` |
| `reference.sqlite` | 2,121,728 | `4b0e38cfa413fc54e50dbe3a1ac74d836f0120cdbed11fe20962f8b3870fc732` |
| `terminal.json` | 2,346 | `2eb7d6133ae2ed424de08484166c8fc16ecbbec170f92c2501eb90cda3ce41a1` |

`reference.json`：1,150 official available + 11 official unavailable。`aggregateVerdict = Unknown` 是 synthetic provider 的 tenant authority/forms gaps 所致；它没有把 11 个 unavailable 提升为成功，也不构成 tenant-wide completeness 声明。

## Binary hashes

| Binary | Length | SHA-256 |
|---|---:|---|
| `microsoft365-assessment.dll` | 136,704 | `5c7e2dc2941e5e7e758451731849429c22698e228d8672a7bb4467f9dfc0a968` |
| `microsoft365-assessment.exe` | 152,064 | `9115fa4ab6f27181b77010f9378d7ea7d97101be899e2124cfa40c0853ced4a4` |
| `PnP.Scanning.Core.dll` | 4,103,168 | `097a8e66d475c0a4fd6e1d1b81193bf2497f80568f1d59c326b5cdba2b6c1405` |
| `PnP.Scanning.Core.Tests.dll` | 302,080 | `2c7d9960961e7e86243ef9fe95f14113af7a1449d6b2ed802ef050352590302a` |

## 剩余路径

本票作者实现完成后交 CCD-834/PnP Lead 消费新的 producer ref，并沿原独立验证路径复验。只有独立 PASS 后，CCD-746 才能准入新 official paths first/resume。本报告不关闭 CCD-746/CCD-393/CCD-443 convergence，也不把 bounded fixture 当 tenant scan。
