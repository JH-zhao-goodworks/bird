@echo off
chcp 65001 > nul
cd /d "%~dp0"

echo 予約データをGitHubに保存します...
echo.

git add data/bird_hotel.db
git commit -m "予約データを保存"
if errorlevel 1 (
    echo.
    echo 変更がないか、保存できませんでした。
    pause
    exit /b
)

git push origin main
if errorlevel 1 (
    echo.
    echo GitHubへの送信に失敗しました。ネットワークやログイン状態を確認してください。
    pause
    exit /b
)

echo.
echo 保存しました。
pause
