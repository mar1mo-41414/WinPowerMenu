# WinPowerMenu

[English README →](README-EN.md)

Windows の物理電源ボタン（またはユーザーが指定した任意のキー）を押したときに、
「シャットダウン」「再起動」「スリープ」「キャンセル」を大きく表示する
半透明ポップアップメニューを出す、軽量な常駐アプリです。

ROG Ally, ASUS / MSI ノート等、**電源ボタンをキー扱いで拾えないハンドヘルド/ノート
にも対応**する「画面 OFF フック」トリガー方式を搭載しています。

## 特徴

- **競合しない**: 低レベルキーボードフックで**登録したキーだけ**を横取り。
  それ以外は完全に素通しなので、AutoHotkey / PowerToys などと共存できます。
- **タッチ対応 UI**: 4 つの大ボタンを 2 × 2 で中央に配置、半透明の角丸パネル。
  Esc / 画面外クリックでキャンセル。
- **キーコード学習**: 「学習開始」を押して、認識させたいキー（電源ボタン）を
  1 回押すだけで VK / スキャンコードを自動登録します。
- **画面 OFF フック**: 電源ボタンが OS に握りつぶされる機種 (ROG Ally 等) 向け。
  Windows の電源ボタン設定を「ディスプレイの電源を切る」にして、
  そこから発生する `PBT_POWERSETTINGCHANGE` を拾ってメニュー表示。
- **タスクトレイ常駐**: 右クリックから「電源メニュー / 設定 / 終了」。
- **自動起動**: HKCU\...\Run にワンクリックで登録。初回起動時に問い合わせ。

## 動作環境

- Windows 10 / 11 (x64)
- .NET 8 Desktop Runtime

## 導入

```powershell
cd WinPowerMenu
dotnet build -c Release
```

出力: `WinPowerMenu\bin\Release\net8.0-windows\win-x64\WinPowerMenu.exe`

単一 exe で配布したい場合:

```powershell
dotnet publish WinPowerMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 使い方（機種別）

### 電源ボタンが「キー」として届く機種

Sleep キー付き外付けキーボード、多くの旧型デスクトップなど。

1. Windows 電源オプション → 電源ボタンの動作を「何もしない」に設定
2. `WinPowerMenu.exe` 起動
3. トレイ右クリック → **設定…** → **学習開始** → 電源ボタンを 1 回押す
4. VK コードが登録される → **OK**

### 電源ボタンが取れない機種 (ROG Ally, ASUS/MSI ノート等)

「何もしない」設定だと OS 内部で ACPI レベルで握りつぶされ、
ユーザーモードにイベントが一切降りてきません（低レベルキーフック / Raw Input /
WM_POWERBROADCAST / ATK-WMI 全部無音、Kernel-Power ログにも記録なし）。

**画面 OFF フック方式**で対応:

1. `WinPowerMenu.exe` 起動
2. トレイ右クリック → **設定…** → 「画面 OFF フック (ROG Ally など)」を選択
3. 「電源ボタン → ディスプレイの電源を切る」ボタンで Windows 側設定を自動変更
4. **OK**
5. 以降、電源ボタンを押すたびに（Modern Standby 機は 1-2 秒画面 OFF 経由で）
   メニュー表示

内部的には Windows の `GUID_CONSOLE_DISPLAY_STATE` (display off) 通知を購読し、
直前 5 秒以内にユーザー入力があれば「押した」と判定してディスプレイを ON
に戻しつつメニューを表示します（アイドルタイムアウトによる自動オフでは反応しない）。

### ⚠️ ROG Ally 特有の注意

Armoury Crate はパフォーマンスモード切替 (Turbo / Silent / Performance) を
**Windows 電源プランのまるごと入れ替え**で実装しています。
ASUS 出荷スキームは 6 個 (+ overlay)、**うち 5 個の `PBUTTONACTION` は
デフォルトで 3 (Shutdown)**。**充電器の抜き差しでモードが切り替わる**タイミングで
「電源ボタン = シャットダウン」になる恐怖のトラップがあります。

WinPowerMenu は `画面 OFF フック` モード起動時に**全プランを PBUTTONACTION=4 に一括上書き**し、
30 秒毎に再適用して Armoury による変更を追い返します。

## 設定ファイル

`%LOCALAPPDATA%\WinPowerMenu\settings.json`

```json
{
  "TriggerSource": "DisplayOff",
  "TriggerVkCode": 95,
  "TriggerScanCode": 0,
  "TriggerHidUsagePage": 1,
  "TriggerHidUsage": 129,
  "TriggerLabel": "VK_SLEEP (0x5F)",
  "FirstLaunchDone": true
}
```

- `TriggerSource`:
  - `Keyboard` — 低レベルキーボードフック (VK 一致)
  - `HidSystemControl` / `HidConsumer` — Raw Input (HID)
  - `HidKeyboard` — Raw Input のキーボードタイプ
  - `DisplayOff` — 画面 OFF フック (ノート/ハンドヘルド向け)

## 診断

`%LOCALAPPDATA%\WinPowerMenu\`:

- `learn.log` — 「学習開始」中に受信した全イベント
- `crash.log` — 起動 / 未処理例外 / PBUTTONACTION 保護の実行ログ

## 実行される電源動作

| ボタン        | 動作                                                   |
|-----------|------------------------------------------------------|
| シャットダウン   | `shutdown.exe /s /t 0`                               |
| 再起動       | `shutdown.exe /r /t 0`                               |
| スリープ      | `powrprof.SetSuspendState(false, false, false)`      |
| キャンセル     | ポップアップを閉じるだけ                                         |

## プロジェクト構成

```
WinPowerMenu/
├── WinPowerMenu.sln
└── WinPowerMenu/
    ├── WinPowerMenu.csproj
    ├── app.manifest
    ├── App.xaml(.cs)              エントリ、トリガー再構築、クラッシュハンドラ
    ├── AppSettings.cs             JSON 永続化 + TriggerSource enum
    ├── AutoStartManager.cs        HKCU\...\Run による自動起動
    ├── LowLevelKeyboardHook.cs    WH_KEYBOARD_LL、登録キーのみ消費
    ├── RawInputHost.cs            Raw Input (HID + WM_POWERBROADCAST)
    ├── HiddenRawInputWindow.cs    HID/RawInput 用ランタイムホスト
    ├── DisplayOffTrigger.cs       GUID_CONSOLE_DISPLAY_STATE 購読 + 全プラン保護
    ├── PowercfgHelper.cs          powercfg /L 全プラン列挙 + PBUTTONACTION 上書き
    ├── ExecutionState.cs          SetThreadExecutionState ラッパ
    ├── PowerActions.cs            shutdown / restart / sleep
    ├── PowerMenuWindow.xaml(.cs)  ポップアップ本体
    ├── SettingsWindow.xaml(.cs)   設定 UI (学習 + 画面 OFF フック + AutoStart)
    ├── TrayIcon.cs                NotifyIcon
    ├── LearnLogger.cs             学習中の診断ログ
    └── CrashLog.cs                未処理例外 & 動作記録
```

## ライセンス

MIT
