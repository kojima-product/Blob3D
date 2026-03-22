# Blob3D — Unity セットアップガイド

## 前提条件

- Unity 2022 LTS 以降
- Unity Hub がインストール済み

---

## クイックスタート（3ステップ）

### Step 1: Unity でプロジェクトを開く

1. Unity Hub → **Open** → この `Blob3D` フォルダを選択
2. Unity バージョン選択で **Unity 2022.3 LTS** 以降を指定
3. プロジェクトが開くと、自動的に URP / TextMeshPro / Input System がインポートされます
4. TextMeshPro の「Import TMP Essentials」ダイアログが出たら **Import** をクリック

### Step 2: 自動セットアップ実行

1. Unity メニューバー → **Blob3D** → **Setup Project (Full Auto)** をクリック
2. セットアップウィンドウが開いたら **「セットアップ開始」** ボタンを押す
3. 以下が全自動で生成されます：
   - **マテリアル** 15種（Player, AI各種, Feed5色, PowerUp5色, Ground, Boundary）
   - **プレハブ** 4種（PlayerBlob, AIBlob, Feed, PowerUp）
   - **ゲームシーン**（全マネージャー + プレイヤー + カメラ + UI + フィールド）
   - **物理レイヤー衝突マトリクス**

### Step 3: プレイ

1. `Assets/Scenes/GameScene` をダブルクリックで開く
2. **Play ボタン** を押す
3. タイトル画面の **PLAY** を押してゲームスタート！

---

## エディタでの操作方法

| キー | 操作 |
|---|---|
| W / A / S / D | 移動 |
| Space | ダッシュ（サイズが少し減る） |
| F | 分裂 |
| 右ドラッグ | カメラ回転 |

---

## プロジェクト構成

```
Blob3D/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/           GameManager, BlobBase, FieldBoundary
│   │   ├── Player/         BlobController, BlobCameraController, MobileInputHandler
│   │   ├── AI/             AIBlobController, AISpawner
│   │   ├── Gameplay/       Feed, FeedSpawner, PowerUp系, PowerUpEffectManager
│   │   ├── UI/             UIManager, HUDController, VirtualJoystick
│   │   ├── Data/           ScoreManager, SkinData, SkinManager
│   │   ├── Utils/          ObjectPool
│   │   └── Editor/         Blob3DProjectSetup（自動セットアップツール）
│   ├── Shaders/
│   │   ├── BlobSurface.shader     サブサーフェススキャタリング風blob表現
│   │   └── GridGround.shader      Agar.io風グリッド地面
│   ├── Materials/          （自動生成）
│   ├── Prefabs/            （自動生成）
│   └── Scenes/             （自動生成）
├── Packages/
│   └── manifest.json       URP, TMP, Input System 等
└── ProjectSettings/
    ├── ProjectSettings.asset   iOS/Android ビルド設定済み
    ├── TagManager.asset        Player/AIBlob/Feed/PowerUp レイヤー定義済み
    ├── QualitySettings.asset   Low/Medium/High 3段階
    └── Physics.asset           衝突マトリクス設定済み
```

---

## カスタムシェーダーについて

### BlobSurface.shader
blob のビジュアルを担当する URP 対応シェーダーです。フレネルエフェクト + サブサーフェススキャタリング近似 + 内部発光で、ゼリーのような半透明質感を表現します。`Blob3D/BlobSurface` としてマテリアルに割り当て可能です。

プロパティ:
- Base Color / Subsurface Color — 表面と内部の色
- Fresnel Power/Intensity — 端の光り具合
- Subsurface Intensity — 光の裏面回り込み
- Inner Glow — 内側からの発光
- Rim Color/Power — リムライト

### GridGround.shader
Agar.io 風のグリッドパターンを動的に描画します。テクスチャ不要で、距離に応じたフェードも内蔵。

---

## モバイルビルド手順

### Android
1. File → Build Settings → Android 選択 → Switch Platform
2. Player Settings で Minimum API Level: 29 を確認
3. Build And Run

### iOS
1. File → Build Settings → iOS 選択 → Switch Platform
2. Xcode プロジェクトが出力される
3. Xcode で Signing 設定後にビルド

---

## 次のステップ（Phase 2 以降）

1. **サウンド実装**: AudioManager を追加し、SE/BGM を組み込む
2. **パーティクル**: 吸収時・パワーアップ取得時のVFXを追加
3. **スキン作成**: `Create > Blob3D > SkinData` で10種類のスキンを作成
4. **チュートリアル**: 初回起動時の3ステップガイド
5. **ストア準備**: アイコン、スクリーンショット、説明文の準備
