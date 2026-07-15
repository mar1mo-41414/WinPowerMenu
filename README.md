# WinPowerMenu

[English README →](README-EN.md)

Windows の物理電源ボタン（またはユーザーが指定した任意のキー）を押したときに、
「シャットダウン」「再起動」「スリープ」「キャンセル」を大きく表示する
半透明ポップアップメニューを出す、軽量な常駐アプリです。

## 特徴

- **競合しない**: 低レベルキーボードフックで**登録したキーだけ**を横取り。
  それ以外は完全に素通しなので、AutoHotkey / PowerToys などと共存できます。
- **タッチ対応 UI**: 4 つの大ボタンを 2 × 2 で中央に配置、半透明の角丸パネル。
  Esc / 画面外クリックでキャンセル。
- **キーコード学習**: 「学習開始」を押して、認識させたいキー（電源ボタン）を
  1 回押すだけで VK / スキャンコードを自動登録します。
- **タスクトレイ常駐**: 右クリックから「電源メニューを表示 / 設定 / 終了」。

## 前提

Windows の電源オプションで、**電源ボタンを押したときの動作 = 何もしない**
に設定しておいてください（そうしないと OS 側で先に処理されます）。

- 設定 → システム → 電源とバッテリー → 電源ボタンの動作
- または `powercfg` の詳細設定

## 動作環境

- Windows 10 / 11 (x64)
- .NET 8 Desktop Runtime

## ビルド

```powershell
cd WinPowerMenu
dotnet build -c Release
```

単一 exe として配布したい場合:

```powershell
dotnet publish WinPowerMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

出力は `WinPowerMenu/bin/Release/net8.0-windows/win-x64/publish/WinPowerMenu.exe` です。

## 使い方

1. `WinPowerMenu.exe` を起動（タスクトレイに常駐します）。
2. トレイアイコンを右クリック → **設定…** → **学習開始** を押して、
   使いたい電源ボタン / キーを 1 回押します。
3. **OK** で保存。以降はそのキーを押すたびにポップアップが出ます。

スタートアップに登録したい場合は `Win+R` → `shell:startup` に
`WinPowerMenu.exe` のショートカットを置いてください。

## 設定ファイル

`%LOCALAPPDATA%\WinPowerMenu\settings.json` に保存されます。

```json
{
  "TriggerVkCode": 95,
  "TriggerScanCode": 0,
  "TriggerLabel": "VK_SLEEP (0x5F)"
}
```

初期値は `VK_SLEEP (0x5F)` です。手で JSON を直接編集しても構いません。

## 実行される電源動作

| ボタン        | 動作                                            |
|-----------|-----------------------------------------------|
| シャットダウン   | `shutdown.exe /s /t 0`                        |
| 再起動       | `shutdown.exe /r /t 0`                        |
| スリープ      | `powrprof.SetSuspendState(false, false, false)` |
| キャンセル     | ポップアップを閉じるだけ                                  |

## プロジェクト構成

```
WinPowerMenu/
├── WinPowerMenu.sln
└── WinPowerMenu/
    ├── WinPowerMenu.csproj
    ├── app.manifest
    ├── App.xaml / App.xaml.cs         … エントリ, タスクトレイ + フック起動
    ├── AppSettings.cs                 … 設定ロード / 保存 (JSON)
    ├── LowLevelKeyboardHook.cs        … WH_KEYBOARD_LL、対象キーのみ消費
    ├── PowerActions.cs                … shutdown / restart / sleep
    ├── PowerMenuWindow.xaml(.cs)      … ポップアップ本体
    ├── SettingsWindow.xaml(.cs)       … 設定画面 / キー学習
    └── TrayIcon.cs                    … NotifyIcon
```

## ライセンス

MIT
