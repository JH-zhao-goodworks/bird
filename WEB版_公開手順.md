# Web版（スマホ対応）の公開手順

スマホのブラウザから使えるWeb版（`BirdHotel.Web`）を、無料で公開するための手順です。
Windows版（`BirdHotel.App`）はこれまで通り使えます。データはそれぞれ別に持ちます。

かかる費用は0円です（Render・Neon の無料プランを使います）。

---

## 全体の流れ

1. Neon で無料のデータベースを作る（5分）
2. Render で無料のWebサービスを作る（10分）
3. スマホのブラウザで開いて、合言葉でログインする

---

## 1. データベースを用意する（Neon）

Renderの無料プランはディスクの内容が消えるため、データベースは別に用意します。

1. https://neon.com にアクセスし、GitHubアカウントなどでサインアップ
2. 「Create project」でプロジェクトを作成（リージョンは `Asia Pacific (Singapore)` が近くて速いです）
3. 作成後に表示される **接続文字列（Connection string）** をコピーして控えておく
   - `postgresql://xxxx:yyyy@ep-zzzz.ap-southeast-1.aws.neon.tech/neondb?sslmode=require` のような文字列です
   - この文字列はパスワードを含むので、他人に見せないでください

---

## 2. Webサービスを作る（Render）

1. https://render.com にアクセスし、GitHubアカウントでサインアップ
2. 「New +」→「Web Service」を選ぶ
3. リポジトリ `JH-zhao-goodworks/bird` を選ぶ（初回はGitHubとの連携許可が必要です）
4. 設定を次のようにする
   - **Name**: `bird-hotel`（好きな名前でOK。URLの一部になります）
   - **Region**: `Singapore`
   - **Branch**: `main`
   - **Runtime**: `Docker`
   - **Instance Type**: `Free`
5. 「Environment Variables」で次の2つを追加する
   | キー | 値 |
   |---|---|
   | `DATABASE_URL` | 手順1でコピーしたNeonの接続文字列 |
   | `APP_PASSWORD` | ログイン用の合言葉（自分で決める。推測されにくいものに） |
6. 「Create Web Service」を押す
7. 初回のビルドは5〜10分ほどかかります。完了すると `https://bird-hotel-xxxx.onrender.com` のようなURLが発行されます

---

## 3. 使い始める

1. スマホのブラウザで発行されたURLを開く
2. 手順2で決めた合言葉を入力してログイン（30日間ログインしたままになります）
3. まずは次の順で登録してください
   1. **籠の管理** から籠を登録（グループ名を付けると籠一覧でまとまります）
   2. **飼い主** を登録
   3. **鳥** を登録（「一括登録」でExcelから貼り付けもできます）
   4. **予約を登録**、または「予約の一括登録」でまとめて登録
4. ホーム画面（籠一覧）をブラウザの「ホーム画面に追加」しておくと、アプリのように開けます

---

## 無料プランで知っておくこと

- **15分ほどアクセスがないとサーバーが停止します**。次に開いたとき、起動に30〜60秒かかります（データは消えません）
- Neonの無料プランは容量0.5GBまでです。この規模の予約データなら十分です
- Renderの無料プランは月750時間まで使えます（1サービスなら止まりません）

## パスワードを変えたいとき

Renderの管理画面 →「Environment」→ `APP_PASSWORD` の値を変更 →「Save, rebuild, and deploy」。
再デプロイ後、全員が再ログインになります。

## Windows版とのデータについて

Web版とWindows版はデータベースが別です（Web版はNeon、Windows版はPCの中のファイル）。
現時点では自動で同期しません。Windows版のデータをWeb版へ移したい場合は、
Windows版の「Excel出力」→ Web版の「予約の一括登録」に貼り付ける方法が使えます。
