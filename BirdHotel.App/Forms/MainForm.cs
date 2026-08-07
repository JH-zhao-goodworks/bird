using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class MainForm : Form
{
    private const int CardWidth = 196;
    private const int CardHeight = 132;
    private const int CardMargin = 4;

    private static readonly Color[] OwnerColorPalette =
    [
        Color.FromArgb(255, 224, 178), // オレンジ
        Color.FromArgb(198, 224, 255), // 水色
        Color.FromArgb(198, 239, 206), // 黄緑
        Color.FromArgb(255, 235, 156), // 黄色
        Color.FromArgb(230, 208, 255), // 紫
        Color.FromArgb(255, 205, 210), // ピンク
        Color.FromArgb(224, 224, 224), // グレー
    ];

    private readonly BirdRepository _birdRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;
    private readonly DatabaseService _database;

    private FlowLayoutPanel _cardsPanel = null!;

    public MainForm(BirdRepository birdRepository, CageRepository cageRepository, ReservationRepository reservationRepository, OwnerRepository ownerRepository, SpeciesRepository speciesRepository, DatabaseService database)
    {
        _birdRepository = birdRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;
        _database = database;

        BuildUi();
        RefreshCards();
    }

    private void BuildUi()
    {
        Text = "小鳥ホテル 籠一覧";
        Width = 1060; // カード4枚がちょうど1行に収まる幅
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(10),
            AutoSize = false,
        };

        var ownerButton = new Button { Text = "飼い主の管理", Width = 120, Height = 36 };
        ownerButton.Click += (_, _) => OpenOwnerList();

        var birdButton = new Button { Text = "鳥の管理", Width = 120, Height = 36 };
        birdButton.Click += (_, _) => OpenBirdList();

        var cageButton = new Button { Text = "籠の管理", Width = 120, Height = 36 };
        cageButton.Click += (_, _) => OpenCageList();

        var reservationButton = new Button { Text = "予約登録・空き確認", Width = 160, Height = 36 };
        reservationButton.Click += (_, _) => OpenReservationForm();

        var bulkReservationButton = new Button { Text = "予約の一括登録", Width = 130, Height = 36 };
        bulkReservationButton.Click += (_, _) => OpenReservationBulkImport();

        var exportButton = new Button { Text = "Excel出力", Width = 100, Height = 36 };
        exportButton.Click += (_, _) => ExportReservations();

        var backupButton = new Button { Text = "バックアップ", Width = 110, Height = 36 };
        backupButton.Click += (_, _) => BackupDatabase();

        var restoreButton = new Button { Text = "復元", Width = 80, Height = 36 };
        restoreButton.Click += (_, _) => RestoreDatabase();

        var clearAllButton = new Button { Text = "全籠クリア", Width = 100, Height = 36, Margin = new Padding(20, 3, 3, 3) };
        clearAllButton.Click += (_, _) => ClearAllCages();

        var refreshButton = new Button { Text = "更新", Width = 80, Height = 36 };
        refreshButton.Click += (_, _) => RefreshCards();

        topPanel.Controls.Add(ownerButton);
        topPanel.Controls.Add(birdButton);
        topPanel.Controls.Add(cageButton);
        topPanel.Controls.Add(reservationButton);
        topPanel.Controls.Add(bulkReservationButton);
        topPanel.Controls.Add(exportButton);
        topPanel.Controls.Add(backupButton);
        topPanel.Controls.Add(restoreButton);
        topPanel.Controls.Add(clearAllButton);
        topPanel.Controls.Add(refreshButton);

        _cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight,
        };

        Controls.Add(_cardsPanel);
        Controls.Add(topPanel);
    }

    private void RefreshCards()
    {
        _cardsPanel.SuspendLayout();
        _cardsPanel.Controls.Clear();

        var cages = _cageRepository.GetAll();
        var allReservations = _reservationRepository.GetAll();

        foreach (var group in OrderGroups(cages))
        {
            var groupLabel = group.Key.Length > 0 ? group.Key : "グループなし";
            _cardsPanel.Controls.Add(BuildCageGroupBox(group.Key, groupLabel, group.ToList(), allReservations));
        }

        _cardsPanel.ResumeLayout();
    }

    // 表示順（GroupOrder）が設定されていればその順、未設定なら名前順。グループ未設定の籠は最後。
    private static List<IGrouping<string, Cage>> OrderGroups(List<Cage> cages) =>
        cages
            .GroupBy(c => c.GroupName)
            .OrderBy(g => g.Min(c => c.GroupOrder) == 0)
            .ThenBy(g => g.Min(c => c.GroupOrder))
            .ThenBy(g => g.Key.Length == 0)
            .ThenBy(g => g.Key, Comparer<string>.Create(CageRepository.CompareNatural))
            .ToList();

    // グループを左右に1つ移動して、その並び順を保存する
    private void MoveGroup(string groupKey, int direction)
    {
        var cages = _cageRepository.GetAll();
        var groupKeys = OrderGroups(cages).Select(g => g.Key).ToList();

        var index = groupKeys.IndexOf(groupKey);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= groupKeys.Count) return;

        (groupKeys[index], groupKeys[target]) = (groupKeys[target], groupKeys[index]);

        // 並び替え後の順番で 1 から振り直す
        for (var i = 0; i < groupKeys.Count; i++)
        {
            foreach (var cage in cages.Where(c => c.GroupName == groupKeys[i]))
            {
                cage.GroupOrder = i + 1;
                _cageRepository.Update(cage);
            }
        }

        RefreshCards();
    }

    private Control BuildCageGroupBox(string groupKey, string groupLabel, List<Cage> cages, List<Reservation> allReservations)
    {
        // 1グループにつき横2枚ずつカードを並べる
        const int columns = 2;
        var rows = (cages.Count + columns - 1) / columns;

        var cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = false,
        };

        foreach (var cage in cages)
        {
            var reservations = allReservations
                .Where(r => r.CageId == cage.Id)
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.BirdName)
                .ToList();
            cardsPanel.Controls.Add(BuildCageCard(cage, reservations));
        }

        // グループの並び順を変えるボタン
        var moveButtonPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 26, FlowDirection = FlowDirection.RightToLeft };
        var moveRightButton = new Button { Text = "▶", Width = 32, Height = 22, Font = new Font("Yu Gothic UI", 8F), Margin = new Padding(2, 0, 2, 0) };
        moveRightButton.Click += (_, _) => MoveGroup(groupKey, 1);
        var moveLeftButton = new Button { Text = "◀", Width = 32, Height = 22, Font = new Font("Yu Gothic UI", 8F), Margin = new Padding(2, 0, 2, 0) };
        moveLeftButton.Click += (_, _) => MoveGroup(groupKey, -1);
        moveButtonPanel.Controls.Add(moveRightButton);
        moveButtonPanel.Controls.Add(moveLeftButton);

        var groupBox = new GroupBox
        {
            Text = groupLabel,
            Width = columns * (CardWidth + CardMargin * 2) + 16,
            Height = Math.Max(1, rows) * (CardHeight + CardMargin * 2) + 54,
            Margin = new Padding(6),
            Padding = new Padding(4),
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
        };
        groupBox.Controls.Add(cardsPanel);
        groupBox.Controls.Add(moveButtonPanel);
        return groupBox;
    }

    private Control BuildCageCard(Cage cage, List<Reservation> reservations)
    {
        var maxConcurrent = 0;
        foreach (var r in reservations)
        {
            var concurrent = reservations.Count(other => r.OverlapsWith(other.StartDate, other.EndDate));
            maxConcurrent = Math.Max(maxConcurrent, concurrent);
        }

        var card = new Panel
        {
            Width = CardWidth,
            Height = CardHeight,
            Margin = new Padding(CardMargin),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(6),
        };

        // 狭いカードに収まるよう、種別は略号で表示する
        var specialText = maxConcurrent > cage.Capacity ? $"（特{maxConcurrent}）" : "";
        var typeText = cage.Type switch
        {
            CageType.経営者籠 => "[経]",
            CageType.持ち込み籠 => "[持]",
            _ => "",
        };
        var headerLabel = new Label
        {
            Text = $"{cage.Name} 定員{cage.Capacity}{typeText}{specialText}",
            Dock = DockStyle.Top,
            Height = 18,
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            AutoEllipsis = true,
        };

        void OpenDetail(object? sender, EventArgs e) => OpenCageDetail(cage);
        headerLabel.Click += OpenDetail;
        card.Cursor = Cursors.Hand;
        card.Click += OpenDetail;

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 20,
            RowTemplate = { Height = 20 },
            Font = new Font("Yu Gothic UI", 8F),
            Cursor = Cursors.Hand,
        };
        grid.Columns.Add("Name", "名前");
        grid.Columns.Add("Period", "期間");
        grid.Columns[0].FillWeight = 42;
        grid.Columns[1].FillWeight = 58;
        foreach (DataGridViewColumn col in grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Click += OpenDetail;

        var ownerColors = new Dictionary<int, Color>();
        var nextColorIndex = 0;
        Color ColorForOwner(int? ownerId)
        {
            var key = ownerId ?? -1;
            if (!ownerColors.TryGetValue(key, out var color))
            {
                color = OwnerColorPalette[nextColorIndex % OwnerColorPalette.Length];
                nextColorIndex++;
                ownerColors[key] = color;
            }
            return color;
        }

        foreach (var r in reservations)
        {
            // 幅が狭いので期間は1列にまとめる。詳しくは籠をクリックした詳細画面で確認する。
            var periodText = r.IsIndefinite
                ? "無期限"
                : $"{r.StartDate:MM/dd}〜{r.EndDate!.Value:MM/dd}";

            var rowIndex = grid.Rows.Add(r.BirdName, periodText);
            grid.Rows[rowIndex].DefaultCellStyle.BackColor = ColorForOwner(r.OwnerId);
        }

        card.Controls.Add(grid);
        card.Controls.Add(headerLabel);
        return card;
    }

    private void OpenCageDetail(Cage cage)
    {
        using var form = new CageDetailForm(cage, _birdRepository, _cageRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void ExportReservations()
    {
        if (_reservationRepository.GetAll().Count == 0)
        {
            MessageBox.Show("出力できる予約がありません。", "Excel出力", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "予約一覧の出力先を選んでください",
            Filter = "Excelブック (*.xlsx)|*.xlsx",
            FileName = $"予約一覧_{DateTime.Today:yyyyMMdd}.xlsx",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var exportService = new ReservationExportService(_birdRepository, _reservationRepository);
            var count = exportService.ExportToExcel(dialog.FileName);
            MessageBox.Show($"{count}件を出力しました。\n{dialog.FileName}", "Excel出力", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"出力に失敗しました。\n{ex.Message}", "Excel出力", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // データを1つのファイルに保存しておく（PCを変えるとき・万一に備えるとき用）
    private void BackupDatabase()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "バックアップの保存先を選んでください",
            Filter = "小鳥ホテルのデータ (*.db)|*.db",
            FileName = $"小鳥ホテル_バックアップ_{DateTime.Today:yyyyMMdd}.db",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            File.Copy(_database.DatabasePath, dialog.FileName, overwrite: true);
            MessageBox.Show($"バックアップを保存しました。\n{dialog.FileName}", "バックアップ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存に失敗しました。\n{ex.Message}", "バックアップ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // バックアップしたファイルから元に戻す（今のデータは上書きされる）
    private void RestoreDatabase()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "復元するバックアップファイルを選んでください",
            Filter = "小鳥ホテルのデータ (*.db)|*.db",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var confirm = MessageBox.Show(
            "選んだバックアップの内容で、今のデータを上書きします。よろしいですか？（今のデータは元に戻せません）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        try
        {
            File.Copy(dialog.FileName, _database.DatabasePath, overwrite: true);
            MessageBox.Show("復元しました。反映のため、アプリを一度閉じて開き直してください。", "復元", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshCards();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"復元に失敗しました。\n{ex.Message}", "復元", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearAllCages()
    {
        var reservations = _reservationRepository.GetAll();
        if (reservations.Count == 0)
        {
            MessageBox.Show("取り消せる予約がありません。", "全籠クリア", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"すべての籠の予約{reservations.Count}件を取り消します。よろしいですか？（元に戻せません）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var reservation in reservations)
            _reservationRepository.Delete(reservation.Id);

        RefreshCards();
    }

    private void OpenOwnerList()
    {
        using var form = new OwnerListForm(_ownerRepository, _birdRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenBirdList()
    {
        using var form = new BirdListForm(_birdRepository, _ownerRepository, _speciesRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenCageList()
    {
        using var form = new CageListForm(_cageRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenReservationForm()
    {
        using var form = new ReservationForm(_birdRepository, _cageRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenReservationBulkImport()
    {
        using var form = new ReservationBulkImportForm(_birdRepository, _ownerRepository, _speciesRepository, _cageRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

}
